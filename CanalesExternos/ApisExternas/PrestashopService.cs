using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Nesto.Modulos.CanalesExternos.ApisExternas
{
    /// <summary>
    /// Cliente del webservice de una tienda Prestashop. Nesto#520: el mismo núcleo para todas las tiendas; la
    /// URL, la clave y cómo se presenta (autenticación básica o <c>ws_key</c> en la URL) los pone la
    /// <see cref="TiendaPrestashop"/>.
    /// </summary>
    public class PrestashopService
    {
        private static readonly XName XLINK_HREF = XName.Get("href", "http://www.w3.org/1999/xlink");

        private readonly Func<string, string> leerAjuste;

        public PrestashopService(TiendaPrestashop tienda) : this(tienda, clave => ConfigurationManager.AppSettings[clave])
        {
        }

        internal PrestashopService(TiendaPrestashop tienda, Func<string, string> leerAjuste)
        {
            Tienda = tienda ?? throw new ArgumentNullException(nameof(tienda));
            this.leerAjuste = leerAjuste;
        }

        public TiendaPrestashop Tienda { get; }

        private string LeerClave()
        {
            string clave = leerAjuste(Tienda.ClaveConfiguracion);
            if (string.IsNullOrWhiteSpace(clave))
            {
                throw new InvalidOperationException($"Falta la clave {Tienda.ClaveConfiguracion} del webservice de la tienda {Tienda.Nombre} en clavesSecretas.config.");
            }
            return clave;
        }

        /// <summary>
        /// La URL tal y como se pide a la tienda: con <c>ws_key</c> si la tienda lleva la clave en la URL (también
        /// en los enlaces xlink:href que devuelve la propia tienda, que vienen sin clave), o igual si va por
        /// autenticación básica. OJO: con la clave dentro, esta URL no se escribe en mensajes ni en ELMAH.
        /// </summary>
        internal string ConstruirUrl(string url)
        {
            if (Tienda.Autenticacion != AutenticacionPrestashop.ClaveEnUrl)
            {
                return url;
            }
            string separador = url.Contains('?') ? "&" : "?";
            return $"{url}{separador}ws_key={Uri.EscapeDataString(LeerClave())}";
        }

        /// <summary>Handler con la clave como usuario de la autenticación básica, o sin credenciales si va en la URL.</summary>
        internal HttpClientHandler CrearHandler()
        {
            return Tienda.Autenticacion == AutenticacionPrestashop.Basica
                ? new HttpClientHandler { Credentials = new NetworkCredential { UserName = LeerClave() } }
                : new HttpClientHandler();
        }

        private HttpClient CrearCliente()
        {
            return new HttpClient(CrearHandler(), disposeHandler: true);
        }

        private async Task<string> LeerTextoAsync(HttpClient client, string url)
        {
            using (HttpResponseMessage response = await client.GetAsync(ConstruirUrl(url)))
            {
                if (!response.IsSuccessStatusCode)
                {
                    // Sin la URL completa: con la clave en la URL acabaría en ELMAH
                    throw new HttpRequestException($"La tienda {Tienda.Nombre} ha respondido {(int)response.StatusCode} ({response.ReasonPhrase}) al leer {RecursoSinClave(url)}.");
                }
                string resultado = await response.Content.ReadAsStringAsync();
                return resultado.TrimStart('\n');
            }
        }

        // Solo el recurso (orders/44, addresses/7...), para los mensajes de error
        internal static string RecursoSinClave(string url)
        {
            string recurso = url ?? string.Empty;
            int posicionApi = recurso.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
            if (posicionApi >= 0)
            {
                recurso = recurso[(posicionApi + 5)..];
            }
            int posicionQuery = recurso.IndexOf('?');
            return posicionQuery >= 0 ? recurso[..posicionQuery] : recurso;
        }

        private async Task<XElement> LeerElementoAsync(HttpClient client, string url, string elemento)
        {
            string resultado = await LeerTextoAsync(client, url);
            return XDocument.Parse(resultado).Element("prestashop").Element(elemento);
        }

        public async Task<List<string>> CargarListaPedidosAsync()
        {
            // estado 2 = Pago Aceptamos
            // estado 3 = Preparación en curso
            // estado 10 = En espera de pago por transferencia
            string urlPrestashop = $"{Tienda.UrlApi}/orders?filter[current_state]=[2|3|10|58]";

            List<string> listaPrestashop = new List<string>();
            using (HttpClient client = CrearCliente())
            {
                var xml = XDocument.Parse(await LeerTextoAsync(client, urlPrestashop));
                foreach (var node in xml.Descendants("order"))
                {
                    listaPrestashop.Add(node.LastAttribute.Value);
                }
            }
            return listaPrestashop;
        }

        internal async Task<PedidoPrestashop> CargarPedidoPorReferenciaAsync(string referenciaPedido)
        {
            string urlPedidoId;
            using (HttpClient client = CrearCliente())
            {
                XElement xmlPedidos = await LeerElementoAsync(client, $"{Tienda.UrlApi}/orders?filter[reference]={referenciaPedido}", "orders");
                XElement xmlOrder = xmlPedidos.Element("order");
                urlPedidoId = (string)xmlOrder.Attribute(XLINK_HREF);
            }

            return await CargarPedidoAsync(urlPedidoId);
        }

        internal async Task<PedidoPrestashop> CargarPedidoAsync(string urlPedido)
        {
            PedidoPrestashop pedidoPrestashop = new PedidoPrestashop();

            using (HttpClient client = CrearCliente())
            {
                // El pedido
                XElement xmlPedido = await LeerElementoAsync(client, urlPedido, "order");

                // La dirección de entrega
                string urlDireccion = (string)xmlPedido.Element("id_address_delivery").Attribute(XLINK_HREF);
                XElement xmlDireccion = await LeerElementoAsync(client, urlDireccion, "address");

                // La provincia (puede no tener)
                XElement xmlProvincia = null;
                string urlProvincia = (string)xmlDireccion.Element("id_state").Attribute(XLINK_HREF);
                if (urlProvincia != null)
                {
                    string resultado = await LeerTextoAsync(client, urlProvincia);
                    if (!string.IsNullOrEmpty(resultado))
                    {
                        xmlProvincia = XDocument.Parse(resultado).Element("prestashop").Element("state");
                    }
                }

                // El país
                string urlPais = (string)xmlDireccion.Element("id_country").Attribute(XLINK_HREF);
                XElement xmlPais = await LeerElementoAsync(client, urlPais, "country");

                // El cliente
                string urlCliente = (string)xmlPedido.Element("id_customer").Attribute(XLINK_HREF);
                XElement xmlCliente = await LeerElementoAsync(client, urlCliente, "customer");

                pedidoPrestashop.Pedido = xmlPedido;
                pedidoPrestashop.Direccion = xmlDireccion;
                pedidoPrestashop.Cliente = xmlCliente;
                pedidoPrestashop.Pais = xmlPais;
                pedidoPrestashop.Provincia = xmlProvincia;
            }
            // Nesto#340: el PedidoNestoId lo resuelve el llamante por la API
            // (api/PedidosVenta/PorReferenciaCanal); este servicio queda como cliente puro
            // de la API de Prestashop, sin EF.

            return pedidoPrestashop;
        }

        internal async Task<string> ObtenerPedidoPorReferenciaAsync(string referenciaPedido)
        {
            using (HttpClient client = CrearCliente())
            {
                // Construir la URL de búsqueda del pedido por referencia
                var searchUrl = $"{Tienda.UrlApi}/orders?display=full&filter[reference]={referenciaPedido}";

                // Realizar la solicitud GET para buscar el pedido por referencia
                var searchResponse = await client.GetAsync(ConstruirUrl(searchUrl));

                if (searchResponse.IsSuccessStatusCode)
                {
                    var searchXml = await searchResponse.Content.ReadAsStringAsync();
                    return searchXml;
                }
            }

            return null; // Si no se encontró el pedido o ocurrió un error, retornar null
        }

        internal async Task<bool> CambiarEstadoPedidoAsync(string referenciaPedido, int nuevoEstado, bool mandarCorreo)
        {
            var pedidoXml = await ObtenerPedidoPorReferenciaAsync(referenciaPedido);

            if (!string.IsNullOrEmpty(pedidoXml))
            {
                // Parsear el XML del pedido
                var xmlPedido = XElement.Parse(pedidoXml);

                // No actualizamos si el pedido está pendiente de transferencia
                var estadoActual = xmlPedido.Descendants("current_state").FirstOrDefault().Value;
                if (estadoActual == "10")
                {
                    return false;
                }

                using (HttpClient client = CrearCliente())
                {
                    // Obtener el ID del pedido
                    var idPedidoElement = xmlPedido.Descendants("id").FirstOrDefault();
                    if (idPedidoElement != null && int.TryParse(idPedidoElement.Value, out int idPedido))
                    {
                        // Crear un nuevo XML para <order_history> con los datos necesarios
                        var orderHistoryXml = new XElement("prestashop",
                            new XElement("order_history",
                                new XElement("id_order", idPedido),
                                new XElement("id_order_state", nuevoEstado) // Aquí debes especificar el ID del estado deseado
                            )
                        );
                        // Actualizar el estado del pedido haciendo un POST a <order_histories>
                        var updateOrderUrl = $"{Tienda.UrlApi}/order_histories";
                        if (mandarCorreo)
                        {
                            updateOrderUrl += "?sendemail=1";
                        }
                        var updateOrderContent = new StringContent(orderHistoryXml.ToString(), Encoding.UTF8, "application/xml");
                        var updateOrderResponse = await client.PostAsync(ConstruirUrl(updateOrderUrl), updateOrderContent);

                        if (updateOrderResponse.IsSuccessStatusCode)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        internal async Task<bool> ConfirmarPedidoAsync(string referenciaPedido, string agenciaId, string numeroSeguimiento, bool mandarCorreo)
        {
            var pedidoXml = await ObtenerPedidoPorReferenciaAsync(referenciaPedido);

            if (!string.IsNullOrEmpty(pedidoXml))
            {

                // Parsear el XML del pedido
                var xmlPedido = XElement.Parse(pedidoXml);

                using (HttpClient client = CrearCliente())
                {
                    // Obtener el ID del pedido
                    var idPedidoElement = xmlPedido.Descendants("id").FirstOrDefault();
                    if (idPedidoElement != null && int.TryParse(idPedidoElement.Value, out int idPedido))
                    {
                        // Nesto#454: el pedido YA TIENE su fila en order_carriers (Prestashop la crea
                        // al nacer el pedido). Lo correcto es ACTUALIZARLA, no crear otra con POST,
                        // que era el apaño de cuando el webservice de Prestashop 1.7 no admitía
                        // PATCH y no llegó a funcionar nunca. Con Prestashop 8 el índice del API ya
                        // expone patch/put por recurso (requiere concederlos a la clave en
                        // Parámetros avanzados → Webservice).
                        var carriersUrl = $"{Tienda.UrlApi}/order_carriers?filter[id_order]={idPedido}&display=full";
                        var carriersResponse = await client.GetAsync(ConstruirUrl(carriersUrl));
                        if (!carriersResponse.IsSuccessStatusCode)
                        {
                            return false;
                        }

                        var carriersXml = XElement.Parse(await carriersResponse.Content.ReadAsStringAsync());
                        // Si el POST antiguo llegó a colar filas duplicadas, la original es la de
                        // menor id, que es la que Prestashop muestra en el pedido.
                        var orderCarrier = carriersXml.Descendants("order_carrier")
                            .OrderBy(oc => (int?)oc.Element("id") ?? int.MaxValue)
                            .FirstOrDefault();
                        var idOrderCarrier = (int?)orderCarrier?.Element("id");
                        if (orderCarrier == null || idOrderCarrier == null)
                        {
                            return false;
                        }

                        orderCarrier.SetElementValue("id_carrier", agenciaId);
                        orderCarrier.SetElementValue("tracking_number", numeroSeguimiento);
                        // Los atributos xlink:href del GET no se aceptan al escribir
                        foreach (var elemento in orderCarrier.DescendantsAndSelf())
                        {
                            elemento.RemoveAttributes();
                        }
                        var orderCarrierXml = new XElement("prestashop", orderCarrier);

                        // sendemail=1: Prestashop avisa al cliente del cambio de transportista con
                        // el número de seguimiento (correo "en tránsito")
                        var updateOrderUrl = $"{Tienda.UrlApi}/order_carriers/{idOrderCarrier}";
                        if (mandarCorreo)
                        {
                            updateOrderUrl += "?sendemail=1";
                        }
                        var updateOrderContent = new StringContent(orderCarrierXml.ToString(), Encoding.UTF8, "application/xml");

                        // PATCH (parcial, lo natural en Prestashop 8); si la clave solo tiene PUT
                        // concedido, se reintenta con PUT y el recurso completo que acabamos de leer
                        var updateOrderResponse = await client.PatchAsync(ConstruirUrl(updateOrderUrl), updateOrderContent);
                        if (!updateOrderResponse.IsSuccessStatusCode)
                        {
                            updateOrderContent = new StringContent(orderCarrierXml.ToString(), Encoding.UTF8, "application/xml");
                            updateOrderResponse = await client.PutAsync(ConstruirUrl(updateOrderUrl), updateOrderContent);
                        }

                        if (updateOrderResponse.IsSuccessStatusCode)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }

    public class PedidoPrestashop
    {
        public XElement Pedido { get; set; }
        public XElement Direccion { get; set; }
        public XElement Cliente { get; set; }
        public XElement Pais { get; set; }
        public XElement Provincia { get; set; }
        public int PedidoNestoId { get; set; }
    }

    [XmlRoot("prestashop")]
    public class PrestashopResponse
    {
        [XmlArray("orders")]
        [XmlArrayItem("order")]
        public List<Order> Orders { get; set; }
    }
    public class Order
    {
        [XmlElement("id")]
        public string Id { get; set; }
    }
}
