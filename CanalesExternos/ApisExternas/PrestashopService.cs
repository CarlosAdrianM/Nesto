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
using Nesto.Models.Nesto.Models;

namespace Nesto.Modulos.CanalesExternos.ApisExternas
{
    public class PrestashopService
    {
        public async Task<List<string>> CargarListaPedidosAsync()
        {
            // estado 2 = Pago Aceptamos
            // estado 3 = Preparación en curso
            // estado 10 = En espera de pago por transferencia
            string urlPrestashop = "http://www.productosdeesteticaypeluqueriaprofesional.com/api/orders?filter[current_state]=[2|3|10|58]";


            List<string> listaPrestashop = new List<string>();
            string userName;
            try
            {
                userName = ConfigurationManager.AppSettings["PrestashopWebserviceKeyNV"];
            } catch
            {
                return listaPrestashop;
            }
            

            using (var handler = new HttpClientHandler { Credentials = new NetworkCredential {UserName = userName} })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(urlPrestashop))
            using (HttpContent content = response.Content)
            {
                try
                {
                    string resultado = await content.ReadAsStringAsync();
                    resultado = resultado.TrimStart('\n');
                    //resultado = string.Format(resultado);
                    var xml = XDocument.Parse(resultado);

                    foreach (var node in xml.Descendants("order"))
                    {
                        listaPrestashop.Add(node.LastAttribute.Value); 
                    }

                } catch (Exception ex)
                {
                    throw ex;
                }
                
                return listaPrestashop;
            }
        }

