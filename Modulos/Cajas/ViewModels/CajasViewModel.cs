using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.Models;
using Prism.Commands;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nesto.Modulos.Cajas.ViewModels
{
    public class CajasViewModel : ViewModelBase
    {
        public const string CUENTA_CAJA_PGC = "570";
        private readonly IConfiguracion _configuracion;
        private readonly IDialogService _dialogService;

        public IContabilidadService Servicio { get; }
        public CajasViewModel(IContabilidadService servicio, IConfiguracion configuracion, IDialogService dialogService) {
            Titulo = "Cajas";
            Servicio = servicio;
            this._configuracion = configuracion;
            this._dialogService = dialogService;
            CargarDatosIniciales();

            ContabilizarTraspasoCommand = new DelegateCommand(OnContabilizarTraspaso, CanContabilizarTraspaso);
        }

        private async Task CargarDatosIniciales()
        {
            ListaCuentasCaja = new ObservableCollection<CuentaContableDTO>(await Servicio.LeerCuentas(Constantes.Empresas.EMPRESA_DEFECTO, CUENTA_CAJA_PGC));
            CuentaOrigen = ListaCuentasCaja.Single(p => p.Cuenta == Constantes.Cuentas.CAJA_PENDIENTE_RECIBIR_TIENDAS);
            var cajaDefecto = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.CajaDefecto);
            CuentaDestino = ListaCuentasCaja.Single(p => p.Cuenta == cajaDefecto);
        }

        private string _concepto = "Traspaso entre cajas";
        public string Concepto { 
            get => _concepto;
            set 
            { 
                SetProperty(ref _concepto, value);
                ((DelegateCommand)ContabilizarTraspasoCommand).RaiseCanExecuteChanged();
            }
        }

        private CuentaContableDTO _cuentaOrigen;
        public CuentaContableDTO CuentaOrigen { 
            get => _cuentaOrigen;
            set 
            { 
                SetProperty(ref _cuentaOrigen, value);
                ((DelegateCommand)ContabilizarTraspasoCommand).RaiseCanExecuteChanged();
            }
        }
        private CuentaContableDTO _cuentaDestino;
        public CuentaContableDTO CuentaDestino { 
            get => _cuentaDestino;
            set 
            { 
                SetProperty(ref _cuentaDestino, value);
                ((DelegateCommand)ContabilizarTraspasoCommand).RaiseCanExecuteChanged();
            }
        }
        private decimal _importe;
        public decimal Importe { 
            get => _importe; 
            set => SetProperty(ref _importe, value); 
        }
        private ObservableCollection<CuentaContableDTO> _listaCuentasCaja;
        public ObservableCollection<CuentaContableDTO> ListaCuentasCaja { 
            get => _listaCuentasCaja; 
            set => SetProperty(ref _listaCuentasCaja, value); 
        }



        public ICommand ContabilizarTraspasoCommand { get; private set; }

        private bool CanContabilizarTraspaso()
        {
            return CuentaOrigen is not null && CuentaDestino is not null && !string.IsNullOrEmpty(Concepto) && CuentaOrigen.Cuenta != CuentaDestino.Cuenta;
        }

        private async void OnContabilizarTraspaso()
        {
            var delegacion = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.DelegacionDefecto);
            var formaVenta = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.FormaVentaDefecto);
            PreContabilidadDTO linea = new PreContabilidadDTO
            {
                Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                TipoApunte = Constantes.TiposApunte.PASO_A_CARTERA,
                TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                Cuenta = CuentaOrigen.Cuenta,
                Concepto = Concepto,
                Haber = Importe,
                Fecha = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day),
                Documento = "TRASP_CAJA",
                Asiento = 1,
                Diario = Constantes.DiariosContables.DIARIO_TRASPASOS_CAJA,
                Delegacion = delegacion,
                FormaVenta = formaVenta,
                Contrapartida = CuentaDestino.Cuenta,
                Origen = Constantes.Empresas.EMPRESA_DEFECTO,
                Usuario = _configuracion.usuario,
                FechaModificacion = DateTime.Now
            };
            try
            {
                int asiento = await Servicio.Contabilizar(linea);
                if (asiento <= 0)
                {
                    _dialogService.ShowError("No se ha podido contabilizar el asiento");
                }
                else
                {
                    _dialogService.ShowError($"Creado asiento {asiento} correctamente");
                }
            } catch (Exception ex)
            {
                _dialogService.ShowError("Error al contabilizar el diario.\n"+ex.Message);
            }
        }
    }
}
