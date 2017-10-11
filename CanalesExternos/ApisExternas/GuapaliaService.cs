using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.ApisExternas
{
    public class GuapaliaService
    {
        internal Task CargarPedidoAsync(object urlPedido)
        {
            throw new NotImplementedException();
        }

        internal async Task<List<GuapaliaOrder>> CargarListaPedidosAsync()
        {
            string urlHost = "https://www.guapalia.com/api/orders/list?apikey=";

            var listaPedidosSalida = new List<GuapaliaOrder>();
            
            try
            {
                urlHost += ConfigurationManager.AppSettings["GuapaliaApikey"];
            }
            catch (Exception ex)
            {
                throw new Exception("No se puede leer la Apikey de Guapalia", ex);
            }

            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(urlHost))
            using (HttpContent content = response.Content)
            {
                try
                {
                    string resultado = await content.ReadAsStringAsync();
                    List<GuapaliaOrder> listaPedidosEntrada = JsonConvert.DeserializeObject<List<GuapaliaOrder>>(resultado);

                    foreach (var pedido in listaPedidosEntrada)
                    {
                        listaPedidosSalida.Add(pedido);
                    }

                }
                catch (Exception ex)
                {
                    throw ex;
                }

                return listaPedidosSalida;
            }
        }
    }
}
