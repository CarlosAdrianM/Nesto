namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models
{
    internal class MiraviaCredential
    {
        public MiraviaCredential(string appKey, string appSecret, string accessToken) { 
            AppKey = appKey;
            AppSecret = appSecret;
            AccessToken = accessToken;
        }
        public string Url { get; set; } = "https://api.miravia.es/rest/";
        public string AppKey { get; set; }
        public string AppSecret { get; set; }
        public string AccessToken { get; set; }
    }
}
