using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using Nesto.Infrastructure.Shared;
using Nesto.Infrastructure.Contracts;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalesExternosMenuBarViewModel : ViewModelBase
    {
        private IServicioNavegacion Navegacion { get; }
        private IConfiguracion Configuracion { get; }
        public CanalesExternosMenuBarViewModel(IServicioNavegacion navegacion, IConfiguracion configuracion)
        {
            Navegacion = navegacion;
            Configuracion = configuracion;

            AbrirModuloPedidosCommand = new RelayCommand(OnAbrirPedidosModulo, CanAbrirModuloPedidos);
            AbrirModuloPagosCommand = new RelayCommand(OnAbrirModuloPagos, CanAbrirModuloPagos);
            AbrirModuloProductosCommand = new RelayCommand(OnAbrirModuloProductos, CanAbrirModuloProductos);
            AbrirModuloFacturasCommand = new RelayCommand(OnAbrirModuloFacturas, CanAbrirModuloFacturas);
            AbrirModuloCuadreFacturasCommand = new RelayCommand(OnAbrirModuloCuadreFacturas, CanAbrirModuloCuadreFacturas);
            AbrirModuloPoisonPillsCommand = new RelayCommand(OnAbrirModuloPoisonPills, CanAbrirModuloPoisonPills);
        }

        public ICommand AbrirModuloFacturasCommand { get; private set; }
        private bool CanAbrirModuloFacturas()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
        }
        private void OnAbrirModuloFacturas()
        {
            Navegacion.RequestNavigate("MainRegion", "CanalesExternosFacturasView");
        }

        public ICommand AbrirModuloCuadreFacturasCommand { get; private set; }
        private bool CanAbrirModuloCuadreFacturas()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
        }
        private void OnAbrirModuloCuadreFacturas()
        {
            Navegacion.RequestNavigate("MainRegion", "CanalesExternosCuadreFacturasView");
        }

        public ICommand AbrirModuloPedidosCommand { get; private set; }
        private bool CanAbrirModuloPedidos()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE);
        }
        private void OnAbrirPedidosModulo()
        {
            Navegacion.RequestNavigate("MainRegion", "CanalesExternosPedidosView");
        }

        public ICommand AbrirModuloPagosCommand { get; private set; }
        private bool CanAbrirModuloPagos()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
        }
        private void OnAbrirModuloPagos()
        {
            Navegacion.RequestNavigate("MainRegion", "CanalesExternosPagosView");
        }


        public ICommand AbrirModuloProductosCommand { get; private set; }
        private bool CanAbrirModuloProductos()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE);
        }
        private void OnAbrirModuloProductos()
        {
            Navegacion.RequestNavigate("MainRegion", "CanalesExternosProductosView");
        }

        public ICommand AbrirModuloPoisonPillsCommand { get; private set; }
        private bool CanAbrirModuloPoisonPills()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION);
        }
        private void OnAbrirModuloPoisonPills()
        {
            Navegacion.RequestNavigate("MainRegion", "PoisonPillsView");
        }
    }
}
