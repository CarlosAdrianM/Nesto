using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4): qué vista abre cada botón del menú. Se escribieron contra el IRegionManager
    /// de Prism ANTES de migrar la navegación (commit 33c736cb) y, al pasar el menú a
    /// IServicioNavegacion, solo ha cambiado el tipo del fake: mismas regiones y mismas vistas.
    /// </summary>
    [TestClass]
    public class ProductoMenuBarViewModelNavegacionTests
    {
        private IServicioNavegacion _navegacion = null!;
        private ProductoMenuBarViewModel _menu = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _navegacion = A.Fake<IServicioNavegacion>();
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

        [TestMethod]
        public void AbrirCalendarioReposicionesCommand_AbreElCalendarioParaCualquiera()
        {
            Assert.IsTrue(_menu.AbrirCalendarioReposicionesCommand.CanExecute(null), "Lo ve cualquiera; la API decide si puede cambiarlo");

            _menu.AbrirCalendarioReposicionesCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "CalendarioReposicionesView")).MustHaveHappenedOnceExactly();
        }
    }
}
