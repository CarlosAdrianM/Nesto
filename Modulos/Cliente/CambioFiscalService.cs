using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// NestoAPI#876: un error de api/Clientes/CambioFiscal con su mensaje (de usted, el de la API) y su código (409 NO_ES_CAMBIO_FISCAL,
    /// OCUPADO…). <see cref="Estado"/> = el código HTTP.
    /// </summary>
    public class CambioFiscalApiException : Exception
    {
        public CambioFiscalApiException(int estado, string mensaje, string codigo = null) : base(mensaje)
        {
            Estado = estado;
            Codigo = codigo;
        }

        public int Estado { get; }
        public string Codigo { get; }
    }

    /// <summary>
    /// NestoAPI#876 (corte nuevo, Carlos 26/11/26): un cliente que cambia de forma fiscal. Ver qué se copiaría de su ficha anterior,
    /// copiarlo por bloques y pasar sus pedidos pendientes al nuevo. Las reglas de verdad están en la API.
    /// </summary>
    public interface IServicioCambioFiscal
    {
        /// <summary>GET api/Clientes/CambioFiscal/Previa (solo lectura).</summary>
        Task<CopiaCambioFiscalModel> Previa(string empresa, string clienteAnterior, string clienteNuevo);
        /// <summary>POST api/Clientes/CambioFiscal/Copiar?ensayo=.</summary>
        Task<CopiaCambioFiscalModel> Copiar(CopiaCambioFiscalPeticion peticion, bool ensayo);
        /// <summary>POST api/Clientes/CambioFiscal/PasarPedidos?ensayo=.</summary>
        Task<PasarPedidosCambioFiscalModel> PasarPedidos(PasarPedidosCambioFiscalPeticion peticion, bool ensayo);
    }

    public class ServicioCambioFiscal : IServicioCambioFiscal
    {
        public const string MENSAJE_SIN_PERMISO =
            "No tiene permiso para copiar las condiciones del cliente anterior ni pasar sus pedidos: solo Administración, Dirección, Informática y el vendedor del cliente (o su jefe de equipo).";
        public const string MENSAJE_SIN_TABLA = "El cambio de forma fiscal todavía no está activado en el servidor.";

        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public ServicioCambioFiscal(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        private async Task<HttpClient> Cliente()
        {
            var client = new HttpClient { BaseAddress = new Uri(_configuracion.servidorAPI) };
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                client.Dispose();
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }
            return client;
        }

        internal static string UrlPrevia(string empresa, string clienteAnterior, string clienteNuevo)
            => $"Clientes/CambioFiscal/Previa?empresa={Uri.EscapeDataString(string.IsNullOrWhiteSpace(empresa) ? "1" : empresa.Trim())}" +
               $"&clienteAnterior={Uri.EscapeDataString(clienteAnterior?.Trim() ?? string.Empty)}&clienteNuevo={Uri.EscapeDataString(clienteNuevo?.Trim() ?? string.Empty)}";

        public async Task<CopiaCambioFiscalModel> Previa(string empresa, string clienteAnterior, string clienteNuevo)
        {
            using HttpClient client = await Cliente();
            HttpResponseMessage response = await client.GetAsync(UrlPrevia(empresa, clienteAnterior, clienteNuevo));
            string body = await response.Content.ReadAsStringAsync();
            Comprobar(response.StatusCode, body);
            return JsonConvert.DeserializeObject<CopiaCambioFiscalModel>(body) ?? new CopiaCambioFiscalModel();
        }

        public async Task<CopiaCambioFiscalModel> Copiar(CopiaCambioFiscalPeticion peticion, bool ensayo)
        {
            using HttpClient client = await Cliente();
            HttpResponseMessage response = await client.PostAsync($"Clientes/CambioFiscal/Copiar?ensayo={(ensayo ? "true" : "false")}",
                new StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json"));
            string body = await response.Content.ReadAsStringAsync();
            Comprobar(response.StatusCode, body);
            return JsonConvert.DeserializeObject<CopiaCambioFiscalModel>(body) ?? new CopiaCambioFiscalModel();
        }

        public async Task<PasarPedidosCambioFiscalModel> PasarPedidos(PasarPedidosCambioFiscalPeticion peticion, bool ensayo)
        {
            using HttpClient client = await Cliente();
            HttpResponseMessage response = await client.PostAsync($"Clientes/CambioFiscal/PasarPedidos?ensayo={(ensayo ? "true" : "false")}",
                new StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json"));
            string body = await response.Content.ReadAsStringAsync();
            Comprobar(response.StatusCode, body);
            return JsonConvert.DeserializeObject<PasarPedidosCambioFiscalModel>(body) ?? new PasarPedidosCambioFiscalModel();
        }

        /// <summary>Si la respuesta no es buena, lanza <see cref="CambioFiscalApiException"/> con el mensaje de la API (o uno de usted).</summary>
        internal static void Comprobar(HttpStatusCode estado, string body)
        {
            if ((int)estado >= 200 && (int)estado < 300)
            {
                return;
            }
            (string mensaje, string codigo) = LeerError(body);
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
                mensaje = $"El servidor ha contestado {(int)estado} al cambio de forma fiscal.";
            }
            throw new CambioFiscalApiException((int)estado, mensaje, codigo);
        }

        /// <summary>{Message, Codigo}, una cadena JSON o el texto tal cual.</summary>
        internal static (string Mensaje, string Codigo) LeerError(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return (null, null);
            }
            try
            {
                JToken json = JToken.Parse(body);
                if (json.Type == JTokenType.String)
                {
                    return (json.ToString(), null);
                }
                if (json is JObject objeto)
                {
                    return (objeto["Message"]?.ToString(), objeto["Codigo"]?.ToString());
                }
            }
            catch
            {
            }
            return (body, null);
        }
    }
}
