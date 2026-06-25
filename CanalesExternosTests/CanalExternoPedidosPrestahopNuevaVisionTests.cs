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

        [TestMethod]
        public void LeerDatosEnvio_CorreosExpress_DevuelveTransportista105YNumero()
        {
            var datos = CanalExternoPedidosPrestashopNuevaVision.LeerDatosEnvio(
                "https://s.correosexpress.com/c?n=12345678901234");

            Assert.AreEqual("105", datos.AgenciaId);
            Assert.AreEqual("12345678901234", datos.NumeroSeguimiento);
        }

        [TestMethod]
        public void LeerDatosEnvio_Sending_DevuelveTransportista103YNumero()
        {
            var datos = CanalExternoPedidosPrestashopNuevaVision.LeerDatosEnvio(
                "https://info.sending.es/fgts/pub/locNumServ.seam?cliente=028040&localizador=987654321");

            Assert.AreEqual("103", datos.AgenciaId);
            Assert.AreEqual("987654321", datos.NumeroSeguimiento);
        }

        [TestMethod]
        public void LeerDatosEnvio_Gls_DevuelveTransportista160YAlbaran()
        {
            // GLS: https://mygls.gls-spain.es/e/{albaran}/{cp} -> transportista genérico 160
            var datos = CanalExternoPedidosPrestashopNuevaVision.LeerDatosEnvio(
                "https://mygls.gls-spain.es/e/6119714024595/28001");

            Assert.AreEqual("160", datos.AgenciaId);
            Assert.AreEqual("6119714024595", datos.NumeroSeguimiento);
        }

        [TestMethod]
        public void LeerDatosEnvio_Innovatrans_DevuelveTransportista160YAlbaran()
        {
            // Innovatrans (antes lanzaba NotImplementedException): id fijo 028040028040 + albarán.
            var datos = CanalExternoPedidosPrestashopNuevaVision.LeerDatosEnvio(
                "https://aplicaciones.tip-sa.com/cliente/datos_env.php?id=0280400280406522393001");

            Assert.AreEqual("160", datos.AgenciaId);
            Assert.AreEqual("6522393001", datos.NumeroSeguimiento);
        }

        [TestMethod]
        [ExpectedException(typeof(System.NotImplementedException))]
        public void LeerDatosEnvio_AgenciaNoReconocida_Lanza()
        {
            CanalExternoPedidosPrestashopNuevaVision.LeerDatosEnvio("https://www.agencia-desconocida.com/track/123");
        }
    }
}
