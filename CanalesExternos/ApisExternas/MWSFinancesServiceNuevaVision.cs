/*******************************************************************************
 * Copyright 2009-2020 Amazon Services. All Rights Reserved.
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 *
 * You may not use this file except in compliance with the License. 
 * You may obtain a copy of the License at: http://aws.amazon.com/apache2.0
 * This file is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
 * CONDITIONS OF ANY KIND, either express or implied. See the License for the 
 * specific language governing permissions and limitations under the License.
 *******************************************************************************
 * MWS Finances Service
 * API Version: 2015-05-01
 * Library Version: 2020-02-21
 * Generated: Fri Feb 21 09:07:27 PST 2020
 */

using Claytondus.AmazonMWS.Finances.Model;
using Nesto.Contratos;
using Nesto.Models.Nesto.Models;
using Nesto.Modulos.CanalesExternos.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace Claytondus.AmazonMWS.Finances
{

    /// <summary>
    /// Runnable sample code to demonstrate usage of the C# client.
    ///
    /// To use, import the client source as a console application,
    /// and mark this class as the startup object. Then, replace
    /// parameters below with sensible values and run.
    /// </summary>
    public class MWSFinancesServiceSample {

        public static ObservableCollection<PagoCanalExterno> LeerFinancialEventGroups(DateTime fechaDesde, int numeroMaxPedidos)
        {
            // TODO: Set the below configuration variables before attempting to run

            // Developer AWS access key
            string accessKey = ConfigurationManager.AppSettings["AmazonAccessKey"];

            // Developer AWS secret key
            string secretKey = ConfigurationManager.AppSettings["AmazonSecretKey"];

            // The client application name
            string appName = "Nesto";

            // The client application version
            string appVersion = "1.0";

            // The endpoint for region service and version (see developer guide)
            // ex: https://mws.amazonservices.com
            string serviceURL = "https://mws-eu.amazonservices.com";

            // Create a configuration object
            MWSFinancesServiceConfig config = new MWSFinancesServiceConfig();
            config.ServiceURL = serviceURL;
            // Set other client connection configurations here if needed
            // Create the client itself
            MWSFinancesService client = new MWSFinancesServiceClient(accessKey, secretKey, appName, appVersion, config);

            MWSFinancesServiceSample sample = new MWSFinancesServiceSample(client);

            // Uncomment the operation you'd like to test here
            // TODO: Modify the request created in the Invoke method to be valid

            try 
            {
                IMWSResponse response = null;
                response = sample.InvokeListFinancialEventGroups(fechaDesde, numeroMaxPedidos);
                // response = sample.InvokeListFinancialEventGroupsByNextToken();
                // response = sample.InvokeListFinancialEvents();
                // response = sample.InvokeListFinancialEventsByNextToken();
                // response = sample.InvokeGetServiceStatus();
                Console.WriteLine("Response:");
                ResponseHeaderMetadata rhmd = response.ResponseHeaderMetadata;
                // We recommend logging the request id and timestamp of every call.
                Console.WriteLine("RequestId: " + rhmd.RequestId);
                Console.WriteLine("Timestamp: " + rhmd.Timestamp);
                // string responseXml = response.ToXML();
                
                ListFinancialEventGroupsResponse respuesta = (ListFinancialEventGroupsResponse)response;

                ObservableCollection<PagoCanalExterno> listaPagos = new ObservableCollection<PagoCanalExterno>();

                foreach (var grupo in respuesta.ListFinancialEventGroupsResult.FinancialEventGroupList.OrderBy(l => l.FundTransferDate))
                {
                    try
                    {
                        PagoCanalExterno pago = new PagoCanalExterno
                        {
                            MonedaOriginal = grupo.OriginalTotal != null ? grupo.OriginalTotal.CurrencyCode : grupo.BeginningBalance.CurrencyCode,
                            PagoExternalId = grupo.FinancialEventGroupId,
                            Estado = grupo.ProcessingStatus,
                            Importe = (decimal)(grupo.OriginalTotal?.CurrencyCode == Constantes.Empresas.MONEDA_CONTABILIDAD || grupo.ConvertedTotal == null ? 
                                grupo.OriginalTotal != null ? 
                                    grupo.OriginalTotal.CurrencyAmount : 0 : grupo.ConvertedTotal?.CurrencyAmount),
                            ImporteOriginal = (decimal)(grupo.OriginalTotal?.CurrencyAmount),
                            SaldoInicial = grupo.BeginningBalance.CurrencyAmount,
                            FechaPago = grupo.FundTransferDate,
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
            catch (MWSFinancesServiceException ex)
            {
                // Exception properties are important for diagnostics.
                ResponseHeaderMetadata rhmd = ex.ResponseHeaderMetadata;
                Console.WriteLine("Service Exception:");
                if(rhmd != null)
                {
                    Console.WriteLine("RequestId: " + rhmd.RequestId);
                    Console.WriteLine("Timestamp: " + rhmd.Timestamp);
                }
                Console.WriteLine("Message: " + ex.Message);
                Console.WriteLine("StatusCode: " + ex.StatusCode);
                Console.WriteLine("ErrorCode: " + ex.ErrorCode);
                Console.WriteLine("ErrorType: " + ex.ErrorType);
                throw ex;
            }
        }

        public static CabeceraDetallePagoCanalExterno LeerFinancialEvents(string pagoId, int numeroMaxEventos)
        {
            // TODO: Set the below configuration variables before attempting to run

            // Developer AWS access key
            string accessKey = ConfigurationManager.AppSettings["AmazonAccessKey"];

            // Developer AWS secret key
            string secretKey = ConfigurationManager.AppSettings["AmazonSecretKey"];

            // The client application name
            string appName = "Nesto";

            // The client application version
            string appVersion = "1.0";

            // The endpoint for region service and version (see developer guide)
            // ex: https://mws.amazonservices.com
            string serviceURL = "https://mws-eu.amazonservices.com";

            // Create a configuration object
            MWSFinancesServiceConfig config = new MWSFinancesServiceConfig();
            config.ServiceURL = serviceURL;
            // Set other client connection configurations here if needed
            // Create the client itself
            MWSFinancesService client = new MWSFinancesServiceClient(accessKey, secretKey, appName, appVersion, config);

            MWSFinancesServiceSample sample = new MWSFinancesServiceSample(client);

            // Uncomment the operation you'd like to test here
            // TODO: Modify the request created in the Invoke method to be valid

            try
            {
                IMWSResponse response = null;
                // response = sample.InvokeListFinancialEventGroups(fechaDesde, numeroMaxPedidos);
                // response = sample.InvokeListFinancialEventGroupsByNextToken();
                response = sample.InvokeListFinancialEvents(pagoId, numeroMaxEventos);
                // response = sample.InvokeListFinancialEventsByNextToken();
                // response = sample.InvokeGetServiceStatus();
                Console.WriteLine("Response:");
                ResponseHeaderMetadata rhmd = response.ResponseHeaderMetadata;
                // We recommend logging the request id and timestamp of every call.
                Console.WriteLine("RequestId: " + rhmd.RequestId);
                Console.WriteLine("Timestamp: " + rhmd.Timestamp);
                // string responseXml = response.ToXML();

                ListFinancialEventsResponse respuesta = (ListFinancialEventsResponse)response;
                FinancialEvents listaEventos = respuesta.ListFinancialEventsResult.FinancialEvents;

                CabeceraDetallePagoCanalExterno cabecera = new CabeceraDetallePagoCanalExterno();
                ProcesarListaEventos(listaEventos, cabecera);

                ListFinancialEventsByNextTokenResponse nuevaRespuesta;
                if (!respuesta.ListFinancialEventsResult.IsSetNextToken())
                {
                    return cabecera;
                }

                nuevaRespuesta = LeerFinancialEventsByNextToken(respuesta.ListFinancialEventsResult.NextToken);
                listaEventos = nuevaRespuesta.ListFinancialEventsByNextTokenResult.FinancialEvents;
                ProcesarListaEventos(listaEventos, cabecera);

                while (nuevaRespuesta.ListFinancialEventsByNextTokenResult.IsSetNextToken())
                {
                    nuevaRespuesta = LeerFinancialEventsByNextToken(nuevaRespuesta.ListFinancialEventsByNextTokenResult.NextToken);
                    listaEventos = nuevaRespuesta.ListFinancialEventsByNextTokenResult.FinancialEvents;
                    ProcesarListaEventos(listaEventos, cabecera);
                }

                return cabecera;
            }
            catch (MWSFinancesServiceException ex)
            {
                // Exception properties are important for diagnostics.
                ResponseHeaderMetadata rhmd = ex.ResponseHeaderMetadata;
                Console.WriteLine("Service Exception:");
                if (rhmd != null)
                {
                    Console.WriteLine("RequestId: " + rhmd.RequestId);
                    Console.WriteLine("Timestamp: " + rhmd.Timestamp);
                }
                Console.WriteLine("Message: " + ex.Message);
                Console.WriteLine("StatusCode: " + ex.StatusCode);
                Console.WriteLine("ErrorCode: " + ex.ErrorCode);
                Console.WriteLine("ErrorType: " + ex.ErrorType);
                throw ex;
            }
        }

        private static void ProcesarListaEventos(FinancialEvents listaEventos, CabeceraDetallePagoCanalExterno cabecera)
        {
            foreach (var evento in listaEventos.ProductAdsPaymentEventList)
            {
                cabecera.Publicidad = evento.TransactionValue.CurrencyAmount;
                cabecera.FacturaPublicidad = evento.InvoiceId;
            }
            foreach (var evento in listaEventos.ServiceFeeEventList)
            {
                foreach (var fee in evento.FeeList)
                {
                    cabecera.Comision += fee.FeeAmount.CurrencyAmount;
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
                    foreach (var cargo in item.ItemChargeList)
                    {
                        detalle.Importe += cargo.ChargeAmount.CurrencyAmount;
                    }
                    foreach (var tasa in item.ItemFeeList)
                    {
                        detalle.Comisiones += tasa.FeeAmount.CurrencyAmount;
                    }
                    foreach (var promocion in item.PromotionList)
                    {
                        detalle.Promociones += promocion.PromotionAmount.CurrencyAmount;
                    }
                    foreach (var impuesto in item.ItemTaxWithheldList)
                    {
                        foreach (var tax in impuesto.TaxesWithheld)
                        {
                            detalle.Importe += tax.ChargeAmount.CurrencyAmount;
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
                    CuentaContablePago = DatosMarkets.Buscar(DatosMarkets.Mercados.Single(m => m.NombreMarket == evento.MarketplaceName).Id).CuentaContablePago
                };
                foreach (var item in evento.ShipmentItemAdjustmentList)
                {
                    foreach (var cargo in item.ItemChargeAdjustmentList)
                    {
                        detalle.Importe += cargo.ChargeAmount.CurrencyAmount;
                    }
                    foreach (var tasa in item.ItemFeeAdjustmentList)
                    {
                        detalle.Comisiones += tasa.FeeAmount.CurrencyAmount;
                    }
                    foreach (var promocion in item.PromotionAdjustmentList)
                    {
                        detalle.Comisiones += promocion.PromotionAmount.CurrencyAmount;
                    }
                    foreach (var impuesto in item.ItemTaxWithheldList)
                    {
                        foreach (var tax in impuesto.TaxesWithheld)
                        {
                            detalle.Importe += tax.ChargeAmount.CurrencyAmount;
                        }
                    }
                }

                cabecera.DetallePagos.Add(detalle);
            }

            foreach (var ajuste in listaEventos.AdjustmentEventList)
            {
                if (ajuste.AdjustmentType == "ReserveDebit")
                {
                    cabecera.AjusteRetencion += ajuste.AdjustmentAmount.CurrencyAmount;
                } else if (ajuste.AdjustmentType == "ReserveCredit")
                {
                    cabecera.RestoAjustes += ajuste.AdjustmentAmount.CurrencyAmount;
                } else
                {
                    cabecera.Comision += ajuste.AdjustmentAmount.CurrencyAmount;
                }
                
            }
        }

        public static ListFinancialEventsByNextTokenResponse LeerFinancialEventsByNextToken(string nextToken)
        {
            // TODO: Set the below configuration variables before attempting to run

            // Developer AWS access key
            string accessKey = ConfigurationManager.AppSettings["AmazonAccessKey"];

            // Developer AWS secret key
            string secretKey = ConfigurationManager.AppSettings["AmazonSecretKey"];

            // The client application name
            string appName = "Nesto";

            // The client application version
            string appVersion = "1.0";

            // The endpoint for region service and version (see developer guide)
            // ex: https://mws.amazonservices.com
            string serviceURL = "https://mws-eu.amazonservices.com";

            // Create a configuration object
            MWSFinancesServiceConfig config = new MWSFinancesServiceConfig();
            config.ServiceURL = serviceURL;
            // Set other client connection configurations here if needed
            // Create the client itself
            MWSFinancesService client = new MWSFinancesServiceClient(accessKey, secretKey, appName, appVersion, config);

            MWSFinancesServiceSample sample = new MWSFinancesServiceSample(client);

            // Uncomment the operation you'd like to test here
            // TODO: Modify the request created in the Invoke method to be valid

            try
            {
                IMWSResponse response = null;
                // response = sample.InvokeListFinancialEventGroups(fechaDesde, numeroMaxPedidos);
                // response = sample.InvokeListFinancialEventGroupsByNextToken();
                //response = sample.InvokeListFinancialEvents(pagoId, numeroMaxEventos);
                response = sample.InvokeListFinancialEventsByNextToken(nextToken);
                // response = sample.InvokeGetServiceStatus();
                Console.WriteLine("Response:");
                ResponseHeaderMetadata rhmd = response.ResponseHeaderMetadata;
                // We recommend logging the request id and timestamp of every call.
                Console.WriteLine("RequestId: " + rhmd.RequestId);
                Console.WriteLine("Timestamp: " + rhmd.Timestamp);
                // string responseXml = response.ToXML();

                ListFinancialEventsByNextTokenResponse respuesta = (ListFinancialEventsByNextTokenResponse)response;

                return respuesta;
            }
            catch (MWSFinancesServiceException ex)
            {
                // Exception properties are important for diagnostics.
                ResponseHeaderMetadata rhmd = ex.ResponseHeaderMetadata;
                Console.WriteLine("Service Exception:");
                if (rhmd != null)
                {
                    Console.WriteLine("RequestId: " + rhmd.RequestId);
                    Console.WriteLine("Timestamp: " + rhmd.Timestamp);
                }
                Console.WriteLine("Message: " + ex.Message);
                Console.WriteLine("StatusCode: " + ex.StatusCode);
                Console.WriteLine("ErrorCode: " + ex.ErrorCode);
                Console.WriteLine("ErrorType: " + ex.ErrorType);
                throw ex;
            }
        }


        private readonly MWSFinancesService client;

        public MWSFinancesServiceSample(MWSFinancesService client)
        {
            this.client = client;
        }

        public ListFinancialEventGroupsResponse InvokeListFinancialEventGroups(DateTime fechaDesde, int numeroMaxGruposEventos)
        {
            // Create a request.
            ListFinancialEventGroupsRequest request = new ListFinancialEventGroupsRequest();
            string sellerId = "A302IUJ673AU08";
            request.SellerId = sellerId;
            /*
            string mwsAuthToken = "example";
            request.MWSAuthToken = mwsAuthToken;
            */
            /*
            decimal maxResultsPerPage = 1;
            request.MaxResultsPerPage = maxResultsPerPage;
            */
            request.MaxResultsPerPage = numeroMaxGruposEventos;
            /*
            DateTime financialEventGroupStartedAfter = new DateTime();
            request.FinancialEventGroupStartedAfter = financialEventGroupStartedAfter;
            */
            request.FinancialEventGroupStartedAfter = fechaDesde;
            /*
            DateTime financialEventGroupStartedBefore = new DateTime();
            request.FinancialEventGroupStartedBefore = financialEventGroupStartedBefore;
            */
            return this.client.ListFinancialEventGroups(request);
        }

        public ListFinancialEventGroupsByNextTokenResponse InvokeListFinancialEventGroupsByNextToken()
        {
            // Create a request.
            ListFinancialEventGroupsByNextTokenRequest request = new ListFinancialEventGroupsByNextTokenRequest();
            string sellerId = "example";
            request.SellerId = sellerId;
            string mwsAuthToken = "example";
            request.MWSAuthToken = mwsAuthToken;
            string nextToken = "example";
            request.NextToken = nextToken;
            return this.client.ListFinancialEventGroupsByNextToken(request);
        }

        public ListFinancialEventsResponse InvokeListFinancialEvents(string pagoId, int numeroMaxEventos)
        {
            // Create a request.
            ListFinancialEventsRequest request = new ListFinancialEventsRequest();
            string sellerId = "A302IUJ673AU08";
            request.SellerId = sellerId;
            /*
            string mwsAuthToken = "example";
            request.MWSAuthToken = mwsAuthToken;
            */
            decimal maxResultsPerPage = numeroMaxEventos;
            request.MaxResultsPerPage = maxResultsPerPage;
            /*
            string amazonOrderId = "example";
            request.AmazonOrderId = amazonOrderId;
            */
            string financialEventGroupId = pagoId;
            request.FinancialEventGroupId = financialEventGroupId;
            /*
            DateTime postedAfter = new DateTime();
            request.PostedAfter = postedAfter;
            DateTime postedBefore = new DateTime();
            request.PostedBefore = postedBefore;
            */
            return this.client.ListFinancialEvents(request);
        }

        public ListFinancialEventsByNextTokenResponse InvokeListFinancialEventsByNextToken(string nextToken)
        {
            // Create a request.
            ListFinancialEventsByNextTokenRequest request = new ListFinancialEventsByNextTokenRequest();
            string sellerId = "A302IUJ673AU08";
            request.SellerId = sellerId;
            /*
            string mwsAuthToken = "example";
            request.MWSAuthToken = mwsAuthToken;
            */
            //string nextToken = "example";
            request.NextToken = nextToken;
            return this.client.ListFinancialEventsByNextToken(request);
        }

        public GetServiceStatusResponse InvokeGetServiceStatus()
        {
            // Create a request.
            GetServiceStatusRequest request = new GetServiceStatusRequest();
            string sellerId = "example";
            request.SellerId = sellerId;
            string mwsAuthToken = "example";
            request.MWSAuthToken = mwsAuthToken;
            return this.client.GetServiceStatus(request);
        }


    }
}
