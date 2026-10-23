using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// NestoAPI#553: recibir en la tienda las reposiciones que salen de Algete, con la misma API que Ariadna
    /// (api/Almacen/Recepciones, tipo REPO). Nesto no decide nada: entra lo leído y el servidor informa de las
    /// diferencias a quien creó el traspaso.
    /// </summary>
    public interface IServicioRecepcionReposiciones
    {
        /// <summary>Solo las reposiciones (las compras de proveedor se reciben en Ariadna).</summary>
        Task<List<RecepcionPendiente>> LeerPendientes(string empresa, string almacen);
        Task<RecepcionReposicion> LeerRecepcion(string empresa, string almacen, string traspaso);
        /// <exception cref="RecepcionReposicionException">La API no la ha terminado (sin permiso, ya terminada…).</exception>
        Task<ResultadoRecepcionReposicion> Terminar(string empresa, string almacen, string traspaso, TerminarRecepcionReposicion terminar);
        /// <summary>
        /// Sugerencia 564: el PDF de lo que llega con la reposición, para comprobarlo a mano
        /// (GET api/Reposiciones/Recepcion/{traspaso}/Pdf).
        /// </summary>
        /// <exception cref="RecepcionReposicionException">Ya no está pendiente (404) o la API no lo ha generado.</exception>
        Task<byte[]> DescargarListadoPdf(string empresa, string almacen, string traspaso);
        /// <summary>
        /// Nesto#530 (NestoAPI#645): la etiqueta de bulto leída (GET api/Reposiciones/Bultos/{codigo}): de qué reposición es y
        /// qué bulto.
        /// </summary>
        /// <exception cref="RecepcionReposicionException">No hay ningún bulto con ese código (404), con el motivo de la API.</exception>
        Task<EtiquetaBultoReposicion> LeerBulto(string empresa, string codigo);
        /// <summary>
        /// Nesto#538: de qué producto es lo leído que no está en la reposición. Si es una referencia (tecleada), ese producto
        /// (GET api/Productos); si no, los que tienen ese código de barras, principal o alternativo
        /// (GET api/Productos/PorCodigoBarras). Vacía si no es de ninguno.
        /// </summary>
        /// <exception cref="RecepcionReposicionException">La API no ha contestado bien.</exception>
        Task<List<ProductoCodigoRecepcion>> BuscarProductosPorCodigo(string empresa, string codigo);
        /// <summary>
        /// Ariadna#51 (corte 2): lo leído hasta ahora de la recepción en curso, compartido con Ariadna y otros Nesto
        /// (GET api/Almacen/Recepciones/REPO/{traspaso}/Lecturas). Si no hay nada en curso, el servidor crea el IdRecepcion.
        /// </summary>
        /// <returns>Null con una API anterior (404): entonces lo leído vive solo en esta pantalla, como antes.</returns>
        /// <exception cref="RecepcionReposicionException">La API no ha contestado bien.</exception>
        Task<LecturasRecepcionReposicion> LeerLecturas(string empresa, string almacen, string traspaso);
        /// <summary>
        /// Ariadna#51: guarda en el servidor lo que se acaba de leer (o quitar) y devuelve lo leído de todos, ya sumado
        /// (POST …/Lecturas). El servidor avisa por SignalR a los demás equipos del grupo, salvo a esta conexión.
        /// </summary>
        /// <param name="conexionSignalR">La conexión de SignalR de este Nesto (cabecera X-Conexion-SignalR), para que no se le
        /// avise de lo suyo. Null si no está conectado.</param>
        /// <exception cref="RecepcionReposicionException">La API no la ha guardado.</exception>
        Task<LecturasRecepcionReposicion> GuardarLecturas(string empresa, string almacen, string traspaso,
            List<LecturaEnCursoRecepcion> lecturas, string conexionSignalR);
    }

    public class ServicioRecepcionReposiciones : IServicioRecepcionReposiciones
    {
        public const string TIPO_REPOSICION = "REPO";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioRecepcionReposiciones(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<List<RecepcionPendiente>> LeerPendientes(string empresa, string almacen)
        {
            List<RecepcionPendiente> todas = await LeerAsync<List<RecepcionPendiente>>("Almacen/Recepciones" + Consulta(empresa, almacen))
                .ConfigureAwait(false);
            return (todas ?? new List<RecepcionPendiente>())
                .Where(r => string.Equals(r.Tipo?.Trim(), TIPO_REPOSICION, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public Task<RecepcionReposicion> LeerRecepcion(string empresa, string almacen, string traspaso)
            => LeerAsync<RecepcionReposicion>($"Almacen/Recepciones/{TIPO_REPOSICION}/{Uri.EscapeDataString(traspaso?.Trim() ?? string.Empty)}" + Consulta(empresa, almacen));

        public async Task<ResultadoRecepcionReposicion> Terminar(string empresa, string almacen, string traspaso, TerminarRecepcionReposicion terminar)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                string url = $"Almacen/Recepciones/{TIPO_REPOSICION}/{Uri.EscapeDataString(traspaso?.Trim() ?? string.Empty)}/Terminar" + Consulta(empresa, almacen);
                var contenido = new StringContent(JsonConvert.SerializeObject(terminar), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, contenido).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new RecepcionReposicionException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode));
                }
                return JsonConvert.DeserializeObject<ResultadoRecepcionReposicion>(json) ?? new ResultadoRecepcionReposicion();
            }
        }

        public async Task<byte[]> DescargarListadoPdf(string empresa, string almacen, string traspaso)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                string url = $"Reposiciones/Recepcion/{Uri.EscapeDataString(traspaso?.Trim() ?? string.Empty)}/Pdf" + Consulta(empresa, almacen);
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new RecepcionReposicionException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode));
                }
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        public Task<EtiquetaBultoReposicion> LeerBulto(string empresa, string codigo)
            => LeerAsync<EtiquetaBultoReposicion>($"Reposiciones/Bultos/{Uri.EscapeDataString(codigo?.Trim() ?? string.Empty)}"
                + "?empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty));

        public async Task<List<ProductoCodigoRecepcion>> BuscarProductosPorCodigo(string empresa, string codigo)
        {
            string limpio = codigo?.Trim() ?? string.Empty;
            string empresaLimpia = Uri.EscapeDataString(empresa?.Trim() ?? string.Empty);
            if (limpio.Length == 0)
            {
                return new List<ProductoCodigoRecepcion>();
            }
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                // La referencia tecleada (44724 + Intro) es un producto aunque no tenga ese código de barras
                HttpResponseMessage response = await client.GetAsync($"Productos?empresa={empresaLimpia}&id={Uri.EscapeDataString(limpio)}")
                    .ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    ProductoCodigoRecepcion producto = JsonConvert.DeserializeObject<ProductoCodigoRecepcion>(json);
                    if (!string.IsNullOrWhiteSpace(producto?.Producto))
                    {
                        return new List<ProductoCodigoRecepcion>
                        {
                            new ProductoCodigoRecepcion { Producto = producto.Producto.Trim(), Nombre = producto.Nombre?.Trim() }
                        };
                    }
                }
                else if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw new RecepcionReposicionException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode));
                }

                response = await client.GetAsync($"Productos/PorCodigoBarras?codigo={Uri.EscapeDataString(limpio)}&empresa={empresaLimpia}")
                    .ConfigureAwait(false);
                json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new RecepcionReposicionException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode));
                }
                return (JsonConvert.DeserializeObject<List<ProductoCodigoRecepcion>>(json) ?? new List<ProductoCodigoRecepcion>())
                    .Where(p => !string.IsNullOrWhiteSpace(p?.Producto))
                    .GroupBy(p => p.Producto.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => new ProductoCodigoRecepcion { Producto = g.Key, Nombre = g.First().Nombre?.Trim() })
                    .ToList();
            }
        }

        /// <summary>Ariadna#51: la conexión de SignalR que hace el POST (la API no le avisa de lo suyo).</summary>
        public const string CABECERA_CONEXION_SIGNALR = "X-Conexion-SignalR";
        /// <summary>Ariadna#51: el equipo que hace el POST (si no llega la conexión, la API no avisa a las de este equipo).</summary>
        public const string CABECERA_DISPOSITIVO = "X-Dispositivo";

        private static string UrlLecturas(string empresa, string almacen, string traspaso)
            => $"Almacen/Recepciones/{TIPO_REPOSICION}/{Uri.EscapeDataString(traspaso?.Trim() ?? string.Empty)}/Lecturas" + Consulta(empresa, almacen);

        public async Task<LecturasRecepcionReposicion> LeerLecturas(string empresa, string almacen, string traspaso)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(UrlLecturas(empresa, almacen, traspaso)).ConfigureAwait(false);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Una API anterior a Ariadna#51 (o sin la tabla): lo leído vive solo en la pantalla, como antes
                    return null;
                }
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new RecepcionReposicionException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode));
                }
                return JsonConvert.DeserializeObject<LecturasRecepcionReposicion>(json);
            }
        }

        public async Task<LecturasRecepcionReposicion> GuardarLecturas(string empresa, string almacen, string traspaso,
            List<LecturaEnCursoRecepcion> lecturas, string conexionSignalR)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var peticion = new HttpRequestMessage(HttpMethod.Post, UrlLecturas(empresa, almacen, traspaso))
                {
                    Content = new StringContent(JsonConvert.SerializeObject(lecturas ?? new List<LecturaEnCursoRecepcion>()), Encoding.UTF8, "application/json")
                };
                if (!string.IsNullOrWhiteSpace(conexionSignalR))
                {
                    _ = peticion.Headers.TryAddWithoutValidation(CABECERA_CONEXION_SIGNALR, conexionSignalR.Trim());
                }
                _ = peticion.Headers.TryAddWithoutValidation(CABECERA_DISPOSITIVO, Environment.MachineName);
                HttpResponseMessage response = await client.SendAsync(peticion).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new RecepcionReposicionException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode));
                }
                return JsonConvert.DeserializeObject<LecturasRecepcionReposicion>(json)
                    ?? throw new RecepcionReposicionException("El servidor no ha devuelto lo leído.");
            }
        }

        private async Task<T> LeerAsync<T>(string url) where T : class
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new RecepcionReposicionException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode));
                }
                return JsonConvert.DeserializeObject<T>(json);
            }
        }

        private static string Consulta(string empresa, string almacen)
            => "?almacen=" + Uri.EscapeDataString(almacen?.Trim() ?? string.Empty)
               + "&empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty);
    }
}
