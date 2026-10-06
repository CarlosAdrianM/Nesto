using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// La tienda prepara la reposición que manda a Algete y la termina (NestoAPI#553), en lugar de hacerlo en Nesto
    /// viejo: el servidor propone, aquí solo se baja lo que no se manda y se termina.
    /// </summary>
    [TestClass]
    public class EnviarReposicionViewModelTests
    {
        private IServicioEnvioReposiciones _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private IConfiguracion _configuracion = null!;
        private EnviarReposicionViewModel _vm = null!;

        private static ReposicionEnPreparacion Reposicion(int cantidadCera = 3) => new()
        {
            Empresa = "1",
            Origen = "ALC",
            Destino = "ALG",
            Lineas = new List<LineaReposicionEnPreparacion>
            {
                new() { NumeroOrden = 101, Producto = "17404", Nombre = "Cera", CodigoBarras = "8411", Cantidad = cantidadCera, StockOrigen = 7 },
                new() { NumeroOrden = 102, Producto = "40057", Nombre = "Banda", CodigoBarras = "8422", Cantidad = 2, StockOrigen = 4 }
            }
        };

        [TestInitialize]
        public void Preparar()
        {
            _servicio = A.Fake<IServicioEnvioReposiciones>();
            _dialogos = A.Fake<IServicioDialogos>();
            _configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("alc ");
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Reposicion());
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);
            _vm = new EnviarReposicionViewModel(_servicio, _dialogos, _configuracion);
        }

        private async Task CambiarCantidadAsync(int fila, int cantidad)
        {
            _vm.Lineas[fila].Cantidad = cantidad;
            if (_vm.CambiarCantidadCommand.ExecutionTask != null)
            {
                await _vm.CambiarCantidadCommand.ExecutionTask;
            }
        }

        [TestMethod]
        public async Task Cargar_ConUnaEnPreparacion_LaEnsena()
        {
            await _vm.CargarAsync();

            Assert.AreEqual("ALC", _vm.Almacen);
            Assert.IsTrue(_vm.HayReposicion);
            Assert.IsFalse(_vm.PuedePreparar);
            Assert.AreEqual(2, _vm.Lineas.Count);
            Assert.AreEqual(5, _vm.Unidades);
            Assert.AreEqual(2, _vm.Productos);
            Assert.AreEqual(7, _vm.Lineas[0].StockOrigen);
            Assert.IsTrue(_vm.TerminarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Cargar_SinNingunaEnPreparacion_DejaPreparar()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));

            await _vm.CargarAsync();

            Assert.IsFalse(_vm.HayReposicion);
            Assert.IsTrue(_vm.PuedePreparar);
            Assert.IsTrue(_vm.PrepararCommand.CanExecute(null));
            Assert.IsFalse(_vm.TerminarCommand.CanExecute(null));
            StringAssert.Contains(_vm.Mensaje, "Preparar reposición");
        }

        [TestMethod]
        public async Task Cargar_DesdeAlgete_RemiteAAriadnaYNoPideNada()
        {
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("ALG");

            await _vm.CargarAsync();

            Assert.AreEqual("Las reposiciones desde Algete se hacen en Ariadna.", _vm.Mensaje);
            Assert.IsFalse(_vm.PuedePreparar);
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cargar_SinAlmacenDelUsuario_LoDiceYNoPideNada()
        {
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns(" ");

            await _vm.CargarAsync();

            StringAssert.Contains(_vm.Mensaje, "almacén");
            Assert.IsFalse(_vm.PuedePreparar);
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Preparar_PideLaPropuestaDeLaTiendaAAlgeteYLaEnsena()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            CrearReposicion? pedida = null;
            A.CallTo(() => _servicio.Crear(A<CrearReposicion>._))
                .ReturnsLazily((CrearReposicion c) => { pedida = c; return Task.FromResult(Reposicion()); });
            await _vm.CargarAsync();

            await _vm.PrepararCommand.ExecuteAsync(null);

            Assert.IsNotNull(pedida);
            Assert.AreEqual("ALC", pedida!.Origen);
            Assert.AreEqual("ALG", pedida.Destino);
            Assert.IsNull(pedida.Lineas, "Sin líneas: la propuesta la calcula el servidor");
            Assert.IsTrue(_vm.HayReposicion);
            Assert.AreEqual(2, _vm.Lineas.Count);
        }

        [TestMethod]
        public async Task Preparar_ConConflicto_EnsenaElTextoDelServidorEnElDialogo()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            const string motivo = "El almacén ALC tiene un inventario en curso: termínalo antes de crear una reposición.";
            A.CallTo(() => _servicio.Crear(A<CrearReposicion>._)).Throws(new EnvioReposicionException(motivo));
            await _vm.CargarAsync();

            await _vm.PrepararCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowError(motivo)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(motivo, _vm.Mensaje);
            Assert.IsFalse(_vm.HayReposicion);
            Assert.IsTrue(_vm.PuedePreparar);
        }

        [TestMethod]
        public async Task BajarCantidad_LaMandaAlServidorYPoneLoQueContesta()
        {
            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, "ALC", 101, 0)).Returns(Reposicion(cantidadCera: 0));
            await _vm.CargarAsync();

            await CambiarCantidadAsync(0, 0);

            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, "ALC", 101, 0)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, _vm.Lineas[0].Preparada);
            Assert.IsTrue(_vm.Lineas[0].EstaACero, "Se queda en gris y se borra al terminar");
            Assert.AreEqual(2, _vm.Unidades);
            Assert.AreEqual(1, _vm.Productos);
        }

        [TestMethod]
        public async Task SubirCantidad_NoLlamaALaApiYVuelveALaPreparada()
        {
            await _vm.CargarAsync();

            await CambiarCantidadAsync(0, 5);

            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, A<string>._, A<int>._, A<int>._)).MustNotHaveHappened();
            Assert.AreEqual(3, _vm.Lineas[0].Cantidad);
            StringAssert.Contains(_vm.Mensaje, "Solo se puede bajar");
        }

        [TestMethod]
        public async Task BajarCantidad_SiElServidorNoLaAcepta_VuelveALaPreparadaYEnsenaElMotivo()
        {
            const string motivo = "La línea 101 ha cambiado mientras tanto: vuelve a cargar la reposición.";
            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, A<string>._, A<int>._, A<int>._)).Throws(new EnvioReposicionException(motivo));
            await _vm.CargarAsync();

            await CambiarCantidadAsync(0, 1);

            Assert.AreEqual(3, _vm.Lineas[0].Cantidad);
            A.CallTo(() => _dialogos.ShowError(motivo)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Leer_UnCodigoDeLaLista_SeleccionaSuLineaYPideElFocoEnLaCantidad()
        {
            await _vm.CargarAsync();
            LineaEnviarReposicion? enfocada = null;
            _vm.PedirFocoEnCantidad += l => enfocada = l;

            _vm.Lectura = " 8422 ";
            _vm.LeerLecturaCommand.Execute(null);

            Assert.AreSame(_vm.Lineas[1], _vm.Seleccionada);
            Assert.AreSame(_vm.Lineas[1], enfocada);
            Assert.AreEqual(string.Empty, _vm.Lectura);
        }

        [TestMethod]
        public async Task Leer_UnCodigoQueNoEsta_LoDice()
        {
            await _vm.CargarAsync();
            var pedido = false;
            _vm.PedirFocoEnCantidad += _ => pedido = true;

            _vm.LeerCommand.Execute("55555");

            Assert.IsFalse(pedido);
            StringAssert.Contains(_vm.Mensaje, "no está");
        }

        [TestMethod]
        public async Task Terminar_PideConfirmacionConElResumenYVuelveAPreparar()
        {
            A.CallTo(() => _servicio.Terminar(A<string>._, "ALC")).Returns(new ResultadoTerminarReposicion
            {
                NumTraspaso = 80999,
                Lineas = new List<LineaTraspasoTerminado> { new() { Producto = "17404", Cantidad = 3 }, new() { Producto = "40057", Cantidad = 2 } }
            });
            await _vm.CargarAsync();

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, "Se mandan 5 unidades de 2 productos a Algete. ¿Terminar?"))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.Terminar(A<string>._, "ALC")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _dialogos.ShowNotification(A<string>._, A<string>.That.Contains("80999"))).MustHaveHappenedOnceExactly();
            StringAssert.Contains(_vm.Mensaje, "80999");
            Assert.AreEqual(0, _vm.Lineas.Count);
            Assert.IsFalse(_vm.HayReposicion);
            Assert.IsTrue(_vm.PuedePreparar);
        }

        [TestMethod]
        public async Task Terminar_ConLineasACero_NoLasCuentaEnLaConfirmacion()
        {
            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, "ALC", 101, 0)).Returns(Reposicion(cantidadCera: 0));
            await _vm.CargarAsync();
            await CambiarCantidadAsync(0, 0);

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, "Se mandan 2 unidades de 1 productos a Algete. ¿Terminar?"))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Terminar_SiNoSeConfirma_NoMandaNada()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(false);
            await _vm.CargarAsync();

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.Terminar(A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual(2, _vm.Lineas.Count);
        }

        [TestMethod]
        public async Task Terminar_SiFallaEnElServidor_EnsenaSuTextoYSigueLaReposicion()
        {
            const string motivo = "El producto 17404 quedaría con stock negativo en ALC.";
            A.CallTo(() => _servicio.Terminar(A<string>._, A<string>._)).Throws(new EnvioReposicionException(motivo));
            await _vm.CargarAsync();

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowError(motivo)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(motivo, _vm.Mensaje);
            Assert.AreEqual(2, _vm.Lineas.Count);
            Assert.IsTrue(_vm.HayReposicion);
        }

        [TestMethod]
        public async Task Terminar_ConTodoACero_NoPreguntaNiLlamaALaApi()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(new ReposicionEnPreparacion
            {
                Lineas = new List<LineaReposicionEnPreparacion> { new() { NumeroOrden = 101, Producto = "17404", Cantidad = 0 } }
            });
            await _vm.CargarAsync();

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _servicio.Terminar(A<string>._, A<string>._)).MustNotHaveHappened();
            StringAssert.Contains(_vm.Mensaje, "a 0");
        }
    }
}
