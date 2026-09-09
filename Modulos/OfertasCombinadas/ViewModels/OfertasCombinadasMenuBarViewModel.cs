using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using CommunityToolkit.Mvvm.Input;
using Prism.Regions;
using System.Windows.Input;

namespace Nesto.Modulos.OfertasCombinadas.ViewModels
{
    public class OfertasCombinadasMenuBarViewModel : ViewModelBase
    {
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }

        public OfertasCombinadasMenuBarViewModel(IRegionManager regionManager, IConfiguracion configuracion)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;

            AbrirModuloOfertasCombinadasCommand = new RelayCommand(OnAbrirOfertasCombinadasModulo, CanAbrirModuloOfertasCombinadas);
        }

        public ICommand AbrirModuloOfertasCombinadasCommand { get; private set; }

        private bool CanAbrirModuloOfertasCombinadas()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS);
        }

        private void OnAbrirOfertasCombinadasModulo()
        {
            RegionManager.RequestNavigate("MainRegion", "OfertasCombinadasView");
        }
    }
}
