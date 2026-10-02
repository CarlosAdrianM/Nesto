using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Prism.Regions;
using Nesto.Modulos.Ganavisiones.ViewModels;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#490 (4C.4): qué vista abre cada botón del menú. Se escribieron contra el IRegionManager
    /// de Prism ANTES de migrar la navegación, para que la migración no cambie nada.
    /// </summary>
    [TestClass]
    public class GanavisionesMenuBarViewModelNavegacionTests
    {
        private IRegionManager _navegacion = null!;
        private GanavisionesMenuBarViewModel _menu = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _navegacion = A.Fake<IRegionManager>();
            _menu = new GanavisionesMenuBarViewModel(_navegacion, A.Fake<IConfiguracion>());
        }

        [TestMethod]
        public void AbrirModuloGanavisionesCommand_AbreLaVistaGanavisiones()
        {
            _menu.AbrirModuloGanavisionesCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "GanavisionesView")).MustHaveHappenedOnceExactly();
        }
    }
}
