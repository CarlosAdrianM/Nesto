using ControlesUsuario.Notificaciones.Urgentes;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#593 (NestoAPI#775): las notificaciones urgentes abren su ventana roja, pitan cada 20 s mientras siguen pendientes (como mucho
    /// unos minutos) y se cierran en todos los Nesto cuando alguien contesta.
    /// </summary>
    [TestClass]
    public class NotificacionesUrgentesTests
    {
        private DateTime ahora = new DateTime(2026, 10, 31, 10, 0, 0);
        private IBuzonNotificacionesService buzon;
        private AvisosFalsos avisos;
        private PresentadorFalso presentador;
        private List<NotificacionUrgente> pendientes;
        private GestorNotificacionesUrgentes gestor;

        [TestInitialize]
        public void Setup()
        {
            buzon = A.Fake<IBuzonNotificacionesService>();
            pendientes = new List<NotificacionUrgente>();
            A.CallTo(() => buzon.LeerUrgentes()).ReturnsLazily(() => Task.FromResult(pendientes.ToList()));
            avisos = new AvisosFalsos();
            presentador = new PresentadorFalso();
            gestor = new GestorNotificacionesUrgentes(buzon, avisos, presentador, () => ahora);
        }

        private static NotificacionUrgente Autorizacion(string id = "12", bool escalada = false) => new NotificacionUrgente
        {
            IdBuzon = 500,
            Tipo = NotificacionBuzon.TIPO_AUTORIZACION_REEMBOLSO_REPARTO,
            Referencia = id,
            Titulo = "Autorización: entregar sin cobrar 45,30 € (envío 251300)",
            Escalada = escalada,
            Datos = new Dictionary<string, string>
            {
                ["tipo"] = NotificacionBuzon.TIPO_AUTORIZACION_REEMBOLSO_REPARTO, ["autorizacionId"] = id, ["referencia"] = id, ["envio"] = "251300",
                ["pedido"] = "912345", ["cliente"] = "15191", ["nombre"] = "Peluquería Lucía", ["importe"] = "45.30", ["repartidor"] = "Andre",
                ["motivo"] = "Paga por transferencia"
            }
        };

        [TestMethod]
        public async Task AlIniciar_AbreLaVentanaConLosDatos_YPita()
        {
            pendientes.Add(Autorizacion());

            gestor.Iniciar();
            await Task.Yield();

            AvisoUrgenteViewModel aviso = presentador.Abiertas.Single();
            Assert.AreEqual("Andre", aviso.Repartidor);
            Assert.AreEqual("15191 · Peluquería Lucía", aviso.Cliente);
            Assert.AreEqual("45,30 €", aviso.Importe);
            Assert.AreEqual("Paga por transferencia", aviso.Motivo);
            Assert.AreEqual("Envío 251300 · pedido 912345", aviso.Envio);
            Assert.IsTrue(aviso.PuedeContestar);
            Assert.AreEqual(1, presentador.Pitidos);
            Assert.AreEqual(1, presentador.Atenciones, "Parpadeo de la barra de tareas si no está delante");
        }

        [TestMethod]
        public async Task HayNotificacionUrgente_AbreSoloLasNuevas_SinRepetirLaQueYaEstaAbierta()
        {
            gestor.Iniciar();
            pendientes.Add(Autorizacion("12"));
            avisos.LlegaUrgente();
            await Task.Yield();
            pendientes.Add(Autorizacion("13"));
            avisos.LlegaUrgente();
            await Task.Yield();

            CollectionAssert.AreEquivalent(new[] { "12", "13" }, presentador.Abiertas.Select(a => a.Notificacion.Referencia).ToList());
            Assert.AreEqual(2, gestor.Abiertos.Count);
        }

        [TestMethod]
        public async Task Pitido_Cada20Segundos_MientrasSiguePendiente_YComoMuchoUnosMinutos()
        {
            pendientes.Add(Autorizacion());
            gestor.Iniciar();
            await Task.Yield();
            Assert.AreEqual(1, presentador.Pitidos);

            ahora = ahora.AddSeconds(10);
            gestor.Tic();
            Assert.AreEqual(1, presentador.Pitidos, "Aún no han pasado 20 s");

            ahora = ahora.AddSeconds(10);
            gestor.Tic();
            Assert.AreEqual(2, presentador.Pitidos);

            ahora = ahora.Add(GestorNotificacionesUrgentes.MAXIMO_PITANDO);
            gestor.Tic();
            Assert.AreEqual(2, presentador.Pitidos, "Pasado el máximo deja de pitar (la ventana sigue a la vista)");
            Assert.AreEqual(1, gestor.Abiertos.Count);
        }

        [TestMethod]
        public async Task Escalada_VuelveAPitarDesdeEseMomento()
        {
            pendientes.Add(Autorizacion());
            gestor.Iniciar();
            await Task.Yield();
            ahora = ahora.Add(GestorNotificacionesUrgentes.MAXIMO_PITANDO).AddMinutes(1);
            gestor.Tic();
            int antes = presentador.Pitidos;

            pendientes[0] = Autorizacion(escalada: true);
            avisos.LlegaUrgente();
            await Task.Yield();

            Assert.AreEqual(antes + 1, presentador.Pitidos);
            Assert.IsTrue(gestor.Abiertos.Single().Escalada);
            StringAssert.Contains(gestor.Abiertos.Single().Cabecera, "sin respuesta");
        }

        [TestMethod]
        public async Task NotificacionResuelta_PorOtro_LoDiceYSeCierra_YDejaDePitar()
        {
            pendientes.Add(Autorizacion());
            gestor.Iniciar();
            await Task.Yield();
            AvisoUrgenteViewModel aviso = gestor.Abiertos.Single();

            avisos.LlegaResuelta(new NotificacionResuelta { Tipo = NotificacionBuzon.TIPO_AUTORIZACION_REEMBOLSO_REPARTO, Referencia = "12", Respondio = "Laura", Resultado = "Autorizada" });

            Assert.AreEqual("Lo ha autorizado Laura.", aviso.Mensaje);
            Assert.IsTrue(aviso.Resuelta);
            Assert.IsFalse(aviso.PuedeContestar);
            Assert.AreEqual(GestorNotificacionesUrgentes.RETRASO_CIERRE, presentador.Cierres.Single(c => c.Aviso == aviso).Retraso);
            presentador.CerrarDeVerdad(aviso);
            Assert.AreEqual(0, gestor.Abiertos.Count);

            ahora = ahora.AddSeconds(20);
            gestor.Tic();
            Assert.AreEqual(1, presentador.Pitidos);
        }

        [TestMethod]
        public void TextoResuelta_DenegadaYTelefonica()
        {
            Assert.AreEqual("Lo ha denegado Marta.", AvisoUrgenteViewModel.TextoResuelta(new NotificacionResuelta { Respondio = "Marta", Resultado = "Denegada" }));
            StringAssert.Contains(AvisoUrgenteViewModel.TextoResuelta(new NotificacionResuelta { Respondio = "Me lo dijo Laura", Resultado = "Telefonica" }), "por teléfono");
        }

        [TestMethod]
        public async Task ResueltaDeOtraReferencia_NoCierraNada()
        {
            pendientes.Add(Autorizacion());
            gestor.Iniciar();
            await Task.Yield();

            avisos.LlegaResuelta(new NotificacionResuelta { Tipo = NotificacionBuzon.TIPO_AUTORIZACION_REEMBOLSO_REPARTO, Referencia = "99", Respondio = "Laura", Resultado = "Autorizada" });

            Assert.IsFalse(gestor.Abiertos.Single().Resuelta);
            Assert.AreEqual(0, presentador.Cierres.Count);
        }

        [TestMethod]
        public async Task Autorizar_DesdeLaVentana_ContestaEnLaApi_MarcaLeida_YSeCierra()
        {
            A.CallTo(() => buzon.ResponderAutorizacionReembolso(12, true, null)).Returns(Task.FromResult("Autorizada"));
            pendientes.Add(Autorizacion());
            gestor.Iniciar();
            await Task.Yield();
            AvisoUrgenteViewModel aviso = gestor.Abiertos.Single();

            await aviso.AutorizarCommand.ExecuteAsync(null);

            A.CallTo(() => buzon.ResponderAutorizacionReembolso(12, true, null)).MustHaveHappenedOnceExactly();
            A.CallTo(() => buzon.MarcarLeida(500)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(aviso.Resuelta);
            StringAssert.StartsWith(aviso.Mensaje, "Autorizado");
            Assert.AreEqual(1, presentador.Cierres.Count(c => c.Aviso == aviso && c.Retraso == GestorNotificacionesUrgentes.RETRASO_CIERRE));

            // Luego llega su propio «notificacionResuelta»: no cambia el mensaje
            avisos.LlegaResuelta(new NotificacionResuelta { Tipo = NotificacionBuzon.TIPO_AUTORIZACION_REEMBOLSO_REPARTO, Referencia = "12", Respondio = "Carlos", Resultado = "Autorizada" });
            StringAssert.StartsWith(aviso.Mensaje, "Autorizado");
        }

        [TestMethod]
        public async Task Denegar_SiOtroLaContestoALaVez_EnsenaEl409YSeCierra()
        {
            A.CallTo(() => buzon.ResponderAutorizacionReembolso(12, false, null))
                .ThrowsAsync(new InvalidOperationException("Esta autorización ya está contestada (autorizada por Laura)."));
            pendientes.Add(Autorizacion());
            gestor.Iniciar();
            await Task.Yield();
            AvisoUrgenteViewModel aviso = gestor.Abiertos.Single();

            await aviso.DenegarCommand.ExecuteAsync(null);

            Assert.AreEqual("Esta autorización ya está contestada (autorizada por Laura).", aviso.Mensaje);
            Assert.IsTrue(aviso.Resuelta);
            Assert.AreEqual(1, presentador.Cierres.Count);
        }

        [TestMethod]
        public async Task Autorizar_SinRed_SeQuedaAbiertaParaReintentar()
        {
            A.CallTo(() => buzon.ResponderAutorizacionReembolso(12, true, null)).ThrowsAsync(new InvalidOperationException("No se puede conectar con el servidor"));
            pendientes.Add(Autorizacion());
            gestor.Iniciar();
            await Task.Yield();
            AvisoUrgenteViewModel aviso = gestor.Abiertos.Single();

            await aviso.AutorizarCommand.ExecuteAsync(null);

            Assert.IsFalse(aviso.Resuelta);
            Assert.IsTrue(aviso.AutorizarCommand.CanExecute(null));
            Assert.AreEqual(0, presentador.Cierres.Count);
        }

        [TestMethod]
        public async Task MasTarde_LaCierraAqui_YVuelveASalirSiSiguePendiente()
        {
            pendientes.Add(Autorizacion());
            gestor.Iniciar();
            await Task.Yield();
            AvisoUrgenteViewModel aviso = gestor.Abiertos.Single();

            aviso.OcultarCommand.Execute(null);

            Assert.AreEqual(0, gestor.Abiertos.Count);
            avisos.LlegaUrgente(); // reconexión, escalado…
            await Task.Yield();
            Assert.AreEqual(1, gestor.Abiertos.Count);
            Assert.AreNotSame(aviso, gestor.Abiertos.Single());
        }

        [TestMethod]
        public async Task SiFallaLaApi_NoPasaNada()
        {
            A.CallTo(() => buzon.LeerUrgentes()).ThrowsAsync(new InvalidOperationException("sin red"));

            gestor.Iniciar();
            await Task.Yield();

            Assert.AreEqual(0, presentador.Abiertas.Count);
            Assert.AreEqual(0, presentador.Pitidos);
        }

        private sealed class AvisosFalsos : IAvisosUrgentesTiempoReal
        {
            public event EventHandler HayNotificacionUrgente;
            public event EventHandler<NotificacionResuelta> NotificacionResuelta;
            public void LlegaUrgente() => HayNotificacionUrgente?.Invoke(this, EventArgs.Empty);
            public void LlegaResuelta(NotificacionResuelta r) => NotificacionResuelta?.Invoke(this, r);
        }

        private sealed class PresentadorFalso : IPresentadorAvisosUrgentes
        {
            private readonly Dictionary<AvisoUrgenteViewModel, Action<AvisoUrgenteViewModel>> _alCerrarse = new Dictionary<AvisoUrgenteViewModel, Action<AvisoUrgenteViewModel>>();
            public List<AvisoUrgenteViewModel> Abiertas { get; } = new List<AvisoUrgenteViewModel>();
            public List<(AvisoUrgenteViewModel Aviso, TimeSpan Retraso)> Cierres { get; } = new List<(AvisoUrgenteViewModel, TimeSpan)>();
            public int Pitidos { get; private set; }
            public int Atenciones { get; private set; }

            public void Abrir(AvisoUrgenteViewModel aviso, Action<AvisoUrgenteViewModel> alCerrarse)
            {
                Abiertas.Add(aviso);
                _alCerrarse[aviso] = alCerrarse;
            }

            public void Cerrar(AvisoUrgenteViewModel aviso, TimeSpan retraso)
            {
                if (retraso > TimeSpan.Zero)
                {
                    Cierres.Add((aviso, retraso));
                }
            }

            /// <summary>La ventana se cierra de verdad (pasado el retraso).</summary>
            public void CerrarDeVerdad(AvisoUrgenteViewModel aviso)
            {
                if (_alCerrarse.TryGetValue(aviso, out Action<AvisoUrgenteViewModel> accion))
                {
                    _ = _alCerrarse.Remove(aviso);
                    accion(aviso);
                }
            }

            public void Pitar() => Pitidos++;
            public void LlamarLaAtencion() => Atenciones++;
        }
    }
}
