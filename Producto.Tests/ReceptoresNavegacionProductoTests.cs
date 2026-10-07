using FakeItEasy;
using Nesto.Infrastructure.Contracts;
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
    }
}
