using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    internal class PickUpStoreInfo
    {
        [JsonProperty("pick_up_store_address")]
        public string PickUpStoreAddress { get; set; }

        [JsonProperty("pick_up_store_name")]
        public string PickUpStoreName { get; set; }

        [JsonProperty("pick_up_store_open_hour")]
        public List<string> PickUpStoreOpenHour { get; set; }

        [JsonProperty("pick_up_store_code")]
        public string PickUpStoreCode { get; set; }
    }
}
