using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.Models;
using Prism.Commands;
using Prism.Services.Dialogs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using static Nesto.Infrastructure.Shared.Constantes.Rapports;

namespace Nesto.Modulos.Cajas.ViewModels
{
    public class CajasViewModel : ViewModelBase
    {
        public const string CUENTA_TESORERIA_PGC = "57";
        private readonly IConfiguracion _configuracion;
        private readonly IDialogService _dialogService;
        private readonly IClientesService _clientesService;
        private string _cuentaCajaDefecto;
        private string _cuentaTarjetaDefecto;
        private string _diarioCaja;
        
        public IContabilidadService Servicio { get; }
        public CajasViewModel(IContabilidadService servicio, IConfiguracion configuracion, IDialogService dialogService, IClientesService clientesService) {
            Titulo = "Cajas";
            Servicio = servicio;
            _configuracion = configuracion;
            _dialogService = dialogService;
            _clientesService = clientesService;
            CargarDatosIniciales();

            ContabilizarCobroCommand = new DelegateCommand(OnContabilizarCobro, CanContabilizarCobro);
            ContabilizarTraspasoCommand = new DelegateCommand(OnContabilizarTraspaso, CanContabilizarTraspaso);
            SeleccionarDeudasCommand = new DelegateCommand<IList>(OnSeleccionarDeudas);
        }

        private async Task CargarDatosIniciales()
        {
            ListaCuentasCaja = new ObservableCollection<CuentaContableDTO>(await Servicio.LeerCuentas(Constantes.Empresas.EMPRESA_DEFECTO, CUENTA_TESORERIA_PGC));
            CuentaOrigen = ListaCuentasCaja.Single(p => p.Cuenta == Constantes.Cuentas.CAJA_PENDIENTE_RECIBIR_TIENDAS);
            _cuentaCajaDefecto = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.CajaDefecto);
            _cuentaTarjetaDefecto = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.CuentaBancoTarjeta);
            
