using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Prism.Commands;
using Prism.Regions;
using System.Windows.Input;

namespace Nesto.Modules.Producto
{
    public class ProductoMenuBarViewModel : ViewModelBase
    {
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }
        public ProductoMenuBarViewModel(IRegionManager regionManager, IConfiguracion configuracion)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;

            AbrirModuloFichaCommand = new DelegateCommand(OnAbrirModuloFicha, CanAbrirModuloFicha);
            AbrirModuloReposicionCommand = new DelegateCommand(OnAbrirModuloReposicion, CanAbrirModuloReposicion);
        }

        public ICommand AbrirModuloFichaCommand { get; private set; }
        private bool CanAbrirModuloFicha()
        {
            return true;
        }
        private void OnAbrirModuloFicha()
        {
            RegionManager.RequestNavigate("MainRegion", "ProductoView");
        }

        public ICommand AbrirModuloReposicionCommand { get; private set; }
        private bool CanAbrirModuloReposicion()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN);
        }
        private void OnAbrirModuloReposicion()
        {
            RegionManager.RequestNavigate("MainRegion", "ReposicionView");
        }
    }
}
