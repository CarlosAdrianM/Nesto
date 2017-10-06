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
using Nesto.Models;
using static Nesto.Models.PedidoVenta;

namespace Nesto.Modulos.CanalesExternos.ApisExternas
{
    public class PrestashopService
    {
    
        private const string CLIENTE_PRESTASHOP = "10458";

        public async Task<List<string>> CargarListaPedidosAsync()
        {
            // estado 2 = Pago Aceptamos
            // estado 3 = Preparación en curso
            string urlPrestashop = "http://www.productosdeesteticaypeluqueriaprofesional.com/api/orders?filter[current_state]=[2|3]";


            List<string> listaPrestashop = new List<string>();
            string userName = ConfigurationManager.AppSettings["PrestashopWebserviceKeyNV"];

            using (var handler = new HttpClientHandler { Credentials = new NetworkCredential {UserName = userName} })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(urlPrestashop))
            using (HttpContent content = response.Content)
            {
                try
                {
                    string resultado = await content.ReadAsStringAsync();
                    //resultado = string.Format(resultado);
                    var xml = XDocument.Parse(resultado);

                    foreach (var node in xml.Descendants("order"))
                    {
                        listaPrestashop.Add(node.LastAttribute.Value); //or loop through its children as well
                    }


                    /*
                    var query = from c in xml.Root.Descendants("order")
                                    //where (int)c.Attribute("id") < 4
                                select c.Element("order").LastAttribute.Value;
                    //     c.Element("total_paid_real").Value;

                    foreach (string urlPedido in query)
                    {
                        listaPrestashop.Add(urlPedido);
                    }
                    */
                } catch (Exception ex)
                {
                    throw;
                }
                
                return listaPrestashop;
            }
        }

        internal async Task<PedidoPrestashop> CargarPedidoAsync(string urlPedido)
        {
            string urlPrestashop = urlPedido;
            string userName = ConfigurationManager.AppSettings["PrestashopWebserviceKeyNV"];

            XElement xmlPedido;
            XElement xmlDireccion;
            XElement xmlCliente;

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
                    xmlPedido = XDocument.Parse(resultado).Element("prestashop").Element("order");
                }
                catch (Exception ex)
                {
                    throw;
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
                    xmlDireccion = XDocument.Parse(resultado).Element("prestashop").Element("address");
                }
                catch (Exception ex)
                {
                    throw;
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
                    xmlCliente = XDocument.Parse(resultado).Element("prestashop").Element("customer");
                }
                catch (Exception ex)
                {
                    throw;
                }
            }

            pedidoPrestashop.Pedido = xmlPedido;
            pedidoPrestashop.Direccion = xmlDireccion;
            pedidoPrestashop.Cliente = xmlCliente;

            return pedidoPrestashop;
        }
    }

    public class PedidoPrestashop
    {
        public XElement Pedido { get; set; }
        public XElement Direccion { get; set; }
        public XElement Cliente { get; set; }
    }
}
