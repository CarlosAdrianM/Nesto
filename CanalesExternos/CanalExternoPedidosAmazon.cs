using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nesto.Models;
using MarketplaceWebServiceOrders;
using MarketplaceWebServiceOrders.Model;
using static Nesto.Models.PedidoVenta;
using System.Collections.ObjectModel;
using Nesto.Contratos;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalExternoPedidosAmazon : ICanalExternoPedidos
    {
        private const string CLIENTE_AMAZON = "31482";
        private const string CONTACTO_AMAZON = "0";
        private const string FORMA_VENTA_AMAZON = "STK";
        private const string ALMACEN_AMAZON = "ALG";
        private const string DELEGACION_AMAZON = "ALG";
        private const string VENDEDOR_AMAZON = "NV";

        private IConfiguracion configuracion;

        public CanalExternoPedidosAmazon(IConfiguracion configuracion)
        {
            this.configuracion = configuracion;
        }

        public List<PedidoVentaDTO> GetAllPedidos()
        {
            List<Order> listaAmazon = MarketplaceWebServiceOrdersNuevaVision.Ejecutar();


            List<PedidoVentaDTO> listaNesto = new List<PedidoVentaDTO>();
            foreach (Order order in listaAmazon)
            {
                PedidoVentaDTO pedido = TrasformarPedido(order);
                List<OrderItem> lineasAmazon = MarketplaceWebServiceOrdersNuevaVision.CargarLineas(order.AmazonOrderId);
                pedido.LineasPedido = TrasformarLineas(lineasAmazon);
                listaNesto.Add(pedido);
            }
            
            return listaNesto;
        }

        public PedidoVentaDTO GetPedido(int Id)
        {
            throw new NotImplementedException();
        }

        private PedidoVentaDTO TrasformarPedido(Order order)
        {
            PedidoVentaDTO pedidoSalida = new PedidoVentaDTO();

            pedidoSalida.empresa = "1";
            pedidoSalida.cliente = CLIENTE_AMAZON;
            pedidoSalida.contacto = CONTACTO_AMAZON;
            pedidoSalida.contactoCobro = CONTACTO_AMAZON;
            pedidoSalida.vendedor = VENDEDOR_AMAZON;

            pedidoSalida.iva = null;
            pedidoSalida.comentarios = order.AmazonOrderId + " \r\n";
            pedidoSalida.comentarios += order.ShippingAddress?.Name.ToString().ToUpper() + "\r\n";
            pedidoSalida.comentarios += order.BuyerEmail?.ToString() + "\r\n";
            pedidoSalida.comentarios += order.ShippingAddress?.AddressLine1?.ToString().ToUpper() + "\r\n";
            pedidoSalida.comentarios += order.ShippingAddress?.AddressLine2 != null ? order.ShippingAddress?.AddressLine2?.ToString().ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += order.ShippingAddress?.AddressLine3 != null ? order.ShippingAddress?.AddressLine3?.ToString().ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += order.ShippingAddress?.PostalCode?.ToString().ToUpper() + " ";
            pedidoSalida.comentarios += order.ShippingAddress?.City?.ToString().ToUpper() + " (";
            pedidoSalida.comentarios += order.ShippingAddress?.StateOrRegion?.ToString().ToUpper() + ")\r\n";
            
            pedidoSalida.comentarios += order.ShippingAddress?.Phone != null ? order.ShippingAddress?.Phone?.ToString().ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += "TOTAL PEDIDO: " + order.OrderTotal?.Amount.ToString();
            
            pedidoSalida.fecha = order.PurchaseDate;
            pedidoSalida.formaPago = "TRN";
            pedidoSalida.plazosPago = "PRE";
            pedidoSalida.ruta = "00";
            pedidoSalida.serie = "NV";
            pedidoSalida.periodoFacturacion = "NRM";

            pedidoSalida.usuario = configuracion.usuario;
            

            return pedidoSalida;
        }

        private ObservableCollection<LineaPedidoVentaDTO> TrasformarLineas(List<OrderItem> lineasAmazon)
        {
            ObservableCollection<LineaPedidoVentaDTO> lineasNesto = new ObservableCollection<LineaPedidoVentaDTO>();
            foreach (OrderItem orderItem in lineasAmazon)
            {
                LineaPedidoVentaDTO lineaNesto = new LineaPedidoVentaDTO
                {
                    almacen = ALMACEN_AMAZON,
                    aplicarDescuento = false,
                    cantidad = (short)orderItem.QuantityOrdered,
                    delegacion = DELEGACION_AMAZON,
                    formaVenta = FORMA_VENTA_AMAZON,
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = "G21", // TODO: LEER DEL PRODUCTO
                    precio = Convert.ToDecimal(orderItem.ItemPrice.Amount)/100/orderItem.QuantityOrdered,
                    producto = orderItem.SellerSKU,
                    texto = orderItem.Title.ToUpper(),
                    tipoLinea = 1, // producto
                    usuario = configuracion.usuario
                };
                lineasNesto.Add(lineaNesto);

                if (Convert.ToDecimal(orderItem.ShippingPrice.Amount) != 0)
                {
                    LineaPedidoVentaDTO lineaPortes = new LineaPedidoVentaDTO
                    {
                        almacen = ALMACEN_AMAZON,
                        aplicarDescuento = false,
                        cantidad = (short)1,
                        delegacion = DELEGACION_AMAZON,
                        formaVenta = FORMA_VENTA_AMAZON,
                        estado = 1,
                        fechaEntrega = DateTime.Today,
                        iva = "G21", // TODO: LEER DEL PRODUCTO
                        precio = Convert.ToDecimal(orderItem.ShippingPrice.Amount) / 100,
                        producto = "62400030",
                        texto = "PORTES " + orderItem.Title.ToUpper(),
                        tipoLinea = 2, // cuenta contable
                        usuario = configuracion.usuario
                    };
                    lineasNesto.Add(lineaPortes);
                }
            }

            return lineasNesto;
        }
    }
}
