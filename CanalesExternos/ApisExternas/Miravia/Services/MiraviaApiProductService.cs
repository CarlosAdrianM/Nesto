using Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models;
using System.Configuration;

namespace Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Services
{
    internal static class MiraviaApiProductService
    {
        public static ProductResponse CreateAndUpdate(ProductRequest request)
        {
            return new ProductResponse();
        }
        public static MiraviaConnection ConexionMiravia()
        {
            return new MiraviaConnection(new MiraviaCredential(
                ConfigurationManager.AppSettings["MiraviaAppKey"],
                ConfigurationManager.AppSettings["MiraviaAppSecret"],
                ConfigurationManager.AppSettings["MiraviaAccessToken"]
            ));
        }


    }
}
