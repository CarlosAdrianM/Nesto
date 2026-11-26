using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// NestoAPI#883 (corte 1): la fecha del inventario de cada almacén y las reposiciones que lo tocan (las que llegan al almacén la víspera o
    /// el día y las que salen de él y cierran la víspera o el día), para decidir en cada una «Quitar» o «Mantener». La regla es de la API.
    /// </summary>
    public interface IServicioFechasInventario
    {
        /// <exception cref="FechasInventarioApiException">La API no lo ha devuelto (503 sin el script).</exception>
        Task<List<FechaInventario>> Leer(string almacen);
        /// <summary>Crea o cambia la del almacén y día; devuelve cómo queda.</summary>
        Task<FechaInventario> Guardar(FechaInventario fecha);
        Task<FechaInventario> Borrar(int id);
        /// <summary>«Quitar» (la excepción SinReposicion del calendario) o «Mantener» (deja de avisar); devuelve cómo queda la fecha.</summary>
        Task<FechaInventario> Decidir(int id, DecisionReposicionInventario decision);
    }

    public class FechasInventarioApiException : Exception
    {
        public FechasInventarioApiException(string mensaje, HttpStatusCode? estado = null) : base(mensaje)
        {
            Estado = estado;
        }

        public HttpStatusCode? Estado { get; }
        public bool FaltaElScript => Estado == HttpStatusCode.ServiceUnavailable;
    }

    /// <summary>NestoAPI#883: FechaInventarioDTO.</summary>
    public class FechaInventario
    {
        public int? Id { get; set; }
        public string Empresa { get; set; }
        public string Almacen { get; set; }
        public DateTime Fecha { get; set; }
        public string Comentario { get; set; }
        public string Usuario { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public DateTime? AvisoEnviado { get; set; }
        public DateTime DiaAviso { get; set; }
        public List<ReposicionAfectadaInventario> Reposiciones { get; set; } = new List<ReposicionAfectadaInventario>();
        public int Pendientes { get; set; }
    }

    /// <summary>NestoAPI#883: ReposicionAfectadaInventarioDTO.</summary>
    public class ReposicionAfectadaInventario
    {
        public const string PENDIENTE = "Pendiente";
        public const string MANTENIDA = "Mantenida";
        public const string QUITADA = "Quitada";

        public string Origen { get; set; }
        public string Destino { get; set; }
        public DateTime FechaLlegada { get; set; }
        public DateTime CierraEl { get; set; }
        /// <summary>«Llega» o «Sale».</summary>
        public string Motivo { get; set; }
        /// <summary>«Pendiente», «Mantenida» o «Quitada».</summary>
        public string Estado { get; set; }
        public string DecididoPor { get; set; }
        public DateTime? FechaDecision { get; set; }
        public bool SePuedeQuitar { get; set; }
        public string Texto { get; set; }
    }

    /// <summary>NestoAPI#883: DecisionReposicionInventarioDTO.</summary>
    public class DecisionReposicionInventario
    {
        public const string QUITAR = "Quitar";
        public const string MANTENER = "Mantener";

        public string Origen { get; set; }
        public string Destino { get; set; }
        public DateTime FechaLlegada { get; set; }
        public string Decision { get; set; }
    }

    public class ServicioFechasInventario : IServicioFechasInventario
    {
        public const string RUTA = "Inventarios/Fechas";
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioFechasInventario(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        internal static string UrlLeer(string almacen)
            => string.IsNullOrWhiteSpace(almacen) ? RUTA : $"{RUTA}?almacen={Uri.EscapeDataString(almacen.Trim())}";

        public Task<List<FechaInventario>> Leer(string almacen) => Enviar<List<FechaInventario>>(HttpMethod.Get, UrlLeer(almacen), null);

        public Task<FechaInventario> Guardar(FechaInventario fecha) => Enviar<FechaInventario>(HttpMethod.Post, RUTA, fecha);

        public Task<FechaInventario> Borrar(int id) => Enviar<FechaInventario>(HttpMethod.Delete, $"{RUTA}/{id}", null);

        public Task<FechaInventario> Decidir(int id, DecisionReposicionInventario decision)
            => Enviar<FechaInventario>(HttpMethod.Post, $"{RUTA}/{id}/Reposiciones", decision);

        private async Task<T> Enviar<T>(HttpMethod metodo, string url, object cuerpo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                using (HttpRequestMessage peticion = new HttpRequestMessage(metodo, url))
                {
                    if (cuerpo != null)
                    {
                        peticion.Content = new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json");
                    }
                    HttpResponseMessage response = await client.SendAsync(peticion).ConfigureAwait(false);
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        string motivo = response.StatusCode == HttpStatusCode.NotFound && string.IsNullOrWhiteSpace(json)
                            ? "El servidor todavía no tiene las fechas de inventario."
                            : HttpErrorHelper.ParsearErrorHttp(json);
                        throw new FechasInventarioApiException(motivo, response.StatusCode);
                    }
                    return JsonConvert.DeserializeObject<T>(json);
                }
            }
        }
    }
}
