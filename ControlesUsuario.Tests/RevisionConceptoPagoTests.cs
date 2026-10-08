using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json.Linq;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// NestoAPI#609: antes de crear un enlace de pago se propone la corrección del concepto; el usuario la
    /// acepta o deja el suyo, y si la API falla (o aún no tiene el endpoint) se sigue con lo escrito.
    /// </summary>
    [TestClass]
    public class RevisionConceptoPagoTests
    {
        private const string ESCRITO = "Primer pago curso micronileng Yolanda";
        private const string PROPUESTO = "Primer pago curso Microneedling Yolanda";

        private static RevisionConcepto ConCambios() => new RevisionConcepto
        {
            Original = ESCRITO,
            Propuesto = PROPUESTO,
            HayCambios = true,
            Cambios = new List<CambioConcepto> { new CambioConcepto { De = "micronileng", A = "Microneedling" } }
        };

        #region Revisor (el paso previo a crear el enlace)

        private IServicioRevisionConcepto servicio;
        private IServicioDialogos dialogos;
        private RevisorConceptoPago revisor;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<IServicioRevisionConcepto>();
            dialogos = A.Fake<IServicioDialogos>();
            revisor = new RevisorConceptoPago(servicio, dialogos);
        }

        private void ElUsuarioPulsa(ResultadoBoton boton, string concepto = null)
        {
            var parametros = new ParametrosDialogo();
            if (concepto != null)
            {
                parametros.Add(RevisionConceptoDialogViewModel.PARAMETRO_CONCEPTO, concepto);
            }
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._))
                .Returns(Task.FromResult(new ResultadoDialogo(boton, parametros)));
        }

        [TestMethod]
        public async Task ElegirConcepto_ConCambiosYElUsuarioLosAcepta_DevuelveElCorregido()
        {
            A.CallTo(() => servicio.Revisar(ESCRITO, "1", "15191")).Returns(Task.FromResult(ConCambios()));
            ElUsuarioPulsa(ResultadoBoton.OK, PROPUESTO);

            string concepto = await revisor.ElegirConcepto(ESCRITO, "1", "15191");

            Assert.AreEqual(PROPUESTO, concepto);
            A.CallTo(() => dialogos.ShowDialogAsync(RevisionConceptoDialogViewModel.NOMBRE,
                A<ParametrosDialogo>.That.Matches(p => p.GetValue<RevisionConcepto>(RevisionConceptoDialogViewModel.PARAMETRO_REVISION).Propuesto == PROPUESTO
                    && p.GetValue<string>(RevisionConceptoDialogViewModel.PARAMETRO_ORIGINAL) == ESCRITO)))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ElegirConcepto_ConCambiosYElUsuarioDejaElSuyo_DevuelveElEscrito()
        {
            A.CallTo(() => servicio.Revisar(A<string>._, A<string>._, A<string>._)).Returns(Task.FromResult(ConCambios()));
            ElUsuarioPulsa(ResultadoBoton.Cancel);

            Assert.AreEqual(ESCRITO, await revisor.ElegirConcepto(ESCRITO, "1", "15191"));
        }

        [TestMethod]
        public async Task ElegirConcepto_ConCambiosYCierraLaVentana_DevuelveElEscrito()
        {
            A.CallTo(() => servicio.Revisar(A<string>._, A<string>._, A<string>._)).Returns(Task.FromResult(ConCambios()));
            ElUsuarioPulsa(ResultadoBoton.None);

            Assert.AreEqual(ESCRITO, await revisor.ElegirConcepto(ESCRITO, "1", "15191"));
        }

        [TestMethod]
        public async Task ElegirConcepto_SinCambios_SigueSinPreguntar()
        {
            A.CallTo(() => servicio.Revisar(A<string>._, A<string>._, A<string>._)).Returns(Task.FromResult(new RevisionConcepto
            {
                Original = "Masterclass PDRN 21/10",
                Propuesto = "Masterclass PDRN 21/10",
                HayCambios = false
            }));

            Assert.AreEqual("Masterclass PDRN 21/10", await revisor.ElegirConcepto("Masterclass PDRN 21/10", "1", "15191"));
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ElegirConcepto_ApiFallaOSinEndpoint_SigueConLoEscritoSinAvisar()
        {
            A.CallTo(() => servicio.Revisar(A<string>._, A<string>._, A<string>._)).Returns(Task.FromResult<RevisionConcepto>(null));

            Assert.AreEqual(ESCRITO, await revisor.ElegirConcepto(ESCRITO, "1", "15191"));
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ElegirConcepto_ElServicioLanza_SigueConLoEscrito()
        {
            A.CallTo(() => servicio.Revisar(A<string>._, A<string>._, A<string>._)).ThrowsAsync(new HttpRequestException("caída"));

            Assert.AreEqual(ESCRITO, await revisor.ElegirConcepto(ESCRITO, "1", "15191"));
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ElegirConcepto_Vacio_NoPreguntaALaApi()
        {
            Assert.AreEqual("", await revisor.ElegirConcepto("", "1", "15191"));
            A.CallTo(() => servicio.Revisar(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        #endregion

        #region Servicio (POST api/Pagos/RevisarConcepto)

        private sealed class HandlerFalso : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
            public string Url { get; private set; }
            public string Cuerpo { get; private set; }

            public HandlerFalso(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) => _responder = responder;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Url = request.RequestUri.PathAndQuery;
                Cuerpo = request.Content == null ? null : await request.Content.ReadAsStringAsync();
                return await _responder(request, cancellationToken);
            }
        }

        private static ServicioRevisionConcepto Servicio(HandlerFalso handler, TimeSpan? tiempoMaximo = null)
        {
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() =>
                new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.local/api/") });
            return new ServicioRevisionConcepto(factoria, tiempoMaximo ?? TimeSpan.FromSeconds(4));
        }

        private static Task<HttpResponseMessage> Json(string json, HttpStatusCode codigo = HttpStatusCode.OK)
            => Task.FromResult(new HttpResponseMessage(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

        [TestMethod]
        public async Task Revisar_ApiContesta_DevuelveLaRevisionYMandaElContrato()
        {
            var handler = new HandlerFalso((_, __) => Json(
                "{\"Original\":\"" + ESCRITO + "\",\"Propuesto\":\"" + PROPUESTO + "\",\"HayCambios\":true," +
                "\"Cambios\":[{\"De\":\"micronileng\",\"A\":\"Microneedling\"}]}"));

            RevisionConcepto revision = await Servicio(handler).Revisar(ESCRITO, "1  ", "15191 ");

            Assert.AreEqual("/api/Pagos/RevisarConcepto", handler.Url);
            JObject cuerpo = JObject.Parse(handler.Cuerpo);
            Assert.AreEqual(ESCRITO, (string)cuerpo["Concepto"]);
            Assert.AreEqual("1", (string)cuerpo["Empresa"]);
            Assert.AreEqual("15191", (string)cuerpo["Cliente"]);
            Assert.IsTrue(revision.HayCambios);
            Assert.AreEqual(PROPUESTO, revision.Propuesto);
            Assert.AreEqual("micronileng", revision.Cambios.Single().De);
            Assert.AreEqual("Microneedling", revision.Cambios.Single().A);
        }

        [TestMethod]
        public async Task Revisar_ApiSinPublicar404_DevuelveNull()
        {
            var handler = new HandlerFalso((_, __) => Json("{\"Message\":\"No HTTP resource\"}", HttpStatusCode.NotFound));

            Assert.IsNull(await Servicio(handler).Revisar(ESCRITO, "1", "15191"));
        }

        [TestMethod]
        public async Task Revisar_ErrorDelServidor_DevuelveNull()
        {
            var handler = new HandlerFalso((_, __) => Json("{}", HttpStatusCode.InternalServerError));

            Assert.IsNull(await Servicio(handler).Revisar(ESCRITO, "1", "15191"));
        }

        [TestMethod]
        public async Task Revisar_LaIaTardaMasDelTope_DevuelveNull()
        {
            var handler = new HandlerFalso(async (_, token) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            Assert.IsNull(await Servicio(handler, TimeSpan.FromMilliseconds(50)).Revisar(ESCRITO, "1", "15191"));
        }

        [TestMethod]
        public void TiempoMaximo_PorDefecto_CuatroSegundos()
        {
            Assert.AreEqual(TimeSpan.FromSeconds(4), ServicioRevisionConcepto.TIEMPO_MAXIMO);
        }

        #endregion

        #region Diálogo «¿Quisiste decir…?»

        private static RevisionConceptoDialogViewModel Dialogo(out List<IDialogResult> resultados)
        {
            var vm = new RevisionConceptoDialogViewModel();
            var lista = new List<IDialogResult>();
            vm.RequestClose += lista.Add;
            vm.OnDialogOpened(new DialogParameters
            {
                { RevisionConceptoDialogViewModel.PARAMETRO_ORIGINAL, ESCRITO },
                { RevisionConceptoDialogViewModel.PARAMETRO_REVISION, ConCambios() }
            });
            resultados = lista;
            return vm;
        }

        [TestMethod]
        public void Dialogo_EnsenaLoEscritoYElPropuestoConLosCambiosResaltados()
        {
            RevisionConceptoDialogViewModel vm = Dialogo(out _);

            Assert.AreEqual(ESCRITO, vm.Original);
            Assert.AreEqual(PROPUESTO, vm.Propuesto);
            Assert.AreEqual("Microneedling", vm.Tramos.Single(t => t.EsCambio).Texto);
            Assert.AreEqual(PROPUESTO, string.Concat(vm.Tramos.Select(t => t.Texto)));
            Assert.AreEqual("«micronileng» → «Microneedling»", vm.TextoCambios);
        }

        [TestMethod]
        public void Dialogo_UsarLaCorreccion_CierraConOkYElPropuesto()
        {
            RevisionConceptoDialogViewModel vm = Dialogo(out List<IDialogResult> resultados);

            vm.UsarCorreccionCommand.Execute(null);

            Assert.AreEqual(ButtonResult.OK, resultados.Single().Result);
            Assert.AreEqual(PROPUESTO, resultados.Single().Parameters.GetValue<string>(RevisionConceptoDialogViewModel.PARAMETRO_CONCEPTO));
        }

        [TestMethod]
        public void Dialogo_DejarElMio_CierraConCancel()
        {
            RevisionConceptoDialogViewModel vm = Dialogo(out List<IDialogResult> resultados);

            vm.DejarElMioCommand.Execute(null);

            Assert.AreEqual(ButtonResult.Cancel, resultados.Single().Result);
        }

        [TestMethod]
        public void Vista_PintaElPropuestoConUnRunPorTramo()
        {
            Exception capturada = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    var vista = new RevisionConceptoDialog();
                    var bloque = new System.Windows.Controls.TextBlock();
                    TextoConCambios.SetTramos(bloque, RevisionConcepto.Trocear(PROPUESTO, ConCambios().Cambios));

                    Assert.IsInstanceOfType(vista.DataContext, typeof(RevisionConceptoDialogViewModel));
                    Assert.AreEqual(3, bloque.Inlines.Count);
                    var cambio = bloque.Inlines.Cast<System.Windows.Documents.Run>().ElementAt(1);
                    Assert.AreEqual("Microneedling", cambio.Text);
                    Assert.AreEqual(System.Windows.FontWeights.SemiBold, cambio.FontWeight);
                }
                catch (Exception ex) { capturada = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (capturada != null)
            {
                throw new AssertFailedException(capturada.Message, capturada);
            }
        }

        #endregion

        #region Trocear (para resaltar)

        [TestMethod]
        public void Trocear_VariosCambios_LosMarcaEnOrdenYConservaElTexto()
        {
            var tramos = RevisionConcepto.Trocear("Cloasma, léntigos, gestión con Yolanda", new List<CambioConcepto>
            {
                new CambioConcepto { De = "lentigos", A = "léntigos" },
                new CambioConcepto { De = "gestoon", A = "gestión" }
            });

            CollectionAssert.AreEqual(new[] { "Cloasma, ", "léntigos", ", ", "gestión", " con Yolanda" }, tramos.Select(t => t.Texto).ToArray());
            CollectionAssert.AreEqual(new[] { false, true, false, true, false }, tramos.Select(t => t.EsCambio).ToArray());
        }

        [TestMethod]
        public void Trocear_CambioQueNoEstaEnElPropuesto_NoSeMarca()
        {
            var tramos = RevisionConcepto.Trocear("Pago factura NV2613646", new List<CambioConcepto>
            {
                new CambioConcepto { De = "x", A = "otra cosa" }
            });

            Assert.AreEqual(1, tramos.Count);
            Assert.IsFalse(tramos[0].EsCambio);
        }

        #endregion
    }
}
