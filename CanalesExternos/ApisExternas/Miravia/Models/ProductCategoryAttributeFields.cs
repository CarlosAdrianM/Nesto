using Newtonsoft.Json;
using System;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    public class ProductCategoryAttributeFields
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("short_description")]
        public string ShortDescription { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("brand")]
        public string Brand { get; set; }

        [JsonProperty("warranty_type")]
        public string WarrantyType { get; set; }

        [JsonProperty("made_in")]
        public string MadeIn { get; set; }

        [JsonProperty("delivery_option_economy")]
        [JsonConverter(typeof(YesNoConverter))]
        public bool DeliveryOptionEconomy { get; set; }

        [JsonProperty("delivery_option_sof")]
        public string DeliveryOptionSof { get; set; }

        [JsonProperty("Does_this_product_have_a_safety_warning")]
        [JsonConverter(typeof(YesNoConverter))]
        public bool DoesThisProductHaveASafetyWarning { get; set; }
    }

    public class YesNoConverter : JsonConverter<bool>
    {
        public override bool ReadJson(JsonReader reader, Type objectType, bool existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            // Convierte "Yes" a true, "No" a false
            string value = reader.Value?.ToString();
            return value?.ToLower() == "yes";
        }

        public override void WriteJson(JsonWriter writer, bool value, JsonSerializer serializer)
        {
            // Convierte true a "Yes", false a "No"
            writer.WriteValue(value ? "Yes" : "No");
        }
    }
}
