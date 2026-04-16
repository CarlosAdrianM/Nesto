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
            AbrirModuloFacturasCommand = new DelegateCommand(OnAbrirModuloFacturas, CanAbrirModuloFacturas);
            AbrirModuloCuadreFacturasCommand = new DelegateCommand(OnAbrirModuloCuadreFacturas, CanAbrirModuloCuadreFacturas);
            AbrirModuloPoisonPillsCommand = new DelegateCommand(OnAbrirModuloPoisonPills, CanAbrirModuloPoisonPills);
        }

        public ICommand AbrirModuloFacturasCommand { get; private set; }
        private bool CanAbrirModuloFacturas()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
        }
        private void OnAbrirModuloFacturas()
        {
            RegionManager.RequestNavigate("MainRegion", "CanalesExternosFacturasView");
        }

        public ICommand AbrirModuloCuadreFacturasCommand { get; private set; }
        private bool CanAbrirModuloCuadreFacturas()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
        }
        private void OnAbrirModuloCuadreFacturas()
        {
            RegionManager.RequestNavigate("MainRegion", "CanalesExternosCuadreFacturasView");
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

        public ICommand AbrirModuloPoisonPillsCommand { get; private set; }
        private bool CanAbrirModuloPoisonPills()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION);
        }
        private void OnAbrirModuloPoisonPills()
        {
            RegionManager.RequestNavigate("MainRegion", "PoisonPillsView");
        }
    }
}
