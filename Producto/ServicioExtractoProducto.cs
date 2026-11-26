using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    // Sugerencia 1482 (Manuel, Compras; OK de Carlos 26/11/26): la referencia del proveedor en la ficha y el extracto del producto.

    /// <summary>GET api/Productos/ReferenciasProveedor: una fila de ProveedoresProducto.</summary>
    public class ReferenciaProveedorProducto
    {
        public string Proveedor { get; set; }
        public string NombreProveedor { get; set; }
        public string ReferenciaProveedor { get; set; }
        public int? Orden { get; set; }
        public bool EsHabitual { get; set; }
    }

    /// <summary>Un movimiento del extracto del producto (ExtractoProducto de las empresas 1 y 3).</summary>
    public class MovimientoExtractoProducto
    {
        public int NumeroOrden { get; set; }
        public string Empresa { get; set; }
        public DateTime Fecha { get; set; }
        public string Almacen { get; set; }
        public decimal Cantidad { get; set; }
        public decimal Saldo { get; set; }
        /// <summary>Null a quien no puede ver costes.</summary>
        public decimal? Importe { get; set; }
        public string Texto { get; set; }
        public string Diario { get; set; }
        public int? Albaran { get; set; }
        /// <summary>Null a quien no puede ver de quién se compra.</summary>
        public string Proveedor { get; set; }
        public string NombreProveedor { get; set; }
        public string Cliente { get; set; }
        public string ContactoCliente { get; set; }
        public string NombreCliente { get; set; }
        public int? Pedido { get; set; }
        public int? LineaPedido { get; set; }
        public int? Traspaso { get; set; }
        public bool AsientoAutomatico { get; set; }
        public string Delegacion { get; set; }
        public string FormaVenta { get; set; }
        public string Usuario { get; set; }

        /// <summary>«15191 PELUQUERÍA PEPA» (sin cliente, vacío).</summary>
        public string TextoCliente => string.IsNullOrWhiteSpace(Cliente) ? null
            : (Cliente.Trim() + (string.IsNullOrWhiteSpace(ContactoCliente) || ContactoCliente.Trim() == "0" ? "" : "/" + ContactoCliente.Trim())
               + (string.IsNullOrWhiteSpace(NombreCliente) ? "" : " " + NombreCliente.Trim()));

        /// <summary>«1234 PROVEEDOR S.L.» (sin proveedor, vacío).</summary>
        public string TextoProveedor => string.IsNullOrWhiteSpace(Proveedor) ? null
            : Proveedor.Trim() + (string.IsNullOrWhiteSpace(NombreProveedor) ? "" : " " + NombreProveedor.Trim());
    }

    /// <summary>GET api/Productos/Extracto.</summary>
    public class ExtractoProducto
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public string Almacen { get; set; }
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public decimal SaldoInicial { get; set; }
        public decimal SaldoFinal { get; set; }
        public int Total { get; set; }
        public int Pagina { get; set; }
        public int Tamano { get; set; }
        public List<MovimientoExtractoProducto> Movimientos { get; set; } = new();
    }

    /// <summary>Un error de la API con su mensaje (403 de usted…).</summary>
    public class FichaProductoApiException : Exception
    {
        public FichaProductoApiException(int estado, string mensaje) : base(mensaje)
        {
            Estado = estado;
        }

        public int Estado { get; }
    }

    public interface IServicioExtractoProducto
    {
        /// <summary>Null si no se puede ver (403), falla o no existe: la ficha no pinta la línea.</summary>
        Task<List<ReferenciaProveedorProducto>> LeerReferenciasProveedor(string empresa, string producto);
        /// <summary>Lanza <see cref="FichaProductoApiException"/> con el mensaje de la API.</summary>
        Task<ExtractoProducto> LeerExtracto(string empresa, string producto, string almacen, DateTime? desde, DateTime? hasta, int pagina, int tamano);
    }

    public class ServicioExtractoProducto : IServicioExtractoProducto
    {
        public const string MENSAJE_SIN_PERMISO = "No tiene permiso para ver el extracto del producto.";
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioExtractoProducto(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        private static string Empresa(string empresa) => Uri.EscapeDataString(string.IsNullOrWhiteSpace(empresa) ? "1" : empresa.Trim());

        internal static string UrlReferencias(string empresa, string producto)
            => $"Productos/ReferenciasProveedor?producto={Uri.EscapeDataString(producto?.Trim() ?? string.Empty)}&empresa={Empresa(empresa)}";

        internal static string UrlExtracto(string empresa, string producto, string almacen, DateTime? desde, DateTime? hasta, int pagina, int tamano)
        {
            string url = $"Productos/Extracto?producto={Uri.EscapeDataString(producto?.Trim() ?? string.Empty)}&empresa={Empresa(empresa)}";
            if (!string.IsNullOrWhiteSpace(almacen))
            {
                url += "&almacen=" + Uri.EscapeDataString(almacen.Trim());
            }
            if (desde.HasValue)
            {
                url += "&desde=" + desde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            if (hasta.HasValue)
            {
                url += "&hasta=" + hasta.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            return url + $"&pagina={pagina}&tamano={tamano}";
        }

        public async Task<List<ReferenciaProveedorProducto>> LeerReferenciasProveedor(string empresa, string producto)
        {
            if (string.IsNullOrWhiteSpace(producto))
            {
                return null;
            }
            try
            {
                using HttpClient client = _clienteApiFactory.Crear();
                HttpResponseMessage response = await client.GetAsync(UrlReferencias(empresa, producto));
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                string json = await response.Content.ReadAsStringAsync();
                return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<List<ReferenciaProveedorProducto>>(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task<ExtractoProducto> LeerExtracto(string empresa, string producto, string almacen, DateTime? desde, DateTime? hasta, int pagina, int tamano)
        {
            using HttpClient client = _clienteApiFactory.Crear();
            HttpResponseMessage response = await client.GetAsync(UrlExtracto(empresa, producto, almacen, desde, hasta, pagina, tamano));
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                string mensaje = LeerMensaje(body);
                if (string.IsNullOrWhiteSpace(mensaje))
                {
                    mensaje = response.StatusCode == HttpStatusCode.Forbidden ? MENSAJE_SIN_PERMISO
                        : $"No se ha podido leer el extracto del producto ({(int)response.StatusCode}).";
                }
                throw new FichaProductoApiException((int)response.StatusCode, mensaje);
            }
            return JsonConvert.DeserializeObject<ExtractoProducto>(body) ?? new ExtractoProducto();
        }

        internal static string LeerMensaje(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }
            try
            {
                JToken json = JToken.Parse(body);
                return json.Type == JTokenType.String ? json.ToString() : json["Message"]?.ToString();
            }
            catch
            {
                return body;
            }
        }
    }

    /// <summary>Lo que pinta la ficha de las referencias del proveedor (puro).</summary>
    public static class ReglasReferenciaProveedor
    {
        /// <summary>«Ref. proveedor: ABC-123» del habitual; null si no hay (o sin referencia).</summary>
        public static string Texto(IEnumerable<ReferenciaProveedorProducto> referencias)
        {
            ReferenciaProveedorProducto habitual = Habitual(referencias);
            return string.IsNullOrWhiteSpace(habitual?.ReferenciaProveedor) ? null : "Ref. proveedor: " + habitual.ReferenciaProveedor.Trim();
        }

        /// <summary>El tooltip: el habitual con su nombre y, si hay más, los demás.</summary>
        public static string Ayuda(IEnumerable<ReferenciaProveedorProducto> referencias)
        {
            List<ReferenciaProveedorProducto> lista = (referencias ?? Enumerable.Empty<ReferenciaProveedorProducto>()).Where(r => r != null).ToList();
            ReferenciaProveedorProducto habitual = Habitual(lista);
            if (habitual == null || string.IsNullOrWhiteSpace(habitual.ReferenciaProveedor))
            {
                return null;
            }
            var lineas = new List<string> { "Proveedor habitual: " + Linea(habitual) };
            List<ReferenciaProveedorProducto> otros = lista.Where(r => !ReferenceEquals(r, habitual)).ToList();
            if (otros.Any())
            {
                lineas.Add("Otros proveedores:");
                lineas.AddRange(otros.Select(r => "· " + Linea(r)));
            }
            return string.Join(Environment.NewLine, lineas);
        }

        private static ReferenciaProveedorProducto Habitual(IEnumerable<ReferenciaProveedorProducto> referencias)
            => (referencias ?? Enumerable.Empty<ReferenciaProveedorProducto>()).FirstOrDefault(r => r != null && r.EsHabitual);

        private static string Linea(ReferenciaProveedorProducto r)
        {
            string nombre = string.IsNullOrWhiteSpace(r.NombreProveedor) ? r.Proveedor?.Trim() : $"{r.Proveedor?.Trim()} {r.NombreProveedor.Trim()}";
            return string.IsNullOrWhiteSpace(r.ReferenciaProveedor) ? nombre : $"{nombre} (ref. {r.ReferenciaProveedor.Trim()})";
        }
    }
}
