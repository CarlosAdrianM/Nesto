using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Prism.Commands;
using Prism.Regions;
using System.Windows.Input;

namespace Nesto.Modulos.Ganavisiones.ViewModels
{
    public class GanavisionesMenuBarViewModel : ViewModelBase
    {
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }

        public GanavisionesMenuBarViewModel(IRegionManager regionManager, IConfiguracion configuracion)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;

            AbrirModuloGanavisionesCommand = new DelegateCommand(OnAbrirGanavisionesModulo, CanAbrirModuloGanavisiones);
        }

        public ICommand AbrirModuloGanavisionesCommand { get; private set; }

        private bool CanAbrirModuloGanavisiones()
        {
            // Solo usuarios del grupo COMPRAS pueden acceder
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS);
        }

        private void OnAbrirGanavisionesModulo()
        {
            RegionManager.RequestNavigate("MainRegion", "GanavisionesView");
        }
    }
}