            CuentaDestino = ListaCuentasCaja.Single(p => p.Cuenta == _cuentaCajaDefecto);
            if (EsUsuarioDeAdministracion)
            {
                FormaPagoTransferenciaSeleccionada = true;
                _diarioCaja = Constantes.DiariosContables.DIARIO_CAJA;
            }
            else
            {
                FormaPagoEfectivoSeleccionada = true;
                _diarioCaja = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.UltDiarioCaja);
            }
        }

        private string _clienteSeleccionado;
        public string ClienteSeleccionado
        {
            get => _clienteSeleccionado;
            set
            {
                SetProperty(ref _clienteSeleccionado, value);
                CargarDeudasCliente();
            }
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
        private CuentaContableDTO _cuentaCobro;
        public CuentaContableDTO CuentaCobro
        {
            get => _cuentaCobro;
            set => SetProperty(ref _cuentaCobro, value);
        }
        private IEnumerable<ExtractoClienteDTO> _deudasSeleccionadas;
        public IEnumerable<ExtractoClienteDTO> DeudasSeleccionadas
        {
            get => _deudasSeleccionadas;
            set => SetProperty(ref _deudasSeleccionadas, value);
        }
        private DateTime _fechaCobro = DateTime.Today;
        public DateTime FechaCobro
        {
            get => _fechaCobro;
            set => SetProperty(ref _fechaCobro, value);
        }
        private bool _formaPagoEfectivoSeleccionada;
        public bool FormaPagoEfectivoSeleccionada
        {
            get => _formaPagoEfectivoSeleccionada;
            set
            {
                if (SetProperty(ref _formaPagoEfectivoSeleccionada, value))
                {
                    if (_formaPagoEfectivoSeleccionada)
                    {
                        FormaPagoTarjetaSeleccionada = false;
                        FormaPagoTransferenciaSeleccionada = false;
                        CuentaCobro = ListaCuentasCaja.Single(c => c.Cuenta == _cuentaCajaDefecto);
                        FormaPagoSeleccionada = ListaFormaPago.Single(f => f.formaPago == Constantes.FormasPago.EFECTIVO);
                    }                    
                }
            }
        }
        private bool _formaPagoTarjetaSeleccionada;
        public bool FormaPagoTarjetaSeleccionada
        {
            get => _formaPagoTarjetaSeleccionada;
            set
            {
                if (SetProperty(ref _formaPagoTarjetaSeleccionada, value))
                {
                    if (_formaPagoTarjetaSeleccionada)
                    {
                        FormaPagoEfectivoSeleccionada = false;
                        FormaPagoTransferenciaSeleccionada = false;
                        CuentaCobro = ListaCuentasCaja.Single(c => c.Cuenta == _cuentaTarjetaDefecto);
                        FormaPagoSeleccionada = ListaFormaPago.Single(f => f.formaPago == Constantes.FormasPago.TARJETA);
                    }                    
                }
            }
        }
        private bool _formaPagoTransferenciaSeleccionada;
        public bool FormaPagoTransferenciaSeleccionada
        {
            get => _formaPagoTransferenciaSeleccionada;
            set
            {
                if (SetProperty(ref _formaPagoTransferenciaSeleccionada, value))
                {
                    if (_formaPagoTransferenciaSeleccionada)
                    {
                        FormaPagoEfectivoSeleccionada = false;
                        FormaPagoTarjetaSeleccionada = false;
                        CuentaCobro = ListaCuentasCaja.Single(c => c.Cuenta == _cuentaTarjetaDefecto);
                        FormaPagoSeleccionada = ListaFormaPago.Single(f => f.formaPago == Constantes.FormasPago.TRANSFERENCIA);
                    }
                }
            }
        }


        private FormaPago _formaPagoSeleccionada;
        public FormaPago FormaPagoSeleccionada
        {
            get => _formaPagoSeleccionada;
            set => SetProperty(ref _formaPagoSeleccionada, value);
        }
        private decimal _importe;
        public decimal Importe { 
            get => _importe; 
            set => SetProperty(ref _importe, value); 
        }
        private decimal _importeDeudasSeleccionadas;
        public decimal ImporteDeudasSeleccionadas
        {
            get => _importeDeudasSeleccionadas;
            set => SetProperty(ref _importeDeudasSeleccionadas, value);
        }
        private ObservableCollection<CuentaContableDTO> _listaCuentasCaja;
        public ObservableCollection<CuentaContableDTO> ListaCuentasCaja { 
            get => _listaCuentasCaja; 
            set => SetProperty(ref _listaCuentasCaja, value); 
        }
        private ObservableCollection<ExtractoClienteDTO> _listaDeudas;
        public ObservableCollection<ExtractoClienteDTO> ListaDeudas
        {
            get => _listaDeudas;
            set => SetProperty(ref _listaDeudas,value);
        }
        private List<FormaPago> _listaFormaPago;
        public List<FormaPago> ListaFormaPago
        {
            get => _listaFormaPago;
            set => SetProperty(ref _listaFormaPago, value);
        }
        public bool EsUsuarioDeAdministracion => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);








        public ICommand ContabilizarCobroCommand { get; private set; }
        private bool CanContabilizarCobro()
        {
            return DeudasSeleccionadas is not null &&
                DeudasSeleccionadas.Any() &&
                ImporteDeudasSeleccionadas > 0;
        }
        private async void OnContabilizarCobro()
        {
            List<PreContabilidadDTO> lineas = new();
            foreach (var deuda in DeudasSeleccionadas)
            {
                string tipoApunte = NombreTipo(deuda);
                PreContabilidadDTO linea = new PreContabilidadDTO
                {
                    Empresa = deuda.Empresa,
                    TipoApunte = Constantes.TiposApunte.PAGO,
                    TipoCuenta = Constantes.TiposCuenta.CLIENTE,
                    Cuenta = deuda.Cliente,
                    Contacto = deuda.Contacto,
                    Concepto = $"S/Pago {tipoApunte} {deuda.Documento}/{deuda.Efecto}", //igual estos Trim deberían ir en el controller de la API
                    Haber = deuda.ImportePendiente,
                    Fecha = DateOnly.FromDateTime(FechaCobro),
                    FechaVto = DateOnly.FromDateTime(deuda.Vencimiento),
                    Documento = deuda.Documento,
                    Efecto = deuda.Efecto,
                    Asiento = 1,
                    Diario = _diarioCaja,
                    Delegacion = deuda.Delegacion,
                    FormaVenta = deuda.FormaVenta,
                    FormaPago = FormaPagoSeleccionada.formaPago,
                    Ruta = deuda.Ruta,
                    Origen = Constantes.Empresas.EMPRESA_DEFECTO,
                    Liquidado = deuda.Id,
                    Usuario = _configuracion.usuario,
                    FechaModificacion = DateTime.Now
                };
                if (!EsUsuarioDeAdministracion)
                {
                    linea.Concepto += $" a {_configuracion.usuario}";
                }
                lineas.Add(linea);
            }

            // meter en un bucle para insertar una contrapartida por empresa
            var empresas = lineas.Select(e => e.Empresa).Distinct().ToList();
            var deudaConMayorImporte = DeudasSeleccionadas.OrderByDescending(e => e.Importe).First();
            string tipoApunteContrapartida = NombreTipo(deudaConMayorImporte);
            foreach (var empresa in empresas)
            {
                PreContabilidadDTO contrapartida = new PreContabilidadDTO()
                {
                    Empresa = empresa,
                    TipoApunte = Constantes.TiposApunte.PAGO,
                    TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                    Cuenta = CuentaCobro.Cuenta,
                    Concepto = $"Pago c/{deudaConMayorImporte.Cliente}/{deudaConMayorImporte.Contacto} {tipoApunteContrapartida} {deudaConMayorImporte.Documento}/{deudaConMayorImporte.Efecto}",
                    Debe = ImporteDeudasSeleccionadas,
                    Fecha = DateOnly.FromDateTime(FechaCobro),
                    FechaVto = DateOnly.FromDateTime(deudaConMayorImporte.Vencimiento),
                    Documento = deudaConMayorImporte.Documento,
                    Asiento = 1,
                    Diario = _diarioCaja,
                    Delegacion = deudaConMayorImporte.Delegacion,
                    FormaVenta = deudaConMayorImporte.FormaVenta,
                    FormaPago = FormaPagoSeleccionada.formaPago,
                    Origen = Constantes.Empresas.EMPRESA_DEFECTO,
                    Usuario = _configuracion.usuario,
                    FechaModificacion = DateTime.Now
                };
                lineas.Add(contrapartida);
            }
            
            try
            {
                int asiento = await Servicio.Contabilizar(lineas);
                if (asiento <= 0)
                {
                    _dialogService.ShowError("No se ha podido contabilizar el cobro");
                }
                else
                {
                    _dialogService.ShowError($"Creado asiento {asiento} correctamente");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Error al contabilizar el diario.\n" + ex.Message);
            }
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

        public ICommand SeleccionarDeudasCommand { get; private set; }
        private void OnSeleccionarDeudas(IList apuntes)
        {
            var selectedItems = apuntes.OfType<ExtractoClienteDTO>();
            DeudasSeleccionadas = selectedItems;
            ImporteDeudasSeleccionadas = selectedItems
                .Select(s => s.ImportePendiente)
                .DefaultIfEmpty(0)
                .Sum();
            ((DelegateCommand)ContabilizarCobroCommand).RaiseCanExecuteChanged();
        }





        private async Task CargarDeudasCliente()
        {
            ListaDeudas = new ObservableCollection<ExtractoClienteDTO>((await _clientesService.LeerDeudas(ClienteSeleccionado)).OrderBy(e => e.Id));
        }
                private static string NombreTipo(ExtractoClienteDTO deuda)
        {
            return deuda.Tipo == Constantes.TiposApunte.IMPAGADO ? "Impagado" : "Factura";
        }

    }
}
