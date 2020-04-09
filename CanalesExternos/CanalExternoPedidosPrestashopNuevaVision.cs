using System;
using System.Linq;
using System.Threading.Tasks;
using Nesto.Models;
using Nesto.Modulos.CanalesExternos.ApisExternas;
using static Nesto.Models.PedidoVenta;
using Nesto.Contratos;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Nesto.Models.Nesto.Models;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalExternoPedidosPrestashopNuevaVision : ICanalExternoPedidos
    {
        private IConfiguracion configuracion;
        private const string EMPRESA_DEFECTO = "1";
        private const string FORMA_PAGO_CONTRAREEMBOLSO = "Pago contra reembolso";

        public CanalExternoPedidosPrestashopNuevaVision(IConfiguracion configuracion)
        {
            this.configuracion = configuracion;
        }
        public async Task<ObservableCollection<PedidoVentaDTO>> GetAllPedidosAsync(DateTime fechaDesde, int numeroMaxPedidos)
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

            pedidoSalida.empresa = EMPRESA_DEFECTO;
            pedidoSalida.origen = EMPRESA_DEFECTO;
            Clientes cliente = BuscarCliente(pedidoEntrada.Direccion.Element("dni")?.Value);
            pedidoSalida.cliente = cliente.Nº_Cliente;
            pedidoSalida.contacto = cliente.ContactoDefecto;
            pedidoSalida.contactoCobro = cliente.ContactoCobro;
            pedidoSalida.vendedor = cliente.Vendedor;

            pedidoSalida.iva = cliente.IVA;
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
            decimal totalPagado = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_paid_real")?.Value) / 1000000, 4);
            pedidoSalida.comentarios += "TOTAL PEDIDO: " + totalPagado.ToString("c");

            pedidoSalida.fecha = Convert.ToDateTime(pedidoEntrada.Pedido.Element("date_add")?.Value);

            string formaPago = pedidoEntrada.Pedido.Element("payment")?.Value;
            decimal totalPedido = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_products_wt")?.Value) / 1000000, 4);
            decimal totalPortes = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_shipping_tax_incl")?.Value) / 1000000, 4);
            decimal totalDescuentos = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_discounts_tax_incl")?.Value) / 1000000, 4);
            decimal totalAPagar = totalPedido + totalPortes - totalDescuentos;
            if (formaPago == FORMA_PAGO_CONTRAREEMBOLSO)
            {
                pedidoSalida.formaPago = "EFC";
                pedidoSalida.plazosPago = "CONTADO";
            } else if (formaPago == "PayPal")
            {
                pedidoSalida.formaPago = "TAR";
                pedidoSalida.plazosPago = "PRE";
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
                    precio = Math.Round(Convert.ToDecimal(linea.Element("unit_price_tax_incl").Value) / 1000000, 4),
                    producto = linea.Element("product_reference").Value,
                    texto = linea.Element("product_name").Value.ToUpper(),
                    tipoLinea = 1, // producto
                    usuario = configuracion.usuario
                };

                if (pedidoSalida.iva != null)
                {
                    lineaNesto.precio = Math.Round(lineaNesto.precio / (decimal)1.21, 4);
                }

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

                if (pedidoSalida.iva != null)
                {
                    lineaPortes.precio = lineaPortes.precio / (decimal)1.21;
                }

                pedidoSalida.LineasPedido.Add(lineaPortes);
            }

            // Añadir cupones de descuento
            if (Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_discounts_tax_incl").Value) != 0)
            {
                LineaPedidoVentaDTO lineaCupon = new LineaPedidoVentaDTO
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

                if (pedidoSalida.iva != null)
                {
                    lineaCupon.precio = lineaCupon.precio / (decimal)1.21;
                }

                pedidoSalida.LineasPedido.Add(lineaCupon);
            }
            
            return pedidoSalida;
        }

        private Clientes BuscarCliente(string dniCliente)
        {
            Clientes CLIENTE_TIENDA_ONLINE = new Clientes
            {
                Nº_Cliente = "31517",
                Contacto = "0",
                ContactoDefecto = "0",
                ContactoCobro = "0",
                Vendedor = "NV",
                IVA = "G21"
            };

            dniCliente = LimpiarDni(dniCliente);
            if (dniCliente == null || dniCliente.Trim() == "")
            {
                return CLIENTE_TIENDA_ONLINE;
            }

            using (NestoEntities db = new NestoEntities())
            {
                Clientes clienteEncontrado = db.Clientes.Where(c => c.Empresa == EMPRESA_DEFECTO && c.ClientePrincipal == true && c.Estado >= 0 && c.CIF_NIF.Equals(dniCliente)).SingleOrDefault();
                if (clienteEncontrado == null)
                {
                    clienteEncontrado = db.Clientes.Where(c => c.Empresa == EMPRESA_DEFECTO && c.ClientePrincipal == true && c.Estado >= 0 && c.CIF_NIF.Contains(dniCliente)).FirstOrDefault();
                }
                if (clienteEncontrado != null)
                {
                    return clienteEncontrado;
                }
            }

            return CLIENTE_TIENDA_ONLINE;          
        }

        public string LimpiarDni(string dniCliente)
        {
            if (dniCliente == null)
            {
                return "";
            }
            dniCliente = dniCliente.Trim();
            dniCliente = Regex.Replace(dniCliente, @"[^0-9A-Za-z]", "", RegexOptions.None);
            dniCliente = dniCliente.TrimStart('0');
            return dniCliente;
        }

        public PedidoVentaDTO GetPedido(int Id)
        {
            throw new NotImplementedException();
        }
    }
}
