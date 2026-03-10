using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Models;

namespace Models.Tests
{
    [TestClass]
    public class EfectoTests
    {
        #region CCC

        [TestMethod]
        public void CCC_AlAsignarNull_NoLanzaExcepcion()
        {
            var efecto = new Efecto();
            efecto.CCC = "12345678901234567890";

            efecto.CCC = null;

            Assert.IsNull(efecto.CCC);
        }

        [TestMethod]
        public void CCC_AlAsignarStringVacio_LoConvierteANull()
        {
            var efecto = new Efecto();
            efecto.CCC = "12345678901234567890";

            efecto.CCC = "";

            Assert.IsNull(efecto.CCC);
        }

        [TestMethod]
        public void CCC_AlAsignarValorValido_LoConvierteAMayusculasYRecorta()
        {
            var efecto = new Efecto();

            efecto.CCC = "  abc123  ";

            Assert.AreEqual("ABC123", efecto.CCC);
        }

        #endregion

        #region FormaPago

        [TestMethod]
        public void FormaPago_AlAsignarNull_NoLanzaExcepcion()
        {
            var efecto = new Efecto();
            efecto.FormaPago = "TAR";

            efecto.FormaPago = null;

            Assert.IsNull(efecto.FormaPago);
        }

        [TestMethod]
        public void FormaPago_AlAsignarStringVacio_LoConvierteANull()
        {
            var efecto = new Efecto();
            efecto.FormaPago = "TAR";

            efecto.FormaPago = "";

            Assert.IsNull(efecto.FormaPago);
        }

        [TestMethod]
        public void FormaPago_AlAsignarValorValido_LoConvierteAMayusculasYRecorta()
        {
            var efecto = new Efecto();

            efecto.FormaPago = "  tar  ";

            Assert.AreEqual("TAR", efecto.FormaPago);
        }

        #endregion
    }
}
