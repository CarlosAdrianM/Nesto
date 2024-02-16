using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Prism.Commands;
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

            AbrirModuloCajasCommand = new DelegateCommand(OnAbrirCajasModulo, CanAbrirModuloCajas);
            AbrirModuloBancosCommand = new DelegateCommand(OnAbrirBancosModulo, CanAbrirModuloBancos);
        }

        public ICommand AbrirModuloCajasCommand { get; private set; }
        private bool CanAbrirModuloCajas()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
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
    }
}
