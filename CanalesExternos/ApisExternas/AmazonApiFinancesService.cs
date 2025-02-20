using FikaAmazonAPI.AmazonSpApiSDK.Models.Finances;
using FikaAmazonAPI.Parameter.Finance;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.CanalesExternos.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public class AmazonApiFinancesService {

    public static ObservableCollection<PagoCanalExterno> LeerFinancialEventGroups(DateTime fechaDesde, int numeroMaxPedidos)
    {
        try 
        {
            var conexion = AmazonApiOrdersService.ConexionAmazon();
            var listaEventGroups = InvokeListFinancialEventGroups(fechaDesde, numeroMaxPedidos);
            ObservableCollection<PagoCanalExterno> listaPagos = new ObservableCollection<PagoCanalExterno>();

            foreach (var grupo in listaEventGroups.OrderBy(l => l.FundTransferDate))
            {
                try
                {
                    decimal importe = (decimal)(grupo.OriginalTotal?.CurrencyCode == Constantes.Empresas.MONEDA_CONTABILIDAD || grupo.ConvertedTotal == null ?
                            grupo.OriginalTotal != null ?
                                grupo.OriginalTotal.CurrencyAmount : 0 : grupo.ConvertedTotal?.CurrencyAmount);
                    decimal importeOriginal = grupo.OriginalTotal == null ? 0 : (decimal)(grupo.OriginalTotal?.CurrencyAmount);
                    if (grupo.OriginalTotal == null)
                    {
                        grupo.ProcessingStatus = "Error";
                    }
                    PagoCanalExterno pago = new PagoCanalExterno
                    {
                        MonedaOriginal = grupo.OriginalTotal != null ? grupo.OriginalTotal.CurrencyCode : grupo.BeginningBalance.CurrencyCode,
                        PagoExternalId = grupo.FinancialEventGroupId,
                        Estado = grupo.ProcessingStatus,
                        Importe = importe,
                        //ImporteOriginal = (decimal)(grupo.OriginalTotal?.CurrencyAmount),
                        ImporteOriginal = importeOriginal,
                        SaldoInicial = (decimal)grupo.BeginningBalance.CurrencyAmount,
                        FechaPago = grupo.FundTransferDate == null ? DateTime.MinValue : (DateTime)grupo.FundTransferDate,
                        FechaInicio = grupo.FinancialEventGroupStart,
                        FechaFinal = grupo.FinancialEventGroupEnd
                    };
                    if (pago.MonedaOriginal != Constantes.Empresas.MONEDA_CONTABILIDAD && grupo.ConvertedTotal != null)
                    {
                        pago.CambioDivisas = (decimal)(grupo.ConvertedTotal.CurrencyAmount / grupo.OriginalTotal?.CurrencyAmount);
                    } else
                    {
                        pago.CambioDivisas = 1M;
                    }
                    listaPagos.Add(pago);
                } catch (Exception e)
                {
                    throw e;
                }
            }

            return listaPagos;
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public static CabeceraDetallePagoCanalExterno LeerFinancialEvents(string pagoId, int numeroMaxEventos)
    {        
        try
        {
            var conexion = AmazonApiOrdersService.ConexionAmazon();
            List<FinancialEvents> listaEventos = InvokeListFinancialEvents(pagoId, numeroMaxEventos);

            CabeceraDetallePagoCanalExterno cabecera = new CabeceraDetallePagoCanalExterno();
            ProcesarListaEventos(listaEventos, cabecera);

            return cabecera;
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    private static void ProcesarListaEventos(List<FinancialEvents> listaGeneralEventos, CabeceraDetallePagoCanalExterno cabecera)
    {
        foreach (var listaEventos in listaGeneralEventos)
        {
            foreach (var evento in listaEventos.ProductAdsPaymentEventList)
            {
                cabecera.Publicidad = (decimal)evento.TransactionValue.CurrencyAmount;
                cabecera.FacturaPublicidad = evento.InvoiceId;
            }
            foreach (var evento in listaEventos.ServiceFeeEventList)
            {
                foreach (var fee in evento.FeeList)
                {
                    cabecera.Comision += (decimal)fee.FeeAmount.CurrencyAmount;
                }
            }
            foreach (var evento in listaEventos.ShipmentEventList)
            {
                DetallePagoCanalExterno detalle = new DetallePagoCanalExterno
                {
                    ExternalId = evento.AmazonOrderId,
                    CuentaContablePago = DatosMarkets.Buscar(DatosMarkets.Mercados.Single(m => m.NombreMarket == evento.MarketplaceName).Id).CuentaContablePago,
                    CuentaContableComisiones = DatosMarkets.Buscar(DatosMarkets.Mercados.Single(m => m.NombreMarket == evento.MarketplaceName).Id).CuentaContableComision
                };
                foreach (var item in evento.ShipmentItemList)
                {
                    foreach (var cargo in item?.ItemChargeList)
                    {
                        detalle.Importe += (decimal)cargo.ChargeAmount.CurrencyAmount;
                    }
                    foreach (var tasa in item?.ItemFeeList)
                    {
                        detalle.Comisiones += (decimal)tasa.FeeAmount.CurrencyAmount;
                    }
                    if (item.PromotionList != null)
                    {
                        foreach (var promocion in item?.PromotionList)
                        {
                            detalle.Promociones += (decimal)promocion.PromotionAmount.CurrencyAmount;
                        }
                    }
                    if (item.ItemTaxWithheldList != null)
                    {
                        foreach (var impuesto in item?.ItemTaxWithheldList)
                        {
                            foreach (var tax in impuesto.TaxesWithheld)
                            {
                                detalle.Importe += (decimal)tax.ChargeAmount.CurrencyAmount;
                            }
                        }
                    }
                }

                cabecera.DetallePagos.Add(detalle);
            }
            foreach (var evento in listaEventos.RefundEventList)
            {
                DetallePagoCanalExterno detalle = new DetallePagoCanalExterno
                {
                    ExternalId = evento.AmazonOrderId,
                    CuentaContablePago = DatosMarkets.Buscar(DatosMarkets.Mercados.Single(m => m.NombreMarket == evento.MarketplaceName).Id).CuentaContablePago,
                    CuentaContableComisiones = DatosMarkets.Buscar(DatosMarkets.Mercados.Single(m => m.NombreMarket == evento.MarketplaceName).Id).CuentaContableComision
                };
                foreach (var item in evento.ShipmentItemAdjustmentList)
                {
                    foreach (var cargo in item?.ItemChargeAdjustmentList)
                    {
                        detalle.Importe += (decimal)cargo.ChargeAmount.CurrencyAmount;
                    }
                    if (item?.ItemFeeAdjustmentList != null)
                    {
                        foreach (var tasa in item.ItemFeeAdjustmentList)
                        {
                            detalle.Comisiones += (decimal)tasa.FeeAmount.CurrencyAmount;
                        }
                    }
                    if (item.PromotionAdjustmentList != null)
                    {
                        foreach (var promocion in item?.PromotionAdjustmentList)
                        {
                            detalle.Comisiones += (decimal)promocion.PromotionAmount.CurrencyAmount;
                        }
                    }
                    if (item.ItemTaxWithheldList != null)
                    {
                        foreach (var impuesto in item?.ItemTaxWithheldList)
                        {
                            foreach (var tax in impuesto.TaxesWithheld)
                            {
                                detalle.Importe += (decimal)tax.ChargeAmount.CurrencyAmount;
                            }
                        }
                    }
                }

                cabecera.DetallePagos.Add(detalle);
            }
            foreach (var ajuste in listaEventos.AdjustmentEventList)
            {
                if (ajuste.AdjustmentType == "ReserveDebit")
                {
                    cabecera.AjusteRetencion += (decimal)ajuste.AdjustmentAmount.CurrencyAmount;
                }
                else if (ajuste.AdjustmentType == "ReserveCredit")
                {
                    cabecera.RestoAjustes += (decimal)ajuste.AdjustmentAmount.CurrencyAmount;
                }
                else
                {
                    cabecera.Comision += (decimal)ajuste.AdjustmentAmount.CurrencyAmount;
                }

            }
            foreach (var garantia in listaEventos.GuaranteeClaimEventList)
            {
                foreach (var ajuste in garantia.ShipmentItemAdjustmentList)
                {
                    foreach (var cargo in ajuste.ItemChargeAdjustmentList)
                    {
                        cabecera.Comision += (decimal)cargo.ChargeAmount.CurrencyAmount;
                    }
                    foreach (var comision in ajuste.ItemFeeAdjustmentList)
                    {
                        cabecera.Comision += (decimal)comision.FeeAmount.CurrencyAmount;
                    }
                }

            }
        }
    }

    public static List<FinancialEventGroup> InvokeListFinancialEventGroups(DateTime fechaDesde, int numeroMaxGruposEventos)
    {
        var conexion = AmazonApiOrdersService.ConexionAmazon();
        ParameterListFinancialEventGroup parametros = new();
        parametros.MaxResultsPerPage = numeroMaxGruposEventos;
        parametros.FinancialEventGroupStartedAfter = fechaDesde;
        var listaEventos = conexion.Financial.ListFinancialEventGroups(parametros);
        return listaEventos.ToList();
    }

    public static List<FinancialEvents> InvokeListFinancialEvents(string pagoId, int numeroMaxEventos)
    {
        var conexion = AmazonApiOrdersService.ConexionAmazon();
        var listaEventos = conexion.Financial.ListFinancialEventsByGroupId(pagoId);
        return listaEventos;
    }
}
