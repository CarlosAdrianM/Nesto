using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    public class ProductResponse
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("category_id")]
        public string CategoryId { get; set; }

        [JsonProperty("product_id")]
        public string ProductId { get; set; }

        [JsonProperty("request_id")]
        public string RequestId { get; set; }

        [JsonProperty("errors")]
        public List<ErrorDetail> Errors { get; set; }

        [JsonProperty("sku_data")]
        public List<SkuResponseData> SkuData { get; set; }

        [JsonProperty("category_path_id_map")]
        public Dictionary<string, string> CategoryPathIdMap { get; set; }
    }
}
