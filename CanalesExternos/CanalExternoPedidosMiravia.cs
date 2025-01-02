using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Models.Nesto.Models;
using Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models;
using Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Services;
using Nesto.Modulos.CanalesExternos.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using static Nesto.Infrastructure.Shared.Constantes;
using Clientes = Nesto.Models.Nesto.Models.Clientes;

namespace Nesto.Modulos.CanalesExternos
{
    internal class CanalExternoPedidosMiravia : ICanalExternoPedidos
    {
        private readonly IConfiguracion _configuracion;

        private const string FORMA_VENTA_MIRAVIA = "BLT";

        public CanalExternoPedidosMiravia(IConfiguracion configuracion)
        {
            _configuracion = configuracion;
        }
        public async Task<string> ConfirmarPedido(PedidoCanalExterno pedido)
        {
            return await MiraviaApiOrderService.ConfirmarPedido(pedido);
        }

        public Task<bool> EjecutarTrasCrearPedido(PedidoCanalExterno pedido)
        {
            throw new NotImplementedException();
        }

        public async Task<ObservableCollection<PedidoCanalExterno>> GetAllPedidosAsync(DateTime fechaDesde, int numeroMaxPedidos)
        {
            var listaMiravia = MiraviaApiOrderService.GetOrders(fechaDesde, numeroMaxPedidos);

            ObservableCollection<PedidoCanalExterno> listaNesto = new ObservableCollection<PedidoCanalExterno>();
            await Task.Run(() =>
            {
                foreach (Order order in listaMiravia)
                {

                    PedidoCanalExterno pedidoExterno = TransformarPedido(order);
                    pedidoExterno.Observaciones = "Phone:";
                    pedidoExterno.Observaciones += !string.IsNullOrEmpty(pedidoExterno.TelefonoFijo) ? " " + pedidoExterno.TelefonoFijo : "";
                    pedidoExterno.Observaciones += !string.IsNullOrEmpty(pedidoExterno.TelefonoMovil) ? " " + pedidoExterno.TelefonoMovil : "";
                    pedidoExterno.Observaciones += " " + pedidoExterno.PedidoCanalId;
                    listaNesto.Add(pedidoExterno);
                }
            });

            return listaNesto;
        }
        
        public async Task<ICollection<LineaPedidoVentaDTO>> GetLineas(PedidoCanalExterno pedido)
        {
            try
            {
                return await Task.Run(() =>
                {
                    List<OrderItem> lineasMiravia = MiraviaApiOrderService.CargarLineas(pedido.PedidoCanalId);
                    var lineas = TransformarLineas(lineasMiravia, pedido.Almacen, pedido.Pedido.iva, Constantes.Empresas.MONEDA_CONTABILIDAD);
                    return lineas.ToList();
                });                
            }
            catch (Exception ex)
            {
                throw new Exception("Se ha producido un error al leer las líneas de un pedido de Miravia", ex);
            }
        }

        public Task<PedidoCanalExterno> GetPedido(string Id)
        {
            throw new NotImplementedException();
        }

        private Clientes BuscarCliente(string telefonoCliente)
        {
            Clientes CLIENTE_MIRAVIA = new Clientes
            {
                Nº_Cliente = "31517",
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
                    var clientesEncontrados = db.Clientes.Where(c => c.Empresa == Constantes.Empresas.EMPRESA_DEFECTO && c.Estado >= 0 && c.Teléfono.Contains(t));
                    listaPosiblesClientes.AddRange(clientesEncontrados);
                }
            }
            if (listaPosiblesClientes.Any())
            {
                return listaPosiblesClientes.First();
            }

            return CLIENTE_MIRAVIA;
        }

