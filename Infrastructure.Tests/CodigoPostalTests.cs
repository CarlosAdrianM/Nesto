using Microsoft.VisualStudio.TestTools.UnitTesting;
using CP = Nesto.Infrastructure.Shared.CodigoPostal;

namespace Infrastructure.Tests
{
    /// <summary>
    /// NestoAPI#596: réplica en Nesto de los casos de CodigoPostalTests de la API. Se teclee como se
    /// teclee («4480 670», «4480670», «4480-670»), el código postal es el mismo: «4480-670».
    /// </summary>
    [TestClass]
    public class CodigoPostalTests
    {
        [DataTestMethod]
        [DataRow("4480-670")]
        [DataRow("4480 670")]
        [DataRow("4480 670 ")] // char de la BD rellenado con blancos
        [DataRow("4480670")]
        [DataRow("  4480   670  ")]
        [DataRow("4480 - 670")]
        [DataRow("4480 670")] // espacio duro de un autocompletado
        public void Normalizar_PortugalEnCualquierFormato_DaElCanonicoConGuion(string texto)
        {
            Assert.AreEqual("4480-670", CP.Normalizar(texto, "PT"));
            Assert.AreEqual("4480-670", CP.Normalizar(texto, null), "Sin país se detecta por el formato");
            Assert.AreEqual("4480-670", CP.Normalizar(texto, "ES"), "7 cifras nunca son España");
            Assert.IsTrue(CP.EsPortugues(texto, null));
            Assert.IsTrue(CP.EsPortugues(texto, "PT"));
            Assert.IsTrue(CP.TieneFormatoPortugues(texto));
            Assert.IsTrue(CP.MismoCodigo(texto, "4480-670", null));
        }

        [TestMethod]
        public void Normalizar_PortugalSoloCuatroCifras_SeQuedaIgual()
        {
            Assert.AreEqual("4480", CP.Normalizar("4480 ", "PT"));
            Assert.IsTrue(CP.EsPortugues("4480", "PT"));
            Assert.IsTrue(CP.TieneFormatoPortugues("4480"), "Regla antigua de los perfiles sin país");
            Assert.IsFalse(CP.EsPortugues("4480", null), "Sin país, 4 cifras son ambiguas: no se da por portugués");
        }

        [DataTestMethod]
        [DataRow("8850", "ES")]
        [DataRow("8850", null)]
        [DataRow("08850", "ES")]
        [DataRow(" 08850 ", null)]
        public void Normalizar_EspanaSinElCero_LoRellena(string texto, string pais)
        {
            Assert.AreEqual("08850", CP.Normalizar(texto, pais));
            Assert.IsTrue(CP.EsEspanol(texto, pais));
        }

        [TestMethod]
        public void Normalizar_OtroPais_RecortaYMayusculas()
        {
            Assert.AreEqual("SW1A 1AA", CP.Normalizar(" sw1a 1aa ", "GB"));
            Assert.AreEqual("2000", CP.Normalizar("2000", "BE"), "4 cifras de otro país no se rellenan");
            Assert.IsFalse(CP.EsEspanol("28001", "FR"));
            Assert.IsFalse(CP.EsPortugues("4480-670", "FR"));
        }

        [TestMethod]
        public void Normalizar_NullYVacio_NoRevientan()
        {
            Assert.IsNull(CP.Normalizar(null, "PT"));
            Assert.AreEqual(string.Empty, CP.Normalizar("   ", null));
            Assert.AreEqual(string.Empty, CP.Digitos(null));
            Assert.IsFalse(CP.EsPortugues(null, null));
            Assert.IsFalse(CP.EsEspanol(null, null));
            Assert.IsFalse(CP.TieneFormatoPortugues(null));
            Assert.IsFalse(CP.MismoCodigo(null, "4480-670", null));
        }

        [TestMethod]
        public void Digitos_SoloLasCifras()
        {
            Assert.AreEqual("4480670", CP.Digitos("4480-670 "));
        }

        [DataTestMethod]
        [DataRow("351", "PT")]
        [DataRow("620", "PT")]
        [DataRow("PRT", "PT")]
        [DataRow("pt", "PT")]
        [DataRow("34", "ES")]
        [DataRow("724", "ES")]
        [DataRow(" es ", "ES")]
        [DataRow(null, "")]
        public void PaisIso_TraduceLoQueGuardaCadaTabla(string pais, string esperado)
        {
            Assert.AreEqual(esperado, CP.PaisIso(pais));
        }

        [TestMethod]
        public void MismoCodigo_DistintoCodigo_NoEsElMismo()
        {
            Assert.IsFalse(CP.MismoCodigo("4430 999", "4430-998", null));
            Assert.IsTrue(CP.MismoCodigo("4430 999", "4430-999 ", "PT"));
        }
    }
}
