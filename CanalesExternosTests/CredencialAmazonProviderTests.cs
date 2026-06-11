using FikaAmazonAPI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.CanalesExternos.ApisExternas;
using System;
using System.Configuration;

namespace CanalesExternosTests
{
    /// <summary>
    /// NestoAPI#225 (cierre del bucle): la credencial LWA se pide a la API (donde la mantiene el
    /// job de rotación) y el config queda como fallback. Si esto falla en silencio, la integración
    /// Amazon muere ~7 días después de la primera rotación automática (~nov 2026).
    /// </summary>
    [TestClass]
    public class CredencialAmazonProviderTests
    {
        [TestMethod]
        public void ConstruirCredencial_ConCredencialRotada_UsaLosValoresDeLaApi()
        {
            var rotada = new CredencialAmazonRotada
            {
                ClientId = "client-id-api",
                ClientSecret = "secreto-rotado",
                RefreshToken = "refresh-api",
                SecretExpiry = new DateTime(2026, 12, 7)
            };

            AmazonCredential credencial = AmazonApiOrdersService.ConstruirCredencial(rotada);

            Assert.AreEqual("client-id-api", credencial.ClientId);
            Assert.AreEqual("secreto-rotado", credencial.ClientSecret);
            Assert.AreEqual("refresh-api", credencial.RefreshToken);
        }

        [TestMethod]
        public void ConstruirCredencial_SinCredencialRotada_CaeAlConfig()
        {
            AmazonCredential credencial = AmazonApiOrdersService.ConstruirCredencial(null);

            Assert.AreEqual(ConfigurationManager.AppSettings["AmazonSpApiClientId"], credencial.ClientId);
            Assert.AreEqual(ConfigurationManager.AppSettings["AmazonSpApiClientSecret"], credencial.ClientSecret);
            Assert.AreEqual(ConfigurationManager.AppSettings["AmazonSpApiRefreshToken"], credencial.RefreshToken);
        }

        [TestMethod]
        public void ConstruirCredencial_RotadaSinRefreshToken_CaeAlConfigSoloEnEseCampo()
        {
            var rotada = new CredencialAmazonRotada
            {
                ClientId = "client-id-api",
                ClientSecret = "secreto-rotado",
                RefreshToken = null
            };

            AmazonCredential credencial = AmazonApiOrdersService.ConstruirCredencial(rotada);

            Assert.AreEqual("secreto-rotado", credencial.ClientSecret);
            Assert.AreEqual(ConfigurationManager.AppSettings["AmazonSpApiRefreshToken"], credencial.RefreshToken,
                "Un campo vacío en la credencial de la API no debe pisar el del config");
        }

        [TestMethod]
        public void ConstruirCredencial_LasClavesAwsSiempreVienenDelConfig()
        {
            // Las claves IAM, RoleArn y MerchantId no rotan: la API no las sirve.
            var rotada = new CredencialAmazonRotada { ClientId = "x", ClientSecret = "y", RefreshToken = "z" };

            AmazonCredential credencial = AmazonApiOrdersService.ConstruirCredencial(rotada);

            Assert.AreEqual(ConfigurationManager.AppSettings["AmazonSpApiAccessKey"], credencial.AccessKey);
            Assert.AreEqual(ConfigurationManager.AppSettings["AmazonSpApiSecretKey"], credencial.SecretKey);
            Assert.AreEqual(ConfigurationManager.AppSettings["AmazonSpApiMerchantId"], credencial.SellerID);
        }

        [TestMethod]
        public void Obtener_SinContenedorDisponible_DevuelveNullSinExcepcion()
        {
            // En tests no hay ContainerLocator configurado: el provider debe tragarse el error
            // y devolver null para que el llamante caiga al config (nunca tirar la descarga).
            CredencialAmazonProvider.ReiniciarParaTests();

            CredencialAmazonRotada resultado = CredencialAmazonProvider.Obtener();

            Assert.IsNull(resultado);
        }
    }
}
