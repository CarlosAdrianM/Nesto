using System;
using System.Linq;
using System.Threading.Tasks;
using Nesto.Modulos.CanalesExternos.ApisExternas;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Nesto.Models.Nesto.Models;
using System.Collections.Generic;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.CanalesExternos.Models;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalExternoPedidosPrestashopNuevaVision : ICanalExternoPedidos
    {
        private IConfiguracion configuracion;
        private const string EMPRESA_DEFECTO = "1";
        private const string FORMA_PAGO_CONTRAREEMBOLSO = "Pago contra reembolso";
        private const string FORMA_PAGO_CONTRAREEMBOLSO_INGLES = "Cash on delivery";
        private const string FORMA_PAGO_PAYPAL = "PayPal";
        private const string FORMA_PAGO_REDSYS = "Pago con tarjeta Redsys";
        private const string FORMA_PAGO_AMAZON_PAY = "Amazon Pay (Checkout v2)";
        private const string FORMA_PAGO_BIZUM = "Bizum - Pago online";

        public CanalExternoPedidosPrestashopNuevaVision(IConfiguracion configuracion)
        {
            this.configuracion = configuracion;
        }
        public async Task<ObservableCollection<PedidoCanalExterno>> GetAllPedidosAsync(DateTime fechaDesde, int numeroMaxPedidos)
        {
            var servicio = new PrestashopService();
            var listaNesto = new ObservableCollection<PedidoCanalExterno>();

            var listaPrestashop = await servicio.CargarListaPedidosAsync();

            foreach (var urlPedido in listaPrestashop)
            {
                PedidoPrestashop pedidoPrestashop = await servicio.CargarPedidoAsync(urlPedido);
                PedidoCanalExterno pedidoExterno = TransformarPedido(pedidoPrestashop);
                pedidoExterno.Observaciones = "Phone:";
                pedidoExterno.Observaciones += !string.IsNullOrEmpty(pedidoExterno.TelefonoFijo) ? " " + pedidoExterno.TelefonoFijo : "";
                pedidoExterno.Observaciones += !string.IsNullOrEmpty(pedidoExterno.TelefonoMovil) ? " " + pedidoExterno.TelefonoMovil : "";
                pedidoExterno.Observaciones += " " + pedidoExterno.PedidoCanalId;
                listaNesto.Add(pedidoExterno);
            }
                        
            return listaNesto;
        }

        private PedidoCanalExterno TransformarPedido(PedidoPrestashop pedidoEntrada)
        {
            PedidoCanalExterno pedidoExterno = new PedidoCanalExterno();
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
            if (pedidoEntrada.PedidoNestoId != 0)
            {
                pedidoSalida.comentarios += "N/ Pedido: " + pedidoEntrada.PedidoNestoId + "\r\n";
            }
            decimal totalPagado = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_paid_real")?.Value) / 1000000, 4);
            pedidoSalida.comentarios += "TOTAL PEDIDO: " + totalPagado.ToString("c");

            pedidoSalida.fecha = Convert.ToDateTime(pedidoEntrada.Pedido.Element("date_add")?.Value);

            string formaPago = pedidoEntrada.Pedido.Element("payment")?.Value;
            decimal totalPedido = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_products_wt")?.Value) / 1000000, 4);
            decimal totalPortes = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_shipping_tax_incl")?.Value) / 1000000, 4);
            decimal totalDescuentos = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_discounts_tax_incl")?.Value) / 1000000, 4);
            decimal totalEmbalaje = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_wrapping_tax_incl")?.Value) / 1000000, 4);
            decimal totalAPagar = totalPedido + totalPortes - totalDescuentos;
            if (formaPago == FORMA_PAGO_CONTRAREEMBOLSO || formaPago == FORMA_PAGO_CONTRAREEMBOLSO_INGLES)
            {
                pedidoSalida.formaPago = "EFC";
                pedidoSalida.plazosPago = "CONTADO";
            } else if (formaPago == FORMA_PAGO_PAYPAL || formaPago == FORMA_PAGO_REDSYS || formaPago == FORMA_PAGO_BIZUM)
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
            pedidoSalida.servirJunto = true;

            pedidoSalida.Usuario = configuracion.usuario;


            // añadir líneas
            var listaLineasXML = pedidoEntrada.Pedido.Element("associations").Element("order_rows").Elements();
            foreach(var linea in listaLineasXML)
            {
                decimal porcentajeIva;
                decimal importeSinIva = Convert.ToDecimal(linea.Element("unit_price_tax_excl").Value) / 1000000;
                decimal importeConIva = Convert.ToDecimal(linea.Element("unit_price_tax_incl").Value) / 1000000;

                if (Convert.ToDecimal(linea.Element("unit_price_tax_excl").Value) != 0) {
                    porcentajeIva = Math.Round(importeConIva / importeSinIva - 1, 2);
                } else
                {
                    porcentajeIva = 0;
                }
                    
                string tipoIva;
                if (porcentajeIva == .21M || porcentajeIva == 0 || Math.Round(importeSinIva * 1.21M, 2, MidpointRounding.AwayFromZero) == Math.Round(importeConIva, 2, MidpointRounding.AwayFromZero))
                {
                    tipoIva = "G21";
                } else if (porcentajeIva == .10M || Math.Round(importeSinIva * 1.1M, 2, MidpointRounding.AwayFromZero) == Math.Round(importeConIva, 2, MidpointRounding.AwayFromZero))
                {
                    tipoIva = "R10";
                } else if (porcentajeIva == .04M || Math.Round(importeSinIva * 1.04M, 2, MidpointRounding.AwayFromZero) == Math.Round(importeConIva, 2, MidpointRounding.AwayFromZero))
                {
                    tipoIva = "SR";
                } else
                {
                    throw new ArgumentException(string.Format("Tipo de IVA {0} no definido", porcentajeIva.ToString("p")));
                }
                LineaPedidoVentaDTO lineaNesto = new LineaPedidoVentaDTO
                {
                    Pedido = pedidoSalida,
                    almacen = "ALG",
                    AplicarDescuento = false,
                    Cantidad = short.Parse(linea.Element("product_quantity").Value),
                    delegacion = "ALG",
                    formaVenta = "WEB",
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = tipoIva,
                    PrecioUnitario = Math.Round(Convert.ToDecimal(linea.Element("unit_price_tax_incl").Value) / 1000000, 4),
                    Producto = linea.Element("product_reference").Value,
                    texto = linea.Element("product_name").Value.ToUpper(),
                    tipoLinea = 1, // producto
                    Usuario = configuracion.usuario
                };

                if (pedidoSalida.iva != null)
                {
                    lineaNesto.PrecioUnitario = Math.Round(lineaNesto.PrecioUnitario / (decimal)(1+porcentajeIva), 4);
                    //lineaNesto.BaseImponible = lineaNesto.precio * lineaNesto.cantidad;
                    lineaNesto.PorcentajeIva = porcentajeIva;
                }

                pedidoSalida.Lineas.Add(lineaNesto);
            }

            // Añadir portes
            if (Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_shipping_tax_incl").Value) != 0)
            {
                LineaPedidoVentaDTO lineaPortes = new LineaPedidoVentaDTO
                {
                    almacen = "ALG",
                    AplicarDescuento = false,
                    Cantidad = (short)1,
                    delegacion = "ALG",
                    formaVenta = "WEB",
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = "G21",
                    PrecioUnitario = totalPortes,
                    Producto = "62400003",
                    texto = "GASTOS DE TRANSPORTE",
                    tipoLinea = 2, // cuenta contable
                    Usuario = configuracion.usuario
                };

                if (pedidoSalida.iva != null)
                {
                    lineaPortes.PrecioUnitario = lineaPortes.PrecioUnitario / (decimal)1.21;
                }

                pedidoSalida.Lineas.Add(lineaPortes);
            }

            // Añadir embalaje
            if (Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_wrapping_tax_incl").Value) != 0)
            {
                LineaPedidoVentaDTO lineaEmbalaje = new LineaPedidoVentaDTO
                {
                    almacen = "ALG",
                    AplicarDescuento = false,
                    Cantidad = (short)1,
                    delegacion = "ALG",
                    formaVenta = "WEB",
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = "G21",
                    PrecioUnitario = totalEmbalaje,
                    Producto = "62700020",
                    texto = "EMBALAJE DE REGALO",
                    tipoLinea = 2, // cuenta contable
                    Usuario = configuracion.usuario
                };

                if (pedidoSalida.iva != null)
                {
                    lineaEmbalaje.PrecioUnitario = lineaEmbalaje.PrecioUnitario / (decimal)1.21;
                }

                pedidoSalida.Lineas.Add(lineaEmbalaje);
            }

            // Añadir cupones de descuento
            if (Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_discounts_tax_incl").Value) != 0)
            {
                LineaPedidoVentaDTO lineaCupon = new LineaPedidoVentaDTO
                {
                    almacen = "ALG",
                    AplicarDescuento = false,
                    Cantidad = (short)-1,
                    delegacion = "ALG",
                    formaVenta = "WEB",
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = "G21",
                    PrecioUnitario = totalDescuentos,
                    Producto = "TiCKET",
                    texto = "CUPÓN DE DESCUENTO",
                    tipoLinea = 1, // producto
                    Usuario = configuracion.usuario
                };

                if (pedidoSalida.iva != null)
                {
                    lineaCupon.PrecioUnitario = lineaCupon.PrecioUnitario / (decimal)1.21;
                }

                pedidoSalida.Lineas.Add(lineaCupon);
            }

            pedidoExterno.Pedido = pedidoSalida;
            pedidoExterno.PedidoCanalId = pedidoEntrada.Pedido.Element("reference").Value;
            pedidoExterno.PedidoNestoId = pedidoEntrada.PedidoNestoId;
            pedidoExterno.Nombre = pedidoEntrada.Direccion.Element("firstname").Value.ToString().ToUpper() + " ";
            pedidoExterno.Nombre += pedidoEntrada.Direccion.Element("lastname").Value.ToString().ToUpper();
            pedidoExterno.Direccion = pedidoEntrada.Direccion.Element("address1")?.Value.ToString().ToUpper();
            pedidoExterno.Direccion += pedidoEntrada.Direccion.Element("address2")?.Value != "" ? " " + pedidoEntrada.Direccion.Element("address2")?.Value.ToString().ToUpper() : "";
            pedidoExterno.CodigoPostal = pedidoEntrada.Direccion.Element("postcode")?.Value.ToString().ToUpper();
            pedidoExterno.Poblacion = pedidoEntrada.Direccion.Element("city")?.Value.ToString().ToUpper();
            pedidoExterno.TelefonoFijo = pedidoEntrada.Direccion.Element("phone")?.Value.ToString().ToUpper();
            pedidoExterno.TelefonoMovil = pedidoEntrada.Direccion.Element("phone_mobile")?.Value.ToString().ToUpper();
            pedidoExterno.CorreoElectronico = pedidoEntrada.Cliente.Element("email")?.Value.ToString();
            pedidoExterno.PaisISO = pedidoEntrada.Pais.Element("iso_code")?.Value.ToString();
            if (pedidoEntrada.Provincia != null)
            {
                pedidoExterno.Provincia = pedidoEntrada.Provincia.Element("name")?.Value.ToString().ToUpper();
            } else
            {
                pedidoExterno.Provincia = string.Empty;
            }

            Dictionary<string, string> cuentasFormaPago = new Dictionary<string, string>();
            cuentasFormaPago.Add(FORMA_PAGO_PAYPAL, "57200020"); 
            cuentasFormaPago.Add(FORMA_PAGO_REDSYS, "57200013");
            cuentasFormaPago.Add(FORMA_PAGO_BIZUM, "57200013");
            cuentasFormaPago.Add(FORMA_PAGO_AMAZON_PAY, "57200022"); 

            if (cuentasFormaPago.ContainsKey(formaPago))
            {
                PrepagoDTO prepago = new PrepagoDTO
                {
                    Importe = totalPagado != 0 ? totalPagado : pedidoSalida.Total,
                    CuentaContable = cuentasFormaPago[formaPago],
                    ConceptoAdicional = string.Format("Tienda Online {0}", formaPago)
                };

                if (prepago.ConceptoAdicional.Length > 50)
                {
                    prepago.ConceptoAdicional = prepago.ConceptoAdicional.Substring(0, 50);
                }

                pedidoExterno.Pedido.Prepagos.Add(prepago);
            }

            return pedidoExterno;
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

        public PedidoCanalExterno GetPedido(int Id)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> EjecutarTrasCrearPedido(PedidoCanalExterno pedido)
        {
            return await PrestashopService.CambiarEstadoPedidoAsync(pedido.PedidoCanalId, 3, true); //Preparación en curo
        }

        public async Task<string> ConfirmarPedido(PedidoCanalExterno pedido)
        {
            DatosEnvioConfirmarPrestashop datosEnvio = LeerDatosEnvio(pedido.UltimoSeguimiento);
            string resultado = string.Empty;
            if (await PrestashopService.ConfirmarPedidoAsync(pedido.PedidoCanalId, datosEnvio.AgenciaId, datosEnvio.NumeroSeguimiento, true))
            {
                resultado = $"Se ha añadido el número de seguimiento {datosEnvio.NumeroSeguimiento} al pedido {pedido.PedidoCanalId}";
                if (await PrestashopService.CambiarEstadoPedidoAsync(pedido.PedidoCanalId, 4, false))
                {
                    resultado += " y se ha pasado a estado Enviado.";
                } else
                {
                    resultado += " pero NO se ha podido pasar a estado Enviado.";
                }
            }
            else
            {
                resultado = $"No se ha podido añadir el número de seguimiento {datosEnvio.NumeroSeguimiento} al pedido {pedido.PedidoCanalId}";
            }
            return resultado;
        }

        private DatosEnvioConfirmarPrestashop LeerDatosEnvio(string seguimiento)
        {
            if (seguimiento.Contains("correosexpress"))
            {
                int indiceIgual = seguimiento.IndexOf("="); // Obtiene el índice del símbolo "="

                if (indiceIgual == -1) // Verifica si se encuentra el símbolo "=" en la cadena
                {
                    throw new Exception("El seguimiento de CEX tiene que incluir el símbolo = (igual)");
                }
                return new DatosEnvioConfirmarPrestashop
                {
                    AgenciaId = "105",
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
                return new DatosEnvioConfirmarPrestashop
                {
                    AgenciaId = "103",
                    NumeroSeguimiento = seguimiento.Substring(indiceIgual + 1)
                };
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        private class DatosEnvioConfirmarPrestashop
        {
            public string AgenciaId { get; set; }
            public string NumeroSeguimiento { get; set; }
        }
    }
}
