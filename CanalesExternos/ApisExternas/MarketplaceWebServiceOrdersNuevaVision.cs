/*******************************************************************************
 * Copyright 2009-2017 Amazon Services. All Rights Reserved.
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 *
 * You may not use this file except in compliance with the License. 
 * You may obtain a copy of the License at: http://aws.amazon.com/apache2.0
 * This file is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
 * CONDITIONS OF ANY KIND, either express or implied. See the License for the 
 * specific language governing permissions and limitations under the License.
 *******************************************************************************
 * Marketplace Web Service Orders
 * API Version: 2013-09-01
 * Library Version: 2017-02-22
 * Generated: Thu Mar 02 12:41:05 UTC 2017
 */

using MarketplaceWebServiceOrders.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Threading.Tasks;

namespace MarketplaceWebServiceOrders {

    /// <summary>
    /// Runnable sample code to demonstrate usage of the C# client.
    ///
    /// To use, import the client source as a console application,
    /// and mark this class as the startup object. Then, replace
    /// parameters below with sensible values and run.
    /// </summary>
    public class MarketplaceWebServiceOrdersNuevaVision
    {
        public static List<Order> Ejecutar(DateTime fechaDesde, int numeroMaxPedidos)
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
            MarketplaceWebServiceOrdersConfig config = new MarketplaceWebServiceOrdersConfig();
            config.ServiceURL = serviceURL;
            // Set other client connection configurations here if needed
            // Create the client itself
            MarketplaceWebServiceOrders client = new MarketplaceWebServiceOrdersClient(accessKey, secretKey, appName, appVersion, config);

            MarketplaceWebServiceOrdersNuevaVision sample = new MarketplaceWebServiceOrdersNuevaVision(client);

            // Uncomment the operation you'd like to test here
            // TODO: Modify the request created in the Invoke method to be valid

            try 
            {
                IMWSResponse response = null;
                // response = sample.InvokeGetOrder();
                // response = sample.InvokeGetServiceStatus();
                // response = sample.InvokeListOrderItems();
                // response = sample.InvokeListOrderItemsByNextToken();
                response = sample.InvokeListOrders(fechaDesde, numeroMaxPedidos);
                // response = sample.InvokeListOrdersByNextToken();

                ListOrdersResponse respuesta = (ListOrdersResponse)response;

                // Añadimos los FBA
                response = sample.InvokeListOrdersFBA(fechaDesde, numeroMaxPedidos);
                ListOrdersResponse respuestaFBA = (ListOrdersResponse)response;
                respuesta.ListOrdersResult.Orders.AddRange(respuestaFBA.ListOrdersResult.Orders);
                return respuesta.ListOrdersResult.Orders;

                /*
                Console.WriteLine("Response:");
                ResponseHeaderMetadata rhmd = response.ResponseHeaderMetadata;
                // We recommend logging the request id and timestamp of every call.
                Console.WriteLine("RequestId: " + rhmd.RequestId);
                Console.WriteLine("Timestamp: " + rhmd.Timestamp);
                string responseXml = response.ToXML();
                Console.WriteLine(responseXml);
                */
            }
            catch (MarketplaceWebServiceOrdersException ex)
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

        public static List<OrderItem> CargarLineas(string pedidoAmazon)
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
            MarketplaceWebServiceOrdersConfig config = new MarketplaceWebServiceOrdersConfig();
            config.ServiceURL = serviceURL;
            // Set other client connection configurations here if needed
            // Create the client itself
            MarketplaceWebServiceOrders client = new MarketplaceWebServiceOrdersClient(accessKey, secretKey, appName, appVersion, config);

            MarketplaceWebServiceOrdersNuevaVision sample = new MarketplaceWebServiceOrdersNuevaVision(client);

            // Uncomment the operation you'd like to test here
            // TODO: Modify the request created in the Invoke method to be valid

