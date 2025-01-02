using Iop.Api;
using Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models;
using Nesto.Modulos.PedidoCompra.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Services
{
    internal static class MiraviaApiOrderService
    {
        private static readonly bool _isDebugMode = false;
        public static List<Models.Order> GetOrders(DateTime fechaDesde, int numeroMaxPedidos)
        {
            var credencial = ConexionMiravia().Credential;
            IIopClient client = new IopClient(credencial.Url, credencial.AppKey, credencial.AppSecret);
            IopRequest request = new IopRequest();
            string apiName = "/orders/get";
            if (_isDebugMode) {
                apiName = "/mock" + apiName;
                request.AddApiParameter("createdAfter", fechaDesde.ToUniversalTime().ToString("o")); // API de desarrollo
            }
            request.SetApiName(apiName);
            request.SetHttpMethod("GET");
            //request.AddApiParameter("update_before", "2018-02-10T16:00:00+08:00"); 
            request.AddApiParameter("sort_direction", "DESC");
            request.AddApiParameter("offset", "0");
            request.AddApiParameter("limt", numeroMaxPedidos.ToString());
            //request.AddApiParameter("update_after", "2024-02-10T09:00:00+08:00"); 
            request.AddApiParameter("sort_by", "updated_at");
            //request.AddApiParameter("created_before", "2018-02-10T16:00:00+08:00"); 
            request.AddApiParameter("created_after", fechaDesde.ToUniversalTime().ToString("o")); // API de producción
            //request.AddApiParameter("createdAfter", "2024-08-01T09:00:00+08:00");  // API de desarrollo (/mock)
            //request.AddApiParameter("status", "canceled");
            request.AddApiParameter("marketplace", "miravia");
            //request.AddApiParameter("buyer_id", "12233411222"); 
            //request.AddApiParameter("country", "ES"); 
            IopResponse response = client.Execute(request, credencial.AccessToken);
            if (response.IsError())
            {
                return [];
            }
            
            var llamadaOrders = JsonConvert.DeserializeObject<OrdersResponse>(response.Body);

            return llamadaOrders.Data.Orders;
        }

        public static Models.Order GetOrder(string pedidoId)
        {
            var credencial = ConexionMiravia().Credential;
            IIopClient client = new IopClient(credencial.Url, credencial.AppKey, credencial.AppSecret);
            IopRequest request = new IopRequest();
            string apiName = "/order/get";
            if (_isDebugMode)
            {
                apiName = "/mock" + apiName;
            }
            request.SetApiName(apiName);
            request.SetHttpMethod("GET");
            request.AddApiParameter("order_id", pedidoId);
            IopResponse response = client.Execute(request, credencial.AccessToken);
            if (response.IsError())
            {
                return new Models.Order();
            }

            var llamadaOrders = JsonConvert.DeserializeObject<Models.Order>(response.Body);

            return llamadaOrders;
        }

        public static List<OrderItem> CargarLineas(string pedidoId)
        {
            try
            {
                var credencial = ConexionMiravia().Credential;
                IIopClient client = new IopClient(credencial.Url, credencial.AppKey, credencial.AppSecret);
                IopRequest request = new IopRequest(); 
                request.SetApiName("/order/items/get"); 
                request.SetHttpMethod("GET"); 
                request.AddApiParameter("order_id", pedidoId);
                IopResponse response = client.Execute(request, credencial.AccessToken);
                if (response.IsError())
                {
                    return [];
                }
                var llamadaOrderItems = JsonConvert.DeserializeObject<OrderItemsResponse>(response.Body);

                return llamadaOrderItems.Data;
            }
            catch (Exception ex)
            {
                throw new Exception("No se han podido cargar las líneas del pedido de Miravia", ex);
            }
        }

        public static MiraviaConnection ConexionMiravia()
        {
            return new MiraviaConnection(new MiraviaCredential(
                ConfigurationManager.AppSettings["MiraviaAppKey"],
                ConfigurationManager.AppSettings["MiraviaAppSecret"],
                ConfigurationManager.AppSettings["MiraviaAccessToken"]
            ));
        }

        internal static async Task<string> ConfirmarPedido(PedidoCanalExterno pedido)
        {
            try
            {
                var credencial = ConexionMiravia().Credential;
                IIopClient client = new IopClient(credencial.Url, credencial.AppKey, credencial.AppSecret);
                IopRequest request = new IopRequest();
                request.SetApiName("/v2/order/fulfill");
                request.SetHttpMethod("POST");
                var payload = new
                {
                    order_id = pedido.PedidoCanalId,
                    order_item_id_list = pedido.Pedido.Lineas.Select(l => l.Producto).ToList()
                };
                string payloadJson = JsonConvert.SerializeObject(payload);
                request.AddApiParameter("payload", payloadJson);

                IopResponse response = client.Execute(request, credencial.AccessToken);
                if (response.IsError())
                {
                    return $"No se ha podido confirmar el pedido.\n{response.Message}";
                }

                // Tiene que ser OrderFullfilmentResponse
                var llamadaOrderItems = JsonConvert.DeserializeObject<OrderItemsResponse>(response.Body);

                return $"Se ha confirmado correctamente el pedido {pedido.PedidoNestoId}";
            }
            catch (Exception ex)
            {
                throw new Exception("No se ha podido confirmar el pedido de Miravia", ex);
            }
        }

        internal static string GetAccessToken()
        {
            // Cuando vuelva a caducar mirar como llamar a refresh en vez de a create (cuando de error 401 se debería hacer eso)
            //https://auth.miravia.com/apps/oauth/authorize?response_type=code&force_auth=true&redirect_uri=https://api.nuevavision.es/api/auth/miravia-callback/&client_id=508800
            string authorizationCode = "3_508800_vtPU5DlvAdelkXPocrGFDDeR855";
            var credencial = ConexionMiravia().Credential;
            IIopClient client = new IopClient(credencial.Url, credencial.AppKey, credencial.AppSecret);
            IopRequest request = new IopRequest();
            request.SetApiName("/auth/token/create");
            request.AddApiParameter("code", authorizationCode);
            IopResponse response = client.Execute(request);
            if (response.IsError())
            {
                return "Error";
            }
            return response.Body;
        }
    }
}