        internal async Task<PedidoPrestashop> CargarPedidoPorReferenciaAsync(string urlPedido)
        {
            string urlPrestashop = urlPedido;
            string userName = ConfigurationManager.AppSettings["PrestashopWebserviceKeyNV"];

            XElement xmlPedido;

            // Cargamos el pedido
            using (var handler = new HttpClientHandler { Credentials = new NetworkCredential { UserName = userName } })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(urlPrestashop))
            using (HttpContent content = response.Content)
            {
                try
                {
                    string resultado = await content.ReadAsStringAsync();
                    resultado = resultado.TrimStart('\n');
                    xmlPedido = XDocument.Parse(resultado).Element("prestashop").Element("orders");
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            XElement xmlOrder = xmlPedido.Element("order");
            string urlPedidoId = (string)xmlOrder.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"));

            return await CargarPedidoAsync(urlPedidoId);
        }

        internal async Task<PedidoPrestashop> CargarPedidoAsync(string urlPedido)
        {
            string urlPrestashop = urlPedido;
            string userName = ConfigurationManager.AppSettings["PrestashopWebserviceKeyNV"];

            XElement xmlPedido;
            XElement xmlDireccion;
            XElement xmlCliente;
            XElement xmlPais;
            XElement xmlProvincia = null;

            PedidoPrestashop pedidoPrestashop = new PedidoPrestashop();


            // Cargamos el pedido
            using (var handler = new HttpClientHandler { Credentials = new NetworkCredential { UserName = userName } })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(urlPrestashop))
            using (HttpContent content = response.Content)
            {
                try
                {
                    string resultado = await content.ReadAsStringAsync();
                    resultado = resultado.TrimStart('\n');
                    xmlPedido = XDocument.Parse(resultado).Element("prestashop").Element("order");
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            // Cargamos la direccion
            XElement direccionXML = xmlPedido.Element("id_address_delivery");
            string urlDireccion = (string)direccionXML.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"));
            using (var handler = new HttpClientHandler { Credentials = new NetworkCredential { UserName = userName } })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(urlDireccion))
            using (HttpContent content = response.Content)
            {
                try
                {
                    string resultado = await content.ReadAsStringAsync();
                    resultado = resultado.TrimStart('\n');
                    xmlDireccion = XDocument.Parse(resultado).Element("prestashop").Element("address");
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            // Cargamos la provicina
            XElement provinciaXML = xmlDireccion.Element("id_state");
            string urlProvincia = (string)provinciaXML.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"));
            if (urlProvincia != null)
            {
                using (var handler = new HttpClientHandler { Credentials = new NetworkCredential { UserName = userName } })
                using (HttpClient client = new HttpClient(handler))
                using (HttpResponseMessage response = await client.GetAsync(urlProvincia))
                using (HttpContent content = response.Content)
                {
                    try
                    {
                        string resultado = await content.ReadAsStringAsync();
                        resultado = resultado.TrimStart('\n');
                        if (!string.IsNullOrEmpty(resultado))
                        {
                            xmlProvincia = XDocument.Parse(resultado).Element("prestashop").Element("state");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                }
            }

            // Cargamos el pais
            XElement paisXML = xmlDireccion.Element("id_country");
            string urlPais = (string)paisXML.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"));
            using (var handler = new HttpClientHandler { Credentials = new NetworkCredential { UserName = userName } })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(urlPais))
            using (HttpContent content = response.Content)
            {
                try
                {
                    string resultado = await content.ReadAsStringAsync();
                    resultado = resultado.TrimStart('\n');
                    xmlPais = XDocument.Parse(resultado).Element("prestashop").Element("country");
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            // Cargamos el cliente
            XElement clienteXML = xmlPedido.Element("id_customer");
            string urlCliente = (string)clienteXML.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"));
            using (var handler = new HttpClientHandler { Credentials = new NetworkCredential { UserName = userName } })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(urlCliente))
            using (HttpContent content = response.Content)
            {
                try
                {
                    string resultado = await content.ReadAsStringAsync();
                    resultado = resultado.TrimStart('\n');
                    xmlCliente = XDocument.Parse(resultado).Element("prestashop").Element("customer");
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            pedidoPrestashop.Pedido = xmlPedido;
            pedidoPrestashop.Direccion = xmlDireccion;
            pedidoPrestashop.Cliente = xmlCliente;
            pedidoPrestashop.Pais = xmlPais;
            pedidoPrestashop.Provincia = xmlProvincia;

            using (NestoEntities db = new NestoEntities())
            {
                string referencia = xmlPedido.Element("reference").Value;
                int? pedidoNesto = db.CabPedidoVta.Where(c => c.Comentarios.StartsWith(referencia)).FirstOrDefault()?.Número;
                if (pedidoNesto != null && pedidoNesto != 0)
                {
                    pedidoPrestashop.PedidoNestoId = (int)pedidoNesto;
                }
            }            

            return pedidoPrestashop;
        }
        internal async static Task<string> ObtenerPedidoPorReferenciaAsync(string referenciaPedido)
        {
            string baseUrl = "http://www.productosdeesteticaypeluqueriaprofesional.com/api";
            string apiKey;
            try
            {
                apiKey = ConfigurationManager.AppSettings["PrestashopWebserviceKeyNV"];
            }
            catch
            {
                return null;
            }

            using (var handler = new HttpClientHandler { Credentials = new NetworkCredential { UserName = apiKey } })
            using (HttpClient client = new HttpClient(handler))
            {
                // Construir la URL de búsqueda del pedido por referencia
                var searchUrl = $"{baseUrl}/orders?display=full&filter[reference]={referenciaPedido}";

                // Realizar la solicitud GET para buscar el pedido por referencia
                var searchResponse = await client.GetAsync(searchUrl);

                if (searchResponse.IsSuccessStatusCode)
                {
                    var searchXml = await searchResponse.Content.ReadAsStringAsync();
                    return searchXml;
                }
            }

            return null; // Si no se encontró el pedido o ocurrió un error, retornar null
        }
        internal async static Task<bool> CambiarEstadoPedidoAsync(string referenciaPedido, int nuevoEstado, bool mandarCorreo)
        {
            string baseUrl = "https://www.productosdeesteticaypeluqueriaprofesional.com/api";
            string apiKey;
            try
            {
                apiKey = ConfigurationManager.AppSettings["PrestashopWebserviceKeyNV"];
            }
            catch
            {
                return false;
            }

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

                using (var handler = new HttpClientHandler { Credentials = new NetworkCredential { UserName = apiKey } })
                using (HttpClient client = new HttpClient(handler))
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
                        var updateOrderUrl = $"{baseUrl}/order_histories";
                        if (mandarCorreo)
                        {
                            updateOrderUrl += "?sendemail=1";
                        }
                        var updateOrderContent = new StringContent(orderHistoryXml.ToString(), Encoding.UTF8, "application/xml");
                        var updateOrderResponse = await client.PostAsync(updateOrderUrl, updateOrderContent);

                        if (updateOrderResponse.IsSuccessStatusCode)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        internal async static Task<bool> ConfirmarPedidoAsync(string referenciaPedido, string agenciaId, string numeroSeguimiento, bool mandarCorreo)
        {
            string baseUrl = "https://www.productosdeesteticaypeluqueriaprofesional.com/api";
            string apiKey;
            try
            {
                apiKey = ConfigurationManager.AppSettings["PrestashopWebserviceKeyNV"];
            }
            catch
            {
                return false;
            }

            var pedidoXml = await ObtenerPedidoPorReferenciaAsync(referenciaPedido);

            if (!string.IsNullOrEmpty(pedidoXml))
            {

                // Parsear el XML del pedido
                var xmlPedido = XElement.Parse(pedidoXml);

                using (var handler = new HttpClientHandler { Credentials = new NetworkCredential { UserName = apiKey } })
                using (HttpClient client = new HttpClient(handler))
                {
                    // Obtener el ID del pedido
                    var idPedidoElement = xmlPedido.Descendants("id").FirstOrDefault();
                    if (idPedidoElement != null && int.TryParse(idPedidoElement.Value, out int idPedido))
                    {
                        // Crear un nuevo XML para <order_history> con los datos necesarios
                        var orderCarrierXml = new XElement("prestashop",
                            new XElement("order_carrier",
                                new XElement("id_order", idPedido),
                                new XElement("id_carrier", agenciaId),
                                new XElement("tracking_number", numeroSeguimiento)
                            )
                        );
                        // Actualizar el estado del pedido haciendo un POST a <order_histories>
                        var updateOrderUrl = $"{baseUrl}/order_carriers";
                        if (mandarCorreo)
                        {
                            updateOrderUrl += "?sendemail=1";
                        }
                        var updateOrderContent = new StringContent(orderCarrierXml.ToString(), Encoding.UTF8, "application/xml");
                        var updateOrderResponse = await client.PostAsync(updateOrderUrl, updateOrderContent);

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
