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
using System.Net.Http;
using System.Xml;
using Newtonsoft.Json.Linq;
using static FikaAmazonAPI.Utils.Constants;
using System.Threading.Tasks;
using FikaAmazonAPI.AmazonSpApiSDK.Models.Token;
using FikaAmazonAPI.AmazonSpApiSDK.Services;

public class AmazonApiOrdersService
{
    // Marketplace ID de amazon.ae (Emiratos �rabes Unidos).
    internal const string MARKETPLACE_AE = "A2VIGQ35RCS4UG";

    // Nesto#441: GetOrderAddress tiene un rate limit muy bajo y las direcciones de un
    // pedido no cambian: se cachean por sesi�n para que las recargas no repitan el N+1.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Address> _cacheDirecciones = new();

    // Nesto#441: un pedido con SellerOrderId num�rico ya tiene pedido en Nesto; su direcci�n
    // solo se usa para pintar el grid, as� que no compensa el GetOrderAddress.
    internal static bool EstaRegistradoEnNesto(Order order) =>
        !string.IsNullOrWhiteSpace(order?.SellerOrderId) &&
        order.SellerOrderId != order.AmazonOrderId &&
        int.TryParse(order.SellerOrderId, out _);

    private static async Task CompletarDirecciones(AmazonConnection conexion, List<Order> orders, string consulta)
    {
        int consultadas = 0, deCache = 0, omitidas = 0;
        foreach (var order in orders)
        {
            if (EstaRegistradoEnNesto(order))
            {
                omitidas++;
                continue;
            }
            if (_cacheDirecciones.TryGetValue(order.AmazonOrderId, out Address direccionCacheada))
            {
                order.ShippingAddress = direccionCacheada;
                deCache++;
                continue;
            }
            var address = await conexion.Orders.GetOrderAddressAsync(order.AmazonOrderId).ConfigureAwait(false);
            order.ShippingAddress = address.ShippingAddress;
            if (address.ShippingAddress != null)
            {
                _cacheDirecciones[order.AmazonOrderId] = address.ShippingAddress;
            }
            consultadas++;
        }
        LogDiag($"{consulta}: direcciones -> {consultadas} consultadas a Amazon, {deCache} de cach�, {omitidas} omitidas (ya registradas en Nesto)");
    }

    // Construye la lista de MarketplaceIds para las consultas de pedidos.
    private static List<string> ConstruirMarketplaceIds()
    {
        return DatosMarkets.Mercados.Select(m => m.Id).ToList();
    }

    // Logging de diagn�stico: sale por la ventana de salida (Output) tanto en
    // Debug como en Release. Prefijo greppable [AmazonDiag].
    internal static void LogDiag(string mensaje)
    {
        System.Diagnostics.Trace.WriteLine("[AmazonDiag] " + mensaje);
    }

    private static void LogResultadoConsulta(string consulta, ParameterOrderList parametros, List<Order> orders)
    {
        LogDiag($"{consulta}: CreatedAfter={parametros.CreatedAfter:yyyy-MM-dd HH:mm} | " +
                $"MarketplaceIds=[{string.Join(",", parametros.MarketplaceIds ?? new List<string>())}] | " +
                $"total devueltos={orders?.Count ?? 0}");
        if (orders == null)
        {
            return;
        }
        foreach (var grupo in orders.GroupBy(o => o.MarketplaceId))
        {
            LogDiag($"{consulta}:   marketplace {grupo.Key} -> {grupo.Count()} pedido(s)");
        }
        var ae = orders.Where(o => o.MarketplaceId == MARKETPLACE_AE).ToList();
        if (!ae.Any())
        {
            LogDiag($"{consulta}:   *** NING�N pedido de Amazon.ae ({MARKETPLACE_AE}) en esta consulta ***");
        }
        foreach (var o in ae)
        {
            LogDiag($"{consulta}:   Amazon.ae -> {o.AmazonOrderId} | estado={o.OrderStatus} | " +
                    $"canal={o.FulfillmentChannel} | moneda={o.OrderTotal?.CurrencyCode} | " +
                    $"importe={o.OrderTotal?.Amount} | compra={o.PurchaseDate}");
        }
    }

    public static async Task<List<Order>> Ejecutar(DateTime fechaDesde, int numeroMaxPedidos)
    {

        try
        {
            List<Order> response = await InvokeListOrders(fechaDesde, numeroMaxPedidos).ConfigureAwait(false);
            // A�adimos los FBA
            List<Order> responseFBA = await InvokeListOrdersFBA(fechaDesde, numeroMaxPedidos).ConfigureAwait(false);
            response.AddRange(responseFBA);
            LogDiag($"Ejecutar: MFN={response.Count - responseFBA.Count} + FBA={responseFBA.Count} = {response.Count} pedidos totales. " +
                    $"De Amazon.ae: {response.Count(o => o.MarketplaceId == MARKETPLACE_AE)}");
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
            throw new Exception("No se han podido cargar las l�neas del pedido de Amazon", ex);
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
        searchOrderList.MarketplaceIds = ConstruirMarketplaceIds();
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
            LogResultadoConsulta("MFN (Unshipped/PartiallyShipped)", searchOrderList, orders);
            await CompletarDirecciones(conexion, orders, "MFN").ConfigureAwait(false);
            return orders;
        }
        catch (Exception ex)
        {
            throw new Exception("No se pueden descargar los pedidos de Amazon. " + DescribirCadenaError(ex), ex);
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
        searchOrderList.MarketplaceIds = ConstruirMarketplaceIds();
        List<FulfillmentChannels> fulfillmentChannel = new List<FulfillmentChannels>
        {
            FulfillmentChannels.AFN
        };
        searchOrderList.FulfillmentChannels = fulfillmentChannel;
        // Amazon limita MaxResultsPerPage a 100. El SDK pagina automáticamente,
        // así que lo dejamos sin fijar para que traiga todos los pedidos del rango.
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
            LogResultadoConsulta("FBA (Shipped/AFN)", searchOrderList, orders);
            await CompletarDirecciones(conexion, orders, "FBA").ConfigureAwait(false);
            return orders;
        }
        catch (Exception ex)
        {
            throw new Exception("No se pueden descargar los pedidos FBA de Amazon. " + DescribirCadenaError(ex), ex);
        }
    }

