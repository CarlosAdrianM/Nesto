using Newtonsoft.Json;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    internal class Address
    {
        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("address3")]
        public string Address3 { get; set; }

        [JsonProperty("phone")]
        public string Phone { get; set; }

        [JsonProperty("address2")]
        public string Address2 { get; set; }

        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("address1")]
        public string Address1 { get; set; }

        [JsonProperty("post_code")]
        public string PostCode { get; set; }

        [JsonProperty("phone2")]
        public string Phone2 { get; set; }

        [JsonProperty("last_name")]
        public string LastName { get; set; }

        [JsonProperty("address5")]
        public string Address5 { get; set; }

        [JsonProperty("address4")]
        public string Address4 { get; set; }

        [JsonProperty("first_name")]
        public string FirstName { get; set; }
    }
}
