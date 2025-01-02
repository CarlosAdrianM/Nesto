using Newtonsoft.Json;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    internal class OrdersResponse : MiraviaApiResponse
    {
        [JsonProperty("data")]
        public OrdersData Data { get; set; }
    }
}
