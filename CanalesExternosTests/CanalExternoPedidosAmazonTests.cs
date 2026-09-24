using FikaAmazonAPI.AmazonSpApiSDK.Models.Feeds;
using FikaAmazonAPI.ConstructFeed.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.CanalesExternos;
using System.Collections.Generic;

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
        public void LeerDatosEnvio_ConCarrierCodeDelServidor_LoLlevaTalCual()
        {
            var pedido = new PedidoCanalExterno
            {
                UltimoEnvio = new Nesto.Modulos.PedidoVenta.PedidoVentaModel.EnvioAgenciaDTO
                {
                    Numero = 1, AgenciaNombre = "Correos Express", CarrierCodeAmazon = "Correos Express",
                    CarrierNameAmazon = "Correos Express", ShippingMethodAmazon = "ePaq", NumeroSeguimiento = "123"
                }
            };

            var datos = CanalExternoPedidosAmazon.LeerDatosEnvio(pedido);

            Assert.AreEqual("Correos Express", datos.CodigoAgencia);
        }

        [TestMethod]
        public void LeerDatosEnvio_SinCarrierCodeDeLaApi_UsaOtherYConservaElNombre()
        {
            // Regresión 24/09/26: los envíos de CTT no se marcaban como enviados en Amazon porque el
            // feed iba sin CarrierCode (obligatorio en España). Con una API anterior se manda "Other".
            var pedido = new PedidoCanalExterno
            {
                UltimoEnvio = new Nesto.Modulos.PedidoVenta.PedidoVentaModel.EnvioAgenciaDTO
                {
                    Numero = 249000, AgenciaNombre = "CTT", CarrierNameAmazon = "CTT Express",
                    ShippingMethodAmazon = "Estándar", NumeroSeguimiento = "0082800082809772536836"
                }
            };

            var datos = CanalExternoPedidosAmazon.LeerDatosEnvio(pedido);

            Assert.AreEqual("Other", datos.CodigoAgencia);
            Assert.AreEqual("CTT Express", datos.NombreAgencia);
        }

        [TestMethod]
        public void InterpretarResultadoFeed_DoneConErrorEnElInforme_EsRechazadoConElMensajeDeAmazon()
        {
            var informe = new ProcessingReportMessage
            {
                ProcessingSummary = new ProcessingSummary { MessagesProcessed = "1", MessagesSuccessful = "0", MessagesWithError = "1", MessagesWithWarning = "0" },
                Result = new List<Result>
                {
                    new Result { MessageID = "1", ResultCode = "Error", ResultMessageCode = "18028", ResultDescription = "The data you submitted is incomplete or invalid." }
                }
            };

            var resultado = AmazonApiOrdersService.InterpretarResultadoFeed(Feed.ProcessingStatusEnum.DONE, informe);

            Assert.AreEqual(AmazonApiOrdersService.EstadoFeedAmazon.Rechazado, resultado.Estado);
            StringAssert.Contains(resultado.Detalle, "18028");
            StringAssert.Contains(resultado.Detalle, "incomplete or invalid");
        }

        [TestMethod]
        public void InterpretarResultadoFeed_DoneSinErrores_EsConfirmado()
        {
            var informe = new ProcessingReportMessage
            {
                ProcessingSummary = new ProcessingSummary { MessagesProcessed = "1", MessagesSuccessful = "1", MessagesWithError = "0", MessagesWithWarning = "0" },
                Result = new List<Result>()
            };

            var resultado = AmazonApiOrdersService.InterpretarResultadoFeed(Feed.ProcessingStatusEnum.DONE, informe);

            Assert.AreEqual(AmazonApiOrdersService.EstadoFeedAmazon.Confirmado, resultado.Estado);
        }

        [TestMethod]
        public void InterpretarResultadoFeed_DoneConContadorDeErroresSinDetalle_EsRechazado()
        {
            var informe = new ProcessingReportMessage
            {
                ProcessingSummary = new ProcessingSummary { MessagesProcessed = "1", MessagesSuccessful = "0", MessagesWithError = "1" }
            };

            var resultado = AmazonApiOrdersService.InterpretarResultadoFeed(Feed.ProcessingStatusEnum.DONE, informe);

            Assert.AreEqual(AmazonApiOrdersService.EstadoFeedAmazon.Rechazado, resultado.Estado);
        }

        [TestMethod]
        public void InterpretarResultadoFeed_DoneSinInforme_NoSeDaPorConfirmado()
        {
            var resultado = AmazonApiOrdersService.InterpretarResultadoFeed(Feed.ProcessingStatusEnum.DONE, null);

            Assert.AreEqual(AmazonApiOrdersService.EstadoFeedAmazon.SinConfirmar, resultado.Estado);
        }

        [TestMethod]
        public void InterpretarResultadoFeed_FatalOCancelado_EsRechazado()
        {
            Assert.AreEqual(AmazonApiOrdersService.EstadoFeedAmazon.Rechazado,
                AmazonApiOrdersService.InterpretarResultadoFeed(Feed.ProcessingStatusEnum.FATAL, null).Estado);
            Assert.AreEqual(AmazonApiOrdersService.EstadoFeedAmazon.Rechazado,
                AmazonApiOrdersService.InterpretarResultadoFeed(Feed.ProcessingStatusEnum.CANCELLED, null).Estado);
        }

        [TestMethod]
        public void InterpretarResultadoFeed_TodaviaEnCola_EsSinConfirmar()
        {
            Assert.AreEqual(AmazonApiOrdersService.EstadoFeedAmazon.SinConfirmar,
                AmazonApiOrdersService.InterpretarResultadoFeed(Feed.ProcessingStatusEnum.INPROGRESS, null).Estado);
            Assert.AreEqual(AmazonApiOrdersService.EstadoFeedAmazon.SinConfirmar,
                AmazonApiOrdersService.InterpretarResultadoFeed(Feed.ProcessingStatusEnum.INQUEUE, null).Estado);
            Assert.AreEqual(AmazonApiOrdersService.EstadoFeedAmazon.SinConfirmar,
                AmazonApiOrdersService.InterpretarResultadoFeed(null, null).Estado);
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