            try
            {
                IMWSResponse response = null;
                // response = sample.InvokeGetOrder();
                // response = sample.InvokeGetServiceStatus();
                response = sample.InvokeListOrderItems(pedidoAmazon);
                // response = sample.InvokeListOrderItemsByNextToken();
                // response = sample.InvokeListOrders();
                // response = sample.InvokeListOrdersByNextToken();

                ListOrderItemsResponse respuesta = (ListOrderItemsResponse)response;

                return respuesta.ListOrderItemsResult.OrderItems;

                /*
                Console.WriteLine("Response:");
                ResponseHeaderMetadata rhmd = response.ResponseHeaderMetadata;
                // We recommend logging the request id and timestamp of every call.
                Console.WriteLine("RequestId: " + rhmd.RequestId);
                Console.WriteLine("Timestamp: " + rhmd.Timestamp);
                string responseXml = response.ToXML();
                Console.WriteLine(responseXml);
                */
            }
            catch (MarketplaceWebServiceOrdersException ex)
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

        private readonly MarketplaceWebServiceOrders client;

        public MarketplaceWebServiceOrdersNuevaVision(MarketplaceWebServiceOrders client)
        {
            this.client = client;
        }

        public GetOrderResponse InvokeGetOrder()
        {
            // Create a request.
            GetOrderRequest request = new GetOrderRequest();
            string sellerId = "example";
            request.SellerId = sellerId;
            string mwsAuthToken = "example";
            request.MWSAuthToken = mwsAuthToken;
            List<string> amazonOrderId = new List<string>();
            request.AmazonOrderId = amazonOrderId;
            return this.client.GetOrder(request);
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

        public ListOrderItemsResponse InvokeListOrderItems(string pedidoAmazon)
        {
            // Create a request.
            ListOrderItemsRequest request = new ListOrderItemsRequest();
            string sellerId = "A302IUJ673AU08";
            request.SellerId = sellerId;
            //string mwsAuthToken = "example";
            //request.MWSAuthToken = mwsAuthToken;
            string amazonOrderId = pedidoAmazon;
            request.AmazonOrderId = amazonOrderId;
            return this.client.ListOrderItems(request);
        }

        public ListOrderItemsByNextTokenResponse InvokeListOrderItemsByNextToken()
        {
            // Create a request.
            ListOrderItemsByNextTokenRequest request = new ListOrderItemsByNextTokenRequest();
            string sellerId = "example";
            request.SellerId = sellerId;
            string mwsAuthToken = "example";
            request.MWSAuthToken = mwsAuthToken;
            string nextToken = "example";
            request.NextToken = nextToken;
            return this.client.ListOrderItemsByNextToken(request);
        }
        /*
        public ListOrdersResponse InvokeListOrders()
        {
            DateTime fechaDesde = DateTime.Now.AddYears(-1);
            return InvokeListOrders(fechaDesde, new DateTime());
        }
        */
        public ListOrdersResponse InvokeListOrders(DateTime fechaDesde, int numeroMaxPedidos)
        {
            // Create a request.
            ListOrdersRequest request = new ListOrdersRequest();
            string sellerId = "A302IUJ673AU08";
            request.SellerId = sellerId;
            //string mwsAuthToken = "example";
            //request.MWSAuthToken = mwsAuthToken;
            DateTime createdAfter = fechaDesde;
            request.CreatedAfter = createdAfter;
            /*
            DateTime createdBefore = fechaHasta;
            request.CreatedBefore = createdBefore;
            DateTime lastUpdatedAfter = fechaDesde;
            request.LastUpdatedAfter = lastUpdatedAfter;
            DateTime lastUpdatedBefore = fechaHasta;
            request.LastUpdatedBefore = lastUpdatedBefore;
            */
            List<string> orderStatus = new List<string>();
            orderStatus.Add("Unshipped");
            orderStatus.Add("PartiallyShipped");
            request.OrderStatus = orderStatus;
            List<string> marketplaceId = new List<string>();
            marketplaceId.Add("A1F83G8C2ARO7P"); // Amazon.co.uk
            marketplaceId.Add("A1PA6795UKMFR9"); // Amazon.de
            marketplaceId.Add("A1RKKUPIHCS9HS"); // Amazon.es
            marketplaceId.Add("A13V1IB3VIYZZH"); // Amazon.fr
            marketplaceId.Add("APJ6JRA9NG5V4");  // Amazon.it
            marketplaceId.Add("A302IUJ673AU08");  // Amazon.nl
            request.MarketplaceId = marketplaceId;
            List<string> fulfillmentChannel = new List<string>();
            //request.FulfillmentChannel = fulfillmentChannel;
            List<string> paymentMethod = new List<string>();
            //request.PaymentMethod = paymentMethod;
            //string buyerEmail = "example";
            //request.BuyerEmail = buyerEmail;
            //string sellerOrderId = "example";
            //request.SellerOrderId = sellerOrderId;
            decimal maxResultsPerPage = numeroMaxPedidos;
            request.MaxResultsPerPage = maxResultsPerPage;
            List<string> tfmShipmentStatus = new List<string>();
            //request.TFMShipmentStatus = tfmShipmentStatus;
            return this.client.ListOrders(request);
        }

        public ListOrdersResponse InvokeListOrdersFBA(DateTime fechaDesde, int numeroMaxPedidos)
        {
            // Create a request.
            ListOrdersRequest request = new ListOrdersRequest();
            string sellerId = "A302IUJ673AU08";
            request.SellerId = sellerId;
            //string mwsAuthToken = "example";
            //request.MWSAuthToken = mwsAuthToken;
            DateTime createdAfter = fechaDesde;
            request.CreatedAfter = createdAfter;
            /*
            DateTime createdBefore = new DateTime();
            request.CreatedBefore = createdBefore;
            DateTime lastUpdatedAfter = new DateTime();
            request.LastUpdatedAfter = lastUpdatedAfter;
            DateTime lastUpdatedBefore = new DateTime();
            request.LastUpdatedBefore = lastUpdatedBefore;
            */
            List<string> orderStatus = new List<string>();
            orderStatus.Add("Shipped");
            request.OrderStatus = orderStatus;
            List<string> marketplaceId = new List<string>();
            marketplaceId.Add("A1F83G8C2ARO7P"); // Amazon.co.uk
            marketplaceId.Add("A1PA6795UKMFR9"); // Amazon.de
            marketplaceId.Add("A1RKKUPIHCS9HS"); // Amazon.es
            marketplaceId.Add("A13V1IB3VIYZZH"); // Amazon.fr
            marketplaceId.Add("APJ6JRA9NG5V4");  // Amazon.it
            marketplaceId.Add("A302IUJ673AU08");  // Amazon.nl
            request.MarketplaceId = marketplaceId;
            List<string> fulfillmentChannel = new List<string>();
            fulfillmentChannel.Add("AFN");
            request.FulfillmentChannel = fulfillmentChannel;
            List<string> paymentMethod = new List<string>();
            //request.PaymentMethod = paymentMethod;
            //string buyerEmail = "example";
            //request.BuyerEmail = buyerEmail;
            //string sellerOrderId = "example";
            //request.SellerOrderId = sellerOrderId;
            decimal maxResultsPerPage = numeroMaxPedidos;
            request.MaxResultsPerPage = maxResultsPerPage;
            List<string> tfmShipmentStatus = new List<string>();
            //request.TFMShipmentStatus = tfmShipmentStatus;
            return this.client.ListOrders(request);
        }

        public ListOrdersByNextTokenResponse InvokeListOrdersByNextToken()
        {
            // Create a request.
            ListOrdersByNextTokenRequest request = new ListOrdersByNextTokenRequest();
            string sellerId = "example";
            request.SellerId = sellerId;
            string mwsAuthToken = "example";
            request.MWSAuthToken = mwsAuthToken;
            string nextToken = "example";
            request.NextToken = nextToken;
            return this.client.ListOrdersByNextToken(request);
        }
                
        public static async Task<decimal> CalculaDivisa(string monedaOrigen, string monedaDestino)
        {
            string apiKey = "4894|GMDw0jJ78NXw_GdVnEADMS*XuFQ11Cw9";
            JObject obj;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://api.cambio.today/v1/quotes/");
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = string.Format("{0}/{1}/json?quantity=1&key={2}",monedaOrigen, monedaDestino, apiKey);


                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        obj = JObject.Parse(resultado);
                    }
                    else
                    {
                        throw new Exception("No se ha podido calcular el cambio del día");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return Decimal.Parse(obj["result"]["amount"].ToString());
        }
        // https://api.cambio.today/v1/quotes/GBP/EUR/json?quantity=1&key=4894|GMDw0jJ78NXw_GdVnEADMS*XuFQ11Cw9

    }
}
