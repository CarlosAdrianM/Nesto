using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Prism.Regions;
using Nesto.Modules.Producto;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4): qué vista abre cada botón del menú. Se escribieron contra el IRegionManager
    /// de Prism ANTES de migrar la navegación, para que la migración no cambie nada.
    /// </summary>
    [TestClass]
    public class ProductoMenuBarViewModelNavegacionTests
    {
        private IRegionManager _navegacion = null!;
        private ProductoMenuBarViewModel _menu = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _navegacion = A.Fake<IRegionManager>();
            _menu = new ProductoMenuBarViewModel(_navegacion, A.Fake<IConfiguracion>());
        }

        [TestMethod]
        public void AbrirModuloFichaCommand_AbreLaVistaFicha()
        {
            _menu.AbrirModuloFichaCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "ProductoView")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AbrirModuloReposicionCommand_AbreLaVistaReposicion()
        {
            _menu.AbrirModuloReposicionCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "ReposicionView")).MustHaveHappenedOnceExactly();
        }
    }
}
