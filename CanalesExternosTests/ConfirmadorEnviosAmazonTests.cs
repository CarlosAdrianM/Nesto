using FakeItEasy;
using FikaAmazonAPI.AmazonSpApiSDK.Models.Feeds;
using FikaAmazonAPI.ConstructFeed.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.CanalesExternos.ApisExternas;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CanalesExternosTests
{
    /// <summary>
    /// Nesto#499: desde la 1.10.31.0 confirmar un envío de Amazon tardaba un minuto porque se esperaba
    /// al procesamiento del feed. Ahora vuelve en cuanto Amazon acepta el feed y el resultado se
    /// comprueba en segundo plano: solo se avisa si ha ido mal.
    /// </summary>
    [TestClass]
    public class ConfirmadorEnviosAmazonTests
    {
        private IClienteFeedsAmazon _cliente;
        private IAvisoConfirmacionAmazon _aviso;
        private List<TimeSpan> _esperas;

        [TestInitialize]
        public void Inicializar()
        {
            _cliente = A.Fake<IClienteFeedsAmazon>();
            _aviso = A.Fake<IAvisoConfirmacionAmazon>();
            _esperas = new List<TimeSpan>();
            A.CallTo(() => _cliente.EnviarConfirmacionEnvio(A<ConfirmacionEnvioAmazon>._)).Returns(Task.FromResult("FEED1"));
        }

        private ConfirmadorEnviosAmazon CrearConEsperaInstantanea()
            => new ConfirmadorEnviosAmazon(() => _cliente, _aviso, t => { _esperas.Add(t); return Task.CompletedTask; });

        private static ConfirmacionEnvioAmazon Envio() => new ConfirmacionEnvioAmazon
        {
            AmazonOrderId = "405-1234567-7654321",
            PedidoNesto = 912345,
            CodigoAgencia = "Other",
            NombreAgencia = "CTT Express",
            NombreServicio = "Estándar",
            NumeroSeguimiento = "0082800082809772536836"
        };

        private static Feed FeedTerminado(Feed.ProcessingStatusEnum estado, string documento = "DOC1")
            => new Feed { FeedId = "FEED1", ProcessingStatus = estado, ResultFeedDocumentId = documento };

        [TestMethod]
        public async Task Confirmar_VuelveEnCuantoAmazonAceptaElFeed_SinEsperarAlProcesamiento()
        {
            var nuncaTermina = new TaskCompletionSource<bool>();
            var confirmador = new ConfirmadorEnviosAmazon(() => _cliente, _aviso, _ => nuncaTermina.Task);

            Task<string> confirmacion = confirmador.Confirmar(Envio());

            Assert.IsTrue(confirmacion.IsCompleted, "La confirmación no debe esperar a que Amazon procese el feed");
            string resultado = await confirmacion;
            StringAssert.Contains(resultado, "405-1234567-7654321");
            StringAssert.Contains(resultado, "te avisaremos");
            Assert.IsFalse(confirmador.UltimaVerificacion.IsCompleted, "La verificación sigue en segundo plano");
            A.CallTo(() => _cliente.EnviarConfirmacionEnvio(A<ConfirmacionEnvioAmazon>.That.Matches(e => e.CodigoAgencia == "Other"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => _cliente.LeerFeed(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Confirmar_SiAmazonNoAceptaElFeed_LanzaYNoVerificaNada()
        {
            A.CallTo(() => _cliente.EnviarConfirmacionEnvio(A<ConfirmacionEnvioAmazon>._)).Throws(new Exception("InvalidInput"));
            var confirmador = CrearConEsperaInstantanea();

            var ex = await Assert.ThrowsExceptionAsync<Exception>(() => confirmador.Confirmar(Envio()));

            StringAssert.Contains(ex.Message, "405-1234567-7654321");
            StringAssert.Contains(ex.Message, "InvalidInput");
            A.CallTo(() => _cliente.LeerFeed(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _aviso.Avisar(A<string>._, A<string>._, A<ConfirmacionEnvioAmazon>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Verificacion_SiElInformeTraeErrores_AvisaConElPedidoYElMotivoDeAmazon()
        {
            A.CallTo(() => _cliente.LeerFeed("FEED1")).ReturnsNextFromSequence(
                FeedTerminado(Feed.ProcessingStatusEnum.INPROGRESS),
                FeedTerminado(Feed.ProcessingStatusEnum.DONE));
            A.CallTo(() => _cliente.LeerInforme("DOC1")).Returns(Task.FromResult(new ProcessingReportMessage
            {
                ProcessingSummary = new ProcessingSummary { MessagesProcessed = "1", MessagesSuccessful = "0", MessagesWithError = "1" },
                Result = new List<Result>
                {
                    new Result { MessageID = "1", ResultCode = "Error", ResultMessageCode = "18028", ResultDescription = "The data you submitted is incomplete or invalid." }
                }
            }));
            var confirmador = CrearConEsperaInstantanea();

            await confirmador.Confirmar(Envio());
            await confirmador.UltimaVerificacion;

            A.CallTo(() => _aviso.Avisar(
                ConfirmadorEnviosAmazon.TITULO_RECHAZADA,
                A<string>.That.Matches(m => m.Contains("405-1234567-7654321") && m.Contains("912345") && m.Contains("18028")
                    && m.Contains("incomplete or invalid") && m.Contains("Seller Central")),
                A<ConfirmacionEnvioAmazon>._,
                "FEED1")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Verificacion_SiAmazonLoProcesaBien_NoAvisaDeNada()
        {
            A.CallTo(() => _cliente.LeerFeed("FEED1")).Returns(Task.FromResult(FeedTerminado(Feed.ProcessingStatusEnum.DONE)));
            A.CallTo(() => _cliente.LeerInforme("DOC1")).Returns(Task.FromResult(new ProcessingReportMessage
            {
                ProcessingSummary = new ProcessingSummary { MessagesProcessed = "1", MessagesSuccessful = "1", MessagesWithError = "0" },
                Result = new List<Result>()
            }));
            var confirmador = CrearConEsperaInstantanea();

            await confirmador.Confirmar(Envio());
            await confirmador.UltimaVerificacion;

            A.CallTo(() => _aviso.Avisar(A<string>._, A<string>._, A<ConfirmacionEnvioAmazon>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Verificacion_SiAmazonNoTerminaEnElTope_AvisaQueHayQueRevisarloYNoSigueSondeando()
        {
            A.CallTo(() => _cliente.LeerFeed("FEED1")).Returns(Task.FromResult(FeedTerminado(Feed.ProcessingStatusEnum.INPROGRESS, null)));
            var confirmador = CrearConEsperaInstantanea();

            await confirmador.Confirmar(Envio());
            await confirmador.UltimaVerificacion;

            A.CallTo(() => _aviso.Avisar(ConfirmadorEnviosAmazon.TITULO_SIN_COMPROBAR,
                A<string>.That.Matches(m => m.Contains("405-1234567-7654321") && m.Contains("Seller Central")),
                A<ConfirmacionEnvioAmazon>._, "FEED1")).MustHaveHappenedOnceExactly();
            TimeSpan total = TimeSpan.Zero;
            _esperas.ForEach(e => total += e);
            Assert.IsTrue(total >= ConfirmadorEnviosAmazon.ESPERA_MAXIMA && total < ConfirmadorEnviosAmazon.ESPERA_MAXIMA + TimeSpan.FromMinutes(1),
                $"Debe sondear hasta el tope de {ConfirmadorEnviosAmazon.ESPERA_MAXIMA} y parar (ha esperado {total})");
            Assert.IsTrue(_esperas[0] < _esperas[_esperas.Count - 1], "El intervalo entre lecturas crece");
        }

        [TestMethod]
        public async Task Verificacion_UnFalloPuntualAlLeerElFeed_SeReintentaYNoAvisaSiLuegoVaBien()
        {
            A.CallTo(() => _cliente.LeerFeed("FEED1")).Throws(new Exception("QuotaExceeded")).Once()
                .Then.Returns(Task.FromResult(FeedTerminado(Feed.ProcessingStatusEnum.DONE)));
            A.CallTo(() => _cliente.LeerInforme("DOC1")).Returns(Task.FromResult(new ProcessingReportMessage
            {
                ProcessingSummary = new ProcessingSummary { MessagesProcessed = "1", MessagesSuccessful = "1", MessagesWithError = "0" }
            }));
            var confirmador = CrearConEsperaInstantanea();

            await confirmador.Confirmar(Envio());
            await confirmador.UltimaVerificacion;

            A.CallTo(() => _cliente.LeerFeed("FEED1")).MustHaveHappenedTwiceExactly();
            A.CallTo(() => _aviso.Avisar(A<string>._, A<string>._, A<ConfirmacionEnvioAmazon>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Verificacion_SiElAvisoFalla_NoRevientaLaTareaEnSegundoPlano()
        {
            A.CallTo(() => _cliente.LeerFeed("FEED1")).Returns(Task.FromResult(FeedTerminado(Feed.ProcessingStatusEnum.FATAL, null)));
            A.CallTo(() => _aviso.Avisar(A<string>._, A<string>._, A<ConfirmacionEnvioAmazon>._, A<string>._)).Throws(new InvalidOperationException("sin UI"));
            var confirmador = CrearConEsperaInstantanea();

            await confirmador.Confirmar(Envio());
            await confirmador.UltimaVerificacion;

            Assert.IsFalse(confirmador.UltimaVerificacion.IsFaulted);
        }

        [TestMethod]
        public async Task Confirmar_VariosEnviosSeguidos_CadaUnoConSuVerificacionSinEsperarALosDemas()
        {
            var esperas = new List<TaskCompletionSource<bool>>();
            var confirmador = new ConfirmadorEnviosAmazon(() => _cliente, _aviso, _ =>
            {
                var tcs = new TaskCompletionSource<bool>();
                esperas.Add(tcs);
                return tcs.Task;
            });

            Task<string> primera = confirmador.Confirmar(Envio());
            Task verificacionPrimera = confirmador.UltimaVerificacion;
            Task<string> segunda = confirmador.Confirmar(Envio());

            Assert.IsTrue(primera.IsCompleted && segunda.IsCompleted);
            Assert.AreNotSame(verificacionPrimera, confirmador.UltimaVerificacion);
            Assert.AreEqual(2, esperas.Count, "Cada confirmación lleva su propia verificación");
            await Task.WhenAll(primera, segunda);
        }

        [TestMethod]
        public void EsperaAntesDelIntento_CreceHastaUnMinuto()
        {
            Assert.AreEqual(TimeSpan.FromSeconds(10), ConfirmadorEnviosAmazon.EsperaAntesDelIntento(0));
            Assert.IsTrue(ConfirmadorEnviosAmazon.EsperaAntesDelIntento(1) > ConfirmadorEnviosAmazon.EsperaAntesDelIntento(0));
            Assert.AreEqual(TimeSpan.FromMinutes(1), ConfirmadorEnviosAmazon.EsperaAntesDelIntento(50));
        }
    }
}
