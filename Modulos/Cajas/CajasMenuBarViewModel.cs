using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using CommunityToolkit.Mvvm.Input;
using Prism.Regions;
using System.Windows.Input;

namespace Nesto.Modulos.Cajas
{
    public class CajasMenuBarViewModel : ViewModelBase
    {
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }
        public CajasMenuBarViewModel(IRegionManager regionManager, IConfiguracion configuracion)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;

            AbrirModuloCajasCommand = new RelayCommand(OnAbrirCajasModulo, CanAbrirModuloCajas);
            AbrirModuloBancosCommand = new RelayCommand(OnAbrirBancosModulo, CanAbrirModuloBancos);
            AbrirModuloMayorCuentaCommand = new RelayCommand(OnAbrirMayorCuentaModulo, CanAbrirModuloMayorCuenta);
            AbrirFacturasVerifactuCommand = new RelayCommand(OnAbrirFacturasVerifactu, CanAbrirFacturasVerifactu);
            AbrirAuditoriaEnlacesPagoCommand = new RelayCommand(OnAbrirAuditoriaEnlacesPago, CanAbrirAuditoriaEnlacesPago);
        }

        public ICommand AbrirModuloCajasCommand { get; private set; }
        private bool CanAbrirModuloCajas()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) ||
                Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN) ||
                Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS);
        }
        private void OnAbrirCajasModulo()
        {
            RegionManager.RequestNavigate("MainRegion", "CajasView");
        }

        public ICommand AbrirModuloBancosCommand { get; private set; }
        private bool CanAbrirModuloBancos()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
        }
        private void OnAbrirBancosModulo()
        {
            RegionManager.RequestNavigate("MainRegion", "BancosView");
        }

        public ICommand AbrirModuloMayorCuentaCommand { get; private set; }
        private bool CanAbrirModuloMayorCuenta()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
        }
        private void OnAbrirMayorCuentaModulo()
        {
            RegionManager.RequestNavigate("MainRegion", "MayorCuentaView");
        }

        // NestoAPI#522: facturas pendientes de Verifactu, para Administración y Dirección
        public ICommand AbrirFacturasVerifactuCommand { get; private set; }
        private bool CanAbrirFacturasVerifactu()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) ||
                Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION);
        }
        private void OnAbrirFacturasVerifactu()
        {
            RegionManager.RequestNavigate("MainRegion", Cajas.FACTURAS_PENDIENTES_VERIFACTU_VIEW);
        }

        // Nesto#261: auditoría de enlaces de pago, para Administración y Dirección (la API tampoco deja a nadie más)
        public ICommand AbrirAuditoriaEnlacesPagoCommand { get; private set; }
        private bool CanAbrirAuditoriaEnlacesPago()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) ||
                Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION);
        }
        private void OnAbrirAuditoriaEnlacesPago()
        {
            RegionManager.RequestNavigate("MainRegion", Cajas.AUDITORIA_ENLACES_PAGO_VIEW);
        }
    }
}
