using FikaAmazonAPI.AmazonSpApiSDK.Models.Finances;
using FikaAmazonAPI.Parameter.Finance;
using Nesto.Modulos.CanalesExternos.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.ApisExternas
{
    /// Reconstruye las facturas que Amazon EU SARL emite al vendedor desde la Finances API v1
    /// (ListFinancialEvents). Los importes y cuenta de facturas son aproximaciones:
    /// Amazon usa criterios internos (settlement date, entidad legal emisora, moneda de conversión)
    /// que no se exponen en la API. El usuario ajusta los valores finales con los PDFs de Seller
    /// Central en pantalla antes de contabilizar.
    public static class AmazonApiInvoicesService
    {
        // Tasas aproximadas para convertir a EUR cuando la API devuelve fees en moneda del marketplace.
        // Es solo una guía para que los importes pre-rellenados sean del orden correcto; el usuario
        // introduce el importe exacto desde el PDF.
        private static readonly Dictionary<string, decimal> TasasAprox = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "EUR", 1M },
            { "SEK", 0.09M },
            { "GBP", 1.17M },
            { "PLN", 0.23M },
            { "TRY", 0.03M }
        };

        public static async Task<ObservableCollection<FacturaCanalExterno>> LeerFacturasAsync(DateTime desde, DateTime hasta)
        {
            Trace.TraceInformation($"[AMAZON INVOICES] Solicitando ListFinancialEvents {desde:yyyy-MM-dd} → {hasta:yyyy-MM-dd}");

            var conexion = AmazonApiOrdersService.ConexionAmazon();
            var parametros = new ParameterListFinancialEvents { PostedAfter = desde, PostedBefore = hasta };
            var paginas = await Task.Run(() => conexion.Financial.ListFinancialEvents(parametros));
            var fees = ExtraerFees(paginas).ToList();

            Trace.TraceInformation($"[AMAZON INVOICES] Fees individuales extraídos: {fees.Count}");
            var monedas = fees.GroupBy(f => f.Moneda ?? "?")
                              .Select(g => $"{g.Key}:{g.Count()}");
            Trace.TraceInformation($"[AMAZON INVOICES] Monedas detectadas: [{string.Join(", ", monedas)}]");

            return AgruparEnFacturas(fees);
        }

        internal class Fee
        {
            public DateTime PostedDate { get; set; }
            public string MarketplaceName { get; set; }
            public string Tipo { get; set; }
            public decimal Importe { get; set; }
            public string Moneda { get; set; }
        }

        internal static IEnumerable<Fee> ExtraerFees(IList<FinancialEvents> paginas)
        {
            if (paginas == null) yield break;
            foreach (var pagina in paginas.Where(p => p != null))
            {
                foreach (var f in ExtraerDeShipment(pagina.ShipmentEventList, "Shipment")) yield return f;
                foreach (var f in ExtraerDeShipment(pagina.RefundEventList, "Refund")) yield return f;
                foreach (var f in ExtraerDeShipment(pagina.GuaranteeClaimEventList, "GuaranteeClaim")) yield return f;
                foreach (var f in ExtraerDeShipment(pagina.ChargebackEventList, "Chargeback")) yield return f;
            }
        }

        private static IEnumerable<Fee> ExtraerDeShipment(ShipmentEventList lista, string prefijoTipo)
        {
            if (lista == null) yield break;
            foreach (var ev in lista)
            {
                if (ev == null || !ev.PostedDate.HasValue) continue;
                string mp = ev.MarketplaceName ?? "(desconocido)";
                DateTime fecha = ev.PostedDate.Value;

                foreach (var f in ComponentesComoFees(ev.ShipmentFeeList, prefijoTipo, "ShipmentFee", fecha, mp)) yield return f;
                foreach (var f in ComponentesComoFees(ev.ShipmentFeeAdjustmentList, prefijoTipo, "ShipmentFeeAdj", fecha, mp)) yield return f;
                foreach (var f in ComponentesComoFees(ev.OrderFeeList, prefijoTipo, "OrderFee", fecha, mp)) yield return f;
                foreach (var f in ComponentesComoFees(ev.OrderFeeAdjustmentList, prefijoTipo, "OrderFeeAdj", fecha, mp)) yield return f;

                foreach (var item in (ev.ShipmentItemList ?? new ShipmentItemList()))
                {
                    foreach (var f in ComponentesComoFees(item?.ItemFeeList, prefijoTipo, "ItemFee", fecha, mp)) yield return f;
                    foreach (var f in ComponentesComoFees(item?.ItemFeeAdjustmentList, prefijoTipo, "ItemFeeAdj", fecha, mp)) yield return f;
                }
                foreach (var item in (ev.ShipmentItemAdjustmentList ?? new ShipmentItemList()))
                {
                    foreach (var f in ComponentesComoFees(item?.ItemFeeList, prefijoTipo, "ItemFee", fecha, mp)) yield return f;
                    foreach (var f in ComponentesComoFees(item?.ItemFeeAdjustmentList, prefijoTipo, "ItemFeeAdj", fecha, mp)) yield return f;
                }
            }
        }

        private static IEnumerable<Fee> ComponentesComoFees(FeeComponentList componentes, string prefijo, string sufijo, DateTime fecha, string mp)
        {
            if (componentes == null) yield break;
            foreach (var fc in componentes)
            {
                decimal imp = (decimal)(fc?.FeeAmount?.CurrencyAmount ?? 0);
                if (imp == 0M) continue;
                yield return new Fee
                {
                    PostedDate = fecha,
                    MarketplaceName = mp,
                    Tipo = $"{prefijo}:{sufijo}:{fc.FeeType}",
                    Importe = imp,
                    Moneda = fc.FeeAmount?.CurrencyCode ?? "EUR"
                };
            }
        }

        internal static TipoFacturaCanalExterno DeterminarTipoFactura(string tipoFee)
        {
            if (string.IsNullOrEmpty(tipoFee)) return TipoFacturaCanalExterno.Otro;
            if (tipoFee.StartsWith("Refund:", StringComparison.OrdinalIgnoreCase))
                return TipoFacturaCanalExterno.CreditNote;
            if (tipoFee.IndexOf("FBAPerUnitFulfillmentFee", StringComparison.OrdinalIgnoreCase) >= 0)
                return TipoFacturaCanalExterno.FBA;
            if (tipoFee.StartsWith("Shipment:", StringComparison.OrdinalIgnoreCase))
                return TipoFacturaCanalExterno.MerchantVAT;
            return TipoFacturaCanalExterno.Otro;
        }

        private static string ConceptoDe(TipoFacturaCanalExterno tipo)
        {
            switch (tipo)
            {
                case TipoFacturaCanalExterno.MerchantVAT: return "comisiones";
                case TipoFacturaCanalExterno.FBA: return "FBA";
                case TipoFacturaCanalExterno.CreditNote: return "abono";
                default: return "otros";
            }
        }

        internal static ObservableCollection<FacturaCanalExterno> AgruparEnFacturas(IEnumerable<Fee> fees)
        {
            var resultado = new ObservableCollection<FacturaCanalExterno>();
            if (fees == null) return resultado;

            var conMarketYFecha = fees.Where(f => !string.IsNullOrEmpty(f.MarketplaceName)
                                                && f.MarketplaceName != "(global)"
                                                && f.PostedDate > DateTime.MinValue);

            var grupos = conMarketYFecha.GroupBy(f => new
            {
                MP = f.MarketplaceName,
                Año = f.PostedDate.Year,
                Mes = f.PostedDate.Month,
                Tipo = DeterminarTipoFactura(f.Tipo)
            });

            foreach (var grupo in grupos)
            {
                string marketplaceId = MarketplaceIdPorNombre(grupo.Key.MP);
                string pais = DeducirPais(grupo.Key.MP, marketplaceId);
                string concepto = ConceptoDe(grupo.Key.Tipo);
                string codigoIva = DeterminarCodigoIvaPorDefecto(marketplaceId);
                // Asumimos que los fees vienen con IVA incluido (Amazon sin VCS),
                // así que dividimos entre (1+IVA) para acercarnos a la base imponible del PDF.
                decimal factor = FactorDeIva(codigoIva);

                // Suma en moneda original y convierte a EUR para el importe aproximado inicial.
                decimal sumaConIva = grupo.Sum(f => ConvertirAEur(-f.Importe, f.Moneda));
                decimal baseAprox = Math.Round(sumaConIva / factor, 2, MidpointRounding.AwayFromZero);

                var factura = new FacturaCanalExterno
                {
                    InvoiceId = null,
                    FechaFactura = UltimoDiaDelMes(grupo.Key.Año, grupo.Key.Mes),
                    MarketplaceId = marketplaceId,
                    NombreMarket = grupo.Key.MP,
                    Pais = pais,
                    Moneda = "EUR",
                    Concepto = concepto,
                    TipoFactura = grupo.Key.Tipo,
                    BaseImponible = baseAprox,
                    CodigoIva = codigoIva
                };

                // Desglose informativo por tipo (solo para ver detalle)
                foreach (var tipoGrupo in grupo.GroupBy(f => f.Tipo))
                {
                    decimal detSumaConIva = tipoGrupo.Sum(f => ConvertirAEur(-f.Importe, f.Moneda));
                    decimal detBase = Math.Round(detSumaConIva / factor, 2, MidpointRounding.AwayFromZero);
                    if (detBase == 0M) continue;
                    factura.DetalleLineas.Add(new LineaFacturaCanalExterno
                    {
                        Descripcion = tipoGrupo.Key,
                        BaseImponible = detBase,
                        CodigoIva = codigoIva
                    });
                }

                resultado.Add(factura);
            }

            var ordenado = new ObservableCollection<FacturaCanalExterno>(
                resultado.OrderBy(f => f.FechaFactura).ThenBy(f => f.NombreMarket).ThenBy(f => f.Concepto));

            Trace.TraceInformation($"[AMAZON INVOICES] Facturas pre-generadas: {ordenado.Count}");
            foreach (var f in ordenado)
            {
                Trace.TraceInformation(
                    $"[AMAZON INVOICES] FACTURA {f.FechaFactura:yyyy-MM-dd} {f.NombreMarket,-15} {f.Concepto,-25} " +
                    $"base={f.BaseImponible,12:N2}  iva({f.CodigoIva})={f.ImporteIva,10:N2}  total={f.Total,12:N2}");
            }
            return ordenado;
        }

        internal static decimal ConvertirAEur(decimal importe, string moneda)
        {
            if (string.IsNullOrEmpty(moneda)) return importe;
            if (TasasAprox.TryGetValue(moneda, out var tasa)) return importe * tasa;
            return importe;
        }

        internal static string DeterminarCodigoIvaPorDefecto(string marketplaceId)
        {
            if (string.IsNullOrEmpty(marketplaceId)) return "G21";
            if (marketplaceId == "A1F83G8C2ARO7P") return "EX"; // UK por defecto EX; ajustable por el usuario
            return "G21";
        }

        private static decimal FactorDeIva(string codigoIva)
        {
            switch (codigoIva)
            {
                case "G21": return 1.21M;
                case "G10": return 1.10M;
                case "G04": return 1.04M;
                default: return 1M;
            }
        }

        internal static string MarketplaceIdPorNombre(string nombreMarket)
        {
            if (string.IsNullOrEmpty(nombreMarket)) return null;
            var m = DatosMarkets.Mercados.FirstOrDefault(x =>
                string.Equals(x.NombreMarket, nombreMarket, StringComparison.OrdinalIgnoreCase));
            return m?.Id;
        }

        private static DateTime UltimoDiaDelMes(int año, int mes)
            => new DateTime(año, mes, DateTime.DaysInMonth(año, mes));

        internal static string DeducirPais(string nombreMarket, string marketplaceId)
        {
            if (!string.IsNullOrWhiteSpace(nombreMarket))
            {
                var dot = nombreMarket.LastIndexOf('.');
                if (dot >= 0 && dot < nombreMarket.Length - 1)
                {
                    string sufijo = nombreMarket.Substring(dot + 1).ToLowerInvariant();
                    switch (sufijo)
                    {
                        case "es": return "España";
                        case "fr": return "Francia";
                        case "it": return "Italia";
                        case "de": return "Alemania";
                        case "nl": return "Holanda";
                        case "se": return "Suecia";
                        case "pl": return "Polonia";
                        case "tr": return "Turquía";
                        case "ie": return "Irlanda";
                        case "be": return "Bélgica";
                        case "uk": return "UK";
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(marketplaceId))
            {
                var mercado = DatosMarkets.Mercados.FirstOrDefault(m => m.Id == marketplaceId);
                if (mercado != null) return DeducirPais(mercado.NombreMarket, null);
            }
            return string.Empty;
        }
    }
}
