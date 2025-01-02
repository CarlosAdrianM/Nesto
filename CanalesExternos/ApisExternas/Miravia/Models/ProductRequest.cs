using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    public class ProductRequest
    {
        [JsonProperty("category_id")]
        public string CategoryId { get; set; }

        [JsonProperty("product_category_attribute_fields")]
        public ProductCategoryAttributeFields ProductCategoryAttributeFields { get; set; }

        [JsonProperty("default_images")]
        public List<string> DefaultImages { get; set; }

        [JsonProperty("sku_data")]
        public List<SkuData> SkuData { get; set; }
    }
}
