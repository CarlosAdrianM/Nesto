﻿using ControlesUsuario;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using Microsoft.Reporting.NETCore;
using Nesto.Informes;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Prism.Commands;
using Prism.Services.Dialogs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using static ControlesUsuario.Models.SelectorProveedorModel;
using static Nesto.Infrastructure.Shared.FuncionesAuxiliares;

namespace Nesto.Modulos.Cajas.ViewModels
{
    public class CajasViewModel : ViewModelBase
    {
        public const string CUENTA_TESORERIA_PGC = "57";
        private const int DIAS_ATRAS_PERMITIDOS = 4;
        private readonly IConfiguracion _configuracion;
        private readonly IDialogService _dialogService;
        private readonly IClientesService _clientesService;
        private string _cuentaCajaDefecto;
        private string _cuentaTarjetaDefecto;
        private string _diarioCaja;
        private string _formaVentaUsuario;
        private decimal _saldoCuentaOrigen;

        public IContabilidadService Servicio { get; }
        public CajasViewModel(IContabilidadService servicio, IConfiguracion configuracion, IDialogService dialogService, IClientesService clientesService)
        {
            Titulo = "Cajas";
            Servicio = servicio;
            _configuracion = configuracion;
            _dialogService = dialogService;
            _clientesService = clientesService;
            _fechaCobro = _fechaHasta;

            ArqueoFondo = new();
            DeudasSeleccionadas = [];
            MovimientoCajaPendientesRecibirSeleccionado = null; // para que no haya ninguno selecciondo al iniciar

            CambiarEmpresaTraspasoCommand = new DelegateCommand(OnCambiarEmpresaTraspaso);
            ContabilizarCobroCommand = new DelegateCommand(OnContabilizarCobro, CanContabilizarCobro);
            ContabilizarGastoCommand = new DelegateCommand(OnContabilizarGasto, CanContabilizarGasto);
            ContabilizarTraspasoCommand = new DelegateCommand(OnContabilizarTraspaso, CanContabilizarTraspaso);
            ImprimirExtractoCommand = new DelegateCommand(OnImprimirExtracto, CanImprimirExtracto);
            LoadedCommand = new DelegateCommand(OnLoaded);
            SeleccionarDeudasCommand = new DelegateCommand<IList>(OnSeleccionarDeudas);
            SeleccionarPendientesRecibirCommand = new DelegateCommand<IList>(OnSeleccionarPendientesRecibir);

            // suscribirse a los cambios de ArqueoFondo.TotalArqueo para que cuando cambie actualicemos el importe del traspaso
            ArqueoFondo.PropertyChanged += ArqueoFondo_Changed;
        }


        #region "Propiedades"
        private ArqueoEfectivoModel _arqueoFondo;
        public ArqueoEfectivoModel ArqueoFondo
        {
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
                _ = SetProperty(ref _clienteSeleccionado, value);
                _ = CargarDeudasCliente();
                (ContabilizarCobroCommand as DelegateCommand).RaiseCanExecuteChanged();
            }
        }

        private string _concepto;
        public string Concepto
        {
            get => _concepto;
            set
            {
                _ = SetProperty(ref _concepto, Truncar(value, 50));
                ((DelegateCommand)ContabilizarTraspasoCommand).RaiseCanExecuteChanged();
            }
        }

