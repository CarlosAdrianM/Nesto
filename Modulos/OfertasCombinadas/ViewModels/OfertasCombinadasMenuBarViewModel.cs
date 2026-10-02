using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Nesto.Modulos.OfertasCombinadas.ViewModels
{
    public class OfertasCombinadasMenuBarViewModel : ViewModelBase
    {
        private IServicioNavegacion Navegacion { get; }
        private IConfiguracion Configuracion { get; }

        public OfertasCombinadasMenuBarViewModel(IServicioNavegacion navegacion, IConfiguracion configuracion)
        {
            Navegacion = navegacion;
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
            Navegacion.RequestNavigate("MainRegion", "OfertasCombinadasView");
        }
    }
}
