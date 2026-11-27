using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// NestoAPI#876 (27/11/26): un error de api/Clientes/Equivalentes con el mensaje de la API y su código (409 CICLO u OCUPADO).
    /// <see cref="Estado"/> = el código HTTP (503: el servidor aún no tiene la tabla).
    /// </summary>
    public class ClientesEquivalentesApiException : Exception
    {
        public ClientesEquivalentesApiException(int estado, string mensaje, string codigo = null) : base(mensaje)
        {
            Estado = estado;
            Codigo = codigo;
        }

        public int Estado { get; }
        public string Codigo { get; }
        public bool SinTabla => Estado == (int)HttpStatusCode.ServiceUnavailable;
    }

    /// <summary>
    /// NestoAPI#876 (27/11/26): fichas de cliente equivalentes (la misma persona dada de alta dos veces, o el mismo cliente con otra
    /// forma fiscal). Quién puede enlazar y deshacer lo decide la API (403 con su mensaje).
    /// </summary>
    public interface IServicioClientesEquivalentes
    {
        /// <summary>GET api/Clientes/{empresa}/{cliente}/Equivalentes: el grupo entero (el consultado primero).</summary>
        Task<ClientesEquivalentesModel> Leer(string empresa, string cliente);
        /// <summary>POST api/Clientes/Equivalentes.</summary>
        Task<ResultadoEnlaceEquivalenteModel> Enlazar(PeticionEnlaceEquivalenteModel peticion);
        /// <summary>DELETE api/Clientes/{empresa}/{cliente}/Equivalentes/{clienteAnterior}.</summary>
        Task<ResultadoEnlaceEquivalenteModel> Deshacer(string empresa, string cliente, string clienteAnterior);
    }

    public class ServicioClientesEquivalentes : IServicioClientesEquivalentes
    {
        public const string MENSAJE_SIN_PERMISO = "No tiene permiso para enlazar fichas de cliente equivalentes ni para deshacer el enlace.";
        public const string MENSAJE_SIN_TABLA = "Las fichas equivalentes todavía no están activadas en el servidor.";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioClientesEquivalentes(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        internal static string Empresa(string empresa) => string.IsNullOrWhiteSpace(empresa) ? "1" : empresa.Trim();

        internal static string UrlLeer(string empresa, string cliente)
            => $"Clientes/{Uri.EscapeDataString(Empresa(empresa))}/{Uri.EscapeDataString(cliente?.Trim() ?? string.Empty)}/Equivalentes";

        internal static string UrlDeshacer(string empresa, string cliente, string clienteAnterior)
            => UrlLeer(empresa, cliente) + "/" + Uri.EscapeDataString(clienteAnterior?.Trim() ?? string.Empty);

        public async Task<ClientesEquivalentesModel> Leer(string empresa, string cliente)
        {
            using HttpClient client = _clienteApiFactory.Crear();
            HttpResponseMessage response = await client.GetAsync(UrlLeer(empresa, cliente));
            string body = await response.Content.ReadAsStringAsync();
            Comprobar(response.StatusCode, body);
            return JsonConvert.DeserializeObject<ClientesEquivalentesModel>(body) ?? new ClientesEquivalentesModel();
        }

        public async Task<ResultadoEnlaceEquivalenteModel> Enlazar(PeticionEnlaceEquivalenteModel peticion)
        {
            using HttpClient client = _clienteApiFactory.Crear();
            HttpResponseMessage response = await client.PostAsync("Clientes/Equivalentes",
                new StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json"));
            string body = await response.Content.ReadAsStringAsync();
            Comprobar(response.StatusCode, body);
            return JsonConvert.DeserializeObject<ResultadoEnlaceEquivalenteModel>(body) ?? new ResultadoEnlaceEquivalenteModel();
        }

        public async Task<ResultadoEnlaceEquivalenteModel> Deshacer(string empresa, string cliente, string clienteAnterior)
        {
            using HttpClient client = _clienteApiFactory.Crear();
            HttpResponseMessage response = await client.DeleteAsync(UrlDeshacer(empresa, cliente, clienteAnterior));
            string body = await response.Content.ReadAsStringAsync();
            Comprobar(response.StatusCode, body);
            return JsonConvert.DeserializeObject<ResultadoEnlaceEquivalenteModel>(body) ?? new ResultadoEnlaceEquivalenteModel();
        }

        /// <summary>Si la respuesta no es buena, lanza <see cref="ClientesEquivalentesApiException"/> con el mensaje de la API (o uno de usted).</summary>
        internal static void Comprobar(HttpStatusCode estado, string body)
        {
            if ((int)estado >= 200 && (int)estado < 300)
            {
                return;
            }
            (string mensaje, string codigo) = ServicioCambioFiscal.LeerError(body);
            if (estado == HttpStatusCode.Forbidden && string.IsNullOrWhiteSpace(mensaje))
            {
                mensaje = MENSAJE_SIN_PERMISO;
            }
            else if (estado == HttpStatusCode.ServiceUnavailable)
            {
                mensaje = string.IsNullOrWhiteSpace(mensaje) ? MENSAJE_SIN_TABLA : MENSAJE_SIN_TABLA + "\n" + mensaje;
            }
            else if (string.IsNullOrWhiteSpace(mensaje))
            {
                mensaje = $"El servidor ha contestado {(int)estado} al consultar las fichas equivalentes.";
            }
            throw new ClientesEquivalentesApiException((int)estado, mensaje, codigo);
        }
    }
}
