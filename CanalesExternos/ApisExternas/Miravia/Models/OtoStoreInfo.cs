using Newtonsoft.Json;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    internal class OtoStoreInfo
    {
        [JsonProperty("otoStoreCode")]
        public string OtoStoreCode { get; set; }
    }
}
