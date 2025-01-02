using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    public class SkuResponseData
    {
        [JsonProperty("product_id")]
        public string ProductId { get; set; }

        [JsonProperty("seller_sku")]
        public string SellerSku { get; set; }

        [JsonProperty("errors")]
        public List<ErrorDetail> Errors { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
    }
}
