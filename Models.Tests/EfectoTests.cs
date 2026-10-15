using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Models;

namespace Models.Tests
{
    [TestClass]
    public class EfectoTests
    {
        #region Forma de pago de los efectos manuales (Magan, pedido 928499, 15/10/26)

        [TestMethod]
        public void PedidoVenta_FormaPago_LlegaALosEfectosManuales()
        {
            var pedido = new PedidoVentaDTO();
            pedido.ccc = "3";
            pedido.formaPago = "RCB";

            pedido.Efectos.AnnadirEfectoCommand.Execute(null);

            Assert.AreEqual("RCB", pedido.Efectos.FormaPagoCliente);
            Assert.AreEqual("RCB", pedido.Efectos[0].FormaPago, "El primer efecto nacía sin forma de pago y la API rechazaba el pedido");
            Assert.AreEqual("3", pedido.Efectos[0].CCC);
        }

        #endregion

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
