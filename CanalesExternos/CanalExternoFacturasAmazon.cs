using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.CanalesExternos.ApisExternas;
using Nesto.Modulos.CanalesExternos.Cuadres;
using Nesto.Modulos.CanalesExternos.Models;
using Nesto.Modulos.CanalesExternos.Models.Cuadres;
using Nesto.Modulos.CanalesExternos.Models.Cuadres.Saldo555;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalExternoFacturasAmazon : ICanalExternoFacturas
    {
        private const string EMPRESA_DEFECTO = "1";
        private const string PROVEEDOR_AMAZON = "999";
        private const string CONTACTO_DEFECTO = "0";
        private const string FORMA_VENTA_AMAZON = "STK";
        private const string PRODUCTO_CONTABLE = "60000100";
        private const string DELEGACION_DEFECTO = "ALG";
        private const string IVA_GENERAL = "G21";
        private const string IVA_EXENTO = "EX";
        private const string FORMA_PAGO_AMAZON = "TRN";
        private const string PLAZOS_PAGO_CONTADO = "CONTADO";
        private const string PERIODO_FACTURACION_NORMAL = "NRM";
        private const int MAX_LONGITUD_TEXTO = 50;
        internal const decimal TOLERANCIA_DESCUADRE_SILENCIOSO = 0.005M;
        internal const decimal TOLERANCIA_DESCUADRE_MAXIMA = 0.02M;

        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;
        private readonly string _carpetaPdfs;

        public CanalExternoFacturasAmazon(
            IConfiguracion configuracion,
            IServicioAutenticacion servicioAutenticacion,
            string carpetaPdfs = null)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
            _carpetaPdfs = carpetaPdfs;
        }

        public string NombreCanal => "Amazon";
        public bool SoportaDescargaPdf => true;
        public bool SoportaCuadreLiquidacion => true;

        public async Task<ObservableCollection<FacturaCanalExterno>> GetFacturasMesAsync(int año, int mes, int margenDiasAtras)
        {
            DateTime finMes = new DateTime(año, mes, DateTime.DaysInMonth(año, mes));
            DateTime inicioMes = new DateTime(año, mes, 1);
            DateTime desde = inicioMes.AddDays(-Math.Max(margenDiasAtras, 0));

            var facturas = await AmazonApiInvoicesService.LeerFacturasAsync(desde, finMes);
            var yaContabilizadas = await GetFacturasYaContabilizadasAsync(desde, finMes);
            return MarcarEstados(facturas, yaContabilizadas);
        }

        internal static ObservableCollection<FacturaCanalExterno> MarcarEstados(
            ObservableCollection<FacturaCanalExterno> facturas,
            IReadOnlyDictionary<string, int> yaContabilizadas)
        {
            var dict = yaContabilizadas ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            DateTime? fechaMaxContabilizada = null;
            if (dict.Count > 0)
            {
                var fechas = facturas
                    .Where(f => f.InvoiceId != null && dict.ContainsKey(f.InvoiceId))
                    .Select(f => (DateTime?)f.FechaFactura)
                    .ToList();
                if (fechas.Count > 0) fechaMaxContabilizada = fechas.Max();
            }

            foreach (var f in facturas)
            {
                if (f.InvoiceId != null && dict.TryGetValue(f.InvoiceId, out int numFactura))
                {
                    f.Estado = EstadoFacturaCanalExterno.YaContabilizada;
                    f.NumeroFacturaNesto = numFactura;
                }
                else if (fechaMaxContabilizada.HasValue && f.FechaFactura < fechaMaxContabilizada.Value)
                {
                    f.Estado = EstadoFacturaCanalExterno.Hueco;
                }
                else
                {
                    f.Estado = EstadoFacturaCanalExterno.PendienteContabilizar;
                }
            }
            return facturas;
        }

        public async Task<IReadOnlyDictionary<string, int>> GetFacturasYaContabilizadasAsync(DateTime desde, DateTime hasta)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }
                string url = $"PedidosCompra/FacturasContabilizadasProveedor?proveedor={PROVEEDOR_AMAZON}" +
                    $"&desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error consultando facturas contabilizadas: {response.StatusCode}");
                }
                string json = await response.Content.ReadAsStringAsync();
                var lista = JsonConvert.DeserializeObject<List<FacturaContabilizadaProveedor>>(json)
                            ?? new List<FacturaContabilizadaProveedor>();
                return lista
                    .Where(f => !string.IsNullOrWhiteSpace(f.NumeroDocumentoProv))
                    .GroupBy(f => f.NumeroDocumentoProv, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().NumeroFactura, StringComparer.OrdinalIgnoreCase);
            }
        }

        private class FacturaContabilizadaProveedor
        {
            public string NumeroDocumentoProv { get; set; }
            public int NumeroFactura { get; set; }
        }

        public async Task<FacturaCanalExterno> ContabilizarFacturaAsync(FacturaCanalExterno factura)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(factura.InvoiceId))
                {
                    factura.Estado = EstadoFacturaCanalExterno.Error;
                    factura.MensajeError = "Falta el NºFactura de Amazon (copiar del PDF de Seller Central).";
                    return factura;
                }
                ValidarImportes(factura);

                string pathPdf = await GuardarPdfAsync(factura);
                object request = ConstruirRequest(factura, pathPdf);

                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(_configuracion.servidorAPI);
                    if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                    {
                        throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                    }
                    HttpContent content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("PedidosCompra/CrearAlbaranYFactura", content);
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        factura.Estado = EstadoFacturaCanalExterno.Error;
                        factura.MensajeError = $"NestoAPI {(int)response.StatusCode}: {ExtraerMensajeError(errorBody)}";
                        return factura;
                    }
                    string body = await response.Content.ReadAsStringAsync();
                    dynamic respuesta = JsonConvert.DeserializeObject(body);
                    bool exito = respuesta?.exito == true || respuesta?.Exito == true;
                    if (!exito)
                    {
                        factura.Estado = EstadoFacturaCanalExterno.Error;
                        factura.MensajeError = "El servicio de compras devolvió error al crear la factura";
                        return factura;
                    }
                    int? numFactura = (int?)(respuesta?.factura ?? respuesta?.Factura);
                    int? numPedido = (int?)(respuesta?.pedido ?? respuesta?.Pedido);
                    int? asiento = (int?)(respuesta?.asientoFactura ?? respuesta?.AsientoFactura);
                    int? asientoPago = (int?)(respuesta?.asientoPago ?? respuesta?.AsientoPago);
                    factura.NumeroFacturaNesto = numFactura;
                    factura.NumeroPedidoNesto = numPedido;
                    factura.AsientoNesto = asiento;
                    factura.AsientoPagoNesto = asientoPago;
                    factura.Estado = EstadoFacturaCanalExterno.Contabilizada;
                }
            }
            catch (Exception ex)
            {
                factura.Estado = EstadoFacturaCanalExterno.Error;
                factura.MensajeError = ex.Message;
            }
            return factura;
        }

        public async Task<byte[]> DescargarPdfAsync(FacturaCanalExterno factura)
        {
            if (string.IsNullOrWhiteSpace(factura?.UrlPdf)) return null;
            using (var http = new HttpClient())
            {
                return await http.GetByteArrayAsync(factura.UrlPdf);
            }
        }

        public async Task<int> RefrescarEstadosContabilizadasAsync(
            IList<FacturaCanalExterno> facturas, int año, int mes, int margenDiasAtras)
        {
            if (facturas == null) return 0;
            DateTime finMes = new DateTime(año, mes, DateTime.DaysInMonth(año, mes));
            DateTime inicioMes = new DateTime(año, mes, 1);
            DateTime desde = inicioMes.AddDays(-Math.Max(margenDiasAtras, 0));
            var dict = await GetFacturasYaContabilizadasAsync(desde, finMes);
            int marcadas = 0;
            foreach (var f in facturas)
            {
                if (string.IsNullOrEmpty(f.InvoiceId)) continue;
                if (f.Estado == EstadoFacturaCanalExterno.YaContabilizada) continue;
                if (f.Estado == EstadoFacturaCanalExterno.Contabilizada) continue;
                if (dict.TryGetValue(f.InvoiceId, out int numFactura))
                {
                    f.Estado = EstadoFacturaCanalExterno.YaContabilizada;
                    f.NumeroFacturaNesto = numFactura;
                    marcadas++;
                }
            }
            return marcadas;
        }

        public async Task<CuadreLiquidacionCanalExterno> CuadrarConLiquidacionAsync(int año, int mes)
        {
            DateTime inicio = new DateTime(año, mes, 1);
            DateTime fin = inicio.AddMonths(1).AddDays(-1);

            var facturas = await AmazonApiInvoicesService.LeerFacturasAsync(inicio, fin);
            var liquidaciones = AmazonApiFinancesService.LeerFinancialEventGroups(inicio, 100);

            var cuadre = new CuadreLiquidacionCanalExterno
            {
                Año = año,
                Mes = mes,
                TotalFacturasContabilizadas = facturas.Sum(f => f.Total),
                TotalComisionesLiquidaciones = 0M
            };

            foreach (var liq in liquidaciones.Where(l => l.FechaPago >= inicio && l.FechaPago <= fin))
            {
                var detallePago = AmazonApiFinancesService.LeerFinancialEvents(liq.PagoExternalId, 1000);
                cuadre.TotalComisionesLiquidaciones += detallePago.Comision;
            }

            var agrupadoFacturas = facturas
                .GroupBy(f => new { f.MarketplaceId, f.NombreMarket })
                .Select(g => new CuadreLiquidacionDetalle
                {
                    MarketplaceId = g.Key.MarketplaceId ?? string.Empty,
                    NombreMarket = g.Key.NombreMarket,
                    TotalFacturas = g.Sum(f => f.Total)
                });
            cuadre.Detalle.AddRange(agrupadoFacturas);

            return cuadre;
        }

        /// <inheritdoc />
        public async Task<ResultadoCuadre<string>> CuadrarFacturasAsync(int año, int mes)
        {
            DateTime inicio = new DateTime(año, mes, 1);
            DateTime fin = inicio.AddMonths(1).AddDays(-1);

            var facturasAmazon = await AmazonApiInvoicesService.LeerFacturasAsync(inicio, fin);
            var yaContabilizadas = await GetFacturasYaContabilizadasAsync(inicio, fin);

            // Cuadre por presencia de InvoiceId: el endpoint actual de Nesto solo devuelve
            // mapeo InvoiceId → NúmeroFactura, así que los importes no se comparan en Fase 1.
            var resultado = MotorCuadre.ConciliarPorPresencia(
                nesto: yaContabilizadas,
                amazon: facturasAmazon.Where(f => !string.IsNullOrWhiteSpace(f.InvoiceId)),
                claveNesto: kvp => kvp.Key,
                claveAmazon: f => f.InvoiceId);
            resultado.Nombre = $"Facturas Amazon {mes:D2}/{año}";

            return resultado;
        }

        /// <inheritdoc />
        public async Task<ResultadoCuadre<string>> CuadrarLiquidacionesAsync(int año, int mes)
        {
            DateTime inicio = new DateTime(año, mes, 1);
            DateTime fin = inicio.AddMonths(1).AddDays(-1);

            var apuntesExtracto = await GetExtractoProveedorAsync(inicio, fin);
            var liquidacionesAmazon = AmazonApiFinancesService.LeerFinancialEventGroups(inicio, 100)
                .Where(l => l.FechaPago >= inicio && l.FechaPago <= fin);

            var resultado = ConstruirCuadreLiquidaciones(apuntesExtracto, liquidacionesAmazon);
            resultado.Nombre = $"Liquidaciones Amazon {mes:D2}/{año}";
            return resultado;
        }

        /// <summary>
        /// Construye el cuadre de liquidaciones a partir de los dos lados ya cargados. Puro, sin IO:
        /// extraído para permitir tests unitarios sin HTTP ni SDK de Amazon.
        /// </summary>
        internal static ResultadoCuadre<string> ConstruirCuadreLiquidaciones(
            IEnumerable<ApunteExtractoProveedorDto> apuntesExtracto,
            IEnumerable<PagoCanalExterno> liquidacionesAmazon)
        {
            // Lado Nesto: solo apuntes de pago (importe negativo en extracto proveedor =
            // DEBE, lo que equivale a un pago recibido/compensación). Ignoramos apuntes de
            // factura (positivos) — esos ya se cuadran en el cuadre de facturas.
            var pagosNesto = apuntesExtracto
                .Where(a => a.Importe < 0 && !string.IsNullOrWhiteSpace(a.DocumentoProveedor));

            // Comparamos por FinancialEventGroupId (guardado en DocumentoProveedor cuando se
            // contabiliza el pago de Amazon) y comparamos importes absolutos.
            return MotorCuadre.Conciliar(
                nesto: pagosNesto,
                amazon: liquidacionesAmazon,
                claveNesto: a => a.DocumentoProveedor,
                claveAmazon: l => l.PagoExternalId,
                importeNesto: a => Math.Abs(a.Importe),
                importeAmazon: l => l.Importe);
        }

        /// <summary>
        /// Cliente REST para <c>GET api/Informes/ExtractoProveedor?empresa=1&amp;proveedor=999&amp;...</c>.
        /// </summary>
        public async Task<List<ApunteExtractoProveedorDto>> GetExtractoProveedorAsync(DateTime desde, DateTime hasta)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }
                string url = $"Informes/ExtractoProveedor?empresa={EMPRESA_DEFECTO}&proveedor={PROVEEDOR_AMAZON}" +
                    $"&fechaDesde={desde:yyyy-MM-dd}&fechaHasta={hasta:yyyy-MM-dd}";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error consultando extracto proveedor: {response.StatusCode}");
                }
                string json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<ApunteExtractoProveedorDto>>(json)
                    ?? new List<ApunteExtractoProveedorDto>();
            }
        }

        /// <inheritdoc />
        public async Task<ResultadoCuadre<string>> CuadrarPedidosAsync(int año, int mes)
        {
            DateTime inicio = new DateTime(año, mes, 1);
            DateTime fin = inicio.AddMonths(1).AddDays(-1);

            var pedidosAmazon = await AmazonApiOrdersService.Ejecutar(inicio, 1000);
            var pedidosNesto = await GetPedidosCanalAsync("Amazon", inicio, fin);

            var resultado = ConstruirCuadrePedidos(pedidosNesto, pedidosAmazon, inicio, fin);
            resultado.Nombre = $"Pedidos Amazon {mes:D2}/{año}";
            return resultado;
        }

        /// <summary>
        /// Construye el cuadre de pedidos a partir de los dos lados ya cargados. Puro, sin IO.
        /// Filtra los pedidos Amazon por <c>PurchaseDate</c> dentro del rango para asegurar
        /// que solo entran los del período (la Orders API puede devolver pedidos anteriores).
        /// </summary>
        internal static ResultadoCuadre<string> ConstruirCuadrePedidos(
            IEnumerable<ResumenPedidoVentaCanalExternoDto> pedidosNesto,
            IEnumerable<FikaAmazonAPI.AmazonSpApiSDK.Models.Orders.Order> pedidosAmazon,
            DateTime inicio,
            DateTime fin)
        {
            // Lado Nesto: solo pedidos con CanalOrderId resuelto. Sin identificador no hay
            // clave para emparejar.
            var nestoConOrderId = pedidosNesto
                .Where(p => !string.IsNullOrWhiteSpace(p.CanalOrderId));

            var amazonEnRango = (pedidosAmazon ?? Enumerable.Empty<FikaAmazonAPI.AmazonSpApiSDK.Models.Orders.Order>())
                .Where(o => !string.IsNullOrWhiteSpace(o?.AmazonOrderId))
                .Where(o =>
                {
                    if (!DateTimeOffset.TryParse(o.PurchaseDate, out var purchase)) return false;
                    var fecha = purchase.UtcDateTime;
                    return fecha >= inicio && fecha <= fin;
                });

            return MotorCuadre.ConciliarPorPresencia(
                nesto: nestoConOrderId,
                amazon: amazonEnRango,
                claveNesto: p => NormalizarOrderId(p.CanalOrderId),
                claveAmazon: o => NormalizarOrderId(o.AmazonOrderId));
        }

        /// <summary>
        /// Normaliza el OrderId eliminando el prefijo "FBA " para que los pedidos FBA y los
        /// MFN con el mismo AmazonOrderId se consideren el mismo.
        /// </summary>
        private static string NormalizarOrderId(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId)) return orderId;
            string trimmed = orderId.Trim();
            return trimmed.StartsWith("FBA ", StringComparison.OrdinalIgnoreCase)
                ? trimmed.Substring(4).Trim()
                : trimmed;
        }

        /// <summary>
        /// Issue NestoAPI#164 (Nesto#349 Fase 4): para cada cuenta 555 de cada marketplace
        /// (Pago + Comisión), pide al endpoint el saldo al corte y devuelve una lista
        /// resumen ordenada por antigüedad de la partida más vieja descendente.
        /// </summary>
        public async Task<List<ResumenSaldoCuentaDto>> CalcularSaldos555Async(DateTime fechaCorte, IProgress<string> progreso = null)
        {
            var cuentas = ConstruirListaCuentas555();
            var resultado = new List<ResumenSaldoCuentaDto>();

            using (var client = new HttpClient())
            {
                // Timeout más largo: en cuentas con muchos apuntes el endpoint carga todos
                // los movimientos del año y corre FIFO, puede tardar.
                client.Timeout = TimeSpan.FromMinutes(5);
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                int idx = 0;
                int total = cuentas.Count;
                foreach (var fila in cuentas)
                {
                    idx++;
                    progreso?.Report($"Saldo 555 [{idx}/{total}] — {fila.Cuenta} {fila.NombreMarket} ({fila.Concepto})...");
                    try
                    {
                        string url = $"Informes/SaldoCuenta555?empresa={EMPRESA_DEFECTO}" +
                            $"&cuenta={fila.Cuenta}&fechaCorte={fechaCorte:yyyy-MM-dd}";
                        var response = await client.GetAsync(url);
                        if (!response.IsSuccessStatusCode)
                        {
                            fila.Error = $"HTTP {(int)response.StatusCode}";
                        }
                        else
                        {
                            string json = await response.Content.ReadAsStringAsync();
                            fila.Resultado = JsonConvert.DeserializeObject<SaldoCuenta555ResultadoDto>(json);
                        }
                    }
                    catch (Exception ex)
                    {
                        fila.Error = ex.Message;
                    }
                    resultado.Add(fila);
                }
            }

            return resultado
                .OrderByDescending(r => r.DiasMasAntiguo)
                .ThenByDescending(r => Math.Abs(r.Saldo))
                .ToList();
        }

        /// <summary>
        /// 22 cuentas 555 = 11 marketplaces × 2 (Pago + Comisión). Miravia (572) queda fuera.
        /// </summary>
        internal static List<ResumenSaldoCuentaDto> ConstruirListaCuentas555()
        {
            var lista = new List<ResumenSaldoCuentaDto>();
            foreach (var m in DatosMarkets.Mercados)
            {
                if (!string.IsNullOrEmpty(m.CuentaContablePago) && m.CuentaContablePago.StartsWith("555"))
                {
                    lista.Add(new ResumenSaldoCuentaDto
                    {
                        Cuenta = m.CuentaContablePago,
                        NombreMarket = m.NombreMarket,
                        Concepto = "Pago"
                    });
                }
                if (!string.IsNullOrEmpty(m.CuentaContableComision) && m.CuentaContableComision.StartsWith("555"))
                {
                    lista.Add(new ResumenSaldoCuentaDto
                    {
                        Cuenta = m.CuentaContableComision,
                        NombreMarket = m.NombreMarket,
                        Concepto = "Comisión"
                    });
                }
            }
            return lista;
        }

        /// <summary>
        /// Cliente REST para <c>GET api/PedidosVenta/PorCanalExterno</c>.
        /// </summary>
        public async Task<List<ResumenPedidoVentaCanalExternoDto>> GetPedidosCanalAsync(string canal, DateTime desde, DateTime hasta)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }
                string url = $"PedidosVenta/PorCanalExterno?empresa={EMPRESA_DEFECTO}&canal={canal}" +
                    $"&desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error consultando pedidos por canal: {response.StatusCode}");
                }
                string json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<ResumenPedidoVentaCanalExternoDto>>(json)
                    ?? new List<ResumenPedidoVentaCanalExternoDto>();
            }
        }

        /// Web API devuelve un JSON anidado: { Message, ExceptionMessage, InnerException: {...} }.
        /// Recorremos la cadena y concatenamos los mensajes para que Aida vea el error real.
        internal static string ExtraerMensajeError(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return "(sin detalle)";
            try
            {
                dynamic obj = JsonConvert.DeserializeObject(body);
                var mensajes = new List<string>();
                dynamic actual = obj;
                int profundidad = 0;
                while (actual != null && profundidad < 6)
                {
                    string msg = (string)(actual.ExceptionMessage ?? actual.Message);
                    if (!string.IsNullOrWhiteSpace(msg) && !mensajes.Contains(msg))
                        mensajes.Add(msg);
                    actual = actual.InnerException;
                    profundidad++;
                }
                if (mensajes.Count == 0) return body.Length > 300 ? body.Substring(0, 300) : body;
                return string.Join(" ▸ ", mensajes);
            }
            catch
            {
                return body.Length > 300 ? body.Substring(0, 300) : body;
            }
        }

        internal void ValidarImportes(FacturaCanalExterno factura)
        {
            if (factura.BaseImponible == 0M)
            {
                throw new InvalidOperationException($"La factura {factura.InvoiceId} tiene base 0. Introducir el importe del PDF.");
            }
        }

        internal object ConstruirRequest(FacturaCanalExterno factura, string pathPdf = null)
        {
            // Si conocemos el marketplace, liquidamos automáticamente el extracto del proveedor 999
            // contra la cuenta corriente del marketplace (CuentaContableComision en DatosMarkets).
            // Para facturas sin marketplace (AGL Multiple) no se puede liquidar automáticamente.
            string contrapartida = null;
            bool crearPago = false;
            if (!string.IsNullOrEmpty(factura.MarketplaceId))
            {
                var mercado = DatosMarkets.Mercados.FirstOrDefault(m => m.Id == factura.MarketplaceId);
                if (mercado != null && !string.IsNullOrEmpty(mercado.CuentaContableComision))
                {
                    contrapartida = mercado.CuentaContableComision;
                    crearPago = true;
                }
            }

            return new
            {
                Pedido = ConstruirPedido(factura, pathPdf),
                CrearPago = crearPago,
                ContraPartidaPago = contrapartida,
                Documento = factura.InvoiceId
            };
        }

        internal object ConstruirPedido(FacturaCanalExterno factura, string pathPdf = null)
        {
            return new
            {
                Empresa = EMPRESA_DEFECTO,
                Proveedor = PROVEEDOR_AMAZON,
                Contacto = CONTACTO_DEFECTO,
                Fecha = factura.FechaFactura,
                FormaPago = FORMA_PAGO_AMAZON,
                PlazosPago = PLAZOS_PAGO_CONTADO,
                PrimerVencimiento = factura.FechaFactura,
                CodigoIvaProveedor = IVA_GENERAL,
                PeriodoFacturacion = PERIODO_FACTURACION_NORMAL,
                FacturaProveedor = factura.InvoiceId,
                PathPedido = pathPdf,
                Usuario = _configuracion?.usuario,
                Lineas = new[] { ConstruirLinea(factura) }
            };
        }

        private async Task<string> GuardarPdfAsync(FacturaCanalExterno factura)
        {
            if (string.IsNullOrWhiteSpace(_carpetaPdfs) || string.IsNullOrWhiteSpace(factura?.UrlPdf))
                return null;
            try
            {
                if (!System.IO.Directory.Exists(_carpetaPdfs))
                    System.IO.Directory.CreateDirectory(_carpetaPdfs);

                string safeId = string.Join("_", factura.InvoiceId.Split(System.IO.Path.GetInvalidFileNameChars()));
                string ruta = System.IO.Path.Combine(_carpetaPdfs, $"{safeId}.pdf");
                byte[] datos = await DescargarPdfAsync(factura);
                if (datos == null || datos.Length == 0) return null;
                System.IO.File.WriteAllBytes(ruta, datos);
                return ruta;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[AMAZON] No se pudo guardar el PDF de {factura.InvoiceId}: {ex.Message}");
                return null;
            }
        }

        internal object ConstruirLinea(FacturaCanalExterno factura)
        {
            string texto = $"{factura.Concepto} {factura.Pais}".Trim();
            if (texto.Length > MAX_LONGITUD_TEXTO) texto = texto.Substring(0, MAX_LONGITUD_TEXTO);

            // El SP prdCrearAlbaránCmp filtra: estado=1 AND VistoBueno=1 AND fecharecepción<=@FechaRecepción.
            // VistoBueno lo asigna fijo el converter PedidoCompraDTO.ToCabPedidoCmp; aquí pasamos
            // Estado=1 (EN_CURSO) explícitamente para que el albarán encuentre la línea.
            return new
            {
                TipoLinea = Constantes.LineasPedido.TiposLinea.CUENTA_CONTABLE,
                Producto = PRODUCTO_CONTABLE,
                Texto = texto,
                FechaRecepcion = factura.FechaFactura, // sin hora (es último día del mes 00:00:00)
                Cantidad = 1,
                PrecioUnitario = factura.BaseImponible,
                CodigoIvaProducto = factura.CodigoIva,
                PorcentajeIva = factura.PorcentajeIva / 100M, // 0.21M para G21, etc.
                Estado = 1, // EN_CURSO, requerido por prdCrearAlbaránCmp
                Delegacion = DELEGACION_DEFECTO,
                FormaVenta = FORMA_VENTA_AMAZON,
                Enviado = true
            };
        }

        private static string DeterminarCodigoIva_obsoleto(LineaFacturaCanalExterno linea)
        {
            if (linea.PorcentajeIva <= 0M && linea.ImporteIva == 0M) return IVA_EXENTO;
            return IVA_GENERAL;
        }

        /// Parsea un listado copiado de Seller Central (Tax Document Library) y lo empareja con las
        /// facturas reconstruidas desde la API por (marketplace, tipo). Los documentos sin equivalente
        /// en la API (AGL, Buy Shipping Label, o duplicados por marketplace) se añaden como sintéticos
        /// con Base=0 para que el usuario los complete desde el PDF.
        public (int Emparejadas, int Sinteticas, int NoEmparejadas) EmparejarListado(
            IList<FacturaCanalExterno> facturas, string listadoPegado)
        {
            if (facturas == null) throw new ArgumentNullException(nameof(facturas));
            if (string.IsNullOrWhiteSpace(listadoPegado)) return (0, 0, 0);

            var entradas = ParsearListadoSellerCentral(listadoPegado);
            int emparejadas = 0;
            int sinteticas = 0;
            int noEmparejadas = 0;

            foreach (var entrada in entradas)
            {
                if (string.IsNullOrWhiteSpace(entrada.NumeroFactura)) continue;

                // Evitar duplicar si ya estaba emparejada en una llamada anterior o está contabilizada.
                if (facturas.Any(f => string.Equals(f.InvoiceId, entrada.NumeroFactura, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var match = facturas.FirstOrDefault(f =>
                    string.IsNullOrEmpty(f.InvoiceId) &&
                    string.Equals(f.NombreMarket, entrada.Marketplace, StringComparison.OrdinalIgnoreCase) &&
                    f.TipoFactura == entrada.Tipo &&
                    (!entrada.Fecha.HasValue
                        || (f.FechaFactura.Year == entrada.Fecha.Value.Year
                            && f.FechaFactura.Month == entrada.Fecha.Value.Month)));

                if (match != null)
                {
                    match.InvoiceId = entrada.NumeroFactura;
                    if (entrada.Fecha.HasValue) match.FechaFactura = entrada.Fecha.Value.Date;
                    emparejadas++;
                }
                else
                {
                    facturas.Add(CrearFacturaSintetica(entrada));
                    sinteticas++;
                }
            }

            // Facturas reconstruidas que quedaron sin InvoiceId porque no había documento correspondiente
            // en el listado pegado (suelen indicar que falta PDF en Seller Central o clasificación mal hecha)
            noEmparejadas = facturas.Count(f => string.IsNullOrEmpty(f.InvoiceId)
                && f.TipoFactura != TipoFacturaCanalExterno.Otro
                && f.Estado == EstadoFacturaCanalExterno.PendienteContabilizar);

            return (emparejadas, sinteticas, noEmparejadas);
        }

        internal class EntradaListadoAmazon
        {
            public TipoFacturaCanalExterno Tipo { get; set; }
            public string TipoTexto { get; set; }
            public string NumeroFactura { get; set; }
            public string Marketplace { get; set; }
            public DateTime? Fecha { get; set; }
        }

        internal static List<EntradaListadoAmazon> ParsearListadoSellerCentral(string texto)
        {
            var resultado = new List<EntradaListadoAmazon>();
            if (string.IsNullOrWhiteSpace(texto)) return resultado;

            var lineas = texto.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var lineaRaw in lineas)
            {
                string linea = lineaRaw.Trim();
                if (linea.Length == 0) continue;
                if (linea.StartsWith("Tipo de factura", StringComparison.OrdinalIgnoreCase)) continue;

                // El copy-paste desde Seller Central separa columnas con tab o bloques de 2+ espacios.
                var campos = Regex.Split(linea, "\t+|\\s{2,}")
                    .Select(c => c.Trim())
                    .Where(c => c.Length > 0)
                    .ToArray();

                if (campos.Length < 8) continue;

                string tipoTxt = campos[0];
                string numero = NormalizarInvoiceId(campos[2]);
                string marketplace = campos[7];
                DateTime? fecha = ParsearFechaAmazon(campos.Length > 9 ? campos[9] : null)
                                  ?? ParsearFechaAmazon(campos.Length > 8 ? campos[8] : null);

                resultado.Add(new EntradaListadoAmazon
                {
                    Tipo = ClasificarTipoListado(tipoTxt),
                    TipoTexto = tipoTxt,
                    NumeroFactura = numero,
                    Marketplace = marketplace,
                    Fecha = fecha
                });
            }
            return resultado;
        }

        /// El campo CabPedidoCmp.NºDocumentoProv es char(20). Si el InvoiceId del listado excede,
        /// quitamos guiones (patrón que ya seguía Aida manualmente: "INV-ES-340820-..." → "INVES340820...")
        /// y, si aun así no cabe, truncamos.
        internal static string NormalizarInvoiceId(string invoiceId)
        {
            if (string.IsNullOrEmpty(invoiceId)) return invoiceId;
            string limpio = invoiceId.Trim();
            if (limpio.Length <= 20) return limpio;
            string sinGuiones = limpio.Replace("-", string.Empty);
            if (sinGuiones.Length <= 20) return sinGuiones;
            return sinGuiones.Substring(0, 20);
        }

        internal static TipoFacturaCanalExterno ClasificarTipoListado(string tipoTexto)
        {
            if (string.IsNullOrEmpty(tipoTexto)) return TipoFacturaCanalExterno.Otro;
            if (tipoTexto.StartsWith("Merchant VAT Credit Note", StringComparison.OrdinalIgnoreCase))
                return TipoFacturaCanalExterno.CreditNote;
            if (tipoTexto.StartsWith("Merchant VAT Invoice", StringComparison.OrdinalIgnoreCase))
                return TipoFacturaCanalExterno.MerchantVAT;
            if (tipoTexto.StartsWith("Fulfillment by Amazon Tax Invoice", StringComparison.OrdinalIgnoreCase))
                return TipoFacturaCanalExterno.FBA;
            if (tipoTexto.IndexOf("AGL", StringComparison.OrdinalIgnoreCase) >= 0)
                return TipoFacturaCanalExterno.AGL;
            if (tipoTexto.IndexOf("Buy Shipping", StringComparison.OrdinalIgnoreCase) >= 0)
                return TipoFacturaCanalExterno.BuyShippingLabel;
            return TipoFacturaCanalExterno.Otro;
        }

        private static DateTime? ParsearFechaAmazon(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            // Formato Seller Central: "Sun Mar 01 08:50:45 UTC 2026" o "Tue Mar 31 23:00:00 UTC 2026"
            string limpio = texto.Replace("UTC ", string.Empty);
            if (DateTime.TryParseExact(limpio, "ddd MMM dd HH:mm:ss yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dt))
            {
                return dt;
            }
            return null;
        }

        private FacturaCanalExterno CrearFacturaSintetica(EntradaListadoAmazon entrada)
        {
            bool multiple = string.Equals(entrada.Marketplace, "Multiple", StringComparison.OrdinalIgnoreCase);
            string marketId = multiple ? null : AmazonApiInvoicesService.MarketplaceIdPorNombre(entrada.Marketplace);
            string pais = multiple ? "(varios)" : AmazonApiInvoicesService.DeducirPais(entrada.Marketplace, marketId);
            string codIva = AmazonApiInvoicesService.DeterminarCodigoIvaPorDefecto(marketId);

            return new FacturaCanalExterno
            {
                InvoiceId = entrada.NumeroFactura,
                FechaFactura = (entrada.Fecha ?? DateTime.Today).Date,
                MarketplaceId = marketId,
                NombreMarket = entrada.Marketplace,
                Pais = pais,
                Moneda = "EUR",
                Concepto = entrada.TipoTexto,
                TipoFactura = entrada.Tipo,
                BaseImponible = 0M,
                CodigoIva = codIva,
                Estado = EstadoFacturaCanalExterno.PendienteContabilizar
            };
        }
    }
}
