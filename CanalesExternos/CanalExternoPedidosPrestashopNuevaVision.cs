using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nesto.Models;
using Nesto.Modulos.CanalesExternos.ApisExternas;
using static Nesto.Models.PedidoVenta;
using Nesto.Contratos;
using System.Xml.Linq;
using System.Globalization;
using System.Xml;
using System.Collections.ObjectModel;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalExternoPedidosPrestashopNuevaVision : ICanalExternoPedidos
    {
        private IConfiguracion configuracion;
        public CanalExternoPedidosPrestashopNuevaVision(IConfiguracion configuracion)
        {
            this.configuracion = configuracion;
        }
        public async Task<ObservableCollection<PedidoVentaDTO>> GetAllPedidosAsync()
        {
            var servicio = new PrestashopService();
            var listaNesto = new ObservableCollection<PedidoVentaDTO>();

            var listaPrestashop = await servicio.CargarListaPedidosAsync();

            foreach (var urlPedido in listaPrestashop)
            {
                PedidoPrestashop pedidoPrestashop = await servicio.CargarPedidoAsync(urlPedido);
                listaNesto.Add(TransformarPedido(pedidoPrestashop));
            }
                        
            return listaNesto;
        }

        private PedidoVentaDTO TransformarPedido(PedidoPrestashop pedidoEntrada)
        {
            PedidoVentaDTO pedidoSalida = new PedidoVentaDTO();

            //XElement xmlPedido = pedidoEntrada.Pedido.Element("order");

            pedidoSalida.empresa = "1";
            pedidoSalida.cliente = "31517";
            pedidoSalida.contacto = "0";
            pedidoSalida.contactoCobro = "0";
            pedidoSalida.vendedor = "NV";

            pedidoSalida.iva = null;
            pedidoSalida.comentarios = pedidoEntrada.Pedido.Element("reference").Value + " \r\n";
            pedidoSalida.comentarios += pedidoEntrada.Direccion.Element("firstname").Value.ToString().ToUpper() + " ";
            pedidoSalida.comentarios += pedidoEntrada.Direccion.Element("lastname").Value.ToString().ToUpper() + "\r\n";
            pedidoSalida.comentarios += pedidoEntrada.Cliente.Element("email")?.Value.ToString() + "\r\n";
            pedidoSalida.comentarios += pedidoEntrada.Direccion.Element("address1")?.Value.ToString().ToUpper() + "\r\n";
            pedidoSalida.comentarios += pedidoEntrada.Direccion.Element("address2")?.Value != "" ? pedidoEntrada.Direccion.Element("address2")?.Value.ToString().ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += pedidoEntrada.Direccion.Element("postcode")?.Value.ToString().ToUpper() + " ";
            pedidoSalida.comentarios += pedidoEntrada.Direccion.Element("city")?.Value.ToString().ToUpper() + "\r\n";

            pedidoSalida.comentarios += pedidoEntrada.Direccion.Element("phone")?.Value != "" ? "Tel.: " + pedidoEntrada.Direccion.Element("phone")?.Value.ToString().ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += pedidoEntrada.Direccion.Element("phone_mobile")?.Value != "" ? "Móvil: " + pedidoEntrada.Direccion.Element("phone_mobile")?.Value.ToString().ToUpper() + "\r\n" : "";
            decimal totalPagado = Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_paid_real")?.Value) / 1000000;
            pedidoSalida.comentarios += "TOTAL PEDIDO: " + totalPagado.ToString("c");

            pedidoSalida.fecha = Convert.ToDateTime(pedidoEntrada.Pedido.Element("date_add")?.Value);
            
            decimal totalPedido = Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_products_wt")?.Value) / 1000000;
            decimal totalPortes = Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_shipping_tax_incl")?.Value) / 1000000;
            decimal totalDescuentos = Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_discounts_tax_incl")?.Value) / 1000000;
            if (totalPagado < totalPedido + totalPortes - totalDescuentos)
            {
                pedidoSalida.formaPago = "EFC";
                pedidoSalida.plazosPago = "CONTADO";
            } else
            {
                pedidoSalida.formaPago = "TRN";
                pedidoSalida.plazosPago = "PRE";
            }

            pedidoSalida.ruta = "00";
            pedidoSalida.serie = "NV";
            pedidoSalida.periodoFacturacion = "NRM";

            pedidoSalida.usuario = configuracion.usuario;


            // añadir líneas
            var listaLineasXML = pedidoEntrada.Pedido.Element("associations").Element("order_rows").Elements();
            foreach(var linea in listaLineasXML)
            {
                LineaPedidoVentaDTO lineaNesto = new LineaPedidoVentaDTO
                {
                    almacen = "ALG",
                    aplicarDescuento = false,
                    cantidad = short.Parse(linea.Element("product_quantity").Value),
                    delegacion = "ALG",
                    formaVenta = "WEB",
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = "G21", // TODO: LEER DEL PRODUCTO
                    precio = Convert.ToDecimal(linea.Element("unit_price_tax_incl").Value) / 1000000,
                    producto = linea.Element("product_reference").Value,
                    texto = linea.Element("product_name").Value.ToUpper(),
                    tipoLinea = 1, // producto
                    usuario = configuracion.usuario
                };
                pedidoSalida.LineasPedido.Add(lineaNesto);
            }

            // Añadir portes
            if (Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_shipping_tax_incl").Value) != 0)
            {
                LineaPedidoVentaDTO lineaPortes = new LineaPedidoVentaDTO
                {
                    almacen = "ALG",
                    aplicarDescuento = false,
                    cantidad = (short)1,
                    delegacion = "ALG",
                    formaVenta = "WEB",
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = "G21",
                    precio = totalPortes,
                    producto = "62400003",
                    texto = "GASTOS DE TRANSPORTE",
                    tipoLinea = 2, // cuenta contable
                    usuario = configuracion.usuario
                };
                pedidoSalida.LineasPedido.Add(lineaPortes);
            }

            // Añadir cupones de descuento
            if (Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_discounts_tax_incl").Value) != 0)
            {
                LineaPedidoVentaDTO lineaPortes = new LineaPedidoVentaDTO
                {
                    almacen = "ALG",
                    aplicarDescuento = false,
                    cantidad = (short)-1,
                    delegacion = "ALG",
                    formaVenta = "WEB",
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = "G21",
                    precio = totalDescuentos,
                    producto = "62700020",
                    texto = "CUPÓN DE DESCUENTO",
                    tipoLinea = 2, // cuenta contable
                    usuario = configuracion.usuario
                };
                pedidoSalida.LineasPedido.Add(lineaPortes);
            }
            
            return pedidoSalida;
        }

        public PedidoVentaDTO GetPedido(int Id)
        {
            throw new NotImplementedException();
        }
    }
}
