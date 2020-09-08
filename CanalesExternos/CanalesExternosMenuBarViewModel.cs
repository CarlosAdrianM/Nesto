using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Regions;
using Nesto.Contratos;
using System.Windows.Input;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalesExternosMenuBarViewModel : Contratos.ViewModelBase
    {
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }
        public CanalesExternosMenuBarViewModel(IRegionManager regionManager, IConfiguracion configuracion)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;

            AbrirModuloPedidosCommand = new DelegateCommand(OnAbrirPedidosModulo, CanAbrirModuloPedidos);
            AbrirModuloPagosCommand = new DelegateCommand(OnAbrirModuloPagos, CanAbrirModuloPagos);
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
    }
}
