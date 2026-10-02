using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas;

namespace CajasTests
{
    /// <summary>
    /// Nesto#490 (4C.4): qué vista abre cada botón del menú. Se escribieron contra el IRegionManager
    /// de Prism ANTES de migrar la navegación (commit 33c736cb) y, al pasar el menú a
    /// IServicioNavegacion, solo ha cambiado el tipo del fake: mismas regiones y mismas vistas.
    /// </summary>
    [TestClass]
    public class CajasMenuBarViewModelNavegacionTests
    {
        private IServicioNavegacion _navegacion = null!;
        private CajasMenuBarViewModel _menu = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _navegacion = A.Fake<IServicioNavegacion>();
            _menu = new CajasMenuBarViewModel(_navegacion, A.Fake<IConfiguracion>());
        }

        [TestMethod]
        public void AbrirModuloCajasCommand_AbreLaVistaCajas()
        {
            _menu.AbrirModuloCajasCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "CajasView")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AbrirModuloBancosCommand_AbreLaVistaBancos()
        {
            _menu.AbrirModuloBancosCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "BancosView")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AbrirModuloMayorCuentaCommand_AbreLaVistaMayorCuenta()
        {
            _menu.AbrirModuloMayorCuentaCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "MayorCuentaView")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AbrirFacturasVerifactuCommand_AbreLaVistaFacturasVerifactu()
        {
            _menu.AbrirFacturasVerifactuCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", Cajas.FACTURAS_PENDIENTES_VERIFACTU_VIEW)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AbrirAuditoriaEnlacesPagoCommand_AbreLaVistaAuditoriaEnlacesPago()
        {
            _menu.AbrirAuditoriaEnlacesPagoCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", Cajas.AUDITORIA_ENLACES_PAGO_VIEW)).MustHaveHappenedOnceExactly();
        }
    }
}
