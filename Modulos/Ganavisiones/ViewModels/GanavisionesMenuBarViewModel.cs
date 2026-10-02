using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Nesto.Modulos.Ganavisiones.ViewModels
{
    public class GanavisionesMenuBarViewModel : ViewModelBase
    {
        private IServicioNavegacion Navegacion { get; }
        private IConfiguracion Configuracion { get; }

        public GanavisionesMenuBarViewModel(IServicioNavegacion navegacion, IConfiguracion configuracion)
        {
            Navegacion = navegacion;
            Configuracion = configuracion;

            AbrirModuloGanavisionesCommand = new RelayCommand(OnAbrirGanavisionesModulo, CanAbrirModuloGanavisiones);
        }

        public ICommand AbrirModuloGanavisionesCommand { get; private set; }

        private bool CanAbrirModuloGanavisiones()
        {
            // Solo usuarios del grupo COMPRAS pueden acceder
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS);
        }

        private void OnAbrirGanavisionesModulo()
        {
            Navegacion.RequestNavigate("MainRegion", "GanavisionesView");
        }
    }
}
