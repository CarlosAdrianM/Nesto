using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4): Vídeos y la ficha de producto reciben la navegación por <see cref="IReceptorNavegacionPestanaNueva"/>
    /// en vez de INavigationAware: cada navegación abre una pestaña nueva (antes IsNavigationTarget = false) y
    /// <c>AlLlegar</c> hace lo que hacía <c>OnNavigatedTo</c>, con las mismas claves de parámetros.
    /// </summary>
    [TestClass]
    public class ReceptoresNavegacionProductoTests
    {
        private IProductoService _servicio = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IProductoService>();
            A.CallTo(() => _servicio.CargarVideos(A<int>._, A<int>._)).Returns(Task.FromResult(new List<VideoLookupModel>()));
        }

        [TestMethod]
        public void Videos_NoDependeDeLaNavegacionDePrismYAbrePestanaNueva()
        {
            Assert.IsFalse(typeof(Prism.Regions.INavigationAware).IsAssignableFrom(typeof(VideosViewModel)));
            Assert.IsTrue(typeof(IReceptorNavegacionPestanaNueva).IsAssignableFrom(typeof(VideosViewModel)));
        }

        [TestMethod]
        public void Videos_AlLlegar_CargaLaPrimeraPagina()
        {
            var vm = new VideosViewModel(_servicio, A.Fake<IServicioDialogos>(), A.Fake<IConfiguracion>(), A.Fake<IServicioNavegacion>());

            vm.AlLlegar(new ParametrosNavegacion());

            A.CallTo(() => _servicio.CargarVideos(0, A<int>._)).MustHaveHappenedOnceExactly();
        }

        private ProductoViewModel NuevoProducto(IConfiguracion configuracion) => new ProductoViewModel(A.Fake<IServicioNavegacion>(),
            configuracion, _servicio, new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IServicioAutenticacion>());

        [TestMethod]
        public void Producto_NoDependeDeLaNavegacionDePrismYAbrePestanaNueva()
        {
            Assert.IsFalse(typeof(Prism.Regions.INavigationAware).IsAssignableFrom(typeof(ProductoViewModel)));
            Assert.IsTrue(typeof(IReceptorNavegacionPestanaNueva).IsAssignableFrom(typeof(ProductoViewModel)));
        }

        [TestMethod]
        public void Producto_AlLlegarConNumeroDeProducto_LoBusca()
        {
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.UltNumProducto)).Returns(Task.FromResult("99999"));
            var vm = NuevoProducto(configuracion);

            vm.AlLlegar(new ParametrosNavegacion { { "numeroProductoParameter", "17404" } });

            Assert.AreEqual("17404", vm.ReferenciaBuscar);
        }

        [TestMethod]
        public void Producto_AlLlegarSinParametros_BuscaElUltimoProductoDelUsuario()
        {
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.UltNumProducto)).Returns(Task.FromResult("99999"));
            var vm = NuevoProducto(configuracion);

            vm.AlLlegar(new ParametrosNavegacion());

            Assert.AreEqual("99999", vm.ReferenciaBuscar);
        }

        [TestMethod]
        public void Producto_AlLlegarConBusquedaContextual_BuscaPorNombreYNoCargaProducto()
        {
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.UltNumProducto)).Returns(Task.FromResult("99999"));
            var vm = NuevoProducto(configuracion);

            vm.AlLlegar(new ParametrosNavegacion { { "busquedaContextualParameter", "Alta Frecuencia" } });

            Assert.AreEqual("Alta Frecuencia", vm.FiltroNombre);
            Assert.IsNull(vm.ReferenciaBuscar);
        }
    }
}
