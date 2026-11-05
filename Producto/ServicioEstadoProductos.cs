using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    // Nesto#624 (NestoAPI#738, sugerencia 793 de Manuel): el cambio de estado de los productos, masivo (ventana «Cambiar estado»)
    // y de uno (desplegable de la ficha). Toda la lógica está en la API: Nesto solo pinta y llama. Nada de tocar
    // Productos.Estado ni ControlesStock por EF/SQL desde aquí.

    /// <summary>Un estado de la tabla EstadosProducto de la empresa (GET api/Productos/Estados).</summary>
    public class EstadoProductoDTO
    {
        public short Estado { get; set; }
        public string Descripcion { get; set; }
        public bool AExtinguir { get; set; }
        public bool AAnular { get; set; }

        /// <summary>Lo que sale en los desplegables: «3 · EXTINGUIR (No queremos trabajarlo)».</summary>
        [JsonIgnore]
        public string Texto => string.IsNullOrWhiteSpace(Descripcion) ? Estado.ToString() : $"{Estado} · {Descripcion.Trim()}";

        public override string ToString() => Texto;
    }

    /// <summary>Qué productos entran en el cambio. Los criterios que vengan se cumplen a la vez (Y).</summary>
    public class FiltroEstadoProductos
    {
        public string Proveedor { get; set; }
        public string Familia { get; set; }
        public string Grupo { get; set; }
        public string Subgrupo { get; set; }
        public List<string> Productos { get; set; }
    }

    public class VistaPreviaEstadoProductosPeticion
    {
        public string Empresa { get; set; }
        public FiltroEstadoProductos Filtro { get; set; }
        public short? EstadoNuevo { get; set; }
    }

    public class PedidoPendienteEstadoDTO
    {
        public int Pedido { get; set; }
        public string Cliente { get; set; }
        public int Cantidad { get; set; }
    }

    public class ControlesStockAlmacenesDTO
    {
        public bool ALG { get; set; }
        public bool REI { get; set; }
        public bool ALC { get; set; }
    }

    /// <summary>Un control de stock que la API creará al volver a Disponible.</summary>
    public class ControlStockSugeridoDTO
    {
        public string Almacen { get; set; }
        public int StockMinimo { get; set; }
        public int StockMaximo { get; set; }
        public string Explicacion { get; set; }
    }

    public class ProductoEstadoVistaPreviaDTO
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public short? EstadoActual { get; set; }
        public short EstadoNuevo { get; set; }
        public int StockALG { get; set; }
        public int StockREI { get; set; }
        public int StockALC { get; set; }
        public int PendienteRecibirProveedor { get; set; }
        public List<PedidoPendienteEstadoDTO> PedidosPendientes { get; set; } = new List<PedidoPendienteEstadoDTO>();
        public ControlesStockAlmacenesDTO TieneControlesStock { get; set; } = new ControlesStockAlmacenesDTO();
        public List<ControlStockSugeridoDTO> ControlesSugeridos { get; set; } = new List<ControlStockSugeridoDTO>();
        public List<string> Avisos { get; set; } = new List<string>();
        /// <summary>Null: se cambiará. Si no: por qué no (ya está en ese estado, no existe).</summary>
        public string MotivoNoSeCambia { get; set; }
    }

    public class VistaPreviaEstadoProductosDTO
    {
        public List<ProductoEstadoVistaPreviaDTO> Productos { get; set; } = new List<ProductoEstadoVistaPreviaDTO>();
        public List<string> Avisos { get; set; } = new List<string>();
        public int ControlesStockAfectados { get; set; }
        public int ProductosConPedidosPendientes { get; set; }
        /// <summary>La pregunta que se hace UNA vez por lote (null si no hay que preguntar).</summary>
        public string PreguntaControlesStock { get; set; }
        /// <summary>Si el usuario puede aplicar (Compras o Dirección).</summary>
        public bool PuedeCambiar { get; set; }
    }

    public class CambioEstadoProductosPeticion
    {
        public string Empresa { get; set; }
        public List<string> Productos { get; set; }
        public short? EstadoNuevo { get; set; }
        public bool QuitarControlesStock { get; set; }
        /// <summary>No hace falta: manda el del token.</summary>
        public string Usuario { get; set; }
    }

    public class ProductoOmitidoDTO
    {
        public string Producto { get; set; }
        public string Motivo { get; set; }
    }

    public class ResultadoCambioEstadoProductosDTO
    {
        public int Cambiados { get; set; }
        public List<ProductoOmitidoDTO> Omitidos { get; set; } = new List<ProductoOmitidoDTO>();
        public int ControlesStockQuitados { get; set; }
        public int ControlesStockCreados { get; set; }
        public List<string> Avisos { get; set; } = new List<string>();
    }

    /// <summary>Lo que contesta la API cuando no es un 2xx: su texto (ya en castellano) y el código.</summary>
    public class EstadoProductosException : Exception
    {
        public EstadoProductosException(string mensaje, int codigo) : base(mensaje)
        {
            Codigo = codigo;
        }

        public int Codigo { get; }
    }

    /// <summary>Nesto#624 (NestoAPI#738): el cliente de los tres endpoints del estado de los productos.</summary>
    public interface IServicioEstadoProductos
    {
        /// <summary>GET api/Productos/Estados?empresa=: los estados para los desplegables.</summary>
        Task<List<EstadoProductoDTO>> LeerEstados(string empresa);

        /// <summary>POST api/Productos/Estado/VistaPrevia: no cambia nada.</summary>
        /// <exception cref="EstadoProductosException">400 con el texto de la API.</exception>
        Task<VistaPreviaEstadoProductosDTO> VistaPrevia(string empresa, FiltroEstadoProductos filtro, short estadoNuevo);

        /// <summary>POST api/Productos/Estado: cambia el estado (todo o nada).</summary>
        /// <exception cref="EstadoProductosException">403 (solo Compras y Dirección), 400, 500 con el texto de la API.</exception>
        Task<ResultadoCambioEstadoProductosDTO> Cambiar(string empresa, IEnumerable<string> productos, short estadoNuevo, bool quitarControlesStock);
    }

    public class ServicioEstadoProductos : IServicioEstadoProductos
    {
        internal const string URL_ESTADOS = "Productos/Estados";
        internal const string URL_VISTA_PREVIA = "Productos/Estado/VistaPrevia";
        internal const string URL_CAMBIAR = "Productos/Estado";
        internal const string MENSAJE_SIN_PERMISO = "Solo Compras y Dirección pueden cambiar el estado de los productos.";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioEstadoProductos(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        private static string Empresa(string empresa) => string.IsNullOrWhiteSpace(empresa) ? "1" : empresa.Trim();

        private static string Limpio(string texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

        /// <summary>El filtro tal cual va a la API: lo vacío, null; las referencias sin espacios ni repetidas.</summary>
        internal static FiltroEstadoProductos Normalizar(FiltroEstadoProductos filtro)
        {
            List<string> productos = filtro?.Productos?
                .Select(Limpio)
                .Where(p => p != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new FiltroEstadoProductos
            {
                Proveedor = Limpio(filtro?.Proveedor),
                Familia = Limpio(filtro?.Familia),
                Grupo = Limpio(filtro?.Grupo),
                Subgrupo = Limpio(filtro?.Subgrupo),
                Productos = productos == null || productos.Count == 0 ? null : productos
            };
        }

        public async Task<List<EstadoProductoDTO>> LeerEstados(string empresa)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync($"{URL_ESTADOS}?empresa={Uri.EscapeDataString(Empresa(empresa))}").ConfigureAwait(false);
                await LanzarSiError(response).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<List<EstadoProductoDTO>>(json) ?? new List<EstadoProductoDTO>();
            }
        }

        public async Task<VistaPreviaEstadoProductosDTO> VistaPrevia(string empresa, FiltroEstadoProductos filtro, short estadoNuevo)
        {
            var peticion = new VistaPreviaEstadoProductosPeticion
            {
                Empresa = Empresa(empresa),
                Filtro = Normalizar(filtro),
                EstadoNuevo = estadoNuevo
            };
            return await Post<VistaPreviaEstadoProductosDTO>(URL_VISTA_PREVIA, peticion).ConfigureAwait(false)
                ?? new VistaPreviaEstadoProductosDTO();
        }

        public async Task<ResultadoCambioEstadoProductosDTO> Cambiar(string empresa, IEnumerable<string> productos, short estadoNuevo, bool quitarControlesStock)
        {
            var peticion = new CambioEstadoProductosPeticion
            {
                Empresa = Empresa(empresa),
                Productos = (productos ?? Enumerable.Empty<string>()).Select(Limpio).Where(p => p != null)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                EstadoNuevo = estadoNuevo,
                QuitarControlesStock = quitarControlesStock,
                Usuario = null
            };
            return await Post<ResultadoCambioEstadoProductosDTO>(URL_CAMBIAR, peticion).ConfigureAwait(false)
                ?? new ResultadoCambioEstadoProductosDTO();
        }

        private async Task<T> Post<T>(string url, object cuerpo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpContent contenido = new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, contenido).ConfigureAwait(false);
                await LanzarSiError(response).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<T>(json);
            }
        }

        private static async Task LanzarSiError(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }
            int codigo = (int)response.StatusCode;
            string cuerpo = response.Content == null ? null : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            string motivo = ProductoService.MotivoDelErrorDeTexto(cuerpo);
            if (string.IsNullOrWhiteSpace(motivo))
            {
                motivo = codigo == 403 ? MENSAJE_SIN_PERMISO : $"El servidor ha contestado con el error {codigo}.";
            }
            throw new EstadoProductosException(motivo, codigo);
        }
    }
}
