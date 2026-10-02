using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Nesto.Modules.Producto
{
    public class ProductoMenuBarViewModel : ViewModelBase
    {
        private IServicioNavegacion Navegacion { get; }
        private IConfiguracion Configuracion { get; }
        public ProductoMenuBarViewModel(IServicioNavegacion navegacion, IConfiguracion configuracion)
        {
            Navegacion = navegacion;
            Configuracion = configuracion;

            AbrirModuloFichaCommand = new RelayCommand(OnAbrirModuloFicha, CanAbrirModuloFicha);
            AbrirModuloReposicionCommand = new RelayCommand(OnAbrirModuloReposicion, CanAbrirModuloReposicion);
        }

        public ICommand AbrirModuloFichaCommand { get; private set; }
        private bool CanAbrirModuloFicha()
        {
            return true;
        }
        private void OnAbrirModuloFicha()
        {
            Navegacion.RequestNavigate("MainRegion", "ProductoView");
        }

        public ICommand AbrirModuloReposicionCommand { get; private set; }
        private bool CanAbrirModuloReposicion()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN);
        }
        private void OnAbrirModuloReposicion()
        {
            Navegacion.RequestNavigate("MainRegion", "ReposicionView");
        }
    }
}
