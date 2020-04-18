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
        public async Task<List<string>> CargarListaPedidosAsync()
        {
            // estado 2 = Pago Aceptamos
            // estado 3 = Preparación en curso
            string urlPrestashop = "http://www.productosdeesteticaypeluqueriaprofesional.com/api/orders?filter[current_state]=[2|3]";


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
                        xmlProvincia = XDocument.Parse(resultado).Element("prestashop").Element("state");
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

            return pedidoPrestashop;
        }
    }

    public class PedidoPrestashop
    {
        public XElement Pedido { get; set; }
        public XElement Direccion { get; set; }
        public XElement Cliente { get; set; }
        public XElement Pais { get; set; }
        public XElement Provincia { get; set; }
    }
}
