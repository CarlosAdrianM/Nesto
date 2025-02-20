using FikaAmazonAPI;
using FikaAmazonAPI.AmazonSpApiSDK.Models.Orders;
using FikaAmazonAPI.ConstructFeed.Messages;
using FikaAmazonAPI.ConstructFeed;
using FikaAmazonAPI.Parameter.Order;
using FikaAmazonAPI.Utils;
using Nesto.Modulos.CanalesExternos.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Xml;
using static FikaAmazonAPI.Utils.Constants;
using System.Threading.Tasks;
using FikaAmazonAPI.AmazonSpApiSDK.Models.Token;
using FikaAmazonAPI.AmazonSpApiSDK.Services;

public class AmazonApiOrdersService
{
    public static async Task<List<Order>> Ejecutar(DateTime fechaDesde, int numeroMaxPedidos)
    {

        try 
        {
            List<Order> response = await InvokeListOrders(fechaDesde, numeroMaxPedidos).ConfigureAwait(false);
            // Añadimos los FBA
            List<Order> responseFBA = await InvokeListOrdersFBA(fechaDesde, numeroMaxPedidos).ConfigureAwait(false);
            response.AddRange(responseFBA);
            return response;
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public static async Task<List<OrderItem>> CargarLineas(string pedidoAmazon)
    {
        try
        {
            List<OrderItem> response = await InvokeListOrderItems(pedidoAmazon);
            return response;
        }
        catch (Exception ex)
        {
            throw new Exception("No se han podido cargar las líneas del pedido de Amazon", ex);
        }
    }

    public static async Task<List<OrderItem>> InvokeListOrderItems(string pedidoAmazon)
    {
        var conexion = AmazonApiOrdersService.ConexionAmazon();
        if (pedidoAmazon.StartsWith("FBA "))
        {
            pedidoAmazon = pedidoAmazon.Substring(4);
        }
        var orderItems = await conexion.Orders.GetOrderItemsAsync(pedidoAmazon);
        return orderItems;
    }

    public static async Task<List<Order>> InvokeListOrders(DateTime fechaDesde, int numeroMaxPedidos)
    {
        ParameterOrderList searchOrderList = new ParameterOrderList();
        searchOrderList.CreatedAfter = fechaDesde;
        searchOrderList.OrderStatuses = new List<OrderStatuses>
        {
            OrderStatuses.Unshipped,
            OrderStatuses.PartiallyShipped
        };
        List<string> marketplaceId = new List<string>();
        foreach (var market in DatosMarkets.Mercados)
        {
            marketplaceId.Add(market.Id);
        }
        searchOrderList.MarketplaceIds = marketplaceId;
        //searchOrderList.MaxResultsPerPage = numeroMaxPedidos;
        searchOrderList.IsNeedRestrictedDataToken = false;
        searchOrderList.RestrictedDataTokenRequest = new CreateRestrictedDataTokenRequest
        {
            restrictedResources = new List<RestrictedResource> {
                new RestrictedResource {
                    method = Method.GET.ToString(),
                    path = ApiUrls.OrdersApiUrls.Orders,
                    dataElements = new List<string> {
                        "buyerInfo",
                        "shippingAddress"
                    }
                }
            }
        };

        try
        {
            var conexion = AmazonApiOrdersService.ConexionAmazon();
            var orders = await conexion.Orders.GetOrdersAsync(searchOrderList).ConfigureAwait(false);
            foreach (var orderAdresss in orders)
            {
                var address = await conexion.Orders.GetOrderAddressAsync(orderAdresss.AmazonOrderId).ConfigureAwait(false);
                orderAdresss.ShippingAddress = address.ShippingAddress;
            }
            return orders;
        }
        catch (Exception ex)
        {
            throw new Exception("No se pueden descargar los pedidos de Amazon", ex);
        }        
    }

    public static async Task<List<Order>> InvokeListOrdersFBA(DateTime fechaDesde, int numeroMaxPedidos)
    {
        ParameterOrderList searchOrderList = new ParameterOrderList();
        searchOrderList.CreatedAfter = fechaDesde;
        searchOrderList.OrderStatuses = new List<OrderStatuses>
        {
            OrderStatuses.Shipped
        };
        List<string> marketplaceId = new List<string>();
        foreach (var market in DatosMarkets.Mercados)
        {
            marketplaceId.Add(market.Id);
        }
        searchOrderList.MarketplaceIds = marketplaceId;
        List<FulfillmentChannels> fulfillmentChannel = new List<FulfillmentChannels>
        {
            FulfillmentChannels.AFN
        };
        searchOrderList.FulfillmentChannels = fulfillmentChannel;
        searchOrderList.MaxResultsPerPage = numeroMaxPedidos;
        searchOrderList.IsNeedRestrictedDataToken = false;
        searchOrderList.RestrictedDataTokenRequest = new CreateRestrictedDataTokenRequest
        {
            restrictedResources = new List<RestrictedResource> {
                new RestrictedResource {
                    method = Method.GET.ToString(),
                    path = ApiUrls.OrdersApiUrls.Orders,
                    dataElements = new List<string> {
                        "buyerInfo",
                        "shippingAddress"
                    }
                }
            }
        };



        try
        {
            var conexion = AmazonApiOrdersService.ConexionAmazon();
            var orders = await conexion.Orders.GetOrdersAsync(searchOrderList).ConfigureAwait(false);
            foreach (var orderAdresss in orders)
            {
                var address = await conexion.Orders.GetOrderAddressAsync(orderAdresss.AmazonOrderId).ConfigureAwait(false);
                orderAdresss.ShippingAddress = address.ShippingAddress;
            }
            return orders;
        }
        catch (Exception ex)
        {
            throw new Exception("No se pueden descargar los pedidos FBA de Amazon", ex);
        }
    }

    public static async Task<bool> ActualizarSellerOrderId(string amazonOrderId, int sellerOrderId)
    {
        try
        {
            var conexion = AmazonApiOrdersService.ConexionAmazon();
            ConstructFeedService createDocument = new ConstructFeedService(conexion.GetCurrentSellerID, "1.02");
            var list = new List<OrderAcknowledgementMessage>();
            list.Add(new OrderAcknowledgementMessage()
            {
                AmazonOrderID = amazonOrderId,
                MerchantOrderID = sellerOrderId.ToString(),
                StatusCode = OrderAcknowledgementStatusCode.Success
            });
            createDocument.AddOrderAcknowledgementMessage(list);
            var xml = createDocument.GetXML();

            
            var feedID = await conexion.Feed.SubmitFeedAsync(xml, FeedType.POST_ORDER_ACKNOWLEDGEMENT_DATA);
            //GetFeedDetails(feedID);
            return true;
        } catch
        {
            return false;
        }        
    }

    public static async Task<string> ConfirmarPedido(string amazonOrderId, string nombreAgencia, string nombreServicio, string numeroSeguimiento)
    {
        try
        {
            var conexion = AmazonApiOrdersService.ConexionAmazon();
            ConstructFeedService createDocument = new ConstructFeedService(conexion.GetCurrentSellerID, "1.02");
            var list = new List<OrderFulfillmentMessage>();
            list.Add(new OrderFulfillmentMessage()
            {
                AmazonOrderID = amazonOrderId,
                FulfillmentDate = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK"),
                FulfillmentData = new FulfillmentData()
                {
                    CarrierName = nombreAgencia, // "Correos Express",
                    ShippingMethod = nombreServicio, // "ePaq",
                    ShipperTrackingNumber = numeroSeguimiento// "{trackingNumber}"
                }
            }); 
            
            createDocument.AddOrderFulfillmentMessage(list);
            var xml = createDocument.GetXML();
            var feedID = await conexion.Feed.SubmitFeedAsync(xml, FeedType.POST_ORDER_FULFILLMENT_DATA);

            return $"Se ha confirmado correctamente el pedido {amazonOrderId}";
        }
        catch
        {
            return $"No se ha pedido confirmar el pedido {amazonOrderId}";
        }
    }

    public static decimal CalculaDivisa(string monedaOrigen, string monedaDestino)
    {
        if (monedaDestino != "EUR")
        {
            throw new Exception("No se puede convertir a monedas distantas al Euro");
        }
        try
        {
            List<Rate> rates = new List<Rate>();

            var doc = new XmlDocument();
            doc.Load(@"http://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml");

            XmlNodeList nodes = doc.SelectNodes("//*[@currency]");

            if (nodes != null)
            {
                foreach (XmlNode node in nodes)
                {
                    var rate = new Rate()
                    {
                        Currency = node.Attributes["currency"].Value,
                        Value = Decimal.Parse(node.Attributes["rate"].Value, NumberStyles.Any, new CultureInfo("en-Us"))
                    };
                    rates.Add(rate);
                }
            }
            Rate destino = rates.Single(r => r.Currency == monedaOrigen);
            if (destino != null)
            {
                return 1M / destino.Value;
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
    class Rate
    {
        public string Currency { get; set; }
        public decimal Value { get; set; }
    }

    public static AmazonConnection ConexionAmazon()
    {
        return new AmazonConnection(new AmazonCredential()
        {
            AccessKey = ConfigurationManager.AppSettings["AmazonSpApiAccessKey"],
            SecretKey = ConfigurationManager.AppSettings["AmazonSpApiSecretKey"],
            RoleArn = ConfigurationManager.AppSettings["AmazonSpApiRoleArn"],
            ClientId = ConfigurationManager.AppSettings["AmazonSpApiClientId"],
            ClientSecret = ConfigurationManager.AppSettings["AmazonSpApiClientSecret"],
            RefreshToken = ConfigurationManager.AppSettings["AmazonSpApiRefreshToken"],
            SellerID = ConfigurationManager.AppSettings["AmazonSpApiMerchantId"],
            MarketPlace = MarketPlace.Spain
        });
    }

}
