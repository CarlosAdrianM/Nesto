using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos;

namespace CanalesExternosTests
{
    [TestClass]
    public class CanalExternoPedidosPrestahopNuevaVisionTests
    {
        CanalExternoPedidosPrestashopNuevaVision canal;
        IConfiguracion configuracion = A.Fake<IConfiguracion>();

        [TestInitialize]
        public void Initialize()
        {
            
            canal = new CanalExternoPedidosPrestashopNuevaVision(configuracion);
        }

        [TestMethod]
        public void LimpiarDni_SiEsNulo_DevolvemosCadenaEnBlanco()
        {
            string dniDevuelto = canal.LimpiarDni(null);

            Assert.AreEqual("", dniDevuelto);
        }

        [TestMethod]
        public void LimpiarDni_SiEsCadenaEnBlanco_DevolvemosCadenaEnBlanco()
        {
            string dniDevuelto = canal.LimpiarDni("  ");

            Assert.AreEqual("", dniDevuelto);
        }

        [TestMethod]
        public void LimpiarDni_SiEmpiezaPorUnCero_LoQuitamos()
        {
            string dniDevuelto = canal.LimpiarDni("012345V");

            Assert.AreEqual("12345V", dniDevuelto);
        }

        [TestMethod]
        public void LimpiarDni_SiTieneUnGuion_LoQuitamos()
        {
            string dniDevuelto = canal.LimpiarDni("1234500-V");

            Assert.AreEqual("1234500V", dniDevuelto);
        }

        [TestMethod]
        public void LimpiarDni_SiTieneUnaBarra_LaQuitamos()
        {
            string dniDevuelto = canal.LimpiarDni("B/123456789");

            Assert.AreEqual("B123456789", dniDevuelto);
        }
    }
}
