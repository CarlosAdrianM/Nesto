using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    internal class OrderItemsResponse
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("data")]
        public List<OrderItem> Data { get; set; }

        [JsonProperty("request_id")]
        public string RequestId { get; set; }
    }
}
