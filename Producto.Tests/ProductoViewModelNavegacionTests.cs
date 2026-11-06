using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using System.Threading;
using System.Windows.Controls;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4): la ficha de productos navega y se cierra con IServicioNavegacion en vez del
    /// IRegionManager de Prism: mismas regiones, vistas y claves que antes.
    /// </summary>
    [TestClass]
    public class ProductoViewModelNavegacionTests
    {
        private IServicioNavegacion _navegacion = null!;

        [TestInitialize]
        public void Inicializar() => _navegacion = A.Fake<IServicioNavegacion>();

        private ProductoViewModel Nuevo() => new ProductoViewModel(_navegacion, A.Fake<IConfiguracion>(), A.Fake<IProductoService>(),
            new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IServicioAutenticacion>());

        [TestMethod]
        public void AbrirModulo_NavegaALaFichaDeProductos()
        {
            Nuevo().AbrirModuloCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "ProductoView")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AbrirProducto_NavegaConElNumeroDelProducto()
        {
            ParametrosNavegacion? recibidos = null;
            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "ProductoView", A<ParametrosNavegacion>._))
                .Invokes((string _, string _, ParametrosNavegacion p) => recibidos = p);

            Nuevo().AbrirProductoCommand.Execute("17404");

            Assert.IsNotNull(recibidos);
            Assert.AreEqual("17404", recibidos!.GetValue<string>("numeroProductoParameter"));
        }

        [TestMethod]
        public void SeleccionarProducto_SinVistaActiva_NoCierraNadaNiRevienta()
        {
            var vm = Nuevo();
            vm.ProductoResultadoSeleccionado = new ProductoModel { Producto = "17404" };
            A.CallTo(() => _navegacion.VistaActiva("MainRegion")).Returns(null);

            vm.SeleccionarProductoCommand.Execute(null);

            A.CallTo(() => _navegacion.CerrarVistaActiva(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void SeleccionarProducto_ConLaFichaDeProductosActiva_LaCierra()
        {
            // Como antes: la vista activa es la de Productos (su contenido lleva este ViewModel) → se cierra
            EnHiloSta(() =>
            {
                var vm = Nuevo();
                vm.ProductoResultadoSeleccionado = new ProductoModel { Producto = "17404" };
                var vista = new UserControl { Content = new Grid { DataContext = vm } };
                A.CallTo(() => _navegacion.VistaActiva("MainRegion")).Returns(vista);

                vm.SeleccionarProductoCommand.Execute(null);

                A.CallTo(() => _navegacion.CerrarVistaActiva("MainRegion")).MustHaveHappenedOnceExactly();
            });
        }

        [TestMethod]
        public void SeleccionarProducto_ConOtraPantallaActiva_NoLaCierra()
        {
            EnHiloSta(() =>
            {
                var vm = Nuevo();
                vm.ProductoResultadoSeleccionado = new ProductoModel { Producto = "17404" };
                var vista = new UserControl { Content = new Grid { DataContext = new object() } };
                A.CallTo(() => _navegacion.VistaActiva("MainRegion")).Returns(vista);

                vm.SeleccionarProductoCommand.Execute(null);

                A.CallTo(() => _navegacion.CerrarVistaActiva(A<string>._)).MustNotHaveHappened();
            });
        }

        // Nesto#628 (sugerencia 1106): el número «Pendientes de Entregar» de un almacén abre Pendientes de entregar filtrado

        private static ProductoModel ProductoConPendientes(int pendientesAlg) => new ProductoModel
        {
            Producto = "40442 ",
            Stocks = new List<ProductoModel.StockProducto>
            {
                new ProductoModel.StockProducto { Almacen = "ALG", PendienteEntregar = pendientesAlg },
                new ProductoModel.StockProducto { Almacen = "REI", PendienteEntregar = 0 }
            }
        };

        [TestMethod]
        public void AbrirPendientesEntregar_NavegaConElProductoYElAlmacen()
        {
            ParametrosNavegacion? recibidos = null;
            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", NavegacionPendientesEntregar.NOMBRE_VISTA, A<ParametrosNavegacion>._))
                .Invokes((string _, string _, ParametrosNavegacion p) => recibidos = p);
            var vm = Nuevo();
            vm.ProductoActual = ProductoConPendientes(1);

            vm.AbrirPendientesEntregarCommand.Execute(vm.ProductoActual.Stocks.First());

            Assert.IsNotNull(recibidos);
            Assert.AreEqual("40442", recibidos!.GetValue<string>(NavegacionPendientesEntregar.PARAMETRO_PRODUCTO));
            Assert.AreEqual("ALG", recibidos.GetValue<string>(NavegacionPendientesEntregar.PARAMETRO_ALMACEN));
        }

        [TestMethod]
        public void AbrirPendientesEntregar_ConCeroPendientes_NoEstaHabilitadoNiNavega()
        {
            var vm = Nuevo();
            vm.ProductoActual = ProductoConPendientes(0);
            var alg = vm.ProductoActual.Stocks.First();

            Assert.IsFalse(vm.AbrirPendientesEntregarCommand.CanExecute(alg));
            Assert.IsFalse(vm.AbrirPendientesEntregarCommand.CanExecute(null));
            vm.AbrirPendientesEntregarCommand.Execute(alg);
            A.CallTo(() => _navegacion.RequestNavigate(A<string>._, A<string>._, A<ParametrosNavegacion>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void AbrirPendientesEntregar_ConPendientes_EstaHabilitado()
        {
            var vm = Nuevo();
            vm.ProductoActual = ProductoConPendientes(3);

            Assert.IsTrue(vm.AbrirPendientesEntregarCommand.CanExecute(vm.ProductoActual.Stocks.First()));
        }

        private static void EnHiloSta(Action accion)
        {
            Exception? error = null;
            var hilo = new Thread(() =>
            {
                try { accion(); }
                catch (Exception ex) { error = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null)
            {
                throw new AssertFailedException(error.Message, error);
            }
        }
    }
}
