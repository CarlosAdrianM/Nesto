using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.CanalesExternos;

namespace CanalesExternosTests
{
    /// <summary>
    /// LeerDatosEnvio (Amazon): del enlace de seguimiento del pedido deduce el transportista
    /// (CarrierName/ShippingMethod que ve el comprador) y el nº de seguimiento que se manda a Amazon.
    /// Gemelo del de Prestashop: este canal es el que se usa de verdad para Amazon.
    /// </summary>
    [TestClass]
    public class CanalExternoPedidosAmazonTests
    {
        [TestMethod]
        public void LeerDatosEnvio_Innovatrans_DevuelveInnovatransYAlbaran()
        {
            // Regresión: antes caía en el else -> NotImplementedException al confirmar un envío de Amazon
            // con seguimiento de Innovatrans (tip-sa.com). El nº va tras el prefijo fijo "028040028040".
            var datos = CanalExternoPedidosAmazon.LeerDatosEnvio(
                "https://aplicaciones.tip-sa.com/cliente/datos_env.php?id=0280400280406522393001");

            Assert.AreEqual("Innovatrans", datos.NombreAgencia);
            Assert.AreEqual("Estándar", datos.NombreServicio);
            Assert.AreEqual("6522393001", datos.NumeroSeguimiento);
        }

        [TestMethod]
        public void LeerDatosEnvio_CorreosExpress_DevuelveCexYNumero()
        {
            var datos = CanalExternoPedidosAmazon.LeerDatosEnvio(
                "https://s.correosexpress.com/c?n=12345678901234");

            Assert.AreEqual("Correos Express", datos.NombreAgencia);
            Assert.AreEqual("12345678901234", datos.NumeroSeguimiento);
        }

        [TestMethod]
        public void LeerDatosEnvio_Gls_DevuelveGlsYAlbaran()
        {
            // GLS: https://mygls.gls-spain.es/e/{albaran}/{cp} -> nº entre las dos últimas barras.
            var datos = CanalExternoPedidosAmazon.LeerDatosEnvio(
                "https://mygls.gls-spain.es/e/6119714024595/28001");

            Assert.AreEqual("GLS", datos.NombreAgencia);
            Assert.AreEqual("6119714024595", datos.NumeroSeguimiento);
        }

        [TestMethod]
        [ExpectedException(typeof(System.NotImplementedException))]
        public void LeerDatosEnvio_AgenciaNoReconocida_Lanza()
        {
            CanalExternoPedidosAmazon.LeerDatosEnvio("https://www.agencia-desconocida.com/track/123");
        }

        [TestMethod]
        [ExpectedException(typeof(System.Exception))]
        public void LeerDatosEnvio_SeguimientoVacio_Lanza()
        {
            CanalExternoPedidosAmazon.LeerDatosEnvio("");
        }

        // NestoAPI#258 slice (a): si el servidor manda los identificadores por canal del último
        // envío, se usan directamente sin parsear el enlace.

        [TestMethod]
        public void LeerDatosEnvio_ConDatosDelServidor_NoParseaElEnlace()
        {
            var pedido = new PedidoCanalExterno
            {
                UltimoSeguimiento = "https://url-que-no-se-sabe-parsear.com/x/1",
                UltimoEnvio = new Nesto.Modulos.PedidoVenta.PedidoVentaModel.EnvioAgenciaDTO
                {
                    CarrierNameAmazon = "CTT Express",
                    ShippingMethodAmazon = "24H",
                    NumeroSeguimiento = "CTT0001"
                }
            };

            var datos = CanalExternoPedidosAmazon.LeerDatosEnvio(pedido);

            Assert.AreEqual("CTT Express", datos.NombreAgencia);
            Assert.AreEqual("24H", datos.NombreServicio);
            Assert.AreEqual("CTT0001", datos.NumeroSeguimiento);
        }

        [TestMethod]
        public void LeerDatosEnvio_SinDatosDelServidor_CaeAlParseoDelEnlace()
        {
            var pedido = new PedidoCanalExterno
            {
                UltimoSeguimiento = "https://aplicaciones.tip-sa.com/cliente/datos_env.php?id=0280400280406522393001",
                UltimoEnvio = null
            };

            var datos = CanalExternoPedidosAmazon.LeerDatosEnvio(pedido);

            Assert.AreEqual("Innovatrans", datos.NombreAgencia);
            Assert.AreEqual("6522393001", datos.NumeroSeguimiento);
        }
    }
}
