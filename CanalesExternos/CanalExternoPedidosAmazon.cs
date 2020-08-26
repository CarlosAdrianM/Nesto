using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarketplaceWebServiceOrders;
using MarketplaceWebServiceOrders.Model;
using static Nesto.Models.PedidoVenta;
using System.Collections.ObjectModel;
using Nesto.Contratos;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalExternoPedidosAmazon : ICanalExternoPedidos
    {
        private const string CLIENTE_AMAZON = "32624";
        private const string CONTACTO_AMAZON = "0";
        private const string FORMA_VENTA_AMAZON = "STK";
        private const string ALMACEN_NV = "ALG";
        private const string ALMACEN_AMAZON = "AMZ";
        private const string DELEGACION_AMAZON = "ALG";
        private const string VENDEDOR_AMAZON = "NV";
        private const string IVA_GENERAL = "G21";
        private const decimal PORCENTAJE_IVA = 1.21M;

        private IConfiguracion configuracion;

        public CanalExternoPedidosAmazon(IConfiguracion configuracion)
        {
            this.configuracion = configuracion;
        }

        public async Task<ObservableCollection<PedidoCanalExterno>> GetAllPedidosAsync(DateTime fechaDesde, int numeroMaxPedidos)
        {
            List<Order> listaAmazon = MarketplaceWebServiceOrdersNuevaVision.Ejecutar(fechaDesde, numeroMaxPedidos);


            ObservableCollection<PedidoCanalExterno> listaNesto = new ObservableCollection<PedidoCanalExterno>();
            await Task.Run(() => { 
                foreach (Order order in listaAmazon)
                {
                    PedidoCanalExterno pedidoExterno = TrasformarPedido(order);
                    List<OrderItem> lineasAmazon = MarketplaceWebServiceOrdersNuevaVision.CargarLineas(order.AmazonOrderId);
                    pedidoExterno.Pedido.LineasPedido = TrasformarLineas(lineasAmazon, order.FulfillmentChannel);
                    listaNesto.Add(pedidoExterno);
                }
            });
            
            return listaNesto;
        }

        public PedidoCanalExterno GetPedido(int Id)
        {
            throw new NotImplementedException();
        }

        private PedidoCanalExterno TrasformarPedido(Order order)
        {
            PedidoCanalExterno pedidoExterno = new PedidoCanalExterno();
            PedidoVentaDTO pedidoSalida = new PedidoVentaDTO();

            pedidoSalida.empresa = "1";
            pedidoSalida.origen = "1";
            pedidoSalida.cliente = CLIENTE_AMAZON;
            pedidoSalida.contacto = CONTACTO_AMAZON;
            pedidoSalida.contactoCobro = CONTACTO_AMAZON;
            pedidoSalida.vendedor = VENDEDOR_AMAZON;

            pedidoSalida.iva = IVA_GENERAL;
            string numeroOrderAmazon = order.AmazonOrderId;
            if (order.FulfillmentChannel == "AFN")
            {
                numeroOrderAmazon = "FBA " + numeroOrderAmazon;
            }
            pedidoSalida.comentarios = numeroOrderAmazon + " \r\n";
            pedidoSalida.comentarios += order.ShippingAddress?.Name.ToString().ToUpper() + "\r\n";
            pedidoSalida.comentarios += order.BuyerEmail?.ToString() + "\r\n";
            pedidoSalida.comentarios += order.ShippingAddress?.AddressLine1?.ToString().ToUpper() + "\r\n";
            pedidoSalida.comentarios += order.ShippingAddress?.AddressLine2 != null ? order.ShippingAddress?.AddressLine2?.ToString().ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += order.ShippingAddress?.AddressLine3 != null ? order.ShippingAddress?.AddressLine3?.ToString().ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += order.ShippingAddress?.PostalCode?.ToString().ToUpper() + " ";
            pedidoSalida.comentarios += order.ShippingAddress?.City?.ToString().ToUpper() + " (";
            pedidoSalida.comentarios += order.ShippingAddress?.StateOrRegion?.ToString().ToUpper() + ")\r\n";
            
            pedidoSalida.comentarios += order.ShippingAddress?.Phone != null ? order.ShippingAddress?.Phone?.ToString().ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += "Cumplimiento por " + (order.FulfillmentChannel == "AFN" ? "Amazon" : "Nueva Visión") + "\r\n";
            if (!string.IsNullOrWhiteSpace(order.SellerOrderId) && order.SellerOrderId != order.AmazonOrderId)
            {
                pedidoSalida.comentarios += "N/ Pedido: " + order.SellerOrderId + "\r\n";
            }
            pedidoSalida.comentarios += "TOTAL PEDIDO: " + order.OrderTotal?.Amount.ToString();
            
            pedidoSalida.fecha = order.PurchaseDate;
            pedidoSalida.formaPago = "TRN";
            pedidoSalida.plazosPago = "PRE";
            pedidoSalida.ruta = "00";
            pedidoSalida.serie = "NV";
            pedidoSalida.periodoFacturacion = "NRM";

            pedidoSalida.usuario = configuracion.usuario;

            pedidoExterno.Pedido = pedidoSalida;
            pedidoExterno.PedidoCanalId = numeroOrderAmazon;
            pedidoExterno.Nombre = order.ShippingAddress?.Name.ToString().ToUpper();
            pedidoExterno.CorreoElectronico = order.BuyerEmail?.ToString();
            pedidoExterno.Direccion = order.ShippingAddress?.AddressLine1?.ToString().ToUpper();
            pedidoExterno.Direccion += order.ShippingAddress?.AddressLine2 != null ? " " + order.ShippingAddress?.AddressLine2?.ToString().ToUpper() : "";
            pedidoExterno.Direccion += order.ShippingAddress?.AddressLine3 != null ? " " + order.ShippingAddress?.AddressLine3?.ToString().ToUpper() : "";
            pedidoExterno.CodigoPostal = order.ShippingAddress?.PostalCode?.ToString().ToUpper();
            pedidoExterno.Poblacion = order.ShippingAddress?.City?.ToString().ToUpper();
            pedidoExterno.Provincia = order.ShippingAddress?.StateOrRegion?.ToString().ToUpper();
            pedidoExterno.TelefonoFijo = order.ShippingAddress?.Phone?.ToString().ToUpper();
            pedidoExterno.PaisISO = order.ShippingAddress?.CountryCode?.ToString().ToUpper();

            Dictionary<string, string> cuentasMarkets = new Dictionary<string, string>();
            cuentasMarkets.Add("A1F83G8C2ARO7P", "55500049"); // Amazon.co.uk
            cuentasMarkets.Add("A1PA6795UKMFR9", "55500046"); // Amazon.de
            cuentasMarkets.Add("A1RKKUPIHCS9HS", "55500047"); // Amazon.es
            cuentasMarkets.Add("A13V1IB3VIYZZH", "55500045"); // Amazon.fr
            cuentasMarkets.Add("APJ6JRA9NG5V4", "55500048");  // Amazon.it
            //cuentasMarkets.Add("A302IUJ673AU08");  // Amazon.nl

            Dictionary<string, string> nombresMarkets = new Dictionary<string, string>();
            nombresMarkets.Add("A1F83G8C2ARO7P", "Amazon.co.uk"); 
            nombresMarkets.Add("A1PA6795UKMFR9", "Amazon.de");
            nombresMarkets.Add("A1RKKUPIHCS9HS", "Amazon.es");
            nombresMarkets.Add("A13V1IB3VIYZZH", "Amazon.fr");
            nombresMarkets.Add("APJ6JRA9NG5V4", "Amazon.it");
            //nombresMarkets.Add("A302IUJ673AU08");  // Amazon.nl

            PrepagoDTO prepago = new PrepagoDTO
            {
                Importe = Convert.ToDecimal(order.OrderTotal?.Amount)/100,
                Estado = 0,
                CuentaContable = cuentasMarkets[order.MarketplaceId], 
                ConceptoAdicional = string.Format("{0} {1}", nombresMarkets[order.MarketplaceId], numeroOrderAmazon)
            };

            pedidoExterno.Pedido.Prepagos.Add(prepago);
            

            return pedidoExterno;
        }

        private ObservableCollection<LineaPedidoVentaDTO> TrasformarLineas(List<OrderItem> lineasAmazon, string canalCumplimiento)
        {
            ObservableCollection<LineaPedidoVentaDTO> lineasNesto = new ObservableCollection<LineaPedidoVentaDTO>();
            foreach (OrderItem orderItem in lineasAmazon)
            {
                decimal baseImponible = Convert.ToDecimal(orderItem.ItemPrice?.Amount) / 100 / PORCENTAJE_IVA;
                LineaPedidoVentaDTO lineaNesto = new LineaPedidoVentaDTO
                {
                    almacen = canalCumplimiento == "AFN" ? ALMACEN_AMAZON : ALMACEN_NV,
                    aplicarDescuento = false,
                    cantidad = (short)orderItem.QuantityOrdered,
                    delegacion = DELEGACION_AMAZON,
                    formaVenta = FORMA_VENTA_AMAZON,
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = IVA_GENERAL, 
                    precio = baseImponible/orderItem.QuantityOrdered,
                    producto = orderItem.SellerSKU.EndsWith("FBA") ? orderItem.SellerSKU.Substring(0, orderItem.SellerSKU.Length-3) : orderItem.SellerSKU,
                    texto = orderItem.Title.ToUpper(),
                    tipoLinea = 1, // producto
                    vistoBueno = true,
                    usuario = configuracion.usuario
                };
                lineasNesto.Add(lineaNesto);

                if (Convert.ToDecimal(orderItem.ShippingPrice?.Amount) != 0)
                {
                    decimal baseImponiblePortes = Convert.ToDecimal(orderItem.ShippingPrice.Amount) / 100 / PORCENTAJE_IVA;
                    LineaPedidoVentaDTO lineaPortes = new LineaPedidoVentaDTO
                    {
                        almacen = canalCumplimiento == "AFN" ? ALMACEN_AMAZON : ALMACEN_NV,
                        aplicarDescuento = false,
                        cantidad = (short)1,
                        delegacion = DELEGACION_AMAZON,
                        formaVenta = FORMA_VENTA_AMAZON,
                        estado = 1,
                        fechaEntrega = DateTime.Today,
                        iva = IVA_GENERAL, 
                        precio = baseImponiblePortes,
                        producto = "62400003",
                        texto = "PORTES " + orderItem.Title.ToUpper(),
                        tipoLinea = 2, // cuenta contable
                        vistoBueno = true,
                        usuario = configuracion.usuario
                    };
                    lineasNesto.Add(lineaPortes);
                }
            }

            return lineasNesto;
        }
    }
}
