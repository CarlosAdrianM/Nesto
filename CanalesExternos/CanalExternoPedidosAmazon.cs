using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Nesto.Modulos.CanalesExternos.Models;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using FikaAmazonAPI.AmazonSpApiSDK.Models.Orders;
using static FikaAmazonAPI.AmazonSpApiSDK.Models.Orders.Order;
using Nesto.Models.Nesto.Models;
using System.Linq;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalExternoPedidosAmazon : ICanalExternoPedidos
    {

        private const string EMPRESA_DEFECTO = "1";
        private const string FORMA_VENTA_AMAZON = "STK";
        private const string ALMACEN_NV = "ALG";
        private const string ALMACEN_AMAZON = "AMZ";
        private const string DELEGACION_AMAZON = "ALG";
        private const string VENDEDOR_AMAZON = "NV";
        private const string IVA_GENERAL = "G21";
        private const string IVA_REDUCIDO = "R10";
        private const string IVA_EXPORTACION = "IM";
        private const string IVA_EXENTO = "EX";
        private decimal PORCENTAJE_IVA_GENERAL = 1.21M;
        private decimal PORCENTAJE_IVA_REDUCIDO = 1.10M;

        private IConfiguracion configuracion;

        public CanalExternoPedidosAmazon(IConfiguracion configuracion)
        {
            this.configuracion = configuracion;
        }

        public async Task<ObservableCollection<PedidoCanalExterno>> GetAllPedidosAsync(DateTime fechaDesde, int numeroMaxPedidos)
        {
            List<Order> listaAmazon = AmazonApiOrdersService.Ejecutar(fechaDesde, numeroMaxPedidos);


            ObservableCollection<PedidoCanalExterno> listaNesto = new ObservableCollection<PedidoCanalExterno>();
            await Task.Run(() =>
            {
                foreach (Order order in listaAmazon)
                {
                    if (order.OrderTotal != null && order.OrderTotal.CurrencyCode != Constantes.Empresas.MONEDA_CONTABILIDAD)
                    {
                        try
                        {
                            CambioDivisas = AmazonApiOrdersService.CalculaDivisa(order.OrderTotal.CurrencyCode, Constantes.Empresas.MONEDA_CONTABILIDAD);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    else
                    {
                        CambioDivisas = 1;
                    }
                    PedidoCanalExterno pedidoExterno = TransformarPedido(order);
                    pedidoExterno.Observaciones = "Phone:";
                    pedidoExterno.Observaciones += !string.IsNullOrEmpty(pedidoExterno.TelefonoFijo) ? " " + pedidoExterno.TelefonoFijo : "";
                    pedidoExterno.Observaciones += !string.IsNullOrEmpty(pedidoExterno.TelefonoMovil) ? " " + pedidoExterno.TelefonoMovil : "";
                    pedidoExterno.Observaciones += " " + pedidoExterno.PedidoCanalId;
                    List<OrderItem> lineasAmazon = AmazonApiOrdersService.CargarLineas(order.AmazonOrderId);
                    pedidoExterno.Pedido.Lineas = TransformarLineas(lineasAmazon, order.FulfillmentChannel, pedidoExterno.Pedido.iva);
                    listaNesto.Add(pedidoExterno);
                }
            });
            
            return listaNesto;
        }

        public PedidoCanalExterno GetPedido(int Id)
        {
            throw new NotImplementedException();
        }

        private PedidoCanalExterno TransformarPedido(Order order)
        {
            decimal orderTotal = Convert.ToDecimal(order.OrderTotal?.Amount) /100 * CambioDivisas;
            PedidoCanalExterno pedidoExterno = new PedidoCanalExterno();
            PedidoVentaDTO pedidoSalida = new PedidoVentaDTO();

            pedidoSalida.empresa = "1";
            pedidoSalida.origen = "1";
            string telefonoCliente = order.ShippingAddress?.Phone;
            Clientes cliente = BuscarCliente(telefonoCliente);
            pedidoSalida.cliente = cliente.Nº_Cliente;
            pedidoSalida.contacto = cliente.Contacto;
            pedidoSalida.contactoCobro = cliente.ContactoCobro;
            pedidoSalida.vendedor = cliente.Vendedor;

            pedidoSalida.iva = cliente.IVA;
            string numeroOrderAmazon = order.AmazonOrderId;
            if (order.FulfillmentChannel == FulfillmentChannelEnum.AFN)
            {
                numeroOrderAmazon = "FBA " + numeroOrderAmazon;
            }
            pedidoSalida.comentarios = numeroOrderAmazon + " \r\n";
            pedidoSalida.comentarios += order.ShippingAddress?.Name?.ToString().ToUpper() + "\r\n";
            pedidoSalida.comentarios += order.BuyerInfo.BuyerEmail?.ToString() + "\r\n";
            pedidoSalida.comentarios += order.ShippingAddress?.AddressLine1?.ToString().ToUpper() + "\r\n";
            pedidoSalida.comentarios += order.ShippingAddress?.AddressLine2 != null ? order.ShippingAddress?.AddressLine2?.ToString().ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += order.ShippingAddress?.AddressLine3 != null ? order.ShippingAddress?.AddressLine3?.ToString().ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += order.ShippingAddress?.PostalCode?.ToString().ToUpper() + " ";
            pedidoSalida.comentarios += order.ShippingAddress?.City?.ToString().ToUpper() + " (";
            pedidoSalida.comentarios += order.ShippingAddress?.StateOrRegion?.ToString().ToUpper() + ")\r\n";
            
            pedidoSalida.comentarios += telefonoCliente != null ? telefonoCliente.ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += "Cumplimiento por " + (order.FulfillmentChannel == FulfillmentChannelEnum.AFN ? "Amazon" : "Nueva Visión") + "\r\n";
            if (!string.IsNullOrWhiteSpace(order.SellerOrderId) && order.SellerOrderId != order.AmazonOrderId)
            {
                pedidoSalida.comentarios += "N/ Pedido: " + order.SellerOrderId + "\r\n";
            }
            if (order.OrderTotal != null && order.OrderTotal.CurrencyCode != Constantes.Empresas.MONEDA_CONTABILIDAD)
            {
                pedidoSalida.comentarios += string.Format("Importe original: {0} {1} (cambio {2})", order.OrderTotal.Amount.ToString(), order.OrderTotal.CurrencyCode, CambioDivisas.ToString()) + "\r\n";
            }
            pedidoSalida.comentarios += "TOTAL PEDIDO: " + orderTotal.ToString("C");
                        
            pedidoSalida.fecha = DateTimeOffset.Parse(order.PurchaseDate).UtcDateTime;
            pedidoSalida.formaPago = "TRN";
            pedidoSalida.plazosPago = "PRE";
            pedidoSalida.ruta = "00";
            pedidoSalida.serie = "NV";
            pedidoSalida.periodoFacturacion = "NRM";
            pedidoSalida.servirJunto = true;

            pedidoSalida.Usuario = configuracion.usuario;

            pedidoExterno.Pedido = pedidoSalida;
            pedidoExterno.PedidoCanalId = numeroOrderAmazon;
            if (Int32.TryParse(order.SellerOrderId, out int numValue))
            {
                pedidoExterno.PedidoNestoId = numValue;
            }
            pedidoExterno.Nombre = order.ShippingAddress?.Name?.ToString().ToUpper();
            pedidoExterno.CorreoElectronico = order.BuyerInfo.BuyerEmail?.ToString();
            pedidoExterno.Direccion = order.ShippingAddress?.AddressLine1?.ToString().ToUpper();
            pedidoExterno.Direccion += order.ShippingAddress?.AddressLine2 != null ? " " + order.ShippingAddress?.AddressLine2?.ToString().ToUpper() : "";
            pedidoExterno.Direccion += order.ShippingAddress?.AddressLine3 != null ? " " + order.ShippingAddress?.AddressLine3?.ToString().ToUpper() : "";
            pedidoExterno.CodigoPostal = order.ShippingAddress?.PostalCode?.ToString().ToUpper();
            pedidoExterno.Poblacion = order.ShippingAddress?.City?.ToString().ToUpper();
            pedidoExterno.Provincia = order.ShippingAddress?.StateOrRegion?.ToString().ToUpper();
            pedidoExterno.TelefonoFijo = order.ShippingAddress?.Phone?.ToString().ToUpper();
            pedidoExterno.PaisISO = order.ShippingAddress?.CountryCode?.ToString().ToUpper();
            if (pedidoExterno.PaisISO == "GB")
            {
                pedidoExterno.Pedido.iva = IVA_EXPORTACION;
            }
            

            PrepagoDTO prepago = new PrepagoDTO
            {
                Importe = orderTotal,
                CuentaContable = DatosMarkets.Buscar(order.MarketplaceId).CuentaContablePago, 
                ConceptoAdicional = string.Format("{0} {1}", DatosMarkets.Buscar(order.MarketplaceId).NombreMarket, numeroOrderAmazon)
            };

            if (prepago.ConceptoAdicional.Length > 50)
            {
                prepago.ConceptoAdicional = prepago.ConceptoAdicional.Substring(0, 50);
            }

            pedidoExterno.Pedido.Prepagos.Add(prepago);
            

            return pedidoExterno;
        }

        private Clientes BuscarCliente(string telefonoCliente)
        {
            Clientes CLIENTE_AMAZON = new Clientes
            {
                Nº_Cliente = "32624",
                Contacto = "0",
                ContactoDefecto = "0",
                ContactoCobro = "0",
                Vendedor = "NV",
                IVA = "G21"
            };

            Telefono telefono = new(telefonoCliente, true);
            List<Clientes> listaPosiblesClientes = new();
            using (NestoEntities db = new NestoEntities())
            {                
                foreach (string t in telefono.TodosLosTelefonos)
                {
                    var clientesEncontrados = db.Clientes.Where(c => c.Empresa == EMPRESA_DEFECTO && c.Estado >= 0 && c.Teléfono.Contains(t));
                    listaPosiblesClientes.AddRange(clientesEncontrados);
                }                
            }
            if (listaPosiblesClientes.Any())
            {
                return listaPosiblesClientes.First();
            }

            return CLIENTE_AMAZON;
    }

        private string LimpiarTelefono(string telefonoCliente)
        {
            return telefonoCliente;
        }

        private ObservableCollection<LineaPedidoVentaDTO> TransformarLineas(List<OrderItem> lineasAmazon, FulfillmentChannelEnum? canalCumplimiento, string iva)
        {
            ObservableCollection<LineaPedidoVentaDTO> lineasNesto = new ObservableCollection<LineaPedidoVentaDTO>();
            foreach (OrderItem orderItem in lineasAmazon)
            {
                decimal porcentajeIva;
                if (iva == IVA_EXPORTACION)
                {
                    porcentajeIva = 1;
                }
                else if (orderItem.SellerSKU == "42203" || orderItem.SellerSKU == "42204" || orderItem.SellerSKU == "42205")
                {
                    porcentajeIva = PORCENTAJE_IVA_REDUCIDO;
                }
                else
                {
                    porcentajeIva = PORCENTAJE_IVA_GENERAL;
                }
                decimal baseImponible = Math.Round(Convert.ToDecimal(orderItem.ItemPrice?.Amount) / porcentajeIva * CambioDivisas, 2, MidpointRounding.AwayFromZero);
                short cantidad = ProcesarCantidad(orderItem);
                LineaPedidoVentaDTO lineaNesto = new LineaPedidoVentaDTO
                {
                    almacen = canalCumplimiento == FulfillmentChannelEnum.AFN ? ALMACEN_AMAZON : ALMACEN_NV,
                    AplicarDescuento = false,
                    Cantidad = cantidad,
                    delegacion = DELEGACION_AMAZON,
                    formaVenta = FORMA_VENTA_AMAZON,
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = iva == IVA_EXPORTACION ? IVA_EXENTO : porcentajeIva == PORCENTAJE_IVA_REDUCIDO ? IVA_REDUCIDO : IVA_GENERAL,
                    PrecioUnitario = cantidad != 0 ? Math.Round(baseImponible / cantidad, 4) : baseImponible,
                    Producto = ProcesarSKU(orderItem),
                    texto = orderItem.Title.ToUpper(),
                    tipoLinea = 1, // producto
                    vistoBueno = true,
                    Usuario = configuracion.usuario
                };
                lineasNesto.Add(lineaNesto);

                if (Convert.ToDecimal(orderItem.ShippingPrice?.Amount) != 0)
                {
                    decimal baseImponiblePortes = Math.Round(Convert.ToDecimal(orderItem.ShippingPrice.Amount) / 100 / porcentajeIva * CambioDivisas, 2, MidpointRounding.AwayFromZero);
                    LineaPedidoVentaDTO lineaPortes = new LineaPedidoVentaDTO
                    {
                        almacen = canalCumplimiento == FulfillmentChannelEnum.AFN ? ALMACEN_AMAZON : ALMACEN_NV,
                        AplicarDescuento = false,
                        Cantidad = (short)1,
                        delegacion = DELEGACION_AMAZON,
                        formaVenta = FORMA_VENTA_AMAZON,
                        estado = 1,
                        fechaEntrega = DateTime.Today,
                        iva = iva == IVA_EXPORTACION ? IVA_EXENTO : IVA_GENERAL,
                        PrecioUnitario = baseImponiblePortes,
                        Producto = "62400003",
                        texto = "PORTES " + orderItem.Title.ToUpper(),
                        tipoLinea = 2, // cuenta contable
                        vistoBueno = true,
                        Usuario = configuracion.usuario
                    };
                    lineasNesto.Add(lineaPortes);
                }

                if (Convert.ToDecimal(orderItem.ShippingDiscount?.Amount) != 0)
                {
                    decimal baseImponibleDescuentoPortes = Math.Round(Convert.ToDecimal(orderItem.ShippingDiscount.Amount) / 100 / porcentajeIva * CambioDivisas, 2, MidpointRounding.AwayFromZero);
                    LineaPedidoVentaDTO lineaDescuentoPortes = new LineaPedidoVentaDTO
                    {
                        almacen = canalCumplimiento == FulfillmentChannelEnum.AFN ? ALMACEN_AMAZON : ALMACEN_NV,
                        AplicarDescuento = false,
                        Cantidad = (short)-1,
                        delegacion = DELEGACION_AMAZON,
                        formaVenta = FORMA_VENTA_AMAZON,
                        estado = 1,
                        fechaEntrega = DateTime.Today,
                        iva = iva == IVA_EXPORTACION ? IVA_EXENTO : IVA_GENERAL,
                        PrecioUnitario = baseImponibleDescuentoPortes,
                        Producto = "62400003",
                        texto = "DESCUENTO PORTES " + orderItem.Title.ToUpper(),
                        tipoLinea = 2, // cuenta contable
                        vistoBueno = true,
                        Usuario = configuracion.usuario
                    };
                    lineasNesto.Add(lineaDescuentoPortes);
                }
            }

            return lineasNesto;
        }

        private static short ProcesarCantidad(OrderItem orderItem)
        {
            string producto = orderItem.SellerSKU;
            short cantidadDevolver = (short)orderItem.QuantityOrdered;
            if (producto.Contains("x"))
            {
                short cantidadReferencia;
                if (short.TryParse(producto.Split("x")[1], out cantidadReferencia))
                {
                    cantidadDevolver = (short)(cantidadDevolver * cantidadReferencia);
                }
            }
            return cantidadDevolver;
        }

        private static string ProcesarSKU(OrderItem orderItem)
        {
            string productoDevolver = orderItem.SellerSKU.EndsWith("FBA") ? orderItem.SellerSKU.Substring(0, orderItem.SellerSKU.Length - 3) : orderItem.SellerSKU;
            if (productoDevolver.Contains("/"))
            {
                productoDevolver = productoDevolver.Split("/")[0];
            }
            if (productoDevolver.Contains("x"))
            {
                productoDevolver = productoDevolver.Split("x")[0];
            }
            return productoDevolver;
        }

        public async Task<bool> EjecutarTrasCrearPedido(PedidoCanalExterno pedido)
        {
            return await AmazonApiOrdersService.ActualizarSellerOrderId(pedido.PedidoCanalId, pedido.PedidoNestoId);            
        }

        public async Task<string> ConfirmarPedido(PedidoCanalExterno pedido)
        {
            DatosEnvioConfirmarAmazon datosEnvio = LeerDatosEnvio(pedido.UltimoSeguimiento);
            return await AmazonApiOrdersService.ConfirmarPedido(pedido.PedidoCanalId, datosEnvio.NombreAgencia, datosEnvio.NombreServicio, datosEnvio.NumeroSeguimiento);
        }

        private decimal CambioDivisas { get; set; } = 1;

        private DatosEnvioConfirmarAmazon LeerDatosEnvio(string seguimiento)
        {
            if (seguimiento.Contains("correosexpress"))
            {
                int indiceIgual = seguimiento.IndexOf("="); // Obtiene el índice del símbolo "="

                if (indiceIgual == -1) // Verifica si se encuentra el símbolo "=" en la cadena
                {
                    throw new Exception("El seguimiento de CEX tiene que incluir el símbolo = (igual)");
                }
                return new DatosEnvioConfirmarAmazon
                {
                    NombreAgencia = "Correos Express",
                    NombreServicio = "ePaq",
                    NumeroSeguimiento = seguimiento.Substring(indiceIgual + 1)
                };
            }
            else if (seguimiento.Contains("sending"))
            {
                int indiceIgual = seguimiento.LastIndexOf("=");

                if (indiceIgual == -1) // Verifica si se encuentra el símbolo "=" en la cadena
                {
                    throw new Exception("El seguimiento de Sending tiene que incluir el símbolo = (igual)");
                }
                return new DatosEnvioConfirmarAmazon
                {
                    NombreAgencia = "Sending",
                    NombreServicio = "Send Exprés",
                    NumeroSeguimiento = seguimiento.Substring(indiceIgual + 1)
                };
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        private class DatosEnvioConfirmarAmazon
        {
            public string AmazonOrderId { get; set; }
            public string NombreAgencia { get; set; }
            public string NombreServicio { get; set; }
            public string NumeroSeguimiento { get; set; }
        }

    }
}

