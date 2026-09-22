using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos;
using Nesto.Modulos.CanalesExternos.Interfaces;

namespace CanalesExternosTests
{
    [TestClass]
    public class CanalExternoPedidosPrestahopNuevaVisionTests
    {
        CanalExternoPedidosPrestashopNuevaVision canal;
        IConfiguracion configuracion = A.Fake<IConfiguracion>();
        IClientesPorTelefonoService clientesLookup = A.Fake<IClientesPorTelefonoService>();

        [TestInitialize]
        public void Initialize()
        {

            canal = new CanalExternoPedidosPrestashopNuevaVision(configuracion, clientesLookup);
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

        // Bizum sin prepago (07/09/26): desde que Prestashop 4.2.7 confirma por controllers/front/ipn.php, la
        // etiqueta del Bizum es "Bizum" (bizumName) en vez de "Bizum - Pago online"
        // (bizumDisplayName, el flujo viejo de okpayment.php). Sin reconocerla, el pedido entraba
        // como transferencia y SIN prepago, asi que el cobro no se contabilizaba.

        [TestMethod]
        public void ResolverCobro_BizumEtiquetaNueva_EsTarjetaPrepagadaEnLa57200013()
        {
            var cobro = CanalExternoPedidosPrestashopNuevaVision.ResolverCobro("Bizum");

            Assert.AreEqual("TAR", cobro.FormaPago);
            Assert.AreEqual("PRE", cobro.PlazosPago);
            Assert.AreEqual("57200013", cobro.CuentaPrepago);
        }

        [TestMethod]
        public void ResolverCobro_BizumEtiquetaAntigua_SigueSiendoTarjetaPrepagadaEnLa57200013()
        {
            var cobro = CanalExternoPedidosPrestashopNuevaVision.ResolverCobro("Bizum - Pago online");

            Assert.AreEqual("TAR", cobro.FormaPago);
            Assert.AreEqual("PRE", cobro.PlazosPago);
            Assert.AreEqual("57200013", cobro.CuentaPrepago);
        }

        [TestMethod]
        public void ResolverCobro_Redsys_EsTarjetaPrepagadaEnLa57200013()
        {
            var cobro = CanalExternoPedidosPrestashopNuevaVision.ResolverCobro("Pago con tarjeta Redsys");

            Assert.AreEqual("TAR", cobro.FormaPago);
            Assert.AreEqual("PRE", cobro.PlazosPago);
            Assert.AreEqual("57200013", cobro.CuentaPrepago);
        }

        [TestMethod]
        public void ResolverCobro_PayPal_TieneCuentaPropia()
        {
            var cobro = CanalExternoPedidosPrestashopNuevaVision.ResolverCobro("PayPal");

            Assert.AreEqual("TAR", cobro.FormaPago);
            Assert.AreEqual("PRE", cobro.PlazosPago);
            Assert.AreEqual("57200020", cobro.CuentaPrepago);
        }

        [TestMethod]
        public void ResolverCobro_Contrareembolso_SeCobraAlEntregarYNoLlevaPrepago()
        {
            var cobro = CanalExternoPedidosPrestashopNuevaVision.ResolverCobro("Pago contra reembolso");

            Assert.AreEqual("EFC", cobro.FormaPago);
            Assert.AreEqual("CONTADO", cobro.PlazosPago);
            Assert.IsNull(cobro.CuentaPrepago);
        }

        [TestMethod]
        public void ResolverCobro_AmazonPay_EsTransferenciaPeroConPrepago()
        {
            var cobro = CanalExternoPedidosPrestashopNuevaVision.ResolverCobro("Amazon Pay");

            Assert.AreEqual("TRN", cobro.FormaPago);
            Assert.AreEqual("PRE", cobro.PlazosPago);
            Assert.AreEqual("57200013", cobro.CuentaPrepago);
        }

        [TestMethod]
        public void ResolverCobro_FormaPagoDesconocida_EsTransferenciaSinPrepago()
        {
            var cobro = CanalExternoPedidosPrestashopNuevaVision.ResolverCobro("Un modulo de pago que aun no conocemos");

            Assert.AreEqual("TRN", cobro.FormaPago);
            Assert.AreEqual("PRE", cobro.PlazosPago);
            Assert.IsNull(cobro.CuentaPrepago);
        }

        [TestMethod]
        public void ResolverCobro_SiNoVieneFormaPago_NoRevienta()
        {
            var cobro = CanalExternoPedidosPrestashopNuevaVision.ResolverCobro(null);

            Assert.AreEqual("TRN", cobro.FormaPago);
            Assert.IsNull(cobro.CuentaPrepago);
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
                    TransportistaPrestashop = "160",
                    NumeroSeguimiento = "6522393001"
                }
            };

