using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// NestoAPI#553: la tienda prepara la reposición que manda a Algete y la termina (api/Reposiciones). Nesto no
    /// decide nada: la propuesta, las comprobaciones y la contabilización las hace el servidor, con las mismas
    /// escrituras que Nesto viejo.
    /// </summary>
    public interface IServicioEnvioReposiciones
    {
        /// <summary>La reposición que el origen tiene en preparación, o null si no tiene ninguna (404).</summary>
        Task<ReposicionEnPreparacion> LeerEnPreparacion(string empresa, string origen);
        /// <exception cref="EnvioReposicionException">Inventario en curso, ya hay una en preparación, sin permiso…</exception>
        Task<ReposicionEnPreparacion> Crear(CrearReposicion peticion);
        /// <summary>Solo se puede bajar (0 para no mandarla).</summary>
        Task<ReposicionEnPreparacion> CambiarCantidad(string empresa, string origen, int numeroOrden, int cantidad);
        /// <exception cref="EnvioReposicionException">No hay nada que mandar, falla la contabilización…</exception>
        Task<ResultadoTerminarReposicion> Terminar(string empresa, string origen);
    }

    public class ServicioEnvioReposiciones : IServicioEnvioReposiciones
    {
        private static readonly JsonSerializerSettings _ajustes = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatString = "yyyy-MM-dd"
        };

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioEnvioReposiciones(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<ReposicionEnPreparacion> LeerEnPreparacion(string empresa, string origen)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync("Reposiciones/EnPreparacion" + Consulta(empresa, origen)).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                return await Leer<ReposicionEnPreparacion>(response).ConfigureAwait(false);
            }
        }

        public Task<ReposicionEnPreparacion> Crear(CrearReposicion peticion)
            => Enviar<ReposicionEnPreparacion>(HttpMethod.Post, "Reposiciones", peticion);

        public Task<ReposicionEnPreparacion> CambiarCantidad(string empresa, string origen, int numeroOrden, int cantidad)
            => Enviar<ReposicionEnPreparacion>(HttpMethod.Put, $"Reposiciones/EnPreparacion/Lineas/{numeroOrden}" + Consulta(empresa, origen),
                new { Cantidad = cantidad });

        public Task<ResultadoTerminarReposicion> Terminar(string empresa, string origen)
            => Enviar<ResultadoTerminarReposicion>(HttpMethod.Post, "Reposiciones/EnPreparacion/Terminar" + Consulta(empresa, origen), null);

        private async Task<T> Enviar<T>(HttpMethod metodo, string url, object cuerpo) where T : class, new()
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var peticion = new HttpRequestMessage(metodo, url)
                {
                    Content = new StringContent(cuerpo == null ? string.Empty : JsonConvert.SerializeObject(cuerpo, _ajustes), Encoding.UTF8, "application/json")
                };
                HttpResponseMessage response = await client.SendAsync(peticion).ConfigureAwait(false);
                return await Leer<T>(response).ConfigureAwait(false) ?? new T();
            }
        }

        private static async Task<T> Leer<T>(HttpResponseMessage response) where T : class
        {
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new EnvioReposicionException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode));
            }
            return JsonConvert.DeserializeObject<T>(json);
        }

        private static string Consulta(string empresa, string origen)
            => "?origen=" + Uri.EscapeDataString(origen?.Trim() ?? string.Empty)
               + "&empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty);
    }
}