    private static string DescribirCadenaError(Exception ex)
    {
        var mensajes = new List<string>();
        var actual = ex;
        int profundidad = 0;
        while (actual != null && profundidad < 6)
        {
            if (!string.IsNullOrWhiteSpace(actual.Message) && !mensajes.Contains(actual.Message))
                mensajes.Add(actual.Message);
            actual = actual.InnerException;
            profundidad++;
        }
        return string.Join(" ▸ ", mensajes);
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
            throw new Exception("No se puede convertir a monedas distintas al Euro");
        }

        // El valor que manejamos es "unidades de la divisa por 1 EUR" (igual que el atributo
        // rate del BCE). Para pasar un importe en esa divisa a EUR se multiplica por 1/valor.

        // 1ª fuente: tipos de referencia del BCE. No cotiza divisas como AED (amazon.ae),
        // SAR o EGP, as� que para esos marketplaces devuelve null y usamos el fallback.
        decimal? unidadesPorEuro = ObtenerTasaBancoCentralEuropeo(monedaOrigen);

        // 2� fuente (fallback): API p�blica que s� cotiza divisas fuera del BCE.
        if (unidadesPorEuro == null || unidadesPorEuro <= 0)
        {
            unidadesPorEuro = ObtenerTasaFuenteAlternativa(monedaOrigen);
        }

        if (unidadesPorEuro == null || unidadesPorEuro <= 0)
        {
            throw new Exception($"No se ha podido calcular el cambio de {monedaOrigen} a EUR en ninguna fuente (BCE ni fallback)");
        }

        return 1M / unidadesPorEuro.Value;
    }

    private static decimal? ObtenerTasaBancoCentralEuropeo(string monedaOrigen)
    {
        try
        {
            var doc = new XmlDocument();
            doc.Load(@"http://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml");

            XmlNodeList nodes = doc.SelectNodes("//*[@currency]");
            if (nodes == null)
            {
                return null;
            }
            foreach (XmlNode node in nodes)
            {
                if (string.Equals(node.Attributes["currency"].Value, monedaOrigen, StringComparison.OrdinalIgnoreCase))
                {
                    return Decimal.Parse(node.Attributes["rate"].Value, NumberStyles.Any, new CultureInfo("en-US"));
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static decimal? ObtenerTasaFuenteAlternativa(string monedaOrigen)
    {
        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(15);
                // open.er-api.com: gratuita, sin API key, base EUR, incluye AED/SAR/EGP.
                string json = client.GetStringAsync("https://open.er-api.com/v6/latest/EUR").GetAwaiter().GetResult();
                return ExtraerTasaFuenteAlternativa(json, monedaOrigen);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parsea la respuesta de open.er-api.com y devuelve las unidades de
    /// <paramref name="monedaOrigen"/> por 1 EUR, o null si no est� disponible.
    /// P�blico para poder testear el parseo sin depender de la red.
    /// </summary>
    public static decimal? ExtraerTasaFuenteAlternativa(string json, string monedaOrigen)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        var raiz = JObject.Parse(json);
        if ((string)raiz["result"] != "success")
        {
            return null;
        }
        var rates = raiz["rates"] as JObject;
        var token = rates?[monedaOrigen.ToUpperInvariant()];
        if (token == null)
        {
            return null;
        }
        decimal valor = token.Value<decimal>();
        return valor > 0 ? valor : (decimal?)null;
    }

    public static AmazonConnection ConexionAmazon()
    {
        // NestoAPI#225: la credencial LWA (la que rota cada 180 días) se pide a la API, donde el
        // job de rotación la mantiene al día; el config queda como fallback. Las claves AWS IAM,
        // RoleArn y MerchantId no rotan y siguen viniendo del config.
        return new AmazonConnection(ConstruirCredencial(Nesto.Modulos.CanalesExternos.ApisExternas.CredencialAmazonProvider.Obtener()));
    }

    internal static AmazonCredential ConstruirCredencial(Nesto.Modulos.CanalesExternos.ApisExternas.CredencialAmazonRotada rotada)
    {
        return new AmazonCredential()
        {
            AccessKey = ConfigurationManager.AppSettings["AmazonSpApiAccessKey"],
            SecretKey = ConfigurationManager.AppSettings["AmazonSpApiSecretKey"],
            RoleArn = ConfigurationManager.AppSettings["AmazonSpApiRoleArn"],
            ClientId = string.IsNullOrEmpty(rotada?.ClientId)
                ? ConfigurationManager.AppSettings["AmazonSpApiClientId"] : rotada.ClientId,
            ClientSecret = string.IsNullOrEmpty(rotada?.ClientSecret)
                ? ConfigurationManager.AppSettings["AmazonSpApiClientSecret"] : rotada.ClientSecret,
            RefreshToken = string.IsNullOrEmpty(rotada?.RefreshToken)
                ? ConfigurationManager.AppSettings["AmazonSpApiRefreshToken"] : rotada.RefreshToken,
            SellerID = ConfigurationManager.AppSettings["AmazonSpApiMerchantId"],
            MarketPlace = MarketPlace.Spain
        };
    }

}
