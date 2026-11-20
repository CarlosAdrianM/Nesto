using ControlesUsuario.Services;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Carlos 20/11/26: la pestaña «No recogidas» de «Recogidas solicitadas»: las recogidas que el repartidor marcó «No se ha recogido»
    /// y Almacén decide si se vuelven a mandar (Reprogramar, fecha opcional) o se cierran del todo (Anular con motivo).
    /// </summary>
    [TestClass]
    public class RecogidasNoRecogidasViewModelTests
    {
        private IServicioRecogidasReparto _servicio = null!;
        private IServicioDialogos _dialogos = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioRecogidasReparto>();
            _dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => _servicio.LeerNoRecogidas()).ReturnsLazily(() => Task.FromResult(new List<RecogidaNoRecogidaModel> { NoRecogida(930553), NoRecogida(930600) }));
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).Returns(true);
        }

        private RecogidasNoRecogidasViewModel Crear(bool puedeGestionar = true) => new RecogidasNoRecogidasViewModel(_servicio, _dialogos, puedeGestionar);

        private static RecogidaNoRecogidaModel NoRecogida(int pedido) => new RecogidaNoRecogidaModel
        {
            Pedido = pedido,
            CodigoDocumento = $"NR{pedido}",
            EnvioId = 251104,
            Cliente = "15191",
            Contacto = "0",
            Nombre = "SALÓN DE PRUEBA",
            Direccion = "C/ Mayor 1",
            Poblacion = "Madrid",
            Motivo = "Cerrado, no había nadie",
            Repartidor = "Andre",
            FechaHora = new DateTime(2026, 11, 20, 9, 42, 0)
        };

        [TestMethod]
        public async Task Cargar_EnsenaLaListaConContadorEnLaCabecera()
        {
            var vm = Crear();

            await vm.CargarAsync();

            Assert.AreEqual(2, vm.Lista.Count);
            Assert.AreEqual("No recogidas (2)", vm.Cabecera);
            Assert.AreEqual(930553, vm.Seleccionada!.Pedido);
            Assert.AreEqual("Andre el 20/11/26 9:42", vm.Seleccionada.QuienCuando);
            Assert.AreEqual("15191/0 SALÓN DE PRUEBA", vm.Seleccionada.ClienteTexto);
            Assert.AreEqual("C/ Mayor 1, Madrid", vm.Seleccionada.DireccionTexto);
        }

        [TestMethod]
        public async Task Cargar_ServidorSinActualizar_LoDiceYNoDejaHacerNada()
        {
            A.CallTo(() => _servicio.LeerNoRecogidas()).ThrowsAsync(ErroresApiReparto.Excepcion(404, "", null));
            var vm = Crear();

            await vm.CargarAsync();

            Assert.AreEqual(ErroresApiReparto.MENSAJE_SERVIDOR_SIN_ACTUALIZAR, vm.Aviso);
            Assert.IsFalse(vm.Disponible);
            Assert.AreEqual(0, vm.Lista.Count);
            Assert.AreEqual("No recogidas", vm.Cabecera);
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task VolverAMandar_SinFecha_ReprogramaSinFechaYRelee()
        {
            var vm = Crear();
            await vm.CargarAsync();
            A.CallTo(() => _servicio.Reprogramar(930553, null)).Returns(Task.FromResult(new ReprogramacionRecogidaModel
            {
                Pedido = 930553, Mensaje = "La recogida NR930553 vuelve al reparto el lunes 23/11."
            }));

            await vm.VolverAMandarAsync();

            A.CallTo(() => _servicio.Reprogramar(930553, null)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("La recogida NR930553 vuelve al reparto el lunes 23/11.", vm.Mensaje);
            A.CallTo(() => _servicio.LeerNoRecogidas()).MustHaveHappenedTwiceExactly();
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(RecogidasNoRecogidasViewModel.TITULO_VOLVER_A_MANDAR,
                A<string>.That.Contains("el siguiente día de reparto del cliente"))).MustHaveHappened();
        }

        [TestMethod]
        public async Task VolverAMandar_ConFecha_LaManda()
        {
            var vm = Crear();
            await vm.CargarAsync();
            DateTime dia = DateTime.Today.AddDays(3);
            vm.FechaVolverAMandar = dia;

            await vm.VolverAMandarAsync();

            A.CallTo(() => _servicio.Reprogramar(930553, dia)).MustHaveHappenedOnceExactly();
            Assert.IsNull(vm.FechaVolverAMandar, "se vacía para la siguiente");
        }

        [TestMethod]
        public async Task VolverAMandar_FechaPasada_NoLlama()
        {
            var vm = Crear();
            await vm.CargarAsync();
            vm.FechaVolverAMandar = DateTime.Today.AddDays(-1);

            await vm.VolverAMandarAsync();

            A.CallTo(() => _servicio.Reprogramar(A<int>._, A<DateTime?>._)).MustNotHaveHappened();
            Assert.AreEqual("La fecha no puede ser anterior a hoy.", vm.Mensaje);
        }

        [TestMethod]
        public async Task VolverAMandar_409_SuMensajeYRelee()
        {
            var vm = Crear();
            await vm.CargarAsync();
            A.CallTo(() => _servicio.Reprogramar(A<int>._, A<DateTime?>._))
                .ThrowsAsync(ErroresApiReparto.Excepcion(409, "{\"Message\":\"La recogida NR930553 ya no está pendiente de decidir.\"}", null));

            await vm.VolverAMandarAsync();

            A.CallTo(() => _dialogos.ShowError("La recogida NR930553 ya no está pendiente de decidir.")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.LeerNoRecogidas()).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task VolverAMandar_503_ServidorSinActualizar()
        {
            var vm = Crear();
            await vm.CargarAsync();
            A.CallTo(() => _servicio.Reprogramar(A<int>._, A<DateTime?>._)).ThrowsAsync(ErroresApiReparto.Excepcion(503, null, null));

            await vm.VolverAMandarAsync();

            A.CallTo(() => _dialogos.ShowError(ErroresApiReparto.MENSAJE_SERVIDOR_SIN_ACTUALIZAR)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.Disponible);
            Assert.IsFalse(vm.VolverAMandarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task CerrarDelTodo_ConMotivo_AnulaYRelee()
        {
            var vm = Crear();
            await vm.CargarAsync();
            A.CallTo(() => _dialogos.GetText(AnuladorRecogidas.TITULO, A<string>._)).Returns("  El cliente ya no la quiere  ");
            A.CallTo(() => _servicio.Anular(930553, "El cliente ya no la quiere")).Returns(Task.FromResult(new AnulacionRecogidaModel
            {
                Pedido = 930553, CodigoDocumento = "NR930553", EnvioId = 251104, Estado = "Anulada", Mensaje = "Recogida NR930553 anulada."
            }));

            await vm.CerrarDelTodoAsync();

            A.CallTo(() => _servicio.Anular(930553, "El cliente ya no la quiere")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Recogida NR930553 anulada.", vm.Mensaje);
            A.CallTo(() => _servicio.LeerNoRecogidas()).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task CerrarDelTodo_SinMotivo_NoAnula()
        {
            var vm = Crear();
            await vm.CargarAsync();
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._)).Returns("   ");

            await vm.CerrarDelTodoAsync();

            A.CallTo(() => _servicio.Anular(A<int>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _dialogos.ShowError(AnuladorRecogidas.MENSAJE_FALTA_MOTIVO)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SinGrupo_VeLaListaPeroNoActua()
        {
            var vm = Crear(puedeGestionar: false);
            await vm.CargarAsync();

            Assert.AreEqual(2, vm.Lista.Count);
            Assert.IsFalse(vm.VolverAMandarCommand.CanExecute(null));
            Assert.IsFalse(vm.CerrarDelTodoCommand.CanExecute(null));
            Assert.AreEqual(RecogidasNoRecogidasViewModel.MENSAJE_SIN_GRUPO, vm.Aviso);
        }

        [TestMethod]
        public async Task VentanaRecogidas_DesdeAgencias_AbreLaPestanaYLasLee()
        {
            var solicitadas = A.Fake<IServicioRecogidasSolicitadas>();
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)).Returns(true);
            var vm = new RecogidasSolicitadasViewModel(solicitadas, configuracion, _dialogos, (IGruposAlmacenTiempoReal)null!, _servicio);

            vm.AlLlegar(new ParametrosNavegacion { { NotificacionBuzon.PARAMETRO_RECOGIDAS_PESTANA, NotificacionBuzon.PESTANA_RECOGIDAS_NO_RECOGIDAS } });
            await vm.LlegadaEnCurso;

            Assert.AreEqual(RecogidasSolicitadasViewModel.PESTANA_NO_RECOGIDAS, vm.PestanaSeleccionada);
            Assert.AreEqual(2, vm.NoRecogidas.Lista.Count);
            Assert.IsTrue(vm.NoRecogidas.PuedeGestionar);
        }

        [TestMethod]
        public void ServicioRecogidasReparto_RutasYCuerpos()
        {
            Assert.AreEqual("RepartoPropio/Recogidas/930553/Anular", ServicioRecogidasReparto.RutaAnular(930553));
            Assert.AreEqual("RepartoPropio/Recogidas/930553/Reprogramar", ServicioRecogidasReparto.RutaReprogramar(930553));
            Assert.AreEqual("RepartoPropio/Recogidas/NoRecogidas", ServicioRecogidasReparto.RUTA_NO_RECOGIDAS);
            Assert.AreEqual("{\"Motivo\":\"ya no\"}", ServicioRecogidasReparto.CuerpoAnular("ya no"));
            Assert.AreEqual("{\"Fecha\":null}", ServicioRecogidasReparto.CuerpoReprogramar(null));
            Assert.AreEqual("{\"Fecha\":\"2026-11-23\"}", ServicioRecogidasReparto.CuerpoReprogramar(new DateTime(2026, 11, 23)));
        }
    }
}
