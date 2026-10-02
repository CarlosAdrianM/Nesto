using ControlesUsuario.Dialogs;
using ControlesUsuario.Notificaciones;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#477: campana de la cinta con el buzón de notificaciones (contador, panel, marcar, borrar y el
    /// salto a Novedades cuando el asistente contesta un comentario).
    /// </summary>
    [TestClass]
    public class CampanaNotificacionesViewModelTests
    {
        private static readonly DateTime Ahora = new DateTime(2026, 9, 24, 12, 0, 0);

        private IBuzonNotificacionesService buzon;
        private IServicioDialogos dialogos;
        private IAbridorNovedades abridorNovedades;
        private IServicioNavegacion navegacion;
        private IAbridorPedidos abridorPedidos;
        private CampanaNotificacionesViewModel vm;

        [TestInitialize]
        public void Setup()
        {
            buzon = A.Fake<IBuzonNotificacionesService>();
            dialogos = A.Fake<IServicioDialogos>();
            abridorNovedades = A.Fake<IAbridorNovedades>();
            navegacion = A.Fake<IServicioNavegacion>();
            abridorPedidos = A.Fake<IAbridorPedidos>();
            avisos = new AvisosFalsos();
            reloj = Ahora;
            A.CallTo(() => buzon.LeerBuzon(A<bool>._, A<int>._, A<int>._)).Returns(Task.FromResult(new List<NotificacionBuzon>()));
            vm = new CampanaNotificacionesViewModel(buzon, dialogos, avisos, abridorNovedades, () => reloj, new Random(1), navegacion, abridorPedidos);
        }

        private AvisosFalsos avisos;
        private DateTime reloj;

        private sealed class AvisosFalsos : IAvisosEnTiempoReal
        {
            public event EventHandler HayNotificacionesNuevas;
            public EstadoConexionTiempoReal Estado => EstadoConexionTiempoReal.Desactivado;
            public event EventHandler EstadoCambiado { add { } remove { } }
            public void Avisar() => HayNotificacionesNuevas?.Invoke(this, EventArgs.Empty);
        }

        // ---- Disparadores del refresco (sin sondeo cada poco: no cargar el servidor) ----

        [TestMethod]
        public void AvisoDeNotificacionesNuevas_RefrescaElContador()
        {
            A.CallTo(() => buzon.ContarNoLeidas()).Returns(Task.FromResult(1));

            avisos.Avisar();

            A.CallTo(() => buzon.ContarNoLeidas()).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, vm.NoLeidas);
        }

        [TestMethod]
        public async Task AlActivarseLaVentana_LaPrimeraVez_Refresca()
        {
            await vm.AlActivarseLaVentana();

            A.CallTo(() => buzon.ContarNoLeidas()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AlActivarseLaVentana_ComoMuchoUnRefrescoCada5Minutos()
        {
            await vm.AlActivarseLaVentana();
            reloj = Ahora.AddMinutes(4).AddSeconds(59);
            await vm.AlActivarseLaVentana();
            A.CallTo(() => buzon.ContarNoLeidas()).MustHaveHappenedOnceExactly();

            reloj = Ahora.AddMinutes(5);
            await vm.AlActivarseLaVentana();

            A.CallTo(() => buzon.ContarNoLeidas()).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task AlActivarseLaVentana_ElMinimoSeCuentaDesdeElUltimoRefrescoPorFoco()
        {
            await vm.AlActivarseLaVentana();
            reloj = Ahora.AddMinutes(6);
            await vm.AlActivarseLaVentana();
            reloj = Ahora.AddMinutes(9);
            await vm.AlActivarseLaVentana();

            A.CallTo(() => buzon.ContarNoLeidas()).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public void PrimerSondeo_Entre30Y35Minutos()
        {
            var azar = new Random(7);
            for (int i = 0; i < 200; i++)
            {
                TimeSpan primero = CampanaNotificacionesViewModel.PrimerSondeo(azar);
                Assert.IsTrue(primero >= TimeSpan.FromMinutes(30) && primero <= TimeSpan.FromMinutes(35), primero.ToString());
            }
            Assert.AreEqual(TimeSpan.FromMinutes(30), CampanaNotificacionesViewModel.INTERVALO_SONDEO);
        }

        private static NotificacionBuzon Respuesta(int id, bool leida = false)
            => new NotificacionBuzon
            {
                Id = id,
                Titulo = "Te han contestado en Novedades",
                Cuerpo = "Claude (asistente IA): ya está",
                FechaCreacion = Ahora.AddMinutes(-5),
                Leida = leida,
                Datos = new Dictionary<string, string> { ["tipo"] = "NovedadComentario", ["novedadId"] = "338", ["comentarioId"] = "77" }
            };

        private static NotificacionBuzon NuevaVersion(int id, bool leida = false)
            => new NotificacionBuzon
            {
                Id = id,
                Titulo = "Nesto 1.10.32.0 ya está publicado",
                Cuerpo = "Cuando os venga bien, cerrad Nesto y volved a abrirlo para actualizar a la versión 1.10.32.0. En Novedades tenéis lo que trae.",
                FechaCreacion = Ahora.AddMinutes(-5),
                Leida = leida,
                // Lo que manda NestoAPI en POST api/Notificaciones/NuevaVersionNesto
                Datos = new Dictionary<string, string> { ["tipo"] = "NuevaVersionNesto", ["version"] = "1.10.32.0" }
            };

        private static NotificacionBuzon Otra(int id, bool leida = false)
            => new NotificacionBuzon { Id = id, Titulo = "Aviso", Cuerpo = "Un texto largo", FechaCreacion = Ahora.AddDays(-3), Leida = leida };

        private async Task CargarCon(params NotificacionBuzon[] notificaciones)
        {
            A.CallTo(() => buzon.LeerBuzon(A<bool>._, A<int>._, A<int>._)).Returns(Task.FromResult(notificaciones.ToList()));
            A.CallTo(() => buzon.ContarNoLeidas()).Returns(Task.FromResult(notificaciones.Count(n => !n.Leida)));
            await vm.CargarPanel();
        }

        // ---- Contador ----

        [TestMethod]
        public async Task RefrescarContador_PintaLasNoLeidas()
        {
            A.CallTo(() => buzon.ContarNoLeidas()).Returns(Task.FromResult(3));

            await vm.RefrescarContador();

            Assert.AreEqual(3, vm.NoLeidas);
            Assert.IsTrue(vm.HayNoLeidas);
            Assert.AreEqual("3", vm.TextoNoLeidas);
        }

        [TestMethod]
        public async Task RefrescarContador_ConCero_ElGloboSeOculta()
        {
            A.CallTo(() => buzon.ContarNoLeidas()).Returns(Task.FromResult(0));

            await vm.RefrescarContador();

            Assert.IsFalse(vm.HayNoLeidas);
        }

        [TestMethod]
        public async Task RefrescarContador_ConMasDe99_Pinta99Mas()
        {
            A.CallTo(() => buzon.ContarNoLeidas()).Returns(Task.FromResult(150));

            await vm.RefrescarContador();

            Assert.AreEqual("99+", vm.TextoNoLeidas);
        }

        [TestMethod]
        public async Task RefrescarContador_SiFallaLaRed_EsSilenciosoYConservaElQueHabia()
        {
            A.CallTo(() => buzon.ContarNoLeidas()).Returns(Task.FromResult(2));
            await vm.RefrescarContador();
            A.CallTo(() => buzon.ContarNoLeidas()).Throws(new HttpRequestException("sin red"));

            await vm.RefrescarContador();

            Assert.AreEqual(2, vm.NoLeidas);
            Assert.IsFalse(vm.HayMensaje, "Un fallo del refresco no se enseña");
        }

        // ---- Panel ----

        [TestMethod]
        public async Task AbrirElPanel_CargaLaListaYRefrescaElContador()
        {
            A.CallTo(() => buzon.LeerBuzon(A<bool>._, A<int>._, A<int>._)).Returns(Task.FromResult(new List<NotificacionBuzon> { Otra(1), Respuesta(2) }));
            A.CallTo(() => buzon.ContarNoLeidas()).Returns(Task.FromResult(2));

            vm.PanelAbierto = true;

            Assert.AreEqual(2, vm.Notificaciones.Count);
            Assert.AreEqual(2, vm.Notificaciones[0].Id, "La más reciente, arriba");
            Assert.AreEqual("hace 5 min", vm.Notificaciones[0].FechaRelativa);
            Assert.AreEqual(2, vm.NoLeidas);
            A.CallTo(() => buzon.LeerBuzon(false, 1, CampanaNotificacionesViewModel.TAMANO_PANEL)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void CerrarElPanel_RefrescaElContador()
        {
            vm.PanelAbierto = true;
            Fake.ClearRecordedCalls(buzon);

            vm.PanelAbierto = false;

            A.CallTo(() => buzon.ContarNoLeidas()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task CargarPanel_SinNotificaciones_LoDice()
        {
            await CargarCon();

            Assert.AreEqual("No tienes notificaciones.", vm.Mensaje);
        }

        [TestMethod]
        public async Task CargarPanel_SiFalla_EnseñaElMotivo()
        {
            A.CallTo(() => buzon.LeerBuzon(A<bool>._, A<int>._, A<int>._)).Throws(new InvalidOperationException("No se pudo leer las notificaciones (error 500)."));

            await vm.CargarPanel();

            Assert.AreEqual("No se pudo leer las notificaciones (error 500).", vm.Mensaje);
        }

        // ---- Pulsar una notificación ----

        [TestMethod]
        public async Task PulsarRespuestaDeNovedades_LaMarcaLeidaYAbreNovedadesEnElComentario()
        {
            await CargarCon(Respuesta(7));
            vm.PanelAbierto = true;
            A.CallTo(() => buzon.ContarNoLeidas()).Returns(Task.FromResult(0));
            ParametrosDialogo parametros = null;
            A.CallTo(() => dialogos.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string nombre, ParametrosDialogo p, Action<ResultadoDialogo> cb) => parametros = p);

            await vm.AbrirNotificacion(vm.Notificaciones[0]);

            A.CallTo(() => buzon.MarcarLeida(7)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.Notificaciones[0].Leida);
            Assert.AreEqual(0, vm.NoLeidas);
            Assert.IsFalse(vm.PanelAbierto, "El panel se cierra antes de abrir la ventana");
            A.CallTo(() => dialogos.ShowDialog("NovedadesDialog", A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(338, parametros.GetValue<int>(NovedadesDialogViewModel.PARAMETRO_NOVEDAD_ID));
            Assert.AreEqual(77, parametros.GetValue<int>(NovedadesDialogViewModel.PARAMETRO_COMENTARIO_ID));
        }

        [TestMethod]
        public async Task PulsarUnaYaLeida_NoLaVuelveAMarcar()
        {
            await CargarCon(Respuesta(7, leida: true));

            await vm.AbrirNotificacion(vm.Notificaciones[0]);

            A.CallTo(() => buzon.MarcarLeida(A<int>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowDialog("NovedadesDialog", A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task PulsarElRecordatorioDeFacturasVerifactu_AbreEsaVentana()
        {
            // NestoAPI#522: la nota diaria a administración lleva a la ventana de facturas pendientes de Verifactu
            await CargarCon(new NotificacionBuzon
            {
                Id = 9,
                Titulo = "Facturas pendientes de Verifactu",
                Cuerpo = "Hay 2 facturas que Verifactu todavía no da por buenas",
                FechaCreacion = Ahora.AddHours(-3),
                Datos = new Dictionary<string, string> { ["tipo"] = "FacturasPendientesVerifactu", ["cantidad"] = "2" }
            });
            vm.PanelAbierto = true;

            await vm.AbrirNotificacion(vm.Notificaciones[0]);

            A.CallTo(() => buzon.MarcarLeida(9)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.PanelAbierto, "El panel se cierra antes de abrir la ventana");
            A.CallTo(() => navegacion.RequestNavigate("MainRegion", "FacturasPendientesVerifactuView")).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.Notificaciones[0].Desplegada);
            A.CallTo(() => dialogos.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task PulsarUnAvisoConPedido_AbreEsePedido()
        {
            // NestoAPI#555: «El pedido 927075 ha cogido picking» lleva al pedido
            await CargarCon(new NotificacionBuzon
            {
                Id = 11,
                Titulo = "El pedido 927075 ha cogido picking",
                Cuerpo = "Cliente 15191: 121,00 € a cobrar (IVA incluido).",
                FechaCreacion = Ahora.AddMinutes(-5),
                Datos = new Dictionary<string, string> { ["tipo"] = "AvisoPickingConImporte", ["empresa"] = "1", ["pedido"] = "927075" }
            });
            vm.PanelAbierto = true;

            await vm.AbrirNotificacion(vm.Notificaciones[0]);

            A.CallTo(() => buzon.MarcarLeida(11)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.PanelAbierto);
            A.CallTo(() => abridorPedidos.Abrir("1", 927075)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.Notificaciones[0].Desplegada);
        }

        [TestMethod]
        public async Task PulsarUnAvisoConPedidoSinEmpresa_AbreEnLaEmpresaPorDefecto()
        {
            await CargarCon(new NotificacionBuzon
            {
                Id = 12,
                Titulo = "NIF incorrecto",
                FechaCreacion = Ahora.AddMinutes(-5),
                Datos = new Dictionary<string, string> { ["tipo"] = "Otro", ["pedido"] = "927075" }
            });

            await vm.AbrirNotificacion(vm.Notificaciones[0]);

            A.CallTo(() => abridorPedidos.Abrir("1", 927075)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task PulsarOtroTipo_SoloLaMarcaLeidaYLaDespliega()
        {
            await CargarCon(Otra(8));

            await vm.AbrirNotificacion(vm.Notificaciones[0]);

            A.CallTo(() => buzon.MarcarLeida(8)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.Notificaciones[0].Desplegada);
            A.CallTo(() => dialogos.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task PulsarAvisoDeVersionNueva_LaMarcaLeidaYAbreNovedadesEnEsaVersion()
        {
            // Nesto#501: antes solo se desplegaba el texto («En Novedades tenéis lo que trae»)
            await CargarCon(NuevaVersion(9));
            vm.PanelAbierto = true;

            await vm.AbrirNotificacion(vm.Notificaciones[0]);

            A.CallTo(() => buzon.MarcarLeida(9)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.Notificaciones[0].Leida);
            Assert.IsFalse(vm.PanelAbierto, "El panel se cierra antes de abrir la ventana");
            A.CallTo(() => abridorNovedades.Abrir("1.10.32.0")).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.Notificaciones[0].Desplegada);
        }

        [TestMethod]
        public void BotonNovedades_AbreNovedadesYCierraElPanel()
        {
            // Nesto#501: el botón junto a la campana
            vm.PanelAbierto = true;

            Assert.IsTrue(vm.AbrirNovedadesCommand.CanExecute(null));
            vm.AbrirNovedadesCommand.Execute(null);

            A.CallTo(() => abridorNovedades.Abrir(null)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.PanelAbierto);
        }

        [TestMethod]
        public async Task PulsarYFallaElMarcar_SeAbreIgualYSeDice()
        {
            await CargarCon(Respuesta(7));
            A.CallTo(() => buzon.MarcarLeida(7)).Throws(new InvalidOperationException("No se pudo marcar"));

            await vm.AbrirNotificacion(vm.Notificaciones[0]);

            Assert.IsFalse(vm.Notificaciones[0].Leida);
            Assert.AreEqual(1, vm.NoLeidas);
            A.CallTo(() => dialogos.ShowDialog("NovedadesDialog", A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustHaveHappenedOnceExactly();
        }

        // ---- Marcar todas y borrar ----

        [TestMethod]
        public async Task MarcarTodasLeidas_LasMarcaYPoneElContadorACero()
        {
            await CargarCon(Respuesta(1), Otra(2));
            Assert.IsTrue(vm.MarcarTodasLeidasCommand.CanExecute(null));

            await vm.MarcarTodasLeidas();

            A.CallTo(() => buzon.MarcarTodasLeidas()).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.Notificaciones.All(n => n.Leida));
            Assert.AreEqual(0, vm.NoLeidas);
            Assert.IsFalse(vm.MarcarTodasLeidasCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Borrar_UnaNoLeida_LaQuitaYBajaElContador()
        {
            await CargarCon(Respuesta(1), Otra(2, leida: true));

            await vm.BorrarNotificacion(vm.Notificaciones.Single(n => n.Id == 1));

            A.CallTo(() => buzon.Eliminar(1)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, vm.Notificaciones.Count);
            Assert.AreEqual(0, vm.NoLeidas);
        }

        [TestMethod]
        public async Task Borrar_SiFalla_NoLaQuitaYLoDice()
        {
            await CargarCon(Respuesta(1));
            A.CallTo(() => buzon.Eliminar(1)).Throws(new InvalidOperationException("No se pudo borrar la notificación: no existe"));

            await vm.BorrarNotificacion(vm.Notificaciones[0]);

            Assert.AreEqual(1, vm.Notificaciones.Count);
            Assert.AreEqual("No se pudo borrar la notificación: no existe", vm.Mensaje);
        }

        // ---- Fecha relativa ----

        [TestMethod]
        public void FechaRelativa()
        {
            Assert.AreEqual("ahora", NotificacionBuzonItem.CalcularFechaRelativa(Ahora.AddSeconds(-20), Ahora));
            Assert.AreEqual("hace 12 min", NotificacionBuzonItem.CalcularFechaRelativa(Ahora.AddMinutes(-12), Ahora));
            Assert.AreEqual("hace 3 h", NotificacionBuzonItem.CalcularFechaRelativa(Ahora.AddHours(-3), Ahora));
            Assert.AreEqual("ayer a las 18:30", NotificacionBuzonItem.CalcularFechaRelativa(new DateTime(2026, 9, 23, 18, 30, 0), Ahora));
            Assert.AreEqual("02/09 08:15", NotificacionBuzonItem.CalcularFechaRelativa(new DateTime(2026, 9, 2, 8, 15, 0), Ahora));
            Assert.AreEqual("02/09/2025", NotificacionBuzonItem.CalcularFechaRelativa(new DateTime(2025, 9, 2, 8, 15, 0), Ahora));
        }
    }
}
