using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    public class SkuData
    {
        [JsonProperty("quantity")]
        public string Quantity { get; set; }

        [JsonProperty("sku_images")]
        public List<string> SkuImages { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("seller_sku")]
        public string SellerSku { get; set; }

        [JsonProperty("package_width")]
        public string PackageWidth { get; set; }

        [JsonProperty("package_height")]
        public string PackageHeight { get; set; }

        [JsonProperty("package_length")]
        public string PackageLength { get; set; }

        [JsonProperty("package_weight")]
        public string PackageWeight { get; set; }

        [JsonProperty("sale_price")]
        public decimal SalePrice { get; set; }

        [JsonProperty("price")]
        public decimal Price { get; set; }

        [JsonProperty("sku_category_attribute_fields")]
        public SkuCategoryAttributeFields SkuCategoryAttributeFields { get; set; }
    }
}
