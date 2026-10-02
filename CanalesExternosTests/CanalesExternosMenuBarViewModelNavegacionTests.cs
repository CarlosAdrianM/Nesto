using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Prism.Regions;
using Nesto.Modulos.CanalesExternos;

namespace CanalesExternosTests
{
    /// <summary>
    /// Nesto#490 (4C.4): qué vista abre cada botón del menú. Se escribieron contra el IRegionManager
    /// de Prism ANTES de migrar la navegación, para que la migración no cambie nada.
    /// </summary>
    [TestClass]
    public class CanalesExternosMenuBarViewModelNavegacionTests
    {
        private IRegionManager _navegacion = null!;
        private CanalesExternosMenuBarViewModel _menu = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _navegacion = A.Fake<IRegionManager>();
            _menu = new CanalesExternosMenuBarViewModel(_navegacion, A.Fake<IConfiguracion>());
        }

        [TestMethod]
        public void AbrirModuloPedidosCommand_AbreLaVistaPedidos()
        {
            _menu.AbrirModuloPedidosCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "CanalesExternosPedidosView")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AbrirModuloPagosCommand_AbreLaVistaPagos()
        {
            _menu.AbrirModuloPagosCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "CanalesExternosPagosView")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AbrirModuloProductosCommand_AbreLaVistaProductos()
        {
            _menu.AbrirModuloProductosCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "CanalesExternosProductosView")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AbrirModuloFacturasCommand_AbreLaVistaFacturas()
        {
            _menu.AbrirModuloFacturasCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "CanalesExternosFacturasView")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AbrirModuloCuadreFacturasCommand_AbreLaVistaCuadreFacturas()
        {
            _menu.AbrirModuloCuadreFacturasCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "CanalesExternosCuadreFacturasView")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AbrirModuloPoisonPillsCommand_AbreLaVistaPoisonPills()
        {
            _menu.AbrirModuloPoisonPillsCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "PoisonPillsView")).MustHaveHappenedOnceExactly();
        }
    }
}
