using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using Prism.Regions;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4): las pantallas de vídeos abren la ficha de productos. Escritas contra el
    /// IRegionManager de Prism ANTES de migrar la navegación, para que la migración no cambie nada.
    /// </summary>
    [TestClass]
    public class ProductoNavegacionTests
    {
        private IRegionManager _navegacion = null!;
        private IProductoService _servicio = null!;
        private NavigationParameters? _recibidos;

        [TestInitialize]
        public void Inicializar()
        {
            _navegacion = A.Fake<IRegionManager>();
            _servicio = A.Fake<IProductoService>();
            A.CallTo(() => _servicio.CargarVideos(A<int>._, A<int>._)).Returns(Task.FromResult(new List<VideoLookupModel>()));
            _recibidos = null;
            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "ProductoView", A<NavigationParameters>._))
                .Invokes((string _, string _, NavigationParameters p) => _recibidos = p);
        }

        [TestMethod]
        public void CorreccionVideoProducto_AbrirProductosConBusqueda_AbreProductosBuscandoElNombre()
        {
            var vm = new CorreccionVideoProductoViewModel(_servicio, _navegacion);

            vm.AbrirProductosConBusquedaCommand.Execute("Alta Frecuencia");

            Assert.IsNotNull(_recibidos);
            Assert.AreEqual("Alta Frecuencia", _recibidos!.GetValue<string>("busquedaContextualParameter"));
        }

        [TestMethod]
        public void Videos_AbrirProducto_AbreLaFichaDelProducto()
        {
            var vm = new VideosViewModel(_servicio, A.Fake<IServicioDialogos>(), A.Fake<IConfiguracion>(), _navegacion);

            vm.AbrirProductoCommand.Execute("12345");

            Assert.IsNotNull(_recibidos);
            Assert.AreEqual("12345", _recibidos!.GetValue<string>("numeroProductoParameter"));
        }

        [TestMethod]
        public void Videos_AbrirProductoSinProducto_NoNavega()
        {
            var vm = new VideosViewModel(_servicio, A.Fake<IServicioDialogos>(), A.Fake<IConfiguracion>(), _navegacion);

            vm.AbrirProductoCommand.Execute("");

            Assert.IsNull(_recibidos);
        }
    }
}
