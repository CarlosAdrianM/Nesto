using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.CanalesExternos;

namespace CanalesExternosTests
{
    /// <summary>
    /// LeerDatosEnvio (Amazon): el transportista (CarrierName/ShippingMethod) y el nº de seguimiento
    /// que se mandan a Amazon salen SIEMPRE de lo que declara el servidor para la agencia del último
    /// envío (NestoAPI#258 slice a). Desde el 22/09/26 (primer día de CTT) aquí no se reconoce ninguna
    /// agencia por el enlace: una agencia nueva se da de alta en NestoAPI y punto.
    /// </summary>
    [TestClass]
    public class CanalExternoPedidosAmazonTests
    {
        [TestMethod]
        public void LeerDatosEnvio_ConDatosDelServidor_LosUsaTalCual()
        {
            var pedido = new PedidoCanalExterno
            {
                UltimoSeguimiento = "https://url-que-no-se-sabe-parsear.com/x/1",
                UltimoEnvio = new Nesto.Modulos.PedidoVenta.PedidoVentaModel.EnvioAgenciaDTO
                {
                    Numero = 249000,
                    AgenciaNombre = "CTT",
                    CarrierNameAmazon = "CTT Express",
                    ShippingMethodAmazon = "Estándar",
                    NumeroSeguimiento = "0082800082809772536836"
                }
            };

            var datos = CanalExternoPedidosAmazon.LeerDatosEnvio(pedido);

            Assert.AreEqual("CTT Express", datos.NombreAgencia);
            Assert.AreEqual("Estándar", datos.NombreServicio);
            Assert.AreEqual("0082800082809772536836", datos.NumeroSeguimiento);
        }

        [TestMethod]
        public void LeerDatosEnvio_SinEnvioTramitado_LanzaConMensajeClaro()
        {
            var pedido = new PedidoCanalExterno { UltimoSeguimiento = "https://s.correosexpress.com/c?n=1", UltimoEnvio = null };

            var ex = Assert.ThrowsException<System.InvalidOperationException>(() => CanalExternoPedidosAmazon.LeerDatosEnvio(pedido));
            StringAssert.Contains(ex.Message, "ningún envío tramitado");
        }

        [TestMethod]
        public void LeerDatosEnvio_AgenciaSinTransportistaEnElServidor_LanzaNombrandoLaAgencia()
        {
            // Regresión CTT 22/09/26 (pedido 926717): antes caía a un parseo del enlace con agencias
            // escritas a mano y soltaba "No se reconoce la agencia del enlace", sin llegar a ELMAH.
            var pedido = new PedidoCanalExterno
            {
                UltimoSeguimiento = "https://www.cttexpress.com/localizador-de-envios?sc=0082800082809772536836",
                UltimoEnvio = new Nesto.Modulos.PedidoVenta.PedidoVentaModel.EnvioAgenciaDTO { Numero = 249000, AgenciaNombre = "CTT", NumeroSeguimiento = "0082800082809772536836" }
            };

            var ex = Assert.ThrowsException<System.InvalidOperationException>(() => CanalExternoPedidosAmazon.LeerDatosEnvio(pedido));
            StringAssert.Contains(ex.Message, "CTT");
            StringAssert.Contains(ex.Message, "249000");
            StringAssert.Contains(ex.Message, "NestoAPI");
        }

        [TestMethod]
        public void LeerDatosEnvio_SinNumeroDeSeguimiento_Lanza()
        {
            var pedido = new PedidoCanalExterno
            {
                UltimoEnvio = new Nesto.Modulos.PedidoVenta.PedidoVentaModel.EnvioAgenciaDTO { Numero = 1, AgenciaNombre = "ASM", CarrierNameAmazon = "GLS", NumeroSeguimiento = " " }
            };

            Assert.ThrowsException<System.InvalidOperationException>(() => CanalExternoPedidosAmazon.LeerDatosEnvio(pedido));
        }
    }
}
