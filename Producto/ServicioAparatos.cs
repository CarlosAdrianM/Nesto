using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// NestoAPI#913 (corte 1): la historia de un aparato por número de serie y los descuadres entre el registro (ventana de
    /// Alquileres) y lo que dicen los movimientos. SOLO LECTURA: la situación la calcula la API, Nesto solo la enseña.
    /// </summary>
    public interface IServicioAparatos
    {
        /// <summary>GET api/Aparatos/Historia. Lanza <see cref="AparatosApiException"/> con el mensaje de la API si falla (404, 403…).</summary>
        Task<HistoriaAparato> LeerHistoria(string producto, string numeroSerie);

        /// <summary>GET api/Aparatos/Descuadres. Lanza <see cref="AparatosApiException"/> si falla.</summary>
        Task<InformeDescuadresAparatos> LeerDescuadres(bool soloListaAlquileres);
    }

    public class AparatosApiException : Exception
    {
        public AparatosApiException(string mensaje, HttpStatusCode? estado = null) : base(mensaje)
        {
            Estado = estado;
        }

        public HttpStatusCode? Estado { get; }
    }

    /// <summary>NestoAPI#913: HistoriaAparatoDTO (solo lo que se enseña).</summary>
    public class HistoriaAparato
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public string NumeroSerie { get; set; }
        public string Situacion { get; set; }
        public string TextoSituacion { get; set; }
        public RegistroAparato Registro { get; set; }
        public List<int> OtrosRegistros { get; set; } = new List<int>();
        public bool EnTablaNumerosSerie { get; set; }
        public List<MovimientoAparato> Movimientos { get; set; } = new List<MovimientoAparato>();
    }

    public class RegistroAparato
    {
        public int Numero { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string NombreCliente { get; set; }
        public string Inmovilizado { get; set; }
        public string SituacionSegunRegistro { get; set; }
    }

    public class MovimientoAparato
    {
        public DateTime? Fecha { get; set; }
        public string Tipo { get; set; }
        public string Documento { get; set; }
        public string Tercero { get; set; }
        public string NombreTercero { get; set; }
        public string Texto { get; set; }
    }

    /// <summary>NestoAPI#913: InformeDescuadresAparatosDTO.</summary>
    public class InformeDescuadresAparatos
    {
        public bool SoloListaAlquileres { get; set; }
        public int TotalDescuadres { get; set; }
        public string Aviso { get; set; }
        public List<DescuadreAparato> Descuadres { get; set; } = new List<DescuadreAparato>();
        public List<ResumenProductoAparatos> Productos { get; set; } = new List<ResumenProductoAparatos>();
    }

    public class DescuadreAparato
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public string NumeroSerie { get; set; }
        public string Motivo { get; set; }
        public string TextoMotivo { get; set; }
        public List<string> Motivos { get; set; } = new List<string>();
        public string SituacionSegunRegistro { get; set; }
        public string SituacionCalculada { get; set; }
        public string TextoSituacion { get; set; }
        public List<int> Registros { get; set; } = new List<int>();
        public List<string> OtrasFormasDeLaSerie { get; set; } = new List<string>();
        public string GrupoSerie { get; set; }

        /// <summary>Si tiene alguna fila en el registro (CabAlquileres) a la que ir.</summary>
        public bool TieneRegistro => Registros != null && Registros.Count > 0;
    }

    public class ResumenProductoAparatos
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public int StockContableTotal { get; set; }
        public int EnStockSegunRegistro { get; set; }
        public int EnStockSegunMovimientos { get; set; }
        public int Diferencia { get; set; }
        public int DiferenciaStockContable { get; set; }
    }

    public class ServicioAparatos : IServicioAparatos
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioAparatos(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        internal static string UrlHistoria(string producto, string numeroSerie) =>
            $"Aparatos/Historia?producto={Uri.EscapeDataString(producto?.Trim() ?? string.Empty)}" +
            $"&numeroSerie={Uri.EscapeDataString(numeroSerie?.Trim() ?? string.Empty)}";

        internal static string UrlDescuadres(bool soloListaAlquileres) =>
            $"Aparatos/Descuadres?soloListaAlquileres={(soloListaAlquileres ? "true" : "false")}";

        public Task<HistoriaAparato> LeerHistoria(string producto, string numeroSerie) => Leer<HistoriaAparato>(UrlHistoria(producto, numeroSerie));

        public Task<InformeDescuadresAparatos> LeerDescuadres(bool soloListaAlquileres) => Leer<InformeDescuadresAparatos>(UrlDescuadres(soloListaAlquileres));

        private async Task<T> Leer<T>(string url)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    string motivo = response.StatusCode == HttpStatusCode.NotFound && string.IsNullOrWhiteSpace(json)
                        ? "El servidor todavía no tiene la historia de los aparatos."
                        : HttpErrorHelper.ParsearErrorHttp(json);
                    throw new AparatosApiException(motivo, response.StatusCode);
                }
                return JsonConvert.DeserializeObject<T>(json);
            }
        }
    }
}
