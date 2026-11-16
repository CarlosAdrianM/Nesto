using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// NestoAPI#869: la próxima entrada de compra del producto para la ficha (GET api/Productos/ProximaEntrada). Nesto no decide nada:
    /// la API elige el pedido en el aire o la fecha orientativa (#853) y da la frase hecha (Texto) y la certeza.
    /// </summary>
    public interface IServicioProximaEntradaProducto
    {
        /// <summary>Null si la API falla, es anterior al endpoint (404) o el producto no existe: la ficha se abre igual, sin la línea.</summary>
        Task<ProximaEntradaProducto> Leer(string empresa, string producto, string almacen);
    }

    /// <summary>NestoAPI#869 (ProximaEntradaProductoDTO): solo lo que pinta la ficha.</summary>
    public class ProximaEntradaProducto
    {
        public const string TIPO_PEDIDO = "Pedido";
        public const string TIPO_ORIENTATIVA = "Orientativa";
        public const string TIPO_SIN_DATOS = "SinDatos";

        public string Producto { get; set; }
        public string Almacen { get; set; }
        public string Tipo { get; set; }
        public DateTime? Fecha { get; set; }
        public string Certeza { get; set; }
        public string EtiquetaCerteza { get; set; }
        public string Texto { get; set; }
        public string Motivo { get; set; }
        public int? Pedido { get; set; }
        public string NombreProveedorPedido { get; set; }
        public bool PorCorreoSeguimiento { get; set; }
        public bool Recalculada { get; set; }
        public bool PrevistaPasada { get; set; }
        public string ProveedorHabitual { get; set; }
        public string NombreProveedorHabitual { get; set; }
        public DateTime? FechaSiSePidieraHoy { get; set; }
        public DateTime? FechaProximoPedidoHabitual { get; set; }
        public DateTime? LlegadaConPedidoHabitual { get; set; }
        public int? CadenciaDias { get; set; }
        public int? PlazoDias { get; set; }
        public bool PlazoPropio { get; set; }
    }

    public class ServicioProximaEntradaProducto : IServicioProximaEntradaProducto
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioProximaEntradaProducto(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        internal static string Url(string empresa, string producto, string almacen) =>
            $"Productos/ProximaEntrada?producto={Uri.EscapeDataString(producto?.Trim() ?? string.Empty)}" +
            $"&almacen={Uri.EscapeDataString(string.IsNullOrWhiteSpace(almacen) ? "ALG" : almacen.Trim())}" +
            $"&empresa={Uri.EscapeDataString(string.IsNullOrWhiteSpace(empresa) ? "1" : empresa.Trim())}";

        public async Task<ProximaEntradaProducto> Leer(string empresa, string producto, string almacen)
        {
            if (string.IsNullOrWhiteSpace(producto))
            {
                return null;
            }
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    HttpResponseMessage response = await client.GetAsync(Url(empresa, producto, almacen)).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<ProximaEntradaProducto>(json);
                }
            }
            catch (Exception)
            {
                // Es información de apoyo: sin ella, la ficha se abre igual
                return null;
            }
        }
    }
}
