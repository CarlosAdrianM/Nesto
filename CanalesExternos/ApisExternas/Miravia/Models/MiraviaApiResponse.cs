using Newtonsoft.Json;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    internal abstract class MiraviaApiResponse
    {
        [JsonProperty("code")]
        public string Code { get; set; }
        [JsonProperty("request_id")]
        public string RequestId { get; set; }
        [JsonProperty("_trace_id_")]
        public string TraceId { get; set; }
    }
}
