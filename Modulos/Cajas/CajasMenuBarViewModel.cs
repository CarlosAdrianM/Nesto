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
    }
}
