using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.CanalesExternos.ApisExternas;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalExternoPedidosPrestashopNuevaVision : ICanalExternoPedidos
    {
        private readonly IConfiguracion configuracion;
        private const string EMPRESA_DEFECTO = "1";
        private const string FORMA_PAGO_CONTRAREEMBOLSO = "Pago contra reembolso";
        private const string FORMA_PAGO_CONTRAREEMBOLSO_COMISION = "Pago contra reembolso con comisión";
        private const string FORMA_PAGO_CONTRAREEMBOLSO_INGLES = "Cash on delivery";
        private const string FORMA_PAGO_PAYPAL = "PayPal";
        private const string FORMA_PAGO_REDSYS = "Pago con tarjeta Redsys";
        private const string FORMA_PAGO_AMAZON_PAY = "Amazon Pay";
        private const string FORMA_PAGO_BIZUM = "Bizum - Pago online";
        private const string FORMA_PAGO_APLAZAME = "Aplazame";
        private const string FORMA_PAGO_MIRAVIA = "Miravia";

        private string formaVenta = "WEB";
        private readonly Interfaces.IClientesPorTelefonoService clientesLookup;

        public CanalExternoPedidosPrestashopNuevaVision(IConfiguracion configuracion, Interfaces.IClientesPorTelefonoService clientesLookup)
        {
            this.configuracion = configuracion;
            this.clientesLookup = clientesLookup;
        }
        public async Task<ObservableCollection<PedidoCanalExterno>> GetAllPedidosAsync(DateTime fechaDesde, int numeroMaxPedidos)
        {
            var servicio = new PrestashopService();
            var listaNesto = new ObservableCollection<PedidoCanalExterno>();

            var listaPrestashop = await servicio.CargarListaPedidosAsync();

            foreach (var urlPedido in listaPrestashop)
            {
                PedidoPrestashop pedidoPrestashop = await servicio.CargarPedidoAsync(urlPedido);
                // Nesto#340: la búsqueda por NIF y el nº de pedido Nesto van por la API (sin EF),
                // patrón de CanalExternoPedidosAmazon/Miravia
                pedidoPrestashop.PedidoNestoId = await BuscarPedidoNestoIdAsync(pedidoPrestashop).ConfigureAwait(false);
                Interfaces.ClientePorTelefono cliente = await BuscarClienteAsync(pedidoPrestashop.Direccion.Element("dni")?.Value).ConfigureAwait(false);
                PedidoCanalExterno pedidoExterno = TransformarPedido(pedidoPrestashop, cliente);
                pedidoExterno.Observaciones = "Phone:";
                pedidoExterno.Observaciones += !string.IsNullOrEmpty(pedidoExterno.TelefonoFijo) ? " " + pedidoExterno.TelefonoFijo : "";
                pedidoExterno.Observaciones += !string.IsNullOrEmpty(pedidoExterno.TelefonoMovil) ? " " + pedidoExterno.TelefonoMovil : "";
                pedidoExterno.Observaciones += " " + pedidoExterno.PedidoCanalId;
                listaNesto.Add(pedidoExterno);
            }

            return listaNesto;
        }

        private PedidoCanalExterno TransformarPedido(PedidoPrestashop pedidoEntrada, Interfaces.ClientePorTelefono cliente)
        {
            PedidoCanalExterno pedidoExterno = new();
            PedidoVentaDTO pedidoSalida = new()
            {
                empresa = EMPRESA_DEFECTO,
                origen = EMPRESA_DEFECTO
            };
            pedidoSalida.cliente = cliente.Cliente;
            pedidoSalida.contacto = cliente.ContactoDefecto;
            pedidoSalida.contactoCobro = cliente.ContactoCobro;
            pedidoSalida.vendedor = cliente.Vendedor;
            pedidoSalida.comentarioPicking = cliente.ComentarioPicking;

            pedidoSalida.iva = cliente.Iva;
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
            decimal totalPagado = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_paid")?.Value) / 1000000, 4);
            pedidoSalida.comentarios += "TOTAL PEDIDO: " + totalPagado.ToString("c");

            pedidoSalida.fecha = Convert.ToDateTime(pedidoEntrada.Pedido.Element("date_add")?.Value);

            string formaPago = pedidoEntrada.Pedido.Element("payment")?.Value;
            decimal totalPedido = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_products_wt")?.Value) / 1000000, 4);

            // Aquí iban los totales

            if (formaPago is FORMA_PAGO_CONTRAREEMBOLSO or FORMA_PAGO_CONTRAREEMBOLSO_INGLES or FORMA_PAGO_CONTRAREEMBOLSO_COMISION)
            {
                pedidoSalida.formaPago = "EFC";
                pedidoSalida.plazosPago = "CONTADO";
            }
            else if (formaPago is FORMA_PAGO_PAYPAL or FORMA_PAGO_REDSYS or FORMA_PAGO_BIZUM)
            {
                pedidoSalida.formaPago = "TAR";
                pedidoSalida.plazosPago = "PRE";
            }
            else
            {
                pedidoSalida.formaPago = "TRN";
                pedidoSalida.plazosPago = "PRE";
            }

            if (formaPago == FORMA_PAGO_MIRAVIA)
            {
                formaVenta = "BLT";
            }

            pedidoSalida.ruta = "00";
            pedidoSalida.serie = "NV";
            pedidoSalida.periodoFacturacion = "NRM";
            pedidoSalida.servirJunto = true;

            pedidoSalida.Usuario = configuracion.usuario;


            // aquí iban las líneas


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
            }
            else
            {
                pedidoExterno.Provincia = string.Empty;
            }
            pedidoExterno.Almacen = Constantes.Almacenes.ALMACEN_CENTRAL;

            Dictionary<string, string> cuentasFormaPago = new()
            {
                { FORMA_PAGO_PAYPAL, "57200020" },
                { FORMA_PAGO_REDSYS, "57200013" },
                { FORMA_PAGO_BIZUM, "57200013" },
                { FORMA_PAGO_AMAZON_PAY, "57200013" },
                { FORMA_PAGO_APLAZAME, "57200013" },
                { FORMA_PAGO_MIRAVIA, "57200013" }
            };

            if (cuentasFormaPago.ContainsKey(formaPago))
            {
                PrepagoDTO prepago = new()
                {
                    Importe = totalPagado != 0 ? totalPagado : pedidoSalida.Total,
                    CuentaContable = cuentasFormaPago[formaPago],
                    ConceptoAdicional = string.Format("Tienda Online {0}", formaPago)
                };

                if (prepago.ConceptoAdicional.Length > 50)
                {
                    prepago.ConceptoAdicional = prepago.ConceptoAdicional[..50];
                }

                pedidoExterno.Pedido.Prepagos.Add(prepago);
            }

            return pedidoExterno;
        }
        // Nesto#340: el nº de pedido Nesto (la referencia del canal al principio de Comentarios)
        // va por GET api/PedidosVenta/PorReferenciaCanal; antes lo resolvía PrestashopService con
        // EF. API caída → 0, igual que cuando no hay coincidencias.
        private async Task<int> BuscarPedidoNestoIdAsync(PedidoPrestashop pedidoPrestashop)
        {
            try
            {
                string referencia = pedidoPrestashop.Pedido.Element("reference")?.Value;
                return await clientesLookup.BuscarPedidoPorReferenciaCanalAsync(referencia).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // Nesto#340: la búsqueda por NIF va por GET api/Clientes/PorNif (el servidor aplica el
        // exacto-y-si-no-Contains sobre principales activos, como el EF viejo). Si la API falla,
        // el pedido sale con el cliente genérico de la tienda online, igual que sin coincidencias.
        private async Task<Interfaces.ClientePorTelefono> BuscarClienteAsync(string dniCliente)
        {
            Interfaces.ClientePorTelefono CLIENTE_TIENDA_ONLINE = new()
            {
                Cliente = "31517",
                Contacto = "0",
                ContactoDefecto = "0",
                ContactoCobro = "0",
                Vendedor = "NV",
                Iva = "G21"
            };

            dniCliente = LimpiarDni(dniCliente);
            if (dniCliente == null || dniCliente.Trim() == "")
            {
                return CLIENTE_TIENDA_ONLINE;
            }

            try
            {
                var encontrados = await clientesLookup.BuscarClientesPorNifAsync(dniCliente).ConfigureAwait(false);
                if (encontrados.Count > 0)
                {
                    return encontrados[0];
                }
            }
            catch (Exception)
            {
                // API caída: mejor el cliente genérico que tumbar la carga de pedidos
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

        public async Task<PedidoCanalExterno> GetPedido(string Id)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> EjecutarTrasCrearPedido(PedidoCanalExterno pedido)
        {
            return await PrestashopService.CambiarEstadoPedidoAsync(pedido.PedidoCanalId, 3, true); //Preparación en curo
        }

        public async Task<string> ConfirmarPedido(PedidoCanalExterno pedido)
        {
            DatosEnvioConfirmarPrestashop datosEnvio = LeerDatosEnvio(pedido);
            string resultado;
            if (await PrestashopService.ConfirmarPedidoAsync(pedido.PedidoCanalId, datosEnvio.AgenciaId, datosEnvio.NumeroSeguimiento, true))
            {
                resultado = $"Se ha añadido el número de seguimiento {datosEnvio.NumeroSeguimiento} al pedido {pedido.PedidoCanalId}";
                if (await PrestashopService.CambiarEstadoPedidoAsync(pedido.PedidoCanalId, 4, false))
                {
                    resultado += " y se ha pasado a estado Enviado.";
                }
                else
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

        // Mapeo seguimiento -> (transportista de Prestashop, tracking), por agencia.
        // Para soportar una agencia basta con una fila: el token que identifica su enlace, su id de
        // transportista en Prestashop y qué tracking mandar. NestoAPI#417: para el transportista
        // genérico 160 (GLS/Innovatrans), cuya plantilla de URL en Prestashop está vacía
        // ("https://@"), viaja el ENLACE completo sin esquema — mandar el número pelado dejaba al
        // cliente un seguimiento muerto. CEX/Sending tienen transportista propio con plantilla y
        // siguen con el número extraído de siempre.
        private static readonly (string Token, string AgenciaId, Func<string, string> ExtraerNumero)[] MapeoSeguimiento =
        {
            ("correosexpress", "105", s => DespuesDe(s, "=", ultima: false)),
            ("sending",        "103", s => DespuesDe(s, "=", ultima: true)),
            ("gls-spain.es",   "160", s => SinEsquema(s)),
            ("tip-sa.com",     "160", s => SinEsquema(s)),
        };

        // NestoAPI#258 slice (a): si el servidor ya mandó los identificadores por canal del último
        // envío (los declara la agencia en NestoAPI), se usan directamente. El parseo del enlace
        // queda como fallback para envíos sin esos datos. NestoAPI#417: el tracking preferido es
        // TrackingPrestashop (el enlace hecho); NumeroSeguimiento queda para servidores antiguos.
        internal static DatosEnvioConfirmarPrestashop LeerDatosEnvio(PedidoCanalExterno pedido)
        {
            var envio = pedido?.UltimoEnvio;
            string tracking = !string.IsNullOrWhiteSpace(envio?.TrackingPrestashop)
                ? envio.TrackingPrestashop
                : envio?.NumeroSeguimiento;
            return !string.IsNullOrWhiteSpace(envio?.TransportistaPrestashop) && !string.IsNullOrWhiteSpace(tracking)
                ? new DatosEnvioConfirmarPrestashop { AgenciaId = envio.TransportistaPrestashop, NumeroSeguimiento = tracking }
                : LeerDatosEnvio(pedido?.UltimoSeguimiento);
        }

        internal static DatosEnvioConfirmarPrestashop LeerDatosEnvio(string seguimiento)
        {
            if (string.IsNullOrWhiteSpace(seguimiento))
            {
                throw new Exception("El pedido no tiene un enlace de seguimiento que confirmar.");
            }

            foreach (var (token, agenciaId, extraer) in MapeoSeguimiento)
            {
                if (seguimiento.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string numero = extraer(seguimiento)?.Trim();
                    if (string.IsNullOrWhiteSpace(numero))
                    {
                        throw new Exception($"No se pudo extraer el número de seguimiento del enlace: {seguimiento}");
                    }
                    return new DatosEnvioConfirmarPrestashop { AgenciaId = agenciaId, NumeroSeguimiento = numero };
                }
            }

            throw new NotImplementedException($"No se reconoce la agencia del enlace de seguimiento: {seguimiento}");
        }

        private static string SinEsquema(string url)
        {
            // La plantilla del transportista genérico de Prestashop antepone "https://" al tracking.
            return url != null && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? url.Substring("https://".Length)
                : url;
        }

        private static string DespuesDe(string texto, string marca, bool ultima)
        {
            int i = ultima
                ? texto.LastIndexOf(marca, StringComparison.OrdinalIgnoreCase)
                : texto.IndexOf(marca, StringComparison.OrdinalIgnoreCase);
            return i < 0 ? null : texto.Substring(i + marca.Length);
        }

        public async Task<ICollection<LineaPedidoVentaDTO>> GetLineas(PedidoCanalExterno pedido)
        {
            var servicio = new PrestashopService();
            var urlPedido = $"https://www.productosdeesteticaypeluqueriaprofesional.com/api/orders?filter[reference]={pedido.PedidoCanalId}";

            PedidoPrestashop pedidoEntrada = await servicio.CargarPedidoPorReferenciaAsync(urlPedido); ;
            PedidoVentaDTO pedidoSalida = pedido.Pedido;
            // añadir líneas
            var listaLineasXML = pedidoEntrada.Pedido.Element("associations").Element("order_rows").Elements();
            foreach (var linea in listaLineasXML)
            {
                decimal porcentajeIva;
                decimal importeSinIva = Convert.ToDecimal(linea.Element("unit_price_tax_excl").Value) / 1000000;
                decimal importeConIva = Convert.ToDecimal(linea.Element("unit_price_tax_incl").Value) / 1000000;

                if (Convert.ToDecimal(linea.Element("unit_price_tax_excl").Value) != 0)
                {
                    porcentajeIva = Math.Round((importeConIva / importeSinIva) - 1, 2);
                }
                else
                {
                    porcentajeIva = 0;
                }

                string tipoIva;
                if (porcentajeIva == .21M || porcentajeIva == 0 || Math.Round(importeSinIva * 1.21M, 2, MidpointRounding.AwayFromZero) == Math.Round(importeConIva, 2, MidpointRounding.AwayFromZero))
                {
                    tipoIva = "G21";
                }
                else if (porcentajeIva == .10M || Math.Round(importeSinIva * 1.1M, 2, MidpointRounding.AwayFromZero) == Math.Round(importeConIva, 2, MidpointRounding.AwayFromZero))
                {
                    tipoIva = "R10";
                }
                else if (porcentajeIva == .04M || Math.Round(importeSinIva * 1.04M, 2, MidpointRounding.AwayFromZero) == Math.Round(importeConIva, 2, MidpointRounding.AwayFromZero))
                {
                    tipoIva = "SR";
                }
                else
                {
                    throw new ArgumentException(string.Format("Tipo de IVA {0} no definido", porcentajeIva.ToString("p")));
                }
                string productoRef = linea.Element("product_reference").Value;
                byte tipoLineaProducto = EsCuentaContable(productoRef) ? (byte)2 : (byte)1;

                LineaPedidoVentaDTO lineaNesto = new()
                {
                    Pedido = pedidoSalida,
                    almacen = "ALG",
                    AplicarDescuento = false,
                    Cantidad = short.Parse(linea.Element("product_quantity").Value),
                    delegacion = "ALG",
                    formaVenta = formaVenta,
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = tipoIva,
                    PrecioUnitario = Math.Round(Convert.ToDecimal(linea.Element("unit_price_tax_incl").Value) / 1000000, 4),
                    Producto = productoRef,
                    texto = linea.Element("product_name").Value.ToUpper(),
                    tipoLinea = tipoLineaProducto,
                    vistoBueno = true,
                    Usuario = configuracion.usuario
                };

                if (pedidoSalida.iva != null)
                {
                    lineaNesto.PrecioUnitario = Math.Round(lineaNesto.PrecioUnitario / (1 + porcentajeIva), 4);
                    //lineaNesto.BaseImponible = lineaNesto.precio * lineaNesto.cantidad;
                    lineaNesto.PorcentajeIva = porcentajeIva;
                }

                pedidoSalida.Lineas.Add(lineaNesto);
            }

            // Añadir portes
            if (Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_shipping_tax_incl").Value) != 0)
            {
                decimal totalPortes = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_shipping_tax_incl")?.Value) / 1000000, 4);
                LineaPedidoVentaDTO lineaPortes = new()
                {
                    almacen = "ALG",
                    AplicarDescuento = false,
                    Cantidad = 1,
                    delegacion = "ALG",
                    formaVenta = formaVenta,
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
                    lineaPortes.PrecioUnitario /= (decimal)1.21;
                    lineaPortes.PorcentajeIva = .21M;
                }

                pedidoSalida.Lineas.Add(lineaPortes);
            }

            // Añadir embalaje
            if (Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_wrapping_tax_incl").Value) != 0)
            {
                decimal totalEmbalaje = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_wrapping_tax_incl")?.Value) / 1000000, 4);
                LineaPedidoVentaDTO lineaEmbalaje = new()
                {
                    almacen = "ALG",
                    AplicarDescuento = false,
                    Cantidad = 1,
                    delegacion = "ALG",
                    formaVenta = formaVenta,
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
                    lineaEmbalaje.PrecioUnitario /= (decimal)1.21;
                    lineaEmbalaje.PorcentajeIva = .21M;
                }

                pedidoSalida.Lineas.Add(lineaEmbalaje);
            }

            // Añadir cupones de descuento
            if (Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_discounts_tax_incl").Value) != 0)
            {
                decimal totalDescuentosSinIva = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_discounts_tax_excl")?.Value) / 1000000, 4);
                decimal totalDescuentosConIva = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_discounts_tax_incl")?.Value) / 1000000, 4);
                decimal totalProductosSinIva = Math.Round(Convert.ToDecimal(pedidoEntrada.Pedido.Element("total_products")?.Value) / 1000000, 4);
                AplicarDescuentoCupon(pedidoSalida.Lineas, totalDescuentosSinIva, totalProductosSinIva, totalDescuentosConIva, formaVenta, pedidoSalida.iva, configuracion.usuario);
            }

            return pedidoSalida.Lineas;

        }

        // Cuentas contables que Prestashop envía como producto en order_rows
        private static readonly HashSet<string> CUENTAS_CONTABLES_PRESTASHOP = new() { "62400003", "62700020" };

        internal static bool EsCuentaContable(string productoRef)
        {
            return !string.IsNullOrEmpty(productoRef) && CUENTAS_CONTABLES_PRESTASHOP.Contains(productoRef);
        }

        private static readonly int[] PORCENTAJES_CONOCIDOS = { 5, 10, 15, 20, 25, 30, 100 };

        internal static decimal DetectarPorcentajeConocido(decimal totalDescuentosSinIva, decimal totalProductosSinIva)
        {
            if (totalProductosSinIva == 0)
            {
                return 0;
            }

            foreach (int porcentaje in PORCENTAJES_CONOCIDOS)
            {
                decimal descuentoEsperado = Math.Round(totalProductosSinIva * porcentaje / 100, 2, MidpointRounding.AwayFromZero);
                if (descuentoEsperado == totalDescuentosSinIva)
                {
                    return porcentaje;
                }
            }

            return 0;
        }

        internal static void AplicarDescuentoCupon(
            ICollection<LineaPedidoVentaDTO> lineas,
            decimal totalDescuentosSinIva,
            decimal totalProductosSinIva,
            decimal totalDescuentosConIva,
            string formaVenta,
            string iva,
            string usuario)
        {
            // Issue #328: Calcular total descontable desde las líneas (tipoLinea == 1),
            // excluyendo cuentas contables como la comisión contrarreembolso que
            // Prestashop incluye en total_products pero no deben recibir descuento
            decimal totalDescontable = lineas
                .Where(l => l.tipoLinea == 1)
                .Sum(l => Math.Round(l.PrecioUnitario * l.Cantidad, 2, MidpointRounding.AwayFromZero));

            decimal porcentajeDetectado = DetectarPorcentajeConocido(totalDescuentosSinIva, totalDescontable);

            // Fallback: intentar con el total de Prestashop (por si coincide)
            if (porcentajeDetectado == 0 && totalDescontable != totalProductosSinIva)
            {
                porcentajeDetectado = DetectarPorcentajeConocido(totalDescuentosSinIva, totalProductosSinIva);
            }

            if (porcentajeDetectado > 0)
            {
                foreach (var lineaProducto in lineas.Where(l => l.tipoLinea == 1))
                {
                    lineaProducto.DescuentoLinea = porcentajeDetectado / 100;
                }
            }
            else if (AplicarRegaloLineaCompleta(lineas, totalDescuentosSinIva))
            {
                // Issue #350: el cupón coincide con el importe exacto de una línea → ese producto va
                // gratis (100% en esa línea), en vez de añadir una línea TiCKET que distorsiona stats.
            }
            else
            {
                // Descuento fijo: mantener como línea TICKET
                LineaPedidoVentaDTO lineaCupon = new()
                {
                    almacen = "ALG",
                    AplicarDescuento = false,
                    Cantidad = -1,
                    delegacion = "ALG",
                    formaVenta = formaVenta,
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = "G21",
                    PrecioUnitario = totalDescuentosConIva,
                    Producto = "TiCKET",
                    texto = "CUPÓN DE DESCUENTO",
                    tipoLinea = 1, // producto
                    Usuario = usuario
                };

                if (iva != null)
                {
                    lineaCupon.PrecioUnitario /= (decimal)1.21;
                    lineaCupon.PorcentajeIva = .21M;
                }

                lineas.Add(lineaCupon);
            }
        }

        /// <summary>
        /// Issue #350: si el importe del cupón (sin IVA) coincide exactamente con el importe de una
        /// ÚNICA línea de producto, ese producto es un regalo → 100% de descuento en esa línea.
        /// Si coinciden varias líneas el caso es ambiguo (no sabemos cuál es el regalo) y se deja
        /// como cupón/TiCKET. Devuelve true si aplicó el descuento.
        /// </summary>
        internal static bool AplicarRegaloLineaCompleta(ICollection<LineaPedidoVentaDTO> lineas, decimal totalDescuentosSinIva)
        {
            if (totalDescuentosSinIva <= 0)
            {
                return false;
            }

            var lineasQueCoinciden = lineas
                .Where(l => l.tipoLinea == 1 &&
                            Math.Round(l.PrecioUnitario * l.Cantidad, 2, MidpointRounding.AwayFromZero) == totalDescuentosSinIva)
                .ToList();

            if (lineasQueCoinciden.Count != 1)
            {
                return false;
            }

            lineasQueCoinciden[0].DescuentoLinea = 1m;
            return true;
        }

        internal class DatosEnvioConfirmarPrestashop
        {
            public string AgenciaId { get; set; }
            public string NumeroSeguimiento { get; set; }
        }
    }
}
