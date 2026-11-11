using ControlesUsuario.Dialogs;
using ControlesUsuario.Notificaciones;
using ControlesUsuario.Notificaciones.Menciones;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Sugerencia 1183 (Laura Magán): la campana distingue las menciones sin leer (Datos["mencion"] = "1") y, si el usuario lo tiene
    /// encendido (parámetro AvisoEmergenteMenciones), cada mención nueva sale en una ventanita, una sola vez.
    /// </summary>
    [TestClass]
    public class CampanaMencionesTests
    {
        private static readonly DateTime Ahora = new DateTime(2026, 11, 11, 10, 0, 0);

        private IBuzonNotificacionesService buzon;
        private IConfiguracion configuracion;
        private IServicioDialogos dialogos;
        private PresentadorFalso presentador;
        private AvisosFalsos avisos;
        private List<NotificacionBuzon> noLeidas;
        private CampanaNotificacionesViewModel vm;

        private sealed class PresentadorFalso : IPresentadorAvisoMencion
        {
            public List<AvisoMencion> Mostrados { get; } = new List<AvisoMencion>();
            public void Mostrar(AvisoMencion aviso) => Mostrados.Add(aviso);
        }

        private sealed class AvisosFalsos : IAvisosEnTiempoReal
        {
            public event EventHandler HayNotificacionesNuevas;
            public EstadoConexionTiempoReal Estado => EstadoConexionTiempoReal.Desactivado;
            public event EventHandler EstadoCambiado { add { } remove { } }
            public void Avisar() => HayNotificacionesNuevas?.Invoke(this, EventArgs.Empty);
        }

        [TestInitialize]
        public void Setup()
        {
            buzon = A.Fake<IBuzonNotificacionesService>();
            configuracion = A.Fake<IConfiguracion>();
            dialogos = A.Fake<IServicioDialogos>();
            presentador = new PresentadorFalso();
            avisos = new AvisosFalsos();
            noLeidas = new List<NotificacionBuzon>();
            A.CallTo(() => buzon.ContarNoLeidas()).ReturnsLazily(() => Task.FromResult(noLeidas.Count(n => !n.Leida)));
            A.CallTo(() => buzon.LeerBuzon(true, A<int>._, A<int>._)).ReturnsLazily(() => Task.FromResult(noLeidas.Where(n => !n.Leida).ToList()));
            A.CallTo(() => buzon.LeerBuzon(false, A<int>._, A<int>._)).ReturnsLazily(() => Task.FromResult(noLeidas.ToList()));
            A.CallTo(() => buzon.MarcarLeida(A<int>._)).ReturnsLazily((int id) =>
            {
                noLeidas.Where(n => n.Id == id).ToList().ForEach(n => n.Leida = true);
                return Task.CompletedTask;
            });
            Encendido("1");
            vm = new CampanaNotificacionesViewModel(buzon, dialogos, avisos, null, () => Ahora, new Random(1), null, null, configuracion, presentador);
        }

        private void Encendido(string valor)
        {
            A.CallTo(() => configuracion.leerParametro("1", Parametros.Claves.AvisoEmergenteMenciones)).Returns(Task.FromResult(valor));
        }

        private static NotificacionBuzon Mencion(int id, int minutosAntes = 0) => new NotificacionBuzon
        {
            Id = id,
            Titulo = "Carlos le ha mencionado en Novedades",
            Cuerpo = "@Magan, ¿lo puede mirar?",
            FechaCreacion = Ahora.AddMinutes(-minutosAntes),
            Datos = new Dictionary<string, string> { ["tipo"] = "NovedadComentario", ["novedadId"] = "350", ["comentarioId"] = (60 + id).ToString(), ["mencion"] = "1" }
        };

        private static NotificacionBuzon Respuesta(int id) => new NotificacionBuzon
        {
            Id = id,
            Titulo = "El asistente ha contestado en Novedades",
            FechaCreacion = Ahora,
            Datos = new Dictionary<string, string> { ["tipo"] = "NovedadComentario", ["novedadId"] = "350" }
        };

        /// <summary>Lo que hace al arrancar: lee el parámetro y el primer refresco (que solo apunta lo que hay).</summary>
        private async Task Arrancar()
        {
            await vm.CargarPreferencias();
            await vm.RefrescarContador();
        }

        // ---- La campana se ve distinta ----

        [TestMethod]
        public async Task ConUnaMencionSinLeer_LaCampanaEstaEnEstadoMencion()
        {
            noLeidas.Add(Mencion(1));

            await Arrancar();

            Assert.IsTrue(vm.HayMencionSinLeer);
            StringAssert.Contains(vm.TextoToolTip, "Le han mencionado");
        }

        [TestMethod]
        public async Task SinMenciones_LaCampanaEstaNormal()
        {
            noLeidas.Add(Respuesta(1));

            await Arrancar();

            Assert.IsFalse(vm.HayMencionSinLeer);
            Assert.AreEqual(1, vm.NoLeidas);
        }

        [TestMethod]
        public void LasDeAntesSinElDato_NoSonMenciones()
        {
            Assert.IsFalse(Respuesta(1).EsMencion);
            Assert.IsTrue(Mencion(1).EsMencion);
        }

        [TestMethod]
        public async Task AlLeerLaMencionEnElPanel_LaCampanaVuelveANormal()
        {
            noLeidas.Add(Mencion(1));
            await Arrancar();
            await vm.CargarPanel();

            NotificacionBuzonItem item = vm.Notificaciones.Single();
            Assert.IsTrue(item.EsMencion);
            await vm.AbrirNotificacion(item);

            Assert.IsFalse(vm.HayMencionSinLeer);
        }

        [TestMethod]
        public async Task MarcarTodasLeidas_LaCampanaVuelveANormal()
        {
            noLeidas.Add(Mencion(1));
            await Arrancar();

            await vm.MarcarTodasLeidas();

            Assert.IsFalse(vm.HayMencionSinLeer);
        }

        [TestMethod]
        public async Task LeidaEnOtroSitio_AlRefrescarVuelveANormal()
        {
            noLeidas.Add(Mencion(1));
            noLeidas.Add(Respuesta(2));
            await Arrancar();
            Assert.IsTrue(vm.HayMencionSinLeer);

            noLeidas[0].Leida = true; // la ha leído en otro Nesto
            await vm.RefrescarContador();

            Assert.IsFalse(vm.HayMencionSinLeer);
        }

        // ---- La ventanita ----

        [TestMethod]
        public async Task AlArrancarConMencionesAntiguas_NoSaleLaVentanita()
        {
            noLeidas.Add(Mencion(1, 60));

            await Arrancar();
            avisos.Avisar();

            Assert.AreEqual(0, presentador.Mostrados.Count);
        }

        [TestMethod]
        public async Task Encendido_UnaMencionNueva_SaleLaVentanitaUnaVez()
        {
            await Arrancar();

            noLeidas.Add(Mencion(7));
            avisos.Avisar();
            avisos.Avisar(); // otro aviso (o el sondeo) con la misma mención
            await vm.RefrescarContador();

            Assert.AreEqual(1, presentador.Mostrados.Count);
            Assert.AreEqual(7, presentador.Mostrados[0].NotificacionId);
            Assert.AreEqual("Carlos le ha mencionado en Novedades", presentador.Mostrados[0].Titulo);
        }

        [TestMethod]
        public async Task Encendido_SinAvisoDeSignalR_ElRefrescoTambienLaSaca()
        {
            await Arrancar();

            noLeidas.Add(Mencion(7));
            await vm.AlActivarseLaVentana(); // el primer refresco por foco

            Assert.AreEqual(1, presentador.Mostrados.Count);
        }

        [TestMethod]
        public async Task Apagado_NoSaleLaVentanita_PeroLaCampanaSiCambia()
        {
            Encendido("0");
            await Arrancar();

            noLeidas.Add(Mencion(7));
            avisos.Avisar();

            Assert.AreEqual(0, presentador.Mostrados.Count);
            Assert.IsTrue(vm.HayMencionSinLeer);
        }

        [TestMethod]
        public async Task SinParametro_EstaApagado()
        {
            A.CallTo(() => configuracion.leerParametro(A<string>._, A<string>._)).Throws(new Exception("No se puede leer el parámetro"));
            await Arrancar();

            noLeidas.Add(Mencion(7));
            avisos.Avisar();

            Assert.IsFalse(vm.AvisoEmergenteMenciones);
            Assert.AreEqual(0, presentador.Mostrados.Count);
        }

        [TestMethod]
        public async Task UnaRespuestaQueNoEsMencion_NoSacaLaVentanita()
        {
            await Arrancar();

            noLeidas.Add(Respuesta(8));
            avisos.Avisar();

            Assert.AreEqual(0, presentador.Mostrados.Count);
        }

        [TestMethod]
        public async Task LasYaVistasEnElPanel_NoSacanLaVentanita()
        {
            await Arrancar();
            noLeidas.Add(Mencion(7));
            // La ve en el panel antes de que llegue el aviso
            await vm.CargarPanel();

            avisos.Avisar();

            Assert.AreEqual(0, presentador.Mostrados.Count);
        }

        [TestMethod]
        public async Task VariasNuevasALaVez_UnaSolaVentanitaConLaMasRecienteYCuantasMas()
        {
            await Arrancar();

            noLeidas.Add(Mencion(7, 2));
            noLeidas.Add(Mencion(8, 0));
            noLeidas.Add(Mencion(9, 1));
            avisos.Avisar();

            Assert.AreEqual(1, presentador.Mostrados.Count);
            Assert.AreEqual(8, presentador.Mostrados[0].NotificacionId);
            Assert.AreEqual(2, presentador.Mostrados[0].Otras);
            Assert.AreEqual("Y 2 menciones más en la campana.", presentador.Mostrados[0].TextoOtras);
        }

        [TestMethod]
        public async Task PulsarLaVentanita_HaceLoMismoQuePulsarLaNotificacion()
        {
            await Arrancar();
            noLeidas.Add(Mencion(7));
            avisos.Avisar();

            ParametrosDialogo parametros = null;
            A.CallTo(() => dialogos.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string nombre, ParametrosDialogo p, Action<ResultadoDialogo> cb) => parametros = p);

            presentador.Mostrados.Single().AlPulsar();

            A.CallTo(() => buzon.MarcarLeida(7)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowDialog(CampanaNotificacionesViewModel.DIALOGO_NOVEDADES, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .MustHaveHappenedOnceExactly();
            Assert.AreEqual(350, parametros.GetValue<int>(NovedadesDialogViewModel.PARAMETRO_NOVEDAD_ID));
            Assert.AreEqual(67, parametros.GetValue<int>(NovedadesDialogViewModel.PARAMETRO_COMENTARIO_ID));
            Assert.IsFalse(vm.HayMencionSinLeer);
        }

        // ---- El interruptor ----

        [TestMethod]
        public async Task ElInterruptor_Refleja_ElParametroDelUsuario()
        {
            await vm.CargarPreferencias();

            Assert.IsTrue(vm.AvisoEmergenteMenciones);
            Assert.IsTrue(vm.PuedeCambiarAvisoEmergente);
            A.CallTo(() => configuracion.GuardarParametro(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ApagarElInterruptor_GuardaCero()
        {
            await vm.CargarPreferencias();

            vm.AvisoEmergenteMenciones = false;

            A.CallTo(() => configuracion.GuardarParametro("1", Parametros.Claves.AvisoEmergenteMenciones, "0")).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.AvisoEmergenteMenciones);
        }

        [TestMethod]
        public async Task EncenderElInterruptor_GuardaUno()
        {
            Encendido("0");
            await vm.CargarPreferencias();

            vm.AvisoEmergenteMenciones = true;

            A.CallTo(() => configuracion.GuardarParametro("1", Parametros.Claves.AvisoEmergenteMenciones, "1")).MustHaveHappenedOnceExactly();
            StringAssert.Contains(vm.Mensaje, "ventanita");
        }

        [TestMethod]
        public async Task SiNoSePuedeGuardar_ElInterruptorVuelveAComoEstaba()
        {
            Encendido("0");
            await vm.CargarPreferencias();
            A.CallTo(() => configuracion.GuardarParametro(A<string>._, A<string>._, A<string>._)).Throws(new Exception("Sin conexión"));

            vm.AvisoEmergenteMenciones = true;

            Assert.IsFalse(vm.AvisoEmergenteMenciones);
            StringAssert.Contains(vm.Mensaje, "No se ha podido guardar");
        }

        // ---- Las vistas cargan (XAML, halo animado y ventanita) ----

        [TestMethod]
        public void LaCampanaConMencion_YLaVentanita_SePintanSinErrores()
        {
            noLeidas.Add(Mencion(1));
            Arrancar().GetAwaiter().GetResult();
            Assert.IsTrue(vm.HayMencionSinLeer);

            Exception capturada = null;
            var hilo = new System.Threading.Thread(() =>
            {
                try
                {
                    var campana = new CampanaNotificaciones { DataContext = vm };
                    var ventana = new System.Windows.Window
                    {
                        Content = campana, Left = -10000, Top = -10000, Width = 200, Height = 100,
                        ShowInTaskbar = false, ShowActivated = false, WindowStyle = System.Windows.WindowStyle.None
                    };
                    ventana.Show();
                    campana.UpdateLayout();
                    var aviso = new AvisoMencionWindow
                    {
                        DataContext = new AvisoMencion(1, "Carlos le ha mencionado en Novedades", "@Magan, ¿lo puede mirar?", 2, null),
                        Left = -10000, Top = -10000
                    };
                    aviso.Show();
                    aviso.UpdateLayout();
                    Assert.IsTrue(aviso.ActualHeight > 0);
                    aviso.Close();
                    ventana.Close();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
                catch (Exception ex)
                {
                    capturada = ex;
                }
            });
            hilo.SetApartmentState(System.Threading.ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (capturada != null)
            {
                throw new AssertFailedException(capturada.ToString(), capturada);
            }
        }

        [TestMethod]
        public void SinConfiguracion_ElInterruptorNoSePuedeTocar()
        {
            var sinConfiguracion = new CampanaNotificacionesViewModel(buzon, dialogos, null, null, () => Ahora, new Random(1));

            Assert.IsFalse(sinConfiguracion.PuedeCambiarAvisoEmergente);
        }
    }
}
