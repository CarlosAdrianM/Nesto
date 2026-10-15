using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
        /// <summary>
        /// NestoAPI#577: si el usuario puede rellenar reposiciones a mano (POST api/Reposiciones); las demás las rellena
        /// la API a la hora de corte del calendario. Una API anterior a #577 (404) no lo restringe: true.
        /// </summary>
        Task<bool> PuedeRellenarManual();
        /// <summary>
        /// NestoAPI#622: lo que ha hecho hoy la API con las reposiciones de la tienda que hay que decirle (GET
        /// api/Reposiciones/AvisosAutomatica): la de hoy no se ha rellenado, o se ha sumado a una sin enviar. Vacía si no hay nada
        /// o si la API todavía no lo sabe (404).
        /// </summary>
        Task<List<AvisoReposicionAutomatica>> LeerAvisosAutomatica(string empresa, string origen);
        /// <summary>NestoAPI#577: la próxima reposición de la ruta según el calendario, o null si la ruta no tiene calendario (404).</summary>
        Task<ProximaReposicion> LeerProximaLlegada(string empresa, string origen, string destino);
        /// <summary>
        /// Sugerencia 564: el PDF de la reposición en preparación para prepararla a mano (GET api/Reposiciones/EnPreparacion/Pdf).
        /// </summary>
        /// <exception cref="EnvioReposicionException">No hay ninguna en preparación (404) o la API no lo ha generado.</exception>
        Task<byte[]> DescargarListadoPdf(string empresa, string origen);
        /// <summary>
        /// Sugerencia 581: lo leído en la reposición puntual (GET api/Reposiciones/Puntual/Producto): el producto de esa
        /// referencia o los de ese código de barras, con el stock de la tienda. Vacía si no hay ninguno.
        /// </summary>
        Task<List<ProductoReposicionPuntual>> BuscarProductoPuntual(string empresa, string origen, string codigo);
        /// <summary>
        /// Sugerencia 581: manda a Algete una reposición puntual con solo esas líneas (POST api/Reposiciones/Puntual). Se crea
        /// y se termina de una vez: devuelve el traspaso, ya pendiente de recibir en Algete.
        /// </summary>
        /// <exception cref="EnvioReposicionException">La del calendario sin enviar, falta stock, sin permiso…</exception>
        Task<ResultadoTerminarReposicion> CrearPuntual(CrearReposicion peticion);
        /// <summary>
        /// Sugerencia 581 (Carlos, 13/10/26): si hoy se puede hacer la puntual y si el repartidor ya se ha llevado la de hoy
        /// (GET api/Reposiciones/Puntual/Estado). Null si la API todavía no lo sabe (anterior a esto): sin restricciones.
        /// </summary>
        Task<EstadoReposicionPuntual> LeerEstadoPuntual(string empresa, string origen);
        /// <summary>
        /// Sugerencia 581 (Carlos, 13/10/26): «El repartidor ya se la ha llevado» (POST api/Reposiciones/Recogida). Devuelve el
        /// estado nuevo (con quién y cuándo).
        /// </summary>
        /// <exception cref="EnvioReposicionException">Hoy no hay viaje, la de hoy sin enviar, ya marcada, sin permiso…</exception>
        Task<EstadoReposicionPuntual> MarcarRecogida(MarcarRecogidaReposicion peticion);
        /// <summary>
        /// Nesto#530 (NestoAPI#645): crea los bultos de la reposición enviada y la API imprime una etiqueta por bulto en la
        /// impresora de etiquetas del usuario (POST api/Reposiciones/{traspaso}/Bultos).
        /// </summary>
        /// <exception cref="EnvioReposicionException">502 si no se han podido imprimir (los bultos quedan creados: se reimprimen
        /// con <see cref="ReimprimirBultos"/>); 409 si ya se ha leído alguno.</exception>
        Task<BultosReposicion> CrearBultos(string empresa, string traspaso, int bultos);
        /// <summary>Nesto#530: vuelve a imprimir las etiquetas de los bultos ya creados (POST api/Reposiciones/{traspaso}/Bultos/Reimprimir).</summary>
        Task<BultosReposicion> ReimprimirBultos(string empresa, string traspaso);
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

        public async Task<bool> PuedeRellenarManual()
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync("Reposiciones/PuedeRellenarManual").ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // La API todavía no tiene #577: entonces cualquiera puede preparar la reposición a mano
                    return true;
                }
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<bool>(json);
            }
        }

        public async Task<List<AvisoReposicionAutomatica>> LeerAvisosAutomatica(string empresa, string origen)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync("Reposiciones/AvisosAutomatica" + Consulta(empresa, origen)).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // La API todavía no tiene #622: nada que decir
                    return new List<AvisoReposicionAutomatica>();
                }
                return await Leer<List<AvisoReposicionAutomatica>>(response).ConfigureAwait(false) ?? new List<AvisoReposicionAutomatica>();
            }
        }

        public async Task<ProximaReposicion> LeerProximaLlegada(string empresa, string origen, string destino)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                string url = "Reposiciones/ProximaLlegada?origen=" + Uri.EscapeDataString(origen?.Trim() ?? string.Empty)
                    + "&destino=" + Uri.EscapeDataString(destino?.Trim() ?? string.Empty)
                    + "&empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty);
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                return await Leer<ProximaReposicion>(response).ConfigureAwait(false);
            }
        }

        public async Task<byte[]> DescargarListadoPdf(string empresa, string origen)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync("Reposiciones/EnPreparacion/Pdf" + Consulta(empresa, origen)).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new EnvioReposicionException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode), (int)response.StatusCode);
                }
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        public async Task<List<ProductoReposicionPuntual>> BuscarProductoPuntual(string empresa, string origen, string codigo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync("Reposiciones/Puntual/Producto" + Consulta(empresa, origen)
                    + "&codigo=" + Uri.EscapeDataString(codigo?.Trim() ?? string.Empty)).ConfigureAwait(false);
                return await Leer<List<ProductoReposicionPuntual>>(response).ConfigureAwait(false) ?? new List<ProductoReposicionPuntual>();
            }
        }

        public Task<ResultadoTerminarReposicion> CrearPuntual(CrearReposicion peticion)
            => Enviar<ResultadoTerminarReposicion>(HttpMethod.Post, "Reposiciones/Puntual", peticion);

        public async Task<EstadoReposicionPuntual> LeerEstadoPuntual(string empresa, string origen)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync("Reposiciones/Puntual/Estado" + Consulta(empresa, origen)).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // La API todavía no lo sabe: sin restricciones (la API dirá lo que sea al mandarla)
                    return null;
                }
                return await Leer<EstadoReposicionPuntual>(response).ConfigureAwait(false);
            }
        }

        public Task<EstadoReposicionPuntual> MarcarRecogida(MarcarRecogidaReposicion peticion)
            => Enviar<EstadoReposicionPuntual>(HttpMethod.Post, "Reposiciones/Recogida", peticion);

        public Task<BultosReposicion> CrearBultos(string empresa, string traspaso, int bultos)
            => Enviar<BultosReposicion>(HttpMethod.Post, $"Reposiciones/{Uri.EscapeDataString(traspaso?.Trim() ?? string.Empty)}/Bultos" + ConsultaEmpresa(empresa),
                new { Bultos = bultos, Imprimir = true });

        public Task<BultosReposicion> ReimprimirBultos(string empresa, string traspaso)
            => Enviar<BultosReposicion>(HttpMethod.Post, $"Reposiciones/{Uri.EscapeDataString(traspaso?.Trim() ?? string.Empty)}/Bultos/Reimprimir" + ConsultaEmpresa(empresa), null);

        private static string ConsultaEmpresa(string empresa)
            => "?empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty);

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
                throw new EnvioReposicionException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode), (int)response.StatusCode);
            }
            return JsonConvert.DeserializeObject<T>(json);
        }

        private static string Consulta(string empresa, string origen)
            => "?origen=" + Uri.EscapeDataString(origen?.Trim() ?? string.Empty)
               + "&empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty);
    }
}