        private string _conceptoAdicionalCobros;
        public string ConceptoAdicionalCobros
        {
            get => _conceptoAdicionalCobros;
            set => SetProperty(ref _conceptoAdicionalCobros, value);
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
                _ = SetProperty(ref _cuentaDestino, value);
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
                    _ = CalcularSaldoCuentaOrigen();
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
        private string _empresaTraspaso = Constantes.Empresas.EMPRESA_DEFECTO;
        public string EmpresaTraspaso
        {
            get => _empresaTraspaso;
            set
            {
                if (SetProperty(ref _empresaTraspaso, value))
                {
                    if (_empresaTraspaso == Constantes.Empresas.EMPRESA_ESPEJO)
                    {
                        FormaPagoEfectivoSeleccionada = true;
                    }
                    RaisePropertyChanged(nameof(EmpresaTraspasoMarca));
                }
            }
        }
        public string EmpresaTraspasoMarca => EmpresaTraspaso == Constantes.Empresas.EMPRESA_DEFECTO ? string.Empty : "*";
        public bool EstaCuadradoConCajasPendientesRecibir => MovimientosCajaPendientesRecibirSeleccionados == null ||
            !MovimientosCajaPendientesRecibirSeleccionados.Any() ||
            MovimientosCajaPendientesRecibirSeleccionados.Sum(m => m.Importe) == ArqueoFondo.TotalArqueo;
        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            set => SetProperty(ref _estaOcupado, value);
        }
        public bool EstaVisibleImporteACuenta => ImporteACuenta != 0;
        public bool EsUsuarioDeAdministracion => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
        private DateTime _fechaCobro;
        public DateTime FechaCobro
        {
            get => _fechaCobro;
            set => SetProperty(ref _fechaCobro, value);
        }
        private DateTime _fechaDesde = DateTime.Today;
        public DateTime FechaDesde
        {
            get => _fechaDesde;
            set
            {
                if (value > _fechaHasta)
                {
                    return;
                }
                if (!EsUsuarioDeAdministracion)
                {
                    TimeSpan dias = _fechaHasta - value;
                    if (dias.TotalDays > DIAS_ATRAS_PERMITIDOS)
                    {
                        return;
                    }
                }
                _ = SetProperty(ref _fechaDesde, value);
                RaisePropertyChanged(nameof(FechaDesdeMinima));
                _ = CargarDatosIniciales();
            }
        }
        public DateTime FechaDesdeMinima => EsUsuarioDeAdministracion ? DateTime.MinValue : _fechaDesde.AddDays(-DIAS_ATRAS_PERMITIDOS);
        private DateTime _fechaHasta = DateTime.Today;
        public DateTime FechaHasta
        {
            get => _fechaHasta;
            set => SetProperty(ref _fechaHasta, value);
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
                if (value == true && EmpresaTraspaso == Constantes.Empresas.EMPRESA_ESPEJO)
                {
                    return;
                }
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
                if (value == true && EmpresaTraspaso == Constantes.Empresas.EMPRESA_ESPEJO)
                {
                    return;
                }
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
                _ = SetProperty(ref _gastoNumeroFactura, value);
                (ContabilizarGastoCommand as DelegateCommand).RaiseCanExecuteChanged();
            }
        }
        private decimal _importe;
        public decimal Importe
        {
            get => _importe;
            set
            {
                if (SetProperty(ref _importe, value))
                {
                    ((DelegateCommand)ContabilizarTraspasoCommand).RaiseCanExecuteChanged();
                    RaisePropertyChanged(nameof(ImporteDescuadre));
                }
            }
        }
        public decimal ImporteACuenta => TotalCobrado > ImporteDeudasSeleccionadas ? TotalCobrado - ImporteDeudasSeleccionadas : 0;
        public decimal ImporteDescuadre => HayCajasPendientesDeRecibirSeleccionadas() ?
            ArqueoFondo.TotalArqueo - MovimientosCajaPendientesRecibirSeleccionados.Sum(m => m.Importe) :
            ArqueoFondo.TotalArqueo - _saldoCuentaOrigen - FondoCaja;
        private decimal _importeDeudasSeleccionadas;
        public decimal ImporteDeudasSeleccionadas
        {
            get => _importeDeudasSeleccionadas;
            set
            {
                _ = SetProperty(ref _importeDeudasSeleccionadas, value);
                RaisePropertyChanged(nameof(ImporteACuenta));
                RaisePropertyChanged(nameof(EstaVisibleImporteACuenta));
            }
        }
        private ObservableCollection<CuentaContableDTO> _listaCuentasCaja;
        public ObservableCollection<CuentaContableDTO> ListaCuentasCaja
        {
            get => _listaCuentasCaja;
            set => SetProperty(ref _listaCuentasCaja, value);
        }
        private ObservableCollection<ExtractoClienteDTO> _listaDeudas;
        public ObservableCollection<ExtractoClienteDTO> ListaDeudas
        {
            get => _listaDeudas;
            set => SetProperty(ref _listaDeudas, value);
        }
        private List<FormaPago> _listaFormaPago;
        public List<FormaPago> ListaFormaPago
        {
            get => _listaFormaPago;
            set => SetProperty(ref _listaFormaPago, value);
        }
        private IEnumerable<ContabilidadDTO> _movimientoCajaPendientesRecibirSeleccionado;
        public IEnumerable<ContabilidadDTO> MovimientoCajaPendientesRecibirSeleccionado
        {
            get => _movimientoCajaPendientesRecibirSeleccionado;
            set => SetProperty(ref _movimientoCajaPendientesRecibirSeleccionado, value);
        }
        private ObservableCollection<ContabilidadDTO> _movimientosCajaPendientesRecibir;
        public ObservableCollection<ContabilidadDTO> MovimientosCajaPendientesRecibir
        {
            get => _movimientosCajaPendientesRecibir;
            set => SetProperty(ref _movimientosCajaPendientesRecibir, value);
        }

        private IEnumerable<ContabilidadDTO> _movimientosCajaPendientesRecibirSeleccionados;
        public IEnumerable<ContabilidadDTO> MovimientosCajaPendientesRecibirSeleccionados
        {
            get => _movimientosCajaPendientesRecibirSeleccionados;
            set => SetProperty(ref _movimientosCajaPendientesRecibirSeleccionados, value);
        }

        private ObservableCollection<ContabilidadDTO> _movimientosEfectivoDia;
        public ObservableCollection<ContabilidadDTO> MovimientosEfectivoDia
        {
            get => _movimientosEfectivoDia;
            set
            {
                if (SetProperty(ref _movimientosEfectivoDia, value))
                {
                    RaisePropertyChanged(nameof(ImporteDescuadre));
                }
            }
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
                _ = SetProperty(ref _proveedorGasto, value);
                (ContabilizarGastoCommand as DelegateCommand).RaiseCanExecuteChanged();
            }
        }

        private decimal _totalCobrado;
        public decimal TotalCobrado
        {
            get => _totalCobrado;
            set
            {
                _ = SetProperty(ref _totalCobrado, value);
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
                _ = SetProperty(ref _totalGasto, value);
                (ContabilizarGastoCommand as DelegateCommand).RaiseCanExecuteChanged();
            }
        }

        #endregion





        #region Comandos
        public ICommand CambiarEmpresaTraspasoCommand { get; private set; }
        private void OnCambiarEmpresaTraspaso()
        {
            EmpresaTraspaso = (EmpresaTraspaso == Constantes.Empresas.EMPRESA_DEFECTO)
                ? Constantes.Empresas.EMPRESA_ESPEJO
                : Constantes.Empresas.EMPRESA_DEFECTO;
        }
        public ICommand ContabilizarCobroCommand { get; private set; }
        private bool CanContabilizarCobro()
        {
            return (ImporteDeudasSeleccionadas > 0 || TotalCobrado != 0) && ClienteSeleccionado is not null;
        }
        private async void OnContabilizarCobro()
        {

            List<PreContabilidadDTO> lineas = [];
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

                PreContabilidadDTO linea = new()
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
                    linea.Concepto += $" a {_configuracion.UsuarioSinDominio}";
                }
                lineas.Add(linea);
                if (importeRestante <= 0)
                {
                    break;
                }
            }

            if (importeRestante != ImporteACuenta)
            {
                throw new Exception($"Error en el algoritmo de cobros: {importeRestante:c} es distinto a {ImporteACuenta:c}.");
            }

            // Metemos una línea por el importe pagado a cuenta
            if (importeRestante != 0)  // no hay ninguna deuda seleccionada
            {
                PreContabilidadDTO linea = new()
                {
                    Empresa = EmpresaTraspaso,
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
                    linea.Concepto += $" a {_configuracion.UsuarioSinDominio}";
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
                    PreContabilidadDTO contrapartida = new()
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
                    PreContabilidadDTO contrapartida = new()
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
                    if (!EsUsuarioDeAdministracion)
                    {
                        contrapartida.Concepto += $" a {_configuracion.UsuarioSinDominio}";
                    }
                    if (contrapartida.Concepto.Length > 50)
                    {
                        contrapartida.Concepto = contrapartida.Concepto[..50];
                    }
                    lineas.Add(contrapartida);
                }
            }

            try
            {
                foreach (var linea in lineas)
                {
                    linea.Concepto = Truncar($"{linea.Concepto} {ConceptoAdicionalCobros}", 50);
                }
                int asiento = await Servicio.Contabilizar(lineas);
                if (asiento <= 0)
                {
                    _dialogService.ShowError($"No se ha podido contabilizar el cobro");
                }
                else
                {
                    _dialogService.ShowNotification($"Creado asiento {asiento} correctamente");
                    ConceptoAdicionalCobros = string.Empty;
                    await CargarDatosIniciales();
                    await CargarDeudasCliente();
                    TotalCobrado = 0;
                    if (!string.IsNullOrEmpty(EmpresaTraspasoMarca))
                    {
                        CambiarEmpresaTraspasoCommand.Execute(null);
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Error al contabilizar el diario.\n" + ex.Message);
            }
        }

        public ICommand ContabilizarGastoCommand { get; private set; }
        private bool CanContabilizarGasto()
        {
            return ProveedorGasto is not null &&
            !string.IsNullOrEmpty(ProveedorGasto.Proveedor) &&
            TotalGasto != 0 &&
            !string.IsNullOrEmpty(GastoNumeroFactura);
        }

        private async void OnContabilizarGasto()
        {
            if (TotalGasto < 0 && !_dialogService.ShowConfirmationAnswer("Gasto negativo", "¿Está seguro que desea contabilizar un gasto negativo?"))
            {
                return;
            }
            string subcadenaGastoNumeroFactura = GastoNumeroFactura.Length >= 10 ? GastoNumeroFactura[..10] : GastoNumeroFactura;
            PreContabilidadDTO linea = new()
            {
                Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                TipoApunte = Constantes.TiposApunte.PAGO,
                TipoCuenta = Constantes.TiposCuenta.PROVEEDOR,
                Cuenta = ProveedorGasto.Proveedor,
                Contacto = ProveedorGasto.Contacto,
                Concepto = $"N/Pago {GastoNumeroFactura} P/{ProveedorGasto.Proveedor}",
                Debe = TotalGasto,
                Fecha = DateOnly.FromDateTime(FechaCobro),
                FechaVto = DateOnly.FromDateTime(FechaCobro),
                Documento = subcadenaGastoNumeroFactura,
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
                linea.Concepto += $" caja {_configuracion.UsuarioSinDominio}";
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
                    await CargarDatosIniciales();
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
            return Importe != 0 && CuentaOrigen is not null && CuentaDestino is not null && !string.IsNullOrEmpty(Concepto) && CuentaOrigen.Cuenta != CuentaDestino.Cuenta
                && ((PuedeContabilizarDescuadrado && EstaCuadradoConCajasPendientesRecibir) || ImporteDescuadre == 0);
        }
        private async void OnContabilizarTraspaso()
        {
            var delegacion = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.DelegacionDefecto);
            var formaVenta = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.FormaVentaDefecto);

            decimal saldoEspejo = 0;
            try
            {
                EstaOcupado = true;
                if (!PuedeContabilizarDescuadrado)
                {
                    if (!string.IsNullOrEmpty(EmpresaTraspasoMarca))
                    {
                        CambiarEmpresaTraspasoCommand.Execute(null);
                    }
                    saldoEspejo = await Servicio.SaldoCuenta(Constantes.Empresas.EMPRESA_ESPEJO, CuentaOrigen.Cuenta, _fechaHasta);
                    if (saldoEspejo != 0)
                    {
                        PreContabilidadDTO lineaEspejo = new()
                        {
                            Empresa = Constantes.Empresas.EMPRESA_ESPEJO,
                            TipoApunte = Constantes.TiposApunte.PASO_A_CARTERA,
                            TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                            Cuenta = CuentaOrigen.Cuenta,
                            Concepto = Concepto,
                            Haber = saldoEspejo,
                            Fecha = new DateOnly(_fechaHasta.Year, _fechaHasta.Month, _fechaHasta.Day),
                            FechaVto = new DateOnly(_fechaHasta.Year, _fechaHasta.Month, _fechaHasta.Day),
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
                        int asientoEspejo = await Servicio.Contabilizar(lineaEspejo);
                        if (asientoEspejo <= 0)
                        {
                            _dialogService.ShowError("No se ha podido contabilizar el asiento");
                        }
                        else
                        {
                            if (saldoEspejo == Importe)
                            {
                                _ = await Servicio.PuntearPorImporte(Constantes.Empresas.EMPRESA_ESPEJO, CuentaOrigen.Cuenta, Importe);
                                _dialogService.ShowNotification($"Creado asiento {asientoEspejo} correctamente");
                                await CargarDatosIniciales();
                                ArqueoFondo.VaciarArqueo();
                                Importe = 0;
                            }
                        }
                    }
                }

                if (saldoEspejo == Importe || Importe == 0)
                {
                    return;
                }

                List<PreContabilidadDTO> lineasContabilizar = [];

                if (HayCajasPendientesDeRecibirSeleccionadas())
                {
                    foreach (var mov in MovimientosCajaPendientesRecibirSeleccionados)
                    {
                        PreContabilidadDTO linea = new()
                        {
                            Empresa = mov.Empresa,
                            TipoApunte = Constantes.TiposApunte.PASO_A_CARTERA,
                            TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                            Cuenta = CuentaOrigen.Cuenta,
                            Concepto = Concepto,
                            Haber = mov.Importe,
                            Fecha = new DateOnly(_fechaHasta.Year, _fechaHasta.Month, _fechaHasta.Day),
                            FechaVto = new DateOnly(_fechaHasta.Year, _fechaHasta.Month, _fechaHasta.Day),
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
                        lineasContabilizar.Add(linea);
                    }
                }
                else
                {
                    PreContabilidadDTO linea = new()
                    {
                        Empresa = EmpresaTraspaso,
                        TipoApunte = Constantes.TiposApunte.PASO_A_CARTERA,
                        TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                        Cuenta = CuentaOrigen.Cuenta,
                        Concepto = Concepto,
                        Haber = Importe - saldoEspejo,
                        Fecha = new DateOnly(_fechaHasta.Year, _fechaHasta.Month, _fechaHasta.Day),
                        FechaVto = new DateOnly(_fechaHasta.Year, _fechaHasta.Month, _fechaHasta.Day),
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
                    lineasContabilizar.Add(linea);
                }

                try
                {
                    List<(int asiento, decimal importe, string empresa)> asientos = [];

                    foreach (var lineaContabilizar in lineasContabilizar)
                    {
                        lineaContabilizar.Concepto = Truncar($"{lineaContabilizar.Concepto}", 50);
                        int asiento = await Servicio.Contabilizar(lineaContabilizar);
                        decimal importe = lineaContabilizar.Debe + lineaContabilizar.Haber;
                        string empresa = lineaContabilizar.Empresa;
                        asientos.Add((asiento, importe, empresa));
                    }

                    foreach (var (asiento, importe, empresa) in asientos)
                    {
                        if (asiento <= 0)
                        {
                            _dialogService.ShowError("No se ha podido contabilizar el asiento");
                        }
                        else
                        {
                            _ = await Servicio.PuntearPorImporte(empresa, CuentaOrigen.Cuenta, importe);
                            _dialogService.ShowNotification($"Creado asiento {asiento} correctamente");
                            await CargarDatosIniciales();
                            ArqueoFondo.VaciarArqueo();
                            Importe = 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError("Error al contabilizar el diario.\n" + ex.Message);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Error al contabilizar el traspaso.\n" + ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }


        }

        public ICommand ImprimirExtractoCommand { get; private set; }
        private bool CanImprimirExtracto()
        {
            return true;
        }

        private async void OnImprimirExtracto()
        {
            foreach (var empresa in MovimientosEfectivoDia.Select(e => e.Empresa).Distinct())
            {
                LocalReport report = await CrearInformeExtractoContable(empresa, CuentaOrigen, FechaDesde, _fechaHasta);
                var pdf = report.Render("PDF");
                string fileName = Path.GetTempPath() + $"ExtractoCuenta{CuentaOrigen.Cuenta}_{empresa}.pdf";
                System.IO.File.WriteAllBytes(fileName, pdf);
                _ = System.Diagnostics.Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
            }
        }

        // Comando para el evento Loaded
        public ICommand LoadedCommand { get; }

        // Método para manejar el evento Loaded
        private async void OnLoaded()
        {
            try
            {
                await CargarDatosIniciales();
            }
            catch (Exception)
            {
                _dialogService.ShowError("Se ha producido un error al cargar los datos de caja necesarios, por favor, cierre la ventana y vuelva a abrirla");
            }
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

        public ICommand SeleccionarPendientesRecibirCommand { get; private set; }
        private void OnSeleccionarPendientesRecibir(IList cajas)
        {
            var selectedItems = cajas.OfType<ContabilidadDTO>();
            MovimientosCajaPendientesRecibirSeleccionados = selectedItems;
            RaisePropertyChanged(nameof(ImporteDescuadre));
            ((DelegateCommand)ContabilizarTraspasoCommand).RaiseCanExecuteChanged();
            var conceptoComun = selectedItems.FirstOrDefault()?.Concepto;
            if (conceptoComun is null)
            {
                return;
            }
            bool todosIguales = selectedItems.All(item => item.Concepto == conceptoComun);

            if (todosIguales)
            {
                Concepto = $"Traspaso {conceptoComun}";
            }
        }
        #endregion


        #region Funciones auxiliares
        private void ArqueoFondo_Changed(object? sender, PropertyChangedEventArgs e)
        {
            Importe = ArqueoFondo.TotalArqueo - FondoCaja;
            RaisePropertyChanged(nameof(ImporteDescuadre));
            ((DelegateCommand)ContabilizarTraspasoCommand).RaiseCanExecuteChanged();
        }
        private async Task CargarDatosIniciales()
        {
            if (ListaCuentasCaja is null || string.IsNullOrEmpty(_diarioCaja))
            {
                ListaCuentasCaja = [.. await Servicio.LeerCuentas(Constantes.Empresas.EMPRESA_DEFECTO, CUENTA_TESORERIA_PGC)];
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
                    if (CuentaOrigen is null)
                    {
                        CuentaOrigen = ListaCuentasCaja.Single(p => p.Cuenta == Constantes.Cuentas.CAJA_PENDIENTE_RECIBIR_TIENDAS);
                    }
                    else
                    {
                        await CalcularSaldoCuentaOrigen();
                    }
                    CuentaDestino ??= ListaCuentasCaja.Single(p => p.Cuenta == _cuentaCajaDefecto);
                    Concepto = "Traspaso entre cajas";
                }
                else
                {
                    PuedeContabilizarDescuadrado = false;
                    _ = decimal.TryParse(await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.FondoCaja), out _fondoCaja);
                    RaisePropertyChanged(nameof(FondoCaja));
                    FormaPagoEfectivoSeleccionada = true;
                    _diarioCaja = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.DiarioCaja);
                    CuentaOrigen = ListaCuentasCaja.Single(p => p.Cuenta == _cuentaCajaDefecto);
                    CuentaDestino = ListaCuentasCaja.Single(p => p.Cuenta == Constantes.Cuentas.CAJA_PENDIENTE_RECIBIR_TIENDAS);
                    Concepto = $"Cierre de caja de {_diarioCaja}";
                }
            }

            //MovimientosEfectivoDia = new ObservableCollection<ContabilidadDTO>(await Servicio.LeerApuntesContabilidad(string.Empty, CuentaOrigen.Cuenta, FechaDesde, _fechaHasta));
            await CalcularSaldoCuentaOrigen();
            MovimientosTarjetaDia = [.. (await Servicio.LeerApuntesContabilidad(string.Empty, _cuentaTarjetaDefecto, FechaDesde, _fechaHasta))
                .Where(c => c.Usuario == _configuracion.usuario)];
            if (EsUsuarioDeAdministracion)
            {
                MovimientosCajaPendientesRecibir = [.. await Servicio.LeerApuntesContabilidad(CuentaOrigen.Cuenta, false)];
            }

            if (string.IsNullOrWhiteSpace(_diarioCaja))
            {
                throw new Exception("No se ha podido determinar el diario de caja a usar");
            }
        }
        private async Task CargarDeudasCliente()
        {
            ListaDeudas = [.. (await _clientesService.LeerDeudas(ClienteSeleccionado)).OrderBy(e => e.Id)];
        }
        private async Task CalcularSaldoCuentaOrigen()
        {
            if (CuentaOrigen is not null)
            {
                _saldoCuentaOrigen = await Servicio.SaldoCuenta(string.Empty, CuentaOrigen.Cuenta, _fechaHasta);
                MovimientosEfectivoDia = [.. await Servicio.LeerApuntesContabilidad(string.Empty, CuentaOrigen.Cuenta, FechaDesde, _fechaHasta)];
                if (EsUsuarioDeAdministracion)
                {
                    MovimientosCajaPendientesRecibir = [.. await Servicio.LeerApuntesContabilidad(CuentaOrigen.Cuenta, false)];
                }
            }
            else
            {
                _saldoCuentaOrigen = 0;
                MovimientosEfectivoDia = [];
                MovimientosCajaPendientesRecibir = [];
            }

            RaisePropertyChanged(nameof(ImporteDescuadre));
            ((DelegateCommand)ContabilizarTraspasoCommand).RaiseCanExecuteChanged();
        }

        private static async Task<LocalReport> CrearInformeExtractoContable(string empresa, CuentaContableDTO cuenta, DateTime fechaDesde, DateTime fechaHasta)
        {
            Stream reportDefinition = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.ExtractoContable.rdlc");
            List<ExtractoContableModel> dataSource = await ExtractoContableModel.CargarDatos(empresa, cuenta.Cuenta, fechaDesde, fechaHasta);
            LocalReport report = new();
            report.LoadReportDefinition(reportDefinition);
            List<ReportParameter> listaParametros =
            [
                new ReportParameter("Cuenta", cuenta.Cuenta),
                new ReportParameter("FechaDesde", fechaDesde.ToString("d")),
                new ReportParameter("FechaHasta", fechaHasta.ToString("d"))
            ];
            report.SetParameters(listaParametros);

            report.DataSources.Add(new ReportDataSource("ExtractoContableDataSet", dataSource));
            return report;
        }
        private bool HayCajasPendientesDeRecibirSeleccionadas()
        {
            return MovimientosCajaPendientesRecibirSeleccionados != null && MovimientosCajaPendientesRecibirSeleccionados.Any();
        }
        private static string NombreTipo(ExtractoClienteDTO deuda)
        {
            return deuda is null ? "a cuenta" : deuda.Tipo == Constantes.TiposApunte.IMPAGADO ? "Impagado" : "Factura";
        }

        #endregion
    }
}
