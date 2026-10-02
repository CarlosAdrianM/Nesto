using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Nesto.Modulos.Cajas
{
    public class CajasMenuBarViewModel : ViewModelBase
    {
        private IServicioNavegacion Navegacion { get; }
        private IConfiguracion Configuracion { get; }
        public CajasMenuBarViewModel(IServicioNavegacion navegacion, IConfiguracion configuracion)
        {
            Navegacion = navegacion;
            Configuracion = configuracion;

            AbrirModuloCajasCommand = new RelayCommand(OnAbrirCajasModulo, CanAbrirModuloCajas);
            AbrirModuloBancosCommand = new RelayCommand(OnAbrirBancosModulo, CanAbrirModuloBancos);
            AbrirModuloMayorCuentaCommand = new RelayCommand(OnAbrirMayorCuentaModulo, CanAbrirModuloMayorCuenta);
            AbrirFacturasVerifactuCommand = new RelayCommand(OnAbrirFacturasVerifactu, CanAbrirFacturasVerifactu);
            AbrirAuditoriaEnlacesPagoCommand = new RelayCommand(OnAbrirAuditoriaEnlacesPago, CanAbrirAuditoriaEnlacesPago);
        }

        public ICommand AbrirModuloCajasCommand { get; private set; }
        private bool CanAbrirModuloCajas()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) ||
                Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN) ||
                Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS);
        }
        private void OnAbrirCajasModulo()
        {
            Navegacion.RequestNavigate("MainRegion", "CajasView");
        }

        public ICommand AbrirModuloBancosCommand { get; private set; }
        private bool CanAbrirModuloBancos()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
        }
        private void OnAbrirBancosModulo()
        {
            Navegacion.RequestNavigate("MainRegion", "BancosView");
        }

        public ICommand AbrirModuloMayorCuentaCommand { get; private set; }
        private bool CanAbrirModuloMayorCuenta()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
        }
        private void OnAbrirMayorCuentaModulo()
        {
            Navegacion.RequestNavigate("MainRegion", "MayorCuentaView");
        }

        // NestoAPI#522: facturas pendientes de Verifactu, para Administración y Dirección
        public ICommand AbrirFacturasVerifactuCommand { get; private set; }
        private bool CanAbrirFacturasVerifactu()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) ||
                Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION);
        }
        private void OnAbrirFacturasVerifactu()
        {
            Navegacion.RequestNavigate("MainRegion", Cajas.FACTURAS_PENDIENTES_VERIFACTU_VIEW);
        }

        // Nesto#261: auditoría de enlaces de pago, para Administración y Dirección (la API tampoco deja a nadie más)
        public ICommand AbrirAuditoriaEnlacesPagoCommand { get; private set; }
        private bool CanAbrirAuditoriaEnlacesPago()
        {
            return Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) ||
                Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION);
        }
        private void OnAbrirAuditoriaEnlacesPago()
        {
            Navegacion.RequestNavigate("MainRegion", Cajas.AUDITORIA_ENLACES_PAGO_VIEW);
        }
    }
}