        private PedidoCanalExterno TransformarPedido(Order order)
        {
            decimal orderTotal = Convert.ToDecimal(order.Price);
            PedidoCanalExterno pedidoExterno = new PedidoCanalExterno();
            PedidoVentaDTO pedidoSalida = new PedidoVentaDTO();

            pedidoSalida.empresa = Constantes.Empresas.EMPRESA_DEFECTO;
            pedidoSalida.origen = Constantes.Empresas.EMPRESA_DEFECTO;
            string telefonoCliente = order.AddressBilling.Phone;
            Clientes cliente = BuscarCliente(telefonoCliente);
            pedidoSalida.cliente = cliente.Nº_Cliente;
            pedidoSalida.contacto = cliente.Contacto;
            pedidoSalida.contactoCobro = cliente.ContactoCobro;
            pedidoSalida.vendedor = cliente.Vendedor;

            pedidoSalida.iva = cliente.IVA;
            string numeroOrderMiravia = order.OrderId.ToString(); // ¿OrderId or OrderNumber?

            pedidoSalida.comentarios = numeroOrderMiravia + " \r\n";
            pedidoSalida.comentarios += order.AddressShipping.FirstName?.ToString().ToUpper() + " " + order.AddressShipping.LastName?.ToString().ToUpper() + "\r\n";
            //pedidoSalida.comentarios += order.BuyerInfo.BuyerEmail?.ToString() + "\r\n";
            pedidoSalida.comentarios += order.AddressShipping?.Address1?.ToString().ToUpper() + "\r\n";
            pedidoSalida.comentarios += order.AddressShipping?.Address2 != null ? order.AddressShipping?.Address2?.ToString().ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += order.AddressShipping?.Address3 != null ? order.AddressShipping?.Address3?.ToString().ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += order.AddressShipping?.Address4 != null ? order.AddressShipping?.Address4?.ToString().ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += order.AddressShipping?.Address5 != null ? order.AddressShipping?.Address5?.ToString().ToUpper() + "\r\n" : "";

            pedidoSalida.comentarios += order.AddressShipping?.PostCode?.ToString().ToUpper() + " ";
            pedidoSalida.comentarios += order.AddressShipping?.City?.ToString().ToUpper() + " (";
            pedidoSalida.comentarios += order.AddressShipping?.Country?.ToString().ToUpper() + ")\r\n";

            pedidoSalida.comentarios += telefonoCliente != null ? telefonoCliente.ToUpper() + "\r\n" : "";
            //if (!string.IsNullOrWhiteSpace(order.SellerOrderId) && order.SellerOrderId != order.AmazonOrderId)
            //{
            //    pedidoSalida.comentarios += "N/ Pedido: " + order.SellerOrderId + "\r\n";
            //}

            pedidoSalida.comentarios += "TOTAL PEDIDO: " + orderTotal.ToString("C", new System.Globalization.CultureInfo("es-ES"));

            pedidoSalida.fecha = order.CreatedAt;
            pedidoSalida.formaPago = "TRN";
            pedidoSalida.plazosPago = "PRE";
            pedidoSalida.ruta = "00";
            pedidoSalida.serie = "NV";
            pedidoSalida.periodoFacturacion = "NRM";
            pedidoSalida.servirJunto = true;

            pedidoSalida.Usuario = _configuracion.usuario;

            pedidoExterno.Pedido = pedidoSalida;
            pedidoExterno.PedidoCanalId = numeroOrderMiravia;
            //if (Int32.TryParse(order.SellerOrderId, out int numValue))
            //{
            //    pedidoExterno.PedidoNestoId = numValue;
            //}
            pedidoExterno.Nombre = order.AddressShipping?.FirstName?.ToString().ToUpper() + " " + order.AddressShipping?.LastName?.ToString().ToUpper();
            //pedidoExterno.CorreoElectronico = order.BuyerInfo.BuyerEmail?.ToString();
            pedidoExterno.Direccion = order.AddressShipping?.Address1?.ToString().ToUpper() + "\r\n";
            pedidoExterno.Direccion += order.AddressShipping?.Address2 != null ? order.AddressShipping?.Address2?.ToString().ToUpper() + "\r\n" : "";
            pedidoExterno.Direccion += order.AddressShipping?.Address3 != null ? order.AddressShipping?.Address3?.ToString().ToUpper() + "\r\n" : "";
            pedidoExterno.Direccion += order.AddressShipping?.Address4 != null ? order.AddressShipping?.Address4?.ToString().ToUpper() + "\r\n" : "";
            pedidoExterno.Direccion += order.AddressShipping?.Address5 != null ? order.AddressShipping?.Address5?.ToString().ToUpper() + "\r\n" : "";
            pedidoExterno.CodigoPostal = order.AddressShipping?.PostCode?.ToString().ToUpper();
            pedidoExterno.Poblacion = order.AddressShipping?.City?.ToString().ToUpper();
            pedidoExterno.Provincia = order.AddressShipping?.Country?.ToString().ToUpper();
            pedidoExterno.TelefonoFijo = order.AddressShipping?.Phone?.ToString().ToUpper();
            pedidoExterno.PaisISO = order.AddressShipping?.Country?.ToString().ToUpper();

            pedidoExterno.Almacen = Constantes.Almacenes.ALMACEN_CENTRAL;

            PrepagoDTO prepago = new PrepagoDTO
            {
                Importe = orderTotal,
                CuentaContable = DatosMarkets.Buscar(order.Marketplace).CuentaContablePago,
                ConceptoAdicional = string.Format("{0} {1}", DatosMarkets.Buscar(order.Marketplace).NombreMarket, numeroOrderMiravia)
            };

            if (prepago.ConceptoAdicional.Length > 50)
            {
                prepago.ConceptoAdicional = prepago.ConceptoAdicional.Substring(0, 50);
            }

            pedidoExterno.Pedido.Prepagos.Add(prepago);

            // BORRAR ESTA LÍNEA CUANDO PASEMOS A PRODUCCIÓN:
            pedidoExterno.PedidoNestoId = 1;
            pedidoExterno.UltimoSeguimiento = "Delivered By Miravia (DBM)";
            pedidoExterno.Pedido.comentarios = "FBA " + pedidoExterno.Pedido.comentarios;

            return pedidoExterno;


        }
        private ObservableCollection<LineaPedidoVentaDTO> TransformarLineas(List<OrderItem> lineasAmazon, string almacen, string iva, string divisa)
        {
            ObservableCollection<LineaPedidoVentaDTO> lineasNesto = new ObservableCollection<LineaPedidoVentaDTO>();
            foreach (OrderItem orderItem in lineasAmazon)
            {
                decimal baseImponible = Math.Round(orderItem.PaidPrice - orderItem.TaxAmount, 2, MidpointRounding.AwayFromZero);
                decimal porcentajeIva = orderItem.PaidPrice / baseImponible; //1.21M

                
                short cantidad = 1; // No veo el campo cantidad en OrderItem
                LineaPedidoVentaDTO lineaNesto = new LineaPedidoVentaDTO
                {
                    almacen = almacen,
                    AplicarDescuento = false,
                    Cantidad = cantidad,
                    delegacion = Constantes.Empresas.DELEGACION_DEFECTO,
                    formaVenta = FORMA_VENTA_MIRAVIA,
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = Constantes.Empresas.IVA_DEFECTO,
                    PrecioUnitario = cantidad != 0 ? Math.Round(baseImponible / cantidad, 4) : baseImponible,
                    Producto = orderItem.ProductId,
                    texto = orderItem.Name.ToUpper(),
                    tipoLinea = 1, // producto
                    vistoBueno = true,
                    Usuario = _configuracion.usuario
                };
                lineasNesto.Add(lineaNesto);

                if (orderItem.ShippingAmount != 0)
                {
                    decimal baseImponiblePortes = Math.Round(orderItem.ShippingAmount / porcentajeIva, 2, MidpointRounding.AwayFromZero);
                    LineaPedidoVentaDTO lineaPortes = new LineaPedidoVentaDTO
                    {
                        almacen = almacen,
                        AplicarDescuento = false,
                        Cantidad = (short)1,
                        delegacion = Constantes.Empresas.DELEGACION_DEFECTO,
                        formaVenta = FORMA_VENTA_MIRAVIA,
                        estado = 1,
                        fechaEntrega = DateTime.Today,
                        iva = Constantes.Empresas.IVA_DEFECTO,
                        PrecioUnitario = baseImponiblePortes,
                        Producto = "62400003",
                        texto = "PORTES " + orderItem.Name.ToUpper(),
                        tipoLinea = 2, // cuenta contable
                        vistoBueno = true,
                        Usuario = _configuracion.usuario
                    };
                    lineasNesto.Add(lineaPortes);
                }
            }
            return lineasNesto;
        }

    }
}
