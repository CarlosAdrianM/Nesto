using Prism.Commands;
using Prism.Regions;
using System.Windows.Input;
using Nesto.Infrastructure.Shared;
using Nesto.Infrastructure.Contracts;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalesExternosMenuBarViewModel : ViewModelBase
    {
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }
        public CanalesExternosMenuBarViewModel(IRegionManager regionManager, IConfiguracion configuracion)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;

            AbrirModuloPedidosCommand = new DelegateCommand(OnAbrirPedidosModulo, CanAbrirModuloPedidos);
            AbrirModuloPagosCommand = new DelegateCommand(OnAbrirModuloPagos, CanAbrirModuloPagos);
            AbrirModuloProductosCommand = new DelegateCommand(OnAbrirModuloProductos, CanAbrirModuloProductos);
        }

        public ICommand AbrirModuloPedidosCommand { get; private set; }
        private bool CanAbrirModuloPedidos()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE);
        }
        private void OnAbrirPedidosModulo()
        {
            RegionManager.RequestNavigate("MainRegion", "CanalesExternosPedidosView");
        }

        public ICommand AbrirModuloPagosCommand { get; private set; }
        private bool CanAbrirModuloPagos()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
        }
        private void OnAbrirModuloPagos()
        {
            RegionManager.RequestNavigate("MainRegion", "CanalesExternosPagosView");
        }


        public ICommand AbrirModuloProductosCommand { get; private set; }
        private bool CanAbrirModuloProductos()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE);
        }
        private void OnAbrirModuloProductos()
        {
            RegionManager.RequestNavigate("MainRegion", "CanalesExternosProductosView");
        }
    }
}
