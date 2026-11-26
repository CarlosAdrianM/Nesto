using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;
using System.Net;

namespace Producto.Tests
{
    /// <summary>NestoAPI#883 (corte 1): la ventana «Fechas de inventario».</summary>
    [TestClass]
    public class FechasInventarioViewModelTests
    {
        private IServicioFechasInventario _servicio = null!;
        private IConfiguracion _configuracion = null!;
        private IServicioDialogos _dialogos = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioFechasInventario>();
            _configuracion = A.Fake<IConfiguracion>();
            _dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)).Returns(true);
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
            A.CallTo(() => _servicio.Leer(A<string>._)).ReturnsLazily(() => Task.FromResult(new List<FechaInventario> { Fecha(7, pendientes: 2) }));
        }

        private static ReposicionAfectadaInventario Reposicion(string estado = ReposicionAfectadaInventario.PENDIENTE, bool sePuedeQuitar = true) => new ReposicionAfectadaInventario
        {
            Origen = "REI",
            Destino = "ALG",
            FechaLlegada = new DateTime(2026, 11, 20),
            CierraEl = new DateTime(2026, 11, 20, 9, 0, 0),
            Motivo = "Llega",
            Estado = estado,
            SePuedeQuitar = sePuedeQuitar,
            Texto = "Reposición Reina → Algete, llega el viernes 20/11 (cierra el viernes 20/11 a las 9:00)"
        };

        private static FechaInventario Fecha(int id, int pendientes = 1, string estado = ReposicionAfectadaInventario.PENDIENTE) => new FechaInventario
        {
            Id = id,
            Empresa = "1",
            Almacen = "ALG",
            Fecha = new DateTime(2026, 11, 21),
            DiaAviso = new DateTime(2026, 11, 18),
            Pendientes = pendientes,
            Reposiciones = new List<ReposicionAfectadaInventario> { Reposicion(estado) }
        };

        private FechasInventarioViewModel Ventana() => new FechasInventarioViewModel(_servicio, _configuracion, _dialogos) { Hoy = () => new DateTime(2026, 11, 16) };

        [TestMethod]
        public void SinGrupo_NoSePuedeUsar()
        {
            var vm = new FechasInventarioViewModel(_servicio, A.Fake<IConfiguracion>(), _dialogos);

            Assert.IsFalse(vm.PuedeUsar);
            Assert.AreEqual(FechasInventarioViewModel.MENSAJE_SIN_GRUPO, vm.Mensaje);
            Assert.IsFalse(vm.CargarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task AlCargar_EnsenaLasFechasYSusReposiciones()
        {
            var vm = Ventana();

            await vm.CargarAsync();

            Assert.AreEqual(1, vm.Fechas.Count);
            Assert.AreEqual("Algete", vm.Seleccionada!.NombreAlmacen);
            Assert.AreEqual("Sale el 18/11", vm.Seleccionada.Aviso);
            Assert.AreEqual(1, vm.Seleccionada.Reposiciones.Count);
            Assert.IsTrue(vm.QuitarCommand.CanExecute(vm.Seleccionada.Reposiciones[0]));
            Assert.IsTrue(vm.MantenerCommand.CanExecute(vm.Seleccionada.Reposiciones[0]));
        }

        [TestMethod]
        public void Guardar_SinDiaOConDiaPasado_NoLlamaYLoDice()
        {
            Assert.AreEqual(FechasInventarioViewModel.MENSAJE_FALTA_DIA, FechasInventarioViewModel.MotivoNoSePuedeGuardar("ALG", null, null, DateTime.Today));
            Assert.AreEqual(FechasInventarioViewModel.MENSAJE_DIA_PASADO,
                FechasInventarioViewModel.MotivoNoSePuedeGuardar("ALG", new DateTime(2026, 11, 15), null, new DateTime(2026, 11, 16)));
            Assert.AreEqual(FechasInventarioViewModel.MENSAJE_FALTA_ALMACEN, FechasInventarioViewModel.MotivoNoSePuedeGuardar("XXX", DateTime.Today, null, DateTime.Today));
            Assert.IsNull(FechasInventarioViewModel.MotivoNoSePuedeGuardar("REI", DateTime.Today, "Inventario anual", DateTime.Today));
        }

        [TestMethod]
        public async Task Guardar_MandaLaFechaYLaPoneEnLaLista()
        {
            FechaInventario? enviada = null;
            A.CallTo(() => _servicio.Guardar(A<FechaInventario>._)).ReturnsLazily((FechaInventario f) =>
            {
                enviada = f;
                return Task.FromResult(Fecha(9, pendientes: 1));
            });
            var vm = Ventana();
            vm.NuevoAlmacen = "ALG";
            vm.NuevoDia = new DateTime(2026, 11, 21, 15, 30, 0);
            vm.NuevoComentario = " Inventario anual ";

            await vm.GuardarAsync();

            Assert.AreEqual("ALG", enviada!.Almacen);
            Assert.AreEqual(new DateTime(2026, 11, 21), enviada.Fecha);
            Assert.AreEqual("Inventario anual", enviada.Comentario);
            Assert.AreEqual(9, vm.Seleccionada!.Id);
            Assert.AreEqual("Apuntado el inventario de Algete del 21/11/2026. Queda 1 reposición por decidir.", vm.Mensaje);
        }

        [TestMethod]
        public async Task Quitar_MandaLaDecisionDeEseViaje_YActualizaLaFecha()
        {
            DecisionReposicionInventario? decision = null;
            A.CallTo(() => _servicio.Decidir(7, A<DecisionReposicionInventario>._)).ReturnsLazily((int id, DecisionReposicionInventario d) =>
            {
                decision = d;
                return Task.FromResult(Fecha(7, pendientes: 0, estado: ReposicionAfectadaInventario.QUITADA));
            });
            var vm = Ventana();
            await vm.CargarAsync();
            var reposicion = vm.Seleccionada!.Reposiciones[0];

            await vm.QuitarCommand.ExecuteAsync(reposicion);

            Assert.AreEqual("REI", decision!.Origen);
            Assert.AreEqual("ALG", decision.Destino);
            Assert.AreEqual(new DateTime(2026, 11, 20), decision.FechaLlegada);
            Assert.AreEqual(DecisionReposicionInventario.QUITAR, decision.Decision);
            Assert.AreEqual(ReposicionAfectadaInventario.QUITADA, vm.Seleccionada!.Reposiciones[0].Estado);
            Assert.AreEqual(1, vm.Fechas.Count, "Sustituye a la que había");
            StringAssert.EndsWith(vm.Mensaje, "No queda ninguna reposición por decidir.");
        }

        [TestMethod]
        public async Task SinElScript_DiceQueNoEstaActivado()
        {
            A.CallTo(() => _servicio.Leer(A<string>._)).ThrowsAsync(
                new FechasInventarioApiException("Falta el script Issue883_InventariosFechas.sql en la base de datos.", HttpStatusCode.ServiceUnavailable));
            var vm = Ventana();

            await vm.CargarAsync();

            Assert.AreEqual($"{FechasInventarioViewModel.MENSAJE_SIN_SCRIPT} Falta el script Issue883_InventariosFechas.sql en la base de datos.", vm.Mensaje);
        }

        [TestMethod]
        public async Task UnErrorDeLaApiAlDecidir_SeEnsenaTalCual()
        {
            A.CallTo(() => _servicio.Decidir(A<int>._, A<DecisionReposicionInventario>._))
                .ThrowsAsync(new FechasInventarioApiException("Ese viaje ya ha cerrado: no se puede quitar.", HttpStatusCode.Conflict));
            var vm = Ventana();
            await vm.CargarAsync();

            await vm.QuitarCommand.ExecuteAsync(vm.Seleccionada!.Reposiciones[0]);

            Assert.AreEqual("Ese viaje ya ha cerrado: no se puede quitar.", vm.Mensaje);
        }

        [TestMethod]
        public async Task Borrar_PideConfirmacionYLaQuita()
        {
            var vm = Ventana();
            await vm.CargarAsync();

            await vm.BorrarAsync();

            A.CallTo(() => _servicio.Borrar(7)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, vm.Fechas.Count);
        }

        [TestMethod]
        public void UnaQuitadaNoSePuedeQuitarNiMantener()
        {
            var fila = new FilaReposicionInventario(Reposicion(ReposicionAfectadaInventario.QUITADA, sePuedeQuitar: false));

            Assert.IsFalse(fila.SePuedeQuitar);
            Assert.IsFalse(fila.SePuedeMantener);
        }

        [TestMethod]
        public void ElMenu_SoloParaAlmacenDireccionEInformatica()
        {
            var navegacion = A.Fake<IServicioNavegacion>();
            var menu = new ProductoMenuBarViewModel(navegacion, _configuracion, null!);

            Assert.IsTrue(menu.AbrirFechasInventarioCommand.CanExecute(null));
            menu.AbrirFechasInventarioCommand.Execute(null);
            A.CallTo(() => navegacion.RequestNavigate("MainRegion", ProductoMenuBarViewModel.VISTA_FECHAS_INVENTARIO)).MustHaveHappenedOnceExactly();

            var tienda = new ProductoMenuBarViewModel(navegacion, A.Fake<IConfiguracion>(), null!);
            Assert.IsFalse(tienda.AbrirFechasInventarioCommand.CanExecute(null));
        }
    }
}
