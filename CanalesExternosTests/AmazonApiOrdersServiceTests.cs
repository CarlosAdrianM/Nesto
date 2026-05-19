using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CanalesExternosTests
{
    [TestClass]
    public class AmazonApiOrdersServiceTests
    {
        // Respuesta representativa de open.er-api.com (base EUR). El valor de cada
        // divisa son "unidades por 1 EUR", igual que el atributo rate del BCE.
        private const string RespuestaOk =
            "{\"result\":\"success\",\"base_code\":\"EUR\",\"rates\":{\"EUR\":1,\"USD\":1.08,\"AED\":3.97,\"GBP\":0.85}}";

        [TestMethod]
        public void ExtraerTasaFuenteAlternativa_DivisaPresente_DevuelveUnidadesPorEuro()
        {
            decimal? tasa = AmazonApiOrdersService.ExtraerTasaFuenteAlternativa(RespuestaOk, "AED");

            Assert.AreEqual(3.97M, tasa);
        }

        [TestMethod]
        public void ExtraerTasaFuenteAlternativa_DivisaPresente_EsInsensibleAMayusculas()
        {
            decimal? tasa = AmazonApiOrdersService.ExtraerTasaFuenteAlternativa(RespuestaOk, "aed");

            Assert.AreEqual(3.97M, tasa);
        }

        [TestMethod]
        public void ExtraerTasaFuenteAlternativa_DivisaNoCotizada_DevuelveNull()
        {
            decimal? tasa = AmazonApiOrdersService.ExtraerTasaFuenteAlternativa(RespuestaOk, "XYZ");

            Assert.IsNull(tasa);
        }

        [TestMethod]
        public void ExtraerTasaFuenteAlternativa_ResultadoNoSuccess_DevuelveNull()
        {
            string respuestaError = "{\"result\":\"error\",\"error-type\":\"unsupported-code\"}";

            decimal? tasa = AmazonApiOrdersService.ExtraerTasaFuenteAlternativa(respuestaError, "AED");

            Assert.IsNull(tasa);
        }

        [TestMethod]
        public void ExtraerTasaFuenteAlternativa_JsonVacio_DevuelveNull()
        {
            Assert.IsNull(AmazonApiOrdersService.ExtraerTasaFuenteAlternativa("", "AED"));
            Assert.IsNull(AmazonApiOrdersService.ExtraerTasaFuenteAlternativa("   ", "AED"));
            Assert.IsNull(AmazonApiOrdersService.ExtraerTasaFuenteAlternativa(null, "AED"));
        }

        [TestMethod]
        public void ExtraerTasaFuenteAlternativa_TasaCeroOInvalida_DevuelveNull()
        {
            string respuestaTasaCero =
                "{\"result\":\"success\",\"rates\":{\"EUR\":1,\"AED\":0}}";

            decimal? tasa = AmazonApiOrdersService.ExtraerTasaFuenteAlternativa(respuestaTasaCero, "AED");

            Assert.IsNull(tasa);
        }
    }
}
