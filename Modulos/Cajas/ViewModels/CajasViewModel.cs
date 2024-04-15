using ControlesUsuario;
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
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using static ControlesUsuario.Models.SelectorProveedorModel;

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
        private string _formaVentaUsuario;
        private decimal _saldoCuentaOrigen;

        public IContabilidadService Servicio { get; }
        public CajasViewModel(IContabilidadService servicio, IConfiguracion configuracion, IDialogService dialogService, IClientesService clientesService) {
            Titulo = "Cajas";
            Servicio = servicio;
            _configuracion = configuracion;
            _dialogService = dialogService;
            _clientesService = clientesService;

            ArqueoFondo = new();
            DeudasSeleccionadas = new List<ExtractoClienteDTO>();

            ContabilizarCobroCommand = new DelegateCommand(OnContabilizarCobro, CanContabilizarCobro);
            ContabilizarGastoCommand = new DelegateCommand(OnContabilizarGasto, CanContabilizarGasto);
            ContabilizarTraspasoCommand = new DelegateCommand(OnContabilizarTraspaso, CanContabilizarTraspaso);
            LoadedCommand = new DelegateCommand(OnLoaded);
            SeleccionarDeudasCommand = new DelegateCommand<IList>(OnSeleccionarDeudas);

            // suscribirse a los cambios de ArqueoFondo.TotalArqueo para que cuando cambie actualicemos el importe del traspaso
            ArqueoFondo.PropertyChanged += ArqueoFondo_Changed;
        }


        #region "Propiedades"
        private ArqueoEfectivoModel _arqueoFondo;
        public ArqueoEfectivoModel ArqueoFondo { 
            get => _arqueoFondo; 
            set => SetProperty(ref _arqueoFondo, value); 
        }
        private ClienteDTO _clienteCompletoSeleccionado;
        public ClienteDTO ClienteCompletoSeleccionado
        {
            get => _clienteCompletoSeleccionado;
            set => SetProperty(ref _clienteCompletoSeleccionado, value);
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

        private CuentaContableDTO _cuentaCobro;
        public CuentaContableDTO CuentaCobro
        {
            get => _cuentaCobro;
            set => SetProperty(ref _cuentaCobro, value);
        }
        private CuentaContableDTO _cuentaDestino;
        public CuentaContableDTO CuentaDestino
        {
            get => _cuentaDestino;
            set
            {
                SetProperty(ref _cuentaDestino, value);
                ((DelegateCommand)ContabilizarTraspasoCommand).RaiseCanExecuteChanged();
            }
        }

        private CuentaContableDTO _cuentaOrigen;
        public CuentaContableDTO CuentaOrigen
        {
            get => _cuentaOrigen;
            set
            {
                if (SetProperty(ref _cuentaOrigen, value))
                {
                    CalcularSaldoCuentaOrigen();
                    ((DelegateCommand)ContabilizarTraspasoCommand).RaiseCanExecuteChanged();
                }                
            }
        }

        private string _delegacionUsuario;

        private IEnumerable<ExtractoClienteDTO> _deudasSeleccionadas;
        public IEnumerable<ExtractoClienteDTO> DeudasSeleccionadas
        {
            get => _deudasSeleccionadas;
            set => SetProperty(ref _deudasSeleccionadas, value);
        }
        public bool EstaVisibleImporteACuenta => ImporteACuenta != 0;
        public bool EsUsuarioDeAdministracion => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
        private DateTime _fechaCobro = DateTime.Today;
        public DateTime FechaCobro
        {
            get => _fechaCobro;
            set => SetProperty(ref _fechaCobro, value);
        }
        private decimal _fondoCaja;
        public decimal FondoCaja
        {
            get => _fondoCaja;
            set => SetProperty(ref _fondoCaja, value);
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
        private string _gastoNumeroFactura;
        public string GastoNumeroFactura
        {
            get => _gastoNumeroFactura;
            set
            {
                SetProperty(ref _gastoNumeroFactura, value);
                (ContabilizarGastoCommand as DelegateCommand).RaiseCanExecuteChanged();
            }
        }
        private decimal _importe;
        public decimal Importe { 
            get => _importe; 
            set => SetProperty(ref _importe, value); 
        }
        public decimal ImporteACuenta => TotalCobrado > ImporteDeudasSeleccionadas ? TotalCobrado - ImporteDeudasSeleccionadas : 0;
        public decimal ImporteDescuadre => ArqueoFondo.TotalArqueo - _saldoCuentaOrigen - FondoCaja;
        private decimal _importeDeudasSeleccionadas;
        public decimal ImporteDeudasSeleccionadas
        {
            get => _importeDeudasSeleccionadas;
            set
            {
                SetProperty(ref _importeDeudasSeleccionadas, value);
                RaisePropertyChanged(nameof(ImporteACuenta));
                RaisePropertyChanged(nameof(EstaVisibleImporteACuenta));
            }
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
        private ObservableCollection<ContabilidadDTO> _movimientosEfectivoDia;
        public ObservableCollection<ContabilidadDTO> MovimientosEfectivoDia
        {
            get => _movimientosEfectivoDia;
            set => SetProperty(ref _movimientosEfectivoDia, value);
        }
        private ObservableCollection<ContabilidadDTO> _movimientosTarjetaDia;
        public ObservableCollection<ContabilidadDTO> MovimientosTarjetaDia
        {
            get => _movimientosTarjetaDia;
            set => SetProperty(ref _movimientosTarjetaDia, value);
        }
        private bool _puedeContabilizarDescuadrado;
        public bool PuedeContabilizarDescuadrado
        {
            get => _puedeContabilizarDescuadrado;
            set => SetProperty(ref _puedeContabilizarDescuadrado, value);
        }


        private ProveedorDTO _proveedorGasto;
        public ProveedorDTO ProveedorGasto
        {
            get => _proveedorGasto;
            set
            {
                SetProperty(ref _proveedorGasto, value);
                (ContabilizarGastoCommand as DelegateCommand).RaiseCanExecuteChanged();
            }
        }

        private decimal _totalCobrado;
        public decimal TotalCobrado
        {
            get => _totalCobrado;
            set
            {
                SetProperty(ref _totalCobrado, value);
                RaisePropertyChanged(nameof(ImporteACuenta));
                RaisePropertyChanged(nameof(EstaVisibleImporteACuenta));
                (ContabilizarCobroCommand as DelegateCommand).RaiseCanExecuteChanged();
            }
        }
        private decimal _totalGasto;
        public decimal TotalGasto
        {
            get => _totalGasto;
            set 
            { 
                SetProperty(ref _totalGasto, value);
                (ContabilizarGastoCommand as DelegateCommand).RaiseCanExecuteChanged();
            }
        }

        #endregion





        #region Comandos
        public ICommand ContabilizarCobroCommand { get; private set; }
        private bool CanContabilizarCobro()
        {
            return ImporteDeudasSeleccionadas > 0 || TotalCobrado != 0;
        }
        private async void OnContabilizarCobro()
        {

            List<PreContabilidadDTO> lineas = new();
            var importeRestante = Math.Round(TotalCobrado, 2, MidpointRounding.AwayFromZero);
            TotalCobrado = Math.Round(TotalCobrado, 2, MidpointRounding.AwayFromZero);

            foreach (var deuda in DeudasSeleccionadas)
            {
                decimal importeApunte = 0;
                string esPagoACuenta = importeRestante < deuda.ImportePendiente ? " a cta." : string.Empty;

                if (deuda.ImportePendiente >= importeRestante)
                {
                    importeApunte = importeRestante;
                    importeRestante = 0;
                }
                else
                {
                    importeApunte = deuda.ImportePendiente;
                    importeRestante -= deuda.ImportePendiente;
                }
                string tipoApunte = NombreTipo(deuda);
                string pagoODevolucion = importeApunte > 0 ? "S/Pago" : "Devolución";
                
                PreContabilidadDTO linea = new PreContabilidadDTO
                {
                    Empresa = deuda.Empresa,
                    TipoApunte = Constantes.TiposApunte.PAGO,
                    TipoCuenta = Constantes.TiposCuenta.CLIENTE,
                    Cuenta = deuda.Cliente,
                    Contacto = deuda.Contacto,
                    Concepto = $"{pagoODevolucion} {tipoApunte} {deuda.Documento}/{deuda.Efecto}{esPagoACuenta}",
                    Debe = importeApunte < 0 ? -importeApunte : 0,
                    Haber = importeApunte > 0 ? importeApunte : 0,
                    Fecha = DateOnly.FromDateTime(FechaCobro),
                    FechaVto = DateOnly.FromDateTime(deuda.Vencimiento),
                    Documento = deuda.Documento,
                    Efecto = deuda.Efecto,
                    Asiento = 1,
                    Diario = _diarioCaja,
                    Delegacion = _delegacionUsuario,
                    FormaVenta = deuda.FormaVenta,
                    FormaPago = FormaPagoSeleccionada.formaPago,
                    Ruta = deuda.Ruta,
                    Origen = Constantes.Empresas.EMPRESA_DEFECTO,
                    Liquidado = deuda.Id,
                    Vendedor = deuda.Vendedor,
                    Usuario = _configuracion.usuario,
                    FechaModificacion = DateTime.Now
                };
                if (!EsUsuarioDeAdministracion)
                {
                    linea.Concepto += $" a {_configuracion.usuario}";
                }
                lineas.Add(linea);
                if(importeRestante <= 0)
                {
                    break;
                }
            }

            if (importeRestante != ImporteACuenta)
            {
                throw new Exception($"Error en el algoritmo de cobros: {importeRestante.ToString("c")} es distinto a {ImporteACuenta.ToString("c")}.");
            }

            // Metemos una línea por el importe pagado a cuenta
            //if (importeRestante != 0 && lineas.Any()) // hay alguna deuda seleccionada
            //{
            //    PreContabilidadDTO linea = new PreContabilidadDTO
            //    {
            //        Empresa = lineas[0].Empresa,
            //        TipoApunte = Constantes.TiposApunte.PAGO,
            //        TipoCuenta = Constantes.TiposCuenta.CLIENTE,
            //        Cuenta = lineas[0].Cuenta,
            //        Contacto = lineas[0].Contacto,
            //        Concepto = $"S/Pago a cuenta {lineas[0].Documento}/{lineas[0].Efecto}",
            //        Debe = importeRestante < 0 ? -importeRestante : 0,
            //        Haber = importeRestante > 0 ? importeRestante : 0,
            //        Fecha = DateOnly.FromDateTime(FechaCobro),
            //        FechaVto = DateOnly.FromDateTime(FechaCobro),
            //        Documento = lineas[0].Documento,
            //        Efecto = lineas[0].Efecto,
            //        Asiento = 1,
            //        Diario = _diarioCaja,
            //        Delegacion = _delegacionUsuario,
            //        FormaVenta = lineas[0].FormaVenta,
            //        FormaPago = FormaPagoSeleccionada.formaPago,
            //        Ruta = lineas[0].Ruta,
            //        Origen = Constantes.Empresas.EMPRESA_DEFECTO,
            //        Vendedor = lineas[0].Vendedor,
            //        Usuario = _configuracion.usuario,
            //        FechaModificacion = DateTime.Now
            //    };
            //    if (!EsUsuarioDeAdministracion)
            //    {
            //        linea.Concepto += $" a {_configuracion.usuario}";
            //    }
            //    lineas.Add(linea);
            //}

            // Metemos una línea por el importe pagado a cuenta
            if (importeRestante != 0)  // no hay ninguna deuda seleccionada
            {
                PreContabilidadDTO linea = new PreContabilidadDTO
                {
                    Empresa = ClienteCompletoSeleccionado.empresa,
                    TipoApunte = Constantes.TiposApunte.PAGO,
                    TipoCuenta = Constantes.TiposCuenta.CLIENTE,
                    Cuenta = ClienteCompletoSeleccionado.cliente,
                    Contacto = ClienteCompletoSeleccionado.contacto,
                    Concepto = $"S/Pago a cuenta",
                    Debe = importeRestante < 0 ? -importeRestante : 0,
                    Haber = importeRestante > 0 ? importeRestante : 0,
                    Fecha = DateOnly.FromDateTime(FechaCobro),
                    FechaVto = DateOnly.FromDateTime(FechaCobro),
                    Documento = "A CUENTA",
                    Asiento = 1,
                    Diario = _diarioCaja,
                    Delegacion = _delegacionUsuario,
                    FormaVenta = _formaVentaUsuario,
                    FormaPago = FormaPagoSeleccionada.formaPago,
                    Origen = Constantes.Empresas.EMPRESA_DEFECTO,
                    Usuario = _configuracion.usuario,
                    FechaModificacion = DateTime.Now
                };
                if (!EsUsuarioDeAdministracion)
                {
                    linea.Concepto += $" a {_configuracion.usuario}";
                }
                lineas.Add(linea);
            }
            

            // Creamos la contrapartida al banco
            var empresas = lineas.Select(e => e.Empresa).Distinct().ToList();
            var deudaConMayorImporte = DeudasSeleccionadas.OrderByDescending(e => e.Importe).FirstOrDefault();
            string tipoApunteContrapartida = NombreTipo(deudaConMayorImporte);
            string coletillaYOtros = string.Empty;
            foreach (var empresa in empresas)
            {
                string pagoODevolucion = TotalCobrado > 0 ? "Pago" : "Devolución";
                if (deudaConMayorImporte is not null)
                {
                    if (deudaConMayorImporte.ImportePendiente < TotalCobrado || DeudasSeleccionadas.Count() > 1)
                    {
                        coletillaYOtros = " y otros";
                    }
                    else if (deudaConMayorImporte.ImportePendiente > TotalCobrado)
                    {
                        coletillaYOtros = " a cta.";
                    }
                    PreContabilidadDTO contrapartida = new PreContabilidadDTO()
                    {
                        Empresa = empresa,
                        TipoApunte = Constantes.TiposApunte.PAGO,
                        TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                        Cuenta = CuentaCobro.Cuenta,
                        Concepto = $"{pagoODevolucion} c/{deudaConMayorImporte.Cliente}/{deudaConMayorImporte.Contacto} {tipoApunteContrapartida} {deudaConMayorImporte.Documento}/{deudaConMayorImporte.Efecto}{coletillaYOtros}",
                        Debe = TotalCobrado > 0 ? TotalCobrado : 0,
                        Haber = TotalCobrado < 0 ? -TotalCobrado : 0,
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
                else
                {
                    PreContabilidadDTO contrapartida = new PreContabilidadDTO()
                    {
                        Empresa = empresa,
                        TipoApunte = Constantes.TiposApunte.PAGO,
                        TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                        Cuenta = CuentaCobro.Cuenta,
                        Concepto = $"{pagoODevolucion} c/{ClienteCompletoSeleccionado.cliente}/{ClienteCompletoSeleccionado.contacto} {tipoApunteContrapartida}",
                        Debe = TotalCobrado > 0 ? TotalCobrado : 0,
                        Haber = TotalCobrado < 0 ? -TotalCobrado : 0,
                        Fecha = DateOnly.FromDateTime(FechaCobro),
                        FechaVto = DateOnly.FromDateTime(FechaCobro),
                        Documento = "A CUENTA",
                        Asiento = 1,
                        Diario = _diarioCaja,
                        Delegacion = _delegacionUsuario,
                        FormaVenta = _formaVentaUsuario,
                        FormaPago = FormaPagoSeleccionada.formaPago,
                        Origen = Constantes.Empresas.EMPRESA_DEFECTO,
                        Usuario = _configuracion.usuario,
                        FechaModificacion = DateTime.Now
                    };
                    lineas.Add(contrapartida);
                }
            }
            
            try
            {
                int asiento = await Servicio.Contabilizar(lineas);
                if (asiento <= 0)
                {
                    _dialogService.ShowError($"No se ha podido contabilizar el cobro");
                }
                else
                {
                    _dialogService.ShowNotification($"Creado asiento {asiento} correctamente");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Error al contabilizar el diario.\n" + ex.Message);
            }
        }

        public ICommand ContabilizarGastoCommand { get; private set; }
        private bool CanContabilizarGasto() => ProveedorGasto is not null &&
            !string.IsNullOrEmpty(ProveedorGasto.Proveedor) &&
            TotalGasto > 0 &&
            !string.IsNullOrEmpty(GastoNumeroFactura);
        private async void OnContabilizarGasto()
        {
            PreContabilidadDTO linea = new PreContabilidadDTO
            {
                Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                TipoApunte = Constantes.TiposApunte.PAGO,
                TipoCuenta = Constantes.TiposCuenta.PROVEEDOR,
                Cuenta = ProveedorGasto.Proveedor,
                Contacto = ProveedorGasto.Contacto,
                Concepto = $"N/Pago {GastoNumeroFactura}",
                Debe = TotalGasto,
                Fecha = DateOnly.FromDateTime(FechaCobro),
                FechaVto = DateOnly.FromDateTime(FechaCobro),
                Documento = GastoNumeroFactura.Substring(0,10),
                FacturaProveedor = GastoNumeroFactura,
                Asiento = 1,
                Diario = _diarioCaja,
                Delegacion = _delegacionUsuario,
                FormaVenta = _formaVentaUsuario,
                FormaPago = Constantes.FormasPago.EFECTIVO,
                Origen = Constantes.Empresas.EMPRESA_DEFECTO,
                Contrapartida = _cuentaCajaDefecto,
                Usuario = _configuracion.usuario,
                FechaModificacion = DateTime.Now
            };
            if (!EsUsuarioDeAdministracion)
            {
                linea.Concepto += $" caja {_configuracion.usuario}";
            }
            try
            {
                int asiento = await Servicio.Contabilizar(linea);
                if (asiento <= 0)
                {
                    _dialogService.ShowError($"No se ha podido contabilizar el gasto");
                }
                else
                {
                    _dialogService.ShowNotification($"Creado asiento {asiento} correctamente");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Error al contabilizar el gasto.\n" + ex.Message);
            }
        }

        public ICommand ContabilizarTraspasoCommand { get; private set; }
        private bool CanContabilizarTraspaso()
        {
            return CuentaOrigen is not null && CuentaDestino is not null && !string.IsNullOrEmpty(Concepto) && CuentaOrigen.Cuenta != CuentaDestino.Cuenta
                && (PuedeContabilizarDescuadrado || ImporteDescuadre == 0);
        }
        private async void OnContabilizarTraspaso()
        {
            var delegacion = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.DelegacionDefecto);
            var formaVenta = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.FormaVentaDefecto);

            // si no _puedeContabilizarDescuadrado y hay movimientos de varias empresas hay que hacer un traspaso por cada empresa.
            decimal saldoEspejo = 0;
            if (!PuedeContabilizarDescuadrado)
            {
                saldoEspejo = await Servicio.SaldoCuenta(Constantes.Empresas.EMPRESA_ESPEJO, CuentaOrigen.Cuenta, DateTime.Today);
                if (saldoEspejo != 0)
                {
                    PreContabilidadDTO lineaEspejo = new PreContabilidadDTO
                    {
                        Empresa = Constantes.Empresas.EMPRESA_ESPEJO,
                        TipoApunte = Constantes.TiposApunte.PASO_A_CARTERA,
                        TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                        Cuenta = CuentaOrigen.Cuenta,
                        Concepto = Concepto,
                        Haber = saldoEspejo,
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
                    int asiento = await Servicio.Contabilizar(lineaEspejo);
                    
                    if (asiento <= 0)
                    {
                        _dialogService.ShowError("No se ha podido contabilizar el asiento");
                    }
                    else
                    {
                        if (saldoEspejo == Importe)
                        {
                            _dialogService.ShowError($"Creado asiento {asiento} correctamente");
                        }
                    }
                }
            }
            
            if (saldoEspejo == Importe)
            {
                return;
            }

            PreContabilidadDTO linea = new PreContabilidadDTO
            {
                Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                TipoApunte = Constantes.TiposApunte.PASO_A_CARTERA,
                TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                Cuenta = CuentaOrigen.Cuenta,
                Concepto = Concepto,
                Haber = Importe - saldoEspejo,
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
            } 
            catch (Exception ex)
            {
                _dialogService.ShowError("Error al contabilizar el diario.\n"+ex.Message);
            }
        }

        // Comando para el evento Loaded
        public ICommand LoadedCommand { get; }

        // Método para manejar el evento Loaded
        private async void OnLoaded()
        {
            await CargarDatosIniciales();
            // Resto de la lógica de inicialización que depende de la carga de datos
        }

        public ICommand SeleccionarDeudasCommand { get; private set; }
        private void OnSeleccionarDeudas(IList apuntes)
        {
            bool actualizarTotalCobrado = TotalCobrado == ImporteDeudasSeleccionadas;
            var selectedItems = apuntes.OfType<ExtractoClienteDTO>();
            DeudasSeleccionadas = selectedItems;
            ImporteDeudasSeleccionadas = selectedItems
                .Select(s => s.ImportePendiente)
                .DefaultIfEmpty(0)
                .Sum();
            ((DelegateCommand)ContabilizarCobroCommand).RaiseCanExecuteChanged();

            if (actualizarTotalCobrado)
            {
                TotalCobrado = ImporteDeudasSeleccionadas;
            }
        }
        #endregion


        #region Funciones auxiliares
        private void ArqueoFondo_Changed(object? sender, PropertyChangedEventArgs e)
        {
            Importe = ArqueoFondo.TotalArqueo;
            RaisePropertyChanged(nameof(ImporteDescuadre));
            ((DelegateCommand)ContabilizarTraspasoCommand).RaiseCanExecuteChanged();
        }
        private async Task CargarDatosIniciales()
        {
            ListaCuentasCaja = new ObservableCollection<CuentaContableDTO>(await Servicio.LeerCuentas(Constantes.Empresas.EMPRESA_DEFECTO, CUENTA_TESORERIA_PGC));
            
            _cuentaCajaDefecto = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.CajaDefecto);
            _cuentaTarjetaDefecto = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.CuentaBancoTarjeta);
            _delegacionUsuario = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.DelegacionDefecto);
            _formaVentaUsuario = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.FormaVentaDefecto);
                        
            if (EsUsuarioDeAdministracion)
            {
                PuedeContabilizarDescuadrado = true;
                FondoCaja = 0;
                FormaPagoTransferenciaSeleccionada = true;
                _diarioCaja = Constantes.DiariosContables.DIARIO_CAJA;
                CuentaOrigen = ListaCuentasCaja.Single(p => p.Cuenta == Constantes.Cuentas.CAJA_PENDIENTE_RECIBIR_TIENDAS);
                CuentaDestino = ListaCuentasCaja.Single(p => p.Cuenta == _cuentaCajaDefecto);                
            }
            else
            {
                PuedeContabilizarDescuadrado = false;
                decimal.TryParse(await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.FondoCaja), out _fondoCaja);
                RaisePropertyChanged(nameof(FondoCaja));
                FormaPagoEfectivoSeleccionada = true;
                _diarioCaja = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.UltDiarioCaja);
                CuentaOrigen = ListaCuentasCaja.Single(p => p.Cuenta == _cuentaCajaDefecto);
                CuentaDestino = ListaCuentasCaja.Single(p => p.Cuenta == Constantes.Cuentas.CAJA_PENDIENTE_RECIBIR_TIENDAS);
            }

            MovimientosEfectivoDia = new ObservableCollection<ContabilidadDTO>(await Servicio.LeerApuntesContabilidad(string.Empty, CuentaOrigen.Cuenta, DateTime.Today, DateTime.Today));
            MovimientosTarjetaDia = new ObservableCollection<ContabilidadDTO>((await Servicio.LeerApuntesContabilidad(string.Empty, _cuentaTarjetaDefecto, DateTime.Today, DateTime.Today))
                .Where(c => c.Usuario == _configuracion.usuario));

            if (string.IsNullOrWhiteSpace(_diarioCaja))
            {
                throw new Exception("No se ha podido determinar el diario de caja a usar");
            }
        }
        private async Task CargarDeudasCliente()
        {
            ListaDeudas = new ObservableCollection<ExtractoClienteDTO>((await _clientesService.LeerDeudas(ClienteSeleccionado)).OrderBy(e => e.Id));
        }
        private async Task CalcularSaldoCuentaOrigen()
        {
            _saldoCuentaOrigen = await Servicio.SaldoCuenta(string.Empty, CuentaOrigen.Cuenta, DateTime.Today);
            MovimientosEfectivoDia = new ObservableCollection<ContabilidadDTO>(await Servicio.LeerApuntesContabilidad(string.Empty, CuentaOrigen.Cuenta, DateTime.Today, DateTime.Today));
            RaisePropertyChanged(nameof(ImporteDescuadre));
            ((DelegateCommand)ContabilizarTraspasoCommand).RaiseCanExecuteChanged();
        }
        private static string NombreTipo(ExtractoClienteDTO deuda)
        {
            if (deuda is null)
            {
                return "a cuenta";
            }
            return deuda.Tipo == Constantes.TiposApunte.IMPAGADO ? "Impagado" : "Factura";
        }
        #endregion
    }
}