            var datos = CanalExternoPedidosPrestashopNuevaVision.LeerDatosEnvio(pedido);

            Assert.AreEqual("160", datos.AgenciaId);
            Assert.AreEqual("6522393001", datos.NumeroSeguimiento);
        }

        [TestMethod]
        public void LeerDatosEnvio_ConTrackingDelServidor_ElTrackingGanaAlNumero()
        {
            // NestoAPI#417: el servidor manda el tracking YA HECHO (enlace sin esquema para el
            // transportista genérico); el número pelado queda para servidores antiguos.
            var pedido = new PedidoCanalExterno
            {
                UltimoSeguimiento = "https://url-que-no-se-sabe-parsear.com/x/1",
                UltimoEnvio = new Nesto.Modulos.PedidoVenta.PedidoVentaModel.EnvioAgenciaDTO
                {
                    TransportistaPrestashop = "160",
                    NumeroSeguimiento = "61197140248079",
                    TrackingPrestashop = "mygls.gls-spain.es/e/61197140248079/31010"
                }
            };

            var datos = CanalExternoPedidosPrestashopNuevaVision.LeerDatosEnvio(pedido);

            Assert.AreEqual("160", datos.AgenciaId);
            Assert.AreEqual("mygls.gls-spain.es/e/61197140248079/31010", datos.NumeroSeguimiento);
        }

        // 22/09/26 (CTT): Nesto no conoce agencias. Sin datos del servidor no se parsea nada: se explica
        // qué agencia falta por declarar en NestoAPI y el error llega a ELMAH.

        [TestMethod]
        public void LeerDatosEnvio_SinEnvioTramitado_LanzaConMensajeClaro()
        {
            var pedido = new PedidoCanalExterno { UltimoSeguimiento = "https://s.correosexpress.com/c?n=1", UltimoEnvio = null };

            var ex = Assert.ThrowsException<System.InvalidOperationException>(() => CanalExternoPedidosPrestashopNuevaVision.LeerDatosEnvio(pedido));
            StringAssert.Contains(ex.Message, "ningún envío tramitado");
        }

        [TestMethod]
        public void LeerDatosEnvio_AgenciaSinTransportistaEnElServidor_LanzaNombrandoLaAgencia()
        {
            // Regresión CTT 22/09/26: antes caía a un parseo del enlace con agencias escritas a mano.
            var pedido = new PedidoCanalExterno
            {
                UltimoSeguimiento = "https://www.cttexpress.com/localizador-de-envios?sc=0082800082809772528297",
                UltimoEnvio = new Nesto.Modulos.PedidoVenta.PedidoVentaModel.EnvioAgenciaDTO { Numero = 248987, AgenciaNombre = "CTT", NumeroSeguimiento = "0082800082809772528297" }
            };

            var ex = Assert.ThrowsException<System.InvalidOperationException>(() => CanalExternoPedidosPrestashopNuevaVision.LeerDatosEnvio(pedido));
            StringAssert.Contains(ex.Message, "CTT");
            StringAssert.Contains(ex.Message, "248987");
            StringAssert.Contains(ex.Message, "NestoAPI");
        }
    }
}
