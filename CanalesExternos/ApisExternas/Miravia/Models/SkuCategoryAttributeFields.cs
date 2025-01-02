using Newtonsoft.Json;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    public class SkuCategoryAttributeFields
    {
        [JsonProperty("color_family")]
        public string ColorFamily { get; set; }

        [JsonProperty("size")]
        public string Size { get; set; }

        [JsonProperty("ean_code")]
        public string EanCode { get; set; }
        
        [JsonProperty("Manufacturer_info")]
        public string ManufacturerInfo { get; set; }

        [JsonProperty("EU_Responsible")]
        public string EUResponsible { get; set; }
    }
}
