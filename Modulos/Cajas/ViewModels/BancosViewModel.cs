using ControlesUsuario.Dialogs;
using Microsoft.Win32;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.Cajas.Bancos;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.Models.ReglasContabilizacion;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Models;
using Nesto.Modulos.PedidoVenta;
using Prism.Commands;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Unity;

namespace Nesto.Modulos.Cajas.ViewModels
{
    public class BancosViewModel : ViewModelBase
    {
        private readonly IBancosService _bancosService;
        private readonly IContabilidadService _contabilidadService;
        private readonly IConfiguracion _configuracion;
        private readonly IDialogService _dialogService;
        private readonly IPedidoCompraService _pedidoCompraService;
        private readonly List<IReglaContabilizacion> _reglasContabilizacion;
        private readonly IUnityContainer _container;
        private readonly IRecursosHumanosService _recursosHumanosService;

        private const string SIMBOLO_PUNTEO_CONCILIACION = "*";

        public BancosViewModel(IBancosService bancosService, IContabilidadService contabilidadService, IConfiguracion configuracion, IDialogService dialogService, IPedidoCompraService pedidoCompraService, IUnityContainer container, IRecursosHumanosService recursosHumanosService)
        {
            _bancosService = bancosService;
            _contabilidadService = contabilidadService;
            _configuracion = configuracion;
            _dialogService = dialogService;
            _pedidoCompraService = pedidoCompraService;
            _container = container;
            _recursosHumanosService = recursosHumanosService;

            ApuntesContabilidad = [];
            ContenidoCuaderno43 = new ContenidoCuaderno43
            {
                Cabecera = new RegistroCabeceraCuenta(),
                FinalCuenta = new RegistroFinalCuenta()
            };

            AbrirPedidoCommand = new DelegateCommand<PrepagoDTO>(OnAbrirPedido);
            CargarArchivoCommand = new DelegateCommand(OnCargarArchivo);
            CargarArchivoTarjetasCommand = new DelegateCommand(OnCargarArchivoTarjetas);
            ContabilizarApunteCommand = new DelegateCommand(OnContabilizarApunte, CanContabilizarApunte);
            CopiarConceptoPortapapelesCommand = new DelegateCommand(OnCopiarConceptoPortapapeles);
            PuntearApuntesCommand = new DelegateCommand(OnPuntearApuntes, CanPuntearApuntes);
            PuntearAutomaticamenteCommand = new DelegateCommand(OnPuntearAutomaticamente, CanPuntearAutomaticamente);
            RegularizarDiferenciaCommand = new DelegateCommand(OnRegularizarDiferencia, CanRegularizarDiferencia);
            SeleccionarApuntesBancoCommand = new DelegateCommand<IList>(OnSeleccionarApuntesBanco);
            SeleccionarApuntesContabilidadCommand = new DelegateCommand<IList>(OnSeleccionarApuntesContabilidad);

            _reglasContabilizacion =
            [
                new ReglaFinanciacionLineaRiesgo(_dialogService, _recursosHumanosService),
                new ReglaAdelantosNomina(),
                new ReglaComisionesBanco(),
                new ReglaComunidadPropietariosAlcobendas(),
                new ReglaComunidadPropietariosAlgete(),
                new ReglaComunidadPropietariosReina(),
                new ReglaSegurosSaludPrivados(),
                new ReglaStripe(),
                new ReglaEmbargo(),
                new ReglaAmazonPayComision(),
                new ReglaPaypal(),
                new ReglaAplazame(),
                new ReglaAsociacionEsteticistas(),
                new ReglaAyuntamientoAlgete(),
                new ReglaAyuntamientoAlcobendas(),
                new ReglaMiraviaComision(),
                new ReglaInteresesAplazamientoConfirming(_dialogService),
                new ReglaComisionRemesaRecibos(_bancosService),
                new ReglaPagoProveedor(_bancosService),
                new ReglaGastoPeriodico(_contabilidadService)
            ];
            ListaBancos =
            [
                new BancoCaixabank(_bancosService),
                new BancoPaypal(_bancosService)
            ];

            Titulo = "Bancos";
        }

        #region "Propiedades"

        private ICollectionView _apuntesBancoCollectionView;
        public ICollectionView ApuntesBancoCollectionView
        {
            get => _apuntesBancoCollectionView;
            set => SetProperty(ref _apuntesBancoCollectionView, value);
        }
        private ApunteBancarioWrapper _apunteBancoSeleccionado;
        public ApunteBancarioWrapper ApunteBancoSeleccionado
        {
            get => _apunteBancoSeleccionado;
            set
            {
                if (SetProperty(ref _apunteBancoSeleccionado, value))
                {
                    // Cancelar la tarea anterior si existe
                    _cargarMovimientosTPVCancellation?.Cancel();
                    _cargarMovimientosTPVCancellation = new CancellationTokenSource();

                    // Comprobamos las condiciones antes de cargar los movimientos relacionados
                    if (BancoSeleccionado.CumpleCondicionesTPV(_apunteBancoSeleccionado))
                    {
                        // Cargar los movimientos relacionados dependiendo del literal Concepto2

                        _ = CargarMovimientosRelacionadosTPV(ApunteBancoSeleccionado.RegistrosConcepto[0].Concepto2[..3],
                                _cargarMovimientosTPVCancellation.Token);
                    }
                    else
                    {
                        MovimientosRelacionados = [];
                        PrepagosPendientes = [];
                    }
                    ((DelegateCommand)ContabilizarApunteCommand).RaiseCanExecuteChanged();
                }
            }
        }
        private ObservableCollection<ApunteBancarioWrapper> _apuntesBanco;
        public ObservableCollection<ApunteBancarioWrapper> ApuntesBanco
        {
            get => _apuntesBanco;
            set
            {
                foreach (ApunteBancarioWrapper apunte in value)
                {
                    if (apunte.ClaveDebeOHaberMovimiento == "1")
                    {
                        apunte.ImporteMovimiento *= -1;
                    }
                }
                _ = SetProperty(ref _apuntesBanco, value);
            }
        }
        private IEnumerable<ApunteBancarioWrapper> _apuntesBancoSeleccionados;
        public IEnumerable<ApunteBancarioWrapper> ApuntesBancoSeleccionados
        {
            get => _apuntesBancoSeleccionados;
            set
            {
                _ = SetProperty(ref _apuntesBancoSeleccionados, value);
                ((DelegateCommand)RegularizarDiferenciaCommand).RaiseCanExecuteChanged();
                ((DelegateCommand)ContabilizarApunteCommand).RaiseCanExecuteChanged();
            }
        }

        private ContabilidadWrapper _apunteContabilidadSeleccionado;
        public ContabilidadWrapper ApunteContabilidadSeleccionado
        {
            get => _apunteContabilidadSeleccionado;
            set
            {
                if (SetProperty(ref _apunteContabilidadSeleccionado, value))
                {
                    _ = CargarExtractoProveedorAsiento(value);
                    ((DelegateCommand)ContabilizarApunteCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private ObservableCollection<ContabilidadWrapper> _apuntesContabilidad;
        public ObservableCollection<ContabilidadWrapper> ApuntesContabilidad
        {
            get => _apuntesContabilidad;
            set
            {
                _ = SetProperty(ref _apuntesContabilidad, value);
                RaisePropertyChanged(nameof(SaldoFinalContabilidad));
                RaisePropertyChanged(nameof(DescuadreSaldoFinal));
            }
        }
        private ICollectionView _apuntesContabilidadCollectionView;
        public ICollectionView ApuntesContabilidadCollectionView
        {
            get => _apuntesContabilidadCollectionView;
            set => SetProperty(ref _apuntesContabilidadCollectionView, value);
        }
        private IEnumerable<ContabilidadWrapper> _apuntesContabilidadSeleccionados;
        public IEnumerable<ContabilidadWrapper> ApuntesContabilidadSeleccionados
        {
            get => _apuntesContabilidadSeleccionados;
            set
            {
                _ = SetProperty(ref _apuntesContabilidadSeleccionados, value);
                ((DelegateCommand)RegularizarDiferenciaCommand).RaiseCanExecuteChanged();
                ((DelegateCommand)ContabilizarApunteCommand).RaiseCanExecuteChanged();
            }
        }

        private IBancoConciliacion _bancoSeleccionado;
        public IBancoConciliacion BancoSeleccionado
        {
            get => _bancoSeleccionado;
            set
            {
                if (SetProperty(ref _bancoSeleccionado, value))
                {
                    if (!_fechasDesdePorBanco.TryGetValue(value.Banco.Codigo, out var fechaDesdeStr))
                    {
                        // Inicializa con la fecha actual o la que prefieras
                        fechaDesdeStr = DateTime.Today.ToString("dd/MM/yy");
                        _fechasDesdePorBanco[value.Banco.Codigo] = fechaDesdeStr;
                        _ = _configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.ConciliacionBancariaFechaDesde, SerializarDiccionarioFechas(_fechasDesdePorBanco));
                    }
                    // Cambiamos la privada para que no se ejecute el setter y por lo tanto no ejecute CargarApuntes.
                    _fechaDesde = DateTime.ParseExact(fechaDesdeStr, "dd/MM/yy", null);

                    if (!_fechasHastaPorBanco.TryGetValue(value.Banco.Codigo, out var fechaHastaStr))
                    {
                        fechaHastaStr = DateTime.Today.ToString("dd/MM/yy");
                        _fechasHastaPorBanco[value.Banco.Codigo] = fechaHastaStr;
                        _ = _configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.ConciliacionBancariaFechaHasta, SerializarDiccionarioFechas(_fechasHastaPorBanco));
                    }
                    FechaHasta = DateTime.ParseExact(fechaHastaStr, "dd/MM/yy", null);
                    RaisePropertyChanged(nameof(FechaDesde));
                }
            }
        }
        private CancellationTokenSource _cargarMovimientosTPVCancellation;
        private ContenidoCuaderno43 _contenidoCuaderno43;
        public ContenidoCuaderno43 ContenidoCuaderno43
        {
            get => _contenidoCuaderno43;
            set
            {
                _ = SetProperty(ref _contenidoCuaderno43, value);
                SaldoInicialBanco = _contenidoCuaderno43?.Cabecera?.ImporteSaldoInicial != null
                    ? _contenidoCuaderno43.Cabecera.ImporteSaldoInicial
                    : 0;
                SaldoFinalBanco = _contenidoCuaderno43?.FinalCuenta?.SaldoFinal != null
                    ? (decimal)_contenidoCuaderno43?.FinalCuenta?.SaldoFinal
                    : 0;
            }
        }
        public decimal DescuadrePunteo => SaldoPunteoApuntesBanco - SaldoPunteoApuntesContabilidad;
        public decimal DescuadreSaldoFinal => SaldoFinalBanco - SaldoFinalContabilidad;
        public decimal DescuadreSaldoInicial => SaldoInicialBanco - SaldoInicialContabilidad;
        private List<ExtractoProveedorDTO> _extractosProveedorAsientoSeleccionado;
        public List<ExtractoProveedorDTO>? ExtractosProveedorAsientoSeleccionado
        {
            get => _extractosProveedorAsientoSeleccionado;
            set => SetProperty(ref _extractosProveedorAsientoSeleccionado, value);
        }
        private DateTime _fechaDesde;
        public DateTime FechaDesde
        {
            get => _fechaDesde;
            set
            {
                if (_fechaDesde != value)
                {
                    _ = SetProperty(ref _fechaDesde, value);
                    // Guardar la fecha en el diccionario y persistir
                    if (BancoSeleccionado?.Banco?.Codigo != null)
                    {
                        _fechasDesdePorBanco[BancoSeleccionado.Banco.Codigo] = value.ToString("dd/MM/yy");
                        _ = _configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.ConciliacionBancariaFechaDesde, SerializarDiccionarioFechas(_fechasDesdePorBanco));
                    }
                    _ = CargarApuntes(FechaDesde, FechaHasta);
                }
            }
        }
        private DateTime _fechaHasta;
        public DateTime FechaHasta
        {
            get => _fechaHasta;
            set
            {
                if (_fechaHasta != value)
                {
                    _ = SetProperty(ref _fechaHasta, value);
                    // Guardar la fecha en el diccionario y persistir
                    if (BancoSeleccionado?.Banco?.Codigo != null)
                    {
                        _fechasHastaPorBanco[BancoSeleccionado.Banco.Codigo] = value.ToString("dd/MM/yy");
                        _ = _configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.ConciliacionBancariaFechaHasta, SerializarDiccionarioFechas(_fechasHastaPorBanco));
                    }
                    _ = CargarApuntes(FechaDesde, FechaHasta);
                }
            }
        }

        private Dictionary<string, string> _fechasDesdePorBanco = [];
        private Dictionary<string, string> _fechasHastaPorBanco = [];

        private bool _isBusyApuntesBanco;
        public bool IsBusyApuntesBanco
        {
            get => _isBusyApuntesBanco;
            set => SetProperty(ref _isBusyApuntesBanco, value);
        }
        private bool _isBusyApuntesContabilidad;
        public bool IsBusyApuntesContabilidad
        {
            get => _isBusyApuntesContabilidad;
            set => SetProperty(ref _isBusyApuntesContabilidad, value);
        }
        private List<IBancoConciliacion> _listaBancos;
        public List<IBancoConciliacion> ListaBancos
        {
            get => _listaBancos;
            set => SetProperty(ref _listaBancos, value);
        }
        private bool _mostrarCompletamentePunteado = false;
        public bool MostrarCompletamentePunteado
        {
            get => _mostrarCompletamentePunteado;
            set
            {
                _ = SetProperty(ref _mostrarCompletamentePunteado, value);
                FiltrarRegistros();
            }
        }

        private bool _mostrarParcialmentePunteado = true;
        public bool MostrarParcialmentePunteado
        {
            get => _mostrarParcialmentePunteado;
            set
            {
                _ = SetProperty(ref _mostrarParcialmentePunteado, value);
                FiltrarRegistros();
            }
        }

        private bool _mostrarSinPuntear = true;
        public bool MostrarSinPuntear
        {
            get => _mostrarSinPuntear;
            set
            {
                _ = SetProperty(ref _mostrarSinPuntear, value);
                FiltrarRegistros();
            }
        }
        private ObservableCollection<MovimientoTPV> _movimientosRelacionados;

        public ObservableCollection<MovimientoTPV> MovimientosRelacionados
        {
            get => _movimientosRelacionados;
            set => SetProperty(ref _movimientosRelacionados, value);
        }


        private List<MovimientoTPV> _movimientosTPV;
        public List<MovimientoTPV> MovimientosTPV
        {
            get => _movimientosTPV;
            set => SetProperty(ref _movimientosTPV, value);
        }
        private MovimientoTPV _movimientoTPVSeleccionado;
        public MovimientoTPV MovimientoTPVSeleccionado
        {
            get => _movimientoTPVSeleccionado;
            set
            {
                if (SetProperty(ref _movimientoTPVSeleccionado, value))
                {
                    _ = CargarPrepagosPendientes();
                }
            }
        }

        private ObservableCollection<PrepagoDTO> _prepagosPendientes;
        public ObservableCollection<PrepagoDTO> PrepagosPendientes
        {
            get => _prepagosPendientes;
            set => SetProperty(ref _prepagosPendientes, value);
        }

        private decimal _saldoFinalBanco;
        public decimal SaldoFinalBanco
        {
            get => _saldoFinalBanco;
            set
            {
                _ = SetProperty(ref _saldoFinalBanco, value);
                RaisePropertyChanged(nameof(DescuadreSaldoFinal));
            }
        }
        private decimal _saldoInicialBanco;
        public decimal SaldoInicialBanco
        {
            get => _saldoInicialBanco;
            set
            {
                _ = SetProperty(ref _saldoInicialBanco, value);
                RaisePropertyChanged(nameof(DescuadreSaldoInicial));
            }
        }
        public decimal SaldoFinalContabilidad => (decimal)(SaldoInicialContabilidad + ApuntesContabilidad?.Sum(c => c.Importe));
        private decimal _saldoInicialContabilidad;
        public decimal SaldoInicialContabilidad
        {
            get => _saldoInicialContabilidad;
            set
            {
                _ = SetProperty(ref _saldoInicialContabilidad, value);
                RaisePropertyChanged(nameof(DescuadreSaldoInicial));
            }
        }
        private decimal _saldoPunteoApuntesBanco;
        public decimal SaldoPunteoApuntesBanco
        {
            get => _saldoPunteoApuntesBanco;
            set
            {
                _ = SetProperty(ref _saldoPunteoApuntesBanco, value);
                RaisePropertyChanged(nameof(DescuadrePunteo));
                ((DelegateCommand)PuntearApuntesCommand).RaiseCanExecuteChanged();
            }
        }
        private decimal _saldoPunteoApuntesContabilidad;
        public decimal SaldoPunteoApuntesContabilidad
        {
            get => _saldoPunteoApuntesContabilidad;
            set
            {
                _ = SetProperty(ref _saldoPunteoApuntesContabilidad, value);
                RaisePropertyChanged(nameof(DescuadrePunteo));
                ((DelegateCommand)PuntearApuntesCommand).RaiseCanExecuteChanged();
            }
        }
        private Dictionary<string, string> _terminalesUsuarios;
        public Dictionary<string, string> TerminalesUsuarios
        {
            get => _terminalesUsuarios; set => SetProperty(ref _terminalesUsuarios, value);
        }
        private string _textoBotonContabilizar;
        public string TextoBotonContabilizar
        {
            get => _textoBotonContabilizar;
            set => SetProperty(ref _textoBotonContabilizar, value);
        }


        private ApunteBancarioWrapper _ultimoApunteBancoSeleccionado;
        private ContabilidadWrapper _ultimoApunteContabilidadSeleccionado;
        private int _ultimoIndiceBancoSeleccionado = -1;
        private int _ultimoIndiceContabilidadSeleccionado = -1;

        #endregion

        #region "Comandos"
        public ICommand AbrirPedidoCommand { get; private set; }
        private void OnAbrirPedido(PrepagoDTO prepago)
        {
            PedidoVentaViewModel.CargarPedido(BancoSeleccionado.Banco.Empresa, prepago.Pedido, _container);
        }


        public ICommand CargarArchivoCommand { get; private set; }
        private async void OnCargarArchivo()
        {
            string ruta = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, BancoSeleccionado.ParametroRutaFicherosMovimientos);

            // Configura el diálogo para abrir ficheros con selección múltiple
            OpenFileDialog openFileDialog = new()
            {
                InitialDirectory = ruta,
                Filter = "Todos los archivos (*.*)|*.*|Archivos de texto (*.txt)|*.txt",
                Multiselect = true // Permitir selección múltiple
            };

            if (openFileDialog.ShowDialog() == true)
            {
                List<(DateTime fechaApunte, string filePath)> apunteInfoList = [];
                ContenidoCuaderno43? ultimaContenidoCuaderno43 = null;
                DateTime maxFechaApunte = DateTime.MinValue;

                try
                {
                    IsBusyApuntesBanco = true;

                    // Procesar cada archivo seleccionado
                    foreach (string filePath in openFileDialog.FileNames)
                    {
                        try
                        {
                            string fileContent = File.ReadAllText(filePath);
                            ContenidoCuaderno43 = await BancoSeleccionado.CargarFicheroMovimientos(fileContent);
                            ultimaContenidoCuaderno43 = ContenidoCuaderno43;

                            // Guardar información para la notificación
                            DateTime fechaApunte = ContenidoCuaderno43.Cabecera.FechaFinal;
                            apunteInfoList.Add((fechaApunte, filePath));

                            // Actualizar la fecha mayor
                            if (fechaApunte > maxFechaApunte)
                            {
                                maxFechaApunte = fechaApunte;
                            }
                        }
                        catch (Exception ex)
                        {
                            _dialogService.ShowError($"Error al procesar el archivo {Path.GetFileName(filePath)}: " + ex.Message);
                        }
                    }
                }
                finally
                {
                    if (ultimaContenidoCuaderno43 != null)
                    {
                        // Solo actualizar Banco y la FechaHasta con la fecha mayor procesada
                        BancoSeleccionado.Banco = await _bancosService.LeerBanco(
                            ultimaContenidoCuaderno43.Cabecera.ClaveEntidad,
                            ultimaContenidoCuaderno43.Cabecera.ClaveOficina,
                            ultimaContenidoCuaderno43.Cabecera.NumeroCuenta
                        );

                        FechaHasta = maxFechaApunte;
                    }

                    IsBusyApuntesBanco = false;

                    // Generar el mensaje de notificación con la información acumulada
                    if (apunteInfoList.Count > 0)
                    {
                        string mensaje = "Apuntes cargados correctamente al sistema:\n\n";
                        foreach ((DateTime fechaApunte, string filePath) in apunteInfoList)
                        {
                            mensaje += $"• Apuntes del día {fechaApunte:dd/MM/yyyy} desde el archivo {Path.GetFileName(filePath)}.\n";
                        }
                        _dialogService.ShowNotification(mensaje);
                    }
                }
            }
        }




        public ICommand CargarArchivoTarjetasCommand { get; private set; }
        private async void OnCargarArchivoTarjetas()
        {
            string ruta = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.PathNormaFB500);

            // Configura el diálogo para abrir ficheros con selección múltiple
            OpenFileDialog openFileDialog = new()
            {
                InitialDirectory = ruta,
                Filter = "Todos los archivos (*.*)|*.*",
                Multiselect = true // Permitir selección múltiple
            };

            if (openFileDialog.ShowDialog() == true)
            {
                List<(DateTime fechaApunte, string filePath)> apunteInfoList = [];
                List<MovimientoTPV>? movimientosTPVFinal = null;

                try
                {
                    // Procesar cada archivo seleccionado
                    foreach (string filePath in openFileDialog.FileNames)
                    {
                        try
                        {
                            // Lee el contenido del fichero
                            string fileContent = File.ReadAllText(filePath);

                            List<MovimientoTPV> movimientosTPV = await BancoSeleccionado.CargarFicheroMovimientosTarjeta(fileContent);

                            // Solo asignar los movimientos para el último archivo procesado
                            movimientosTPVFinal = movimientosTPV;

                            // Guardar información para la notificación
                            DateTime fechaApunte = movimientosTPV.First().FechaOperacion;
                            apunteInfoList.Add((fechaApunte, filePath));
                        }
                        catch (Exception ex)
                        {
                            _dialogService.ShowError($"Error al procesar el archivo {Path.GetFileName(filePath)}: " + ex.Message);
                        }
                    }
                }
                finally
                {
                    if (movimientosTPVFinal != null)
                    {
                        // Asigna la fecha del último archivo procesado a FechaHasta
                        FechaHasta = movimientosTPVFinal.First().FechaOperacion;
                    }

                    // Generar el mensaje de notificación con la información acumulada
                    if (apunteInfoList.Count > 0)
                    {
                        string mensaje = "Movimientos de tarjeta cargados correctamente al sistema:\n\n";
                        foreach ((DateTime fechaApunte, string filePath) in apunteInfoList)
                        {
                            mensaje += $"• Movimientos del día {fechaApunte:dd/MM/yyyy} desde el archivo {Path.GetFileName(filePath)}.\n";
                        }
                        _dialogService.ShowNotification(mensaje);
                    }
                }
            }
        }

        public ICommand ContabilizarApunteCommand { get; private set; }
        public bool CanContabilizarApunte()
        {
            foreach (IReglaContabilizacion regla in _reglasContabilizacion)
            {
                bool esContabilizable = regla.EsContabilizable(ApuntesBancoSeleccionados?.Select(a => a.Model), ApuntesContabilidadSeleccionados?.Select(c => c.Model));
                if (esContabilizable)
                {
                    TextoBotonContabilizar = regla.Nombre;
                    return true;
                }
            }
            TextoBotonContabilizar = string.Empty;
            return false;
        }
        private async void OnContabilizarApunte()
        {
            IReglaContabilizacion? reglaContabilizable = null;
            var idsAntesContabilizar = ApuntesContabilidad?.Select(a => a.Id).ToHashSet() ?? [];

            try
            {
                IsBusyApuntesBanco = true;
                IsBusyApuntesContabilidad = true;
                foreach (IReglaContabilizacion regla in _reglasContabilizacion)
                {
                    bool esContabilizable = regla.EsContabilizable(ApuntesBancoSeleccionados?.Select(a => a.Model), ApuntesContabilidadSeleccionados?.Select(c => c.Model));
                    if (esContabilizable)
                    {
                        reglaContabilizable = regla;
                        break;
                    }
                }
                if (reglaContabilizable is null)
                {
                    return;
                }

                ReglaContabilizacionResponse? respuesta = reglaContabilizable.ApuntesContabilizar(
                    ApuntesBancoSeleccionados?
                    .Where(b => b.EstadoPunteo != EstadoPunteo.CompletamentePunteado)
                    .Select(b => b.Model),
                    ApuntesContabilidadSeleccionados?
                    .Where(c => c.EstadoPunteo != EstadoPunteo.CompletamentePunteado)
                    .Select(c => c.Model),
                    BancoSeleccionado.Banco);

                if (respuesta is null)
                {
                    return;
                }

                string textoMensajeFinal;
                if (respuesta.CrearFacturas)
                {
                    CrearFacturaCmpResponse response = new();
                    foreach (PreContabilidadDTO? linea in respuesta.Lineas.Where(r => r.TipoApunte == Constantes.TiposApunte.FACTURA && r.TipoCuenta == Constantes.TiposCuenta.PROVEEDOR))
                    {
                        PedidoCompraDTO pedido = CrearPedidoDesdePreContabilidad(linea);
                        response = await _pedidoCompraService.CrearAlbaranYFactura(
                            new CrearFacturaCmpRequest
                            {
                                Pedido = pedido,
                                CrearPago = true,
                                ContraPartidaPago = BancoSeleccionado.Banco.CuentaContable,
                                Documento = respuesta.Documento
                            }
                        );
                        if (!response.Exito)
                        {
                            throw new Exception("No se ha podido crear la factura");
                        }
                    }
                    respuesta.Lineas = respuesta.Lineas.Where(l => l.TipoApunte != Constantes.TiposApunte.FACTURA).ToList();
                    textoMensajeFinal = $"Facturas creadas correctamente:\nÚltimo pedido: {response.Pedido}\nÚltima factura: {response.Factura}\nÚltimo asiento: {response.AsientoFactura}";
                }
                else
                {
                    int asiento = await _contabilidadService.Contabilizar(respuesta.Lineas);
                    textoMensajeFinal = $"Apunte contabilizado correctamente en asiento {asiento}";
                }
                await CargarApuntesContabilidad(FechaDesde, FechaHasta);
                FiltrarRegistros();

                // Seleccionar los apuntes que están después y no estaban antes
                if (ApuntesContabilidad?.Any() == true)
                {
                    var apuntesNuevos = ApuntesContabilidad
                        .Where(a => !idsAntesContabilizar.Contains(a.Id))
                        .ToList();

                    if (apuntesNuevos.Any())
                    {
                        ApunteContabilidadSeleccionado = apuntesNuevos.First();
                        ApuntesContabilidadSeleccionados = apuntesNuevos;
                    }
                }

                _dialogService.ShowNotification(textoMensajeFinal);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Error al contabilizar el apunte:\n" + ex.Message);
            }
            finally
            {
                IsBusyApuntesBanco = false;
                IsBusyApuntesContabilidad = false;
            }
        }

        public ICommand CopiarConceptoPortapapelesCommand { get; private set; }
        private void OnCopiarConceptoPortapapeles()
        {
            if (ApunteBancoSeleccionado is null ||
                ApunteBancoSeleccionado.RegistrosConcepto is null ||
                !ApunteBancoSeleccionado.RegistrosConcepto.Any())
            {
                return;
            }
            StringBuilder html = new();
            StringBuilder plainText = new();
            _ = html.Append(Constantes.Formatos.HTML_BANCO_P_TAG);
            foreach (RegistroComplementarioConcepto concepto in ApunteBancoSeleccionado.RegistrosConcepto)
            {
                _ = html.Append(concepto.ConceptoCompleto);
                _ = html.Append("<br/>");
                _ = plainText.AppendLine(concepto.ConceptoCompleto);
            }

            _ = html.Append("</p>");
            ClipboardHelper.CopyToClipboard(html.ToString(), plainText.ToString());
            _dialogService.ShowNotification("Conceptos copiados al portapapeles");
        }


        public ICommand PuntearApuntesCommand { get; private set; }
        private bool CanPuntearApuntes()
        {
            return DescuadrePunteo == 0 && ApuntesBancoSeleccionados is not null
                && ApuntesContabilidadSeleccionados is not null
                && (ApuntesBancoSeleccionados.Any()
                || ApuntesContabilidadSeleccionados.Any());
        }
        private async void OnPuntearApuntes()
        {
            GuardarPosicionesSeleccionadas();

            if (ApuntesBancoSeleccionados is not null && ApuntesContabilidadSeleccionados is not null &&
                ApuntesBancoSeleccionados.Any() && ApuntesContabilidadSeleccionados.Any())
            {
                await PuntearMovimientosBancoYContabilidad();
            }
            else if (ApuntesBancoSeleccionados is not null && ApuntesBancoSeleccionados.Any())
            {
                await PuntearMovimientosSoloBanco();
            }
            else if (ApuntesContabilidadSeleccionados is not null && ApuntesContabilidadSeleccionados.Any())
            {
                await PuntearMovimientosSoloContabilidad();
            }
            else
            {
                throw new Exception("Tipo de apunte no permitido en el sistema");
            }
            FiltrarRegistros();

            RestaurarSeleccionSiguiente();
        }


        public ICommand PuntearAutomaticamenteCommand { get; private set; }
        private bool CanPuntearAutomaticamente()
        {
            return true;
        }
        private async void OnPuntearAutomaticamente()
        {
            if (!_dialogService.ShowConfirmationAnswer("Punteo automático", "¿Desea proceder al punteo automático de apuntes de misma fecha e importe?"))
            {
                return;
            }
            try
            {
                int punteados = 0;
                foreach (ApunteBancarioWrapper? apunteBanco in ApuntesBanco.Where(a => a.EstadoPunteo != EstadoPunteo.CompletamentePunteado))
                {
                    IEnumerable<ContabilidadWrapper> apuntesContabilidadEncontrados = ApuntesContabilidad
                        .Where(c => c.Importe == apunteBanco.ImporteMovimiento && c.Fecha == apunteBanco.FechaOperacion && c.EstadoPunteo != EstadoPunteo.CompletamentePunteado);

                    ContabilidadWrapper apunteContabilidad;

                    if (apuntesContabilidadEncontrados is null || apuntesContabilidadEncontrados.Count() != 1)
                    {
                        continue;
                    }

                    apunteContabilidad = apuntesContabilidadEncontrados.Single();
                    _ = await _bancosService.CrearPunteo(apunteBanco.Id, apunteContabilidad.Id, apunteBanco.ImporteMovimiento, SIMBOLO_PUNTEO_CONCILIACION).ConfigureAwait(true);
                    apunteBanco.EstadoPunteo = EstadoPunteo.CompletamentePunteado;
                    apunteContabilidad.EstadoPunteo = EstadoPunteo.CompletamentePunteado;
                    punteados++;
                }
                _dialogService.ShowNotification($"Punteados automáticamente {punteados} apuntes");
                FiltrarRegistros();
            }
            catch (Exception ex)
            {
                throw new Exception($"No se ha podido realizar el punteo automático", ex);
            }
        }

        public ICommand RegularizarDiferenciaCommand { get; private set; }
        private bool CanRegularizarDiferencia()
        {
            return ApuntesBancoSeleccionados != null && ApuntesContabilidadSeleccionados != null
                && DescuadrePunteo != 0
                && Math.Abs(DescuadrePunteo) < .5M;
        }
        private async void OnRegularizarDiferencia()
        {
            if (!_dialogService.ShowConfirmationAnswer("Regularizar", $"¿Desea proceder a la regularización de {DescuadrePunteo:c}?"))
            {
                return;
            }

            List<ContabilidadDTO> movimientosApunte;
            ContabilidadDTO apunteGasto = new();
            ContabilidadDTO ultimoApunteIngreso = new();
            ContabilidadDTO primerApunte = new();

            foreach (ContabilidadWrapper apunte in ApuntesContabilidadSeleccionados)
            {
                movimientosApunte = await _contabilidadService.LeerAsientoContable(Constantes.Empresas.EMPRESA_DEFECTO, apunte.Asiento);
                apunteGasto = movimientosApunte.Where(m => m.Cuenta.StartsWith("6")).FirstOrDefault();
                if (apunteGasto != null)
                {
                    break;
                }
                ultimoApunteIngreso = movimientosApunte.Where(m => m.Cuenta.StartsWith("7")).FirstOrDefault();
                primerApunte = movimientosApunte.FirstOrDefault();
            }

            if (apunteGasto is null && ultimoApunteIngreso is not null)
            {
                apunteGasto = ultimoApunteIngreso;
            }

            if (apunteGasto is null && BancoSeleccionado.CumpleCondicionesTPV(ApunteBancoSeleccionado) && primerApunte is not null)
            {
                apunteGasto = primerApunte;
                apunteGasto.Cuenta = Constantes.Cuentas.GASTOS_DATAFONO;
                if (string.IsNullOrEmpty(apunteGasto.CentroCoste))
                {
                    apunteGasto.CentroCoste = Constantes.Empresas.CENTRO_COSTE_DEFECTO;
                }
                if (string.IsNullOrEmpty(apunteGasto.Departamento))
                {
                    apunteGasto.Departamento = Constantes.Empresas.DEPARTAMENTO_DEFECTO;
                }                
            }

            if (apunteGasto is null)
            {
                _dialogService.ShowNotification("No se ha encontrado la cuenta de gasto");
                return;
            }
            try
            {
                PreContabilidadDTO apunteNuevo = apunteGasto.ToPreContabilidadDTO();
                apunteNuevo.Diario = "_ConcBanco";
                apunteNuevo.TipoApunte = Constantes.TiposApunte.PASO_A_CARTERA;
                apunteNuevo.TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE;
                apunteNuevo.Concepto = $"Reg. Dif. {ApunteContabilidadSeleccionado.Concepto}";
                apunteNuevo.Concepto = FuncionesAuxiliaresReglas.FormatearConcepto(apunteNuevo.Concepto);
                apunteNuevo.Documento = ApunteContabilidadSeleccionado.Documento;
                apunteNuevo.Fecha = new DateOnly(ApunteContabilidadSeleccionado.Fecha.Year, ApunteContabilidadSeleccionado.Fecha.Month, ApunteContabilidadSeleccionado.Fecha.Day);
                decimal importeRegularizacion = DescuadrePunteo;
                apunteNuevo.Debe = importeRegularizacion < 0 ? -importeRegularizacion : 0;
                apunteNuevo.Haber = importeRegularizacion > 0 ? importeRegularizacion : 0;
                apunteNuevo.Contrapartida = BancoSeleccionado.Banco.CuentaContable;
                int asientoCreado = await _contabilidadService.Contabilizar(apunteNuevo);
                await CargarApuntesContabilidad(FechaDesde, FechaHasta);
                _dialogService.ShowNotification($"Regularizado correctamente en asiento {asientoCreado}");
                FiltrarRegistros();
            }
            catch (Exception ex)
            {
                throw new Exception("No se pudo regularizar el movimiento", ex);
            }
        }


        public ICommand SeleccionarApuntesBancoCommand { get; private set; }
        private void OnSeleccionarApuntesBanco(IList apuntes)
        {
            IEnumerable<ApunteBancarioWrapper> selectedItems = apuntes.OfType<ApunteBancarioWrapper>();
            ApuntesBancoSeleccionados = selectedItems;
            SaldoPunteoApuntesBanco = selectedItems
                .Where(s => s.EstadoPunteo != EstadoPunteo.CompletamentePunteado)
                .Select(s => s.ImporteMovimiento)
                .DefaultIfEmpty(0)
                .Sum();
        }

        public ICommand SeleccionarApuntesContabilidadCommand { get; private set; }
        private void OnSeleccionarApuntesContabilidad(IList apuntes)
        {
            IEnumerable<ContabilidadWrapper> selectedItems = apuntes.OfType<ContabilidadWrapper>();
            ApuntesContabilidadSeleccionados = selectedItems;
            SaldoPunteoApuntesContabilidad = selectedItems
                .Where(s => s.EstadoPunteo != EstadoPunteo.CompletamentePunteado)
                .Select(s => s.Importe)
                .DefaultIfEmpty(0)
                .Sum();
        }


        #endregion

        #region "Métodos Auxiliares"


        private bool ApuntesBancoFilter(object item)
        {
            return item is ApunteBancarioWrapper apunteBanco && apunteBanco.Visible;
        }
        private bool ApuntesContabilidadFilter(object item)
        {
            return item is ContabilidadWrapper apunteContabilidad && apunteContabilidad.Visible;
        }

        private Task _cargaApuntesTask;
        private async Task CargarApuntes(DateTime fechaDesde, DateTime fechaHasta)
        {
            if (fechaDesde == DateTime.MinValue || FechaHasta == DateTime.MinValue)
            {
                return;
            }
            if (fechaDesde > fechaHasta)
            {
                return;
            }
            if (BancoSeleccionado?.Banco is null)
            {
                return;
            }
            // Esperar la carga anterior si aún no ha terminado
            if (_cargaApuntesTask != null && !_cargaApuntesTask.IsCompleted)
            {
                await _cargaApuntesTask.ConfigureAwait(false);
            }

            _cargaApuntesTask = Task.Run(async () =>
            {
                await Task.WhenAll(
                    CargarApuntesBanco(fechaDesde, fechaHasta),
                    CargarApuntesContabilidad(fechaDesde, fechaHasta)
                ).ConfigureAwait(false);
            }).ContinueWith(task =>
            {
                // Código para ejecutar después de que ambas tareas hayan terminado
                if (task.IsFaulted)
                {
                    _dialogService.ShowError("Error al cargar los apuntes:\n" + task.Exception.Message);
                }
                else
                {
                    FiltrarRegistros();
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        private async Task CargarApuntesBanco(DateTime fechaDesde, DateTime fechaHasta)
        {
            try
            {
                IsBusyApuntesBanco = true;
                List<ApunteBancarioDTO> lista = await _bancosService.LeerApuntesBanco(Constantes.Empresas.EMPRESA_DEFECTO, BancoSeleccionado.Banco.Codigo, fechaDesde, fechaHasta).ConfigureAwait(false);
                ApuntesBanco = [.. lista.Select(apunteBancarioDTO => new ApunteBancarioWrapper(apunteBancarioDTO))];
                ApuntesBancoCollectionView = CollectionViewSource.GetDefaultView(ApuntesBanco);
                SaldoInicialBanco = await _bancosService.SaldoBancoInicial(BancoSeleccionado.Banco.Entidad, BancoSeleccionado.Banco.Oficina, BancoSeleccionado.Banco.NumeroCuenta, fechaDesde).ConfigureAwait(false);
                SaldoFinalBanco = await _bancosService.SaldoBancoFinal(BancoSeleccionado.Banco.Entidad, BancoSeleccionado.Banco.Oficina, BancoSeleccionado.Banco.NumeroCuenta, fechaHasta).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new Exception("No se han podido cargar los apuntes del banco", ex);
            }
            finally
            {
                IsBusyApuntesBanco = false;
            }

        }
        private async Task CargarApuntesContabilidad(DateTime fechaDesde, DateTime fechaHasta)
        {
            try
            {
                IsBusyApuntesContabilidad = true;
                List<ContabilidadDTO> lista = await _contabilidadService.LeerApuntesContabilidad(Constantes.Empresas.EMPRESA_DEFECTO, BancoSeleccionado.Banco.CuentaContable, fechaDesde, fechaHasta).ConfigureAwait(false);
                ApuntesContabilidad = [.. lista.Select(contabilidadDTO => new ContabilidadWrapper(contabilidadDTO))];
                ApuntesContabilidadCollectionView = CollectionViewSource.GetDefaultView(ApuntesContabilidad);
                SaldoInicialContabilidad = await _contabilidadService.SaldoCuenta(BancoSeleccionado.Banco.Empresa, BancoSeleccionado.Banco.CuentaContable, fechaDesde.AddDays(-1)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new Exception("No se han podido cargar los apuntes de contabilidad", ex);
            }
            finally
            {
                IsBusyApuntesContabilidad = false;
            }

        }
        private async Task CargarExtractoProveedorAsiento(ContabilidadWrapper value)
        {
            if (value is null)
            {
                ExtractosProveedorAsientoSeleccionado = null;
                return;
            }
            ExtractosProveedorAsientoSeleccionado = await _bancosService.LeerExtractoProveedorAsiento(Constantes.Empresas.EMPRESA_DEFECTO, value.Asiento);
        }
        private async Task CargarMovimientosRelacionadosTPV(string tipoDatafono, CancellationToken cancellationToken = default)
        {
            try
            {
                // Guardar referencia al apunte actual para validación posterior
                var apunteActual = ApunteBancoSeleccionado;
                if (apunteActual == null)
                {
                    return;
                }

                string modoCaptura = tipoDatafono == "WEB" ? "3" : tipoDatafono == "ON " ? "1" : throw new Exception("Tipo de datáfono no contemplado");

                MovimientosTPV = await _bancosService.LeerMovimientosTPV(fechaCaptura: apunteActual.FechaOperacion.AddDays(-1), modoCaptura);

                // Verificar que no se canceló la operación y que el apunte sigue siendo el mismo
                if (cancellationToken.IsCancellationRequested || ApunteBancoSeleccionado != apunteActual)
                {
                    return;
                }

                PrepagosPendientes = [];
                MovimientosRelacionados = [.. MovimientosTPV];

                if (apunteActual.ImporteMovimiento != MovimientosRelacionados.Sum(m => m.ImporteOperacion))
                {
                    MovimientosTPV = await _bancosService.LeerMovimientosTPV(fechaCaptura: apunteActual.FechaOperacion.AddDays(-1), "4");

                    if (cancellationToken.IsCancellationRequested || ApunteBancoSeleccionado != apunteActual)
                    {
                        return;
                    }

                    _ = MovimientosRelacionados.AddRange(MovimientosTPV);
                }

                // Verificaciones finales con el apunte actual
                if (cancellationToken.IsCancellationRequested || ApunteBancoSeleccionado != apunteActual)
                {
                    return;
                }

                if (apunteActual.ImporteMovimiento != MovimientosRelacionados.Sum(m => m.ImporteOperacion))
                {
                    MovimientosRelacionados = new ObservableCollection<MovimientoTPV>(MovimientosRelacionados.Where(m => m.ModoCaptura == modoCaptura));
                }

                if (apunteActual.ImporteMovimiento != MovimientosRelacionados.Sum(m => m.ImporteOperacion))
                {
                    _dialogService.ShowError($"El cierre de datáfono descuadra en {-apunteActual.ImporteMovimiento + MovimientosTPV.Sum(m => m.ImporteOperacion):C}");
                }
            }
            catch (OperationCanceledException)
            {
                // Operación cancelada, no hacer nada
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _dialogService.ShowError("Error al cargar movimientos relacionados: " + ex.Message);
                }
            }
        }
        private async Task CargarPrepagosPendientes()
        {
            PrepagosPendientes = MovimientoTPVSeleccionado is not null
                ? await _bancosService.LeerPrepagosPendientes(MovimientoTPVSeleccionado.ImporteOperacion)
                : null;
        }
        private PedidoCompraDTO? CrearPedidoDesdePreContabilidad(PreContabilidadDTO? linea)
        {
            if (linea == null)
            {
                return null;
            }
            PedidoCompraDTO pedido = new()
            {
                Empresa = BancoSeleccionado.Banco.Empresa,
                CodigoIvaProveedor = Constantes.Empresas.IVA_DEFECTO,
                Proveedor = linea.Cuenta,
                Contacto = linea.Contacto,
                Fecha = new DateTime(linea.Fecha.Year, linea.Fecha.Month, linea.Fecha.Day),
                PrimerVencimiento = new DateTime(linea.Fecha.Year, linea.Fecha.Month, linea.Fecha.Day),
                FacturaProveedor = linea.Documento,
                FormaPago = Constantes.FormasPago.TRANSFERENCIA,
                PlazosPago = Constantes.PlazosPago.CONTADO,
                PeriodoFacturacion = Constantes.PeriodosFacturacion.NORMAL
            };
            pedido.Lineas.Add(new LineaPedidoCompraDTO
            {
                TipoLinea = Constantes.LineasPedido.TiposLinea.CUENTA_CONTABLE,
                Producto = linea.Contrapartida,
                Cantidad = 1,
                PrecioUnitario = linea.Debe - linea.Haber,
                CodigoIvaProducto = Constantes.Empresas.IVA_DEFECTO,
                PorcentajeIva = .21M,
                FechaRecepcion = pedido.Fecha,
                Delegacion = linea.Delegacion,
                Departamento = linea.Departamento,
                CentroCoste = linea.CentroCoste,
                Texto = linea.Concepto,
                Enviado = true
            });
            return pedido;
        }
        public static PreContabilidadDTO CrearPrecontabilidadDefecto()
        {
            return new PreContabilidadDTO
            {
                Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                TipoApunte = Constantes.TiposApunte.PASO_A_CARTERA,
                Asiento = 1
            };
        }
        private void FiltrarRegistros()
        {
            if (ApuntesBanco != null)
            {
                foreach (ApunteBancarioWrapper apunteBanco in ApuntesBanco)
                {
                    if (apunteBanco != null)
                    {
                        apunteBanco.Visible = MostrarEstadoPunteo(apunteBanco.EstadoPunteo);
                    }
                }
                if (ApuntesBancoCollectionView != null)
                {
                    ApuntesBancoCollectionView.Filter = ApuntesBancoFilter;
                }
            }

            if (ApuntesContabilidad != null)
            {
                foreach (ContabilidadWrapper apunteContabilidad in ApuntesContabilidad)
                {
                    if (apunteContabilidad != null)
                    {
                        apunteContabilidad.Visible = MostrarEstadoPunteo(apunteContabilidad.EstadoPunteo);
                    }
                }
                if (ApuntesContabilidadCollectionView != null)
                {
                    ApuntesContabilidadCollectionView.Filter = ApuntesContabilidadFilter;
                }
            }
        }

        private void GuardarPosicionesSeleccionadas()
        {
            if (ApunteBancoSeleccionado != null && ApuntesBancoCollectionView != null)
            {
                _ultimoApunteBancoSeleccionado = ApunteBancoSeleccionado;
                var listaOrdenada = ApuntesBancoCollectionView.Cast<ApunteBancarioWrapper>().ToList();
                _ultimoIndiceBancoSeleccionado = listaOrdenada.IndexOf(ApunteBancoSeleccionado);
            }

            if (ApunteContabilidadSeleccionado != null && ApuntesContabilidadCollectionView != null)
            {
                _ultimoApunteContabilidadSeleccionado = ApunteContabilidadSeleccionado;
                var listaOrdenada = ApuntesContabilidadCollectionView.Cast<ContabilidadWrapper>().ToList();
                _ultimoIndiceContabilidadSeleccionado = listaOrdenada.IndexOf(ApunteContabilidadSeleccionado);
            }
        }
        private static Dictionary<string, string> LeerDiccionarioFechas(string valor, string codigoBanco)
        {
            // Si está vacío, devolvemos diccionario vacío
            if (string.IsNullOrWhiteSpace(valor))
            {
                return [];
            }

            // Intentamos deserializar como JSON
            try
            {
                var diccionario = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(valor);
                if (diccionario != null)
                {
                    return diccionario;
                }
            }
            catch
            {
                // Si falla, comprobamos si es una fecha en formato antiguo
                if (DateTime.TryParseExact(valor, "dd/MM/yy", null, System.Globalization.DateTimeStyles.None, out var fecha))
                {
                    // Creamos diccionario con el banco actual
                    return new Dictionary<string, string> { [codigoBanco] = fecha.ToString("dd/MM/yy") };
                }
            }
            // Si no es ni JSON ni fecha, devolvemos vacío
            return [];
        }

        private bool MostrarEstadoPunteo(EstadoPunteo estadoPunteo)
        {
            return estadoPunteo switch
            {
                EstadoPunteo.SinPuntear => MostrarSinPuntear,
                EstadoPunteo.CompletamentePunteado => MostrarCompletamentePunteado,
                EstadoPunteo.ParcialmentePunteado => MostrarParcialmentePunteado,
                _ => true,
            };
        }
        private async Task PuntearMovimientosSoloBanco()
        {
            int grupoPunteo = ApuntesBancoSeleccionados
                .OrderByDescending(apunte => apunte.ImporteMovimiento)
                .First()
                .Id;
            foreach (ApunteBancarioWrapper apunte in ApuntesBancoSeleccionados)
            {
                _ = await _bancosService.CrearPunteo(apunte.Id, null, apunte.ImporteMovimiento, SIMBOLO_PUNTEO_CONCILIACION, grupoPunteo);
                apunte.EstadoPunteo = EstadoPunteo.CompletamentePunteado;
            }
            _dialogService.ShowNotification("Liquidado correctamente");
        }
        private async Task PuntearMovimientosSoloContabilidad()
        {
            int grupoPunteo = ApuntesContabilidadSeleccionados
                .OrderByDescending(apunte => apunte.Importe)
                .ThenBy(apunte => apunte.Debe)
                .First()
                .Id;
            foreach (ContabilidadWrapper apunte in ApuntesContabilidadSeleccionados)
            {
                _ = await _bancosService.CrearPunteo(null, apunte.Id, apunte.Importe, SIMBOLO_PUNTEO_CONCILIACION, grupoPunteo);
                apunte.EstadoPunteo = EstadoPunteo.CompletamentePunteado;
            }
            _dialogService.ShowNotification("Liquidado correctamente");
        }
        private async Task PuntearMovimientosBancoYContabilidad()
        {
            Queue<ApunteBancarioWrapper> colaBanco = new(ApuntesBancoSeleccionados.Where(b => b.EstadoPunteo != EstadoPunteo.CompletamentePunteado));
            Queue<ContabilidadWrapper> colaContabilidad = new(ApuntesContabilidadSeleccionados.Where(c => c.EstadoPunteo != EstadoPunteo.CompletamentePunteado));

            ApunteBancarioWrapper? apunteBanco = colaBanco.Peek();
            ContabilidadWrapper? apunteContabilidad = colaContabilidad.Peek();

            decimal importeRestanteBanco = apunteBanco.ImporteMovimiento;
            decimal importeRestanteContabilidad = apunteContabilidad.Importe;
            decimal importePunteo = 0;
            try
            {
                while (colaBanco.Count > 0 && colaContabilidad.Count > 0)
                {
                    int apunteBancoId = apunteBanco.Id;
                    int apunteContabilidadId = apunteContabilidad.Id;

                    if (importeRestanteBanco == 0)
                    {
                        importeRestanteBanco = apunteBanco.ImporteMovimiento;
                    }
                    if (importeRestanteContabilidad == 0)
                    {
                        importeRestanteContabilidad = apunteContabilidad.Importe;
                    }

                    if (Math.Abs(importeRestanteBanco) > Math.Abs(importeRestanteContabilidad) ||
                        (importeRestanteBanco != importeRestanteContabilidad && colaBanco.Count == 1 && colaContabilidad.Count > 1))
                    {
                        importePunteo = importeRestanteContabilidad;
                        importeRestanteBanco -= importeRestanteContabilidad;
                        importeRestanteContabilidad = 0;
                        _ = colaContabilidad.Dequeue();
                        apunteBanco.EstadoPunteo = EstadoPunteo.ParcialmentePunteado;
                        apunteContabilidad.EstadoPunteo = EstadoPunteo.CompletamentePunteado;
                        apunteContabilidad = colaContabilidad.Count > 0 ? colaContabilidad.Peek() : null;
                    }
                    else if (Math.Abs(importeRestanteBanco) < Math.Abs(importeRestanteContabilidad) ||
                        (importeRestanteBanco != importeRestanteContabilidad && colaContabilidad.Count == 1 && colaBanco.Count > 1))
                    {
                        importePunteo = importeRestanteBanco;
                        importeRestanteContabilidad -= importeRestanteBanco;
                        importeRestanteBanco = 0;
                        _ = colaBanco.Dequeue();
                        apunteBanco.EstadoPunteo = EstadoPunteo.CompletamentePunteado;
                        apunteContabilidad.EstadoPunteo = EstadoPunteo.ParcialmentePunteado;
                        apunteBanco = colaBanco.Count > 0 ? colaBanco.Peek() : null;
                    }
                    else
                    {
                        importePunteo = importeRestanteBanco; // que es igual a importeRestanteContabilidad
                        importeRestanteContabilidad = 0;
                        importeRestanteBanco = 0;
                        _ = colaBanco.Dequeue();
                        _ = colaContabilidad.Dequeue();
                        apunteBanco.EstadoPunteo = EstadoPunteo.CompletamentePunteado;
                        apunteContabilidad.EstadoPunteo = EstadoPunteo.CompletamentePunteado;
                        apunteBanco = colaBanco.Count > 0 ? colaBanco.Peek() : null;
                        apunteContabilidad = colaContabilidad.Count > 0 ? colaContabilidad.Peek() : null;
                    }

                    _ = await _bancosService.CrearPunteo(apunteBancoId, apunteContabilidadId, importePunteo, SIMBOLO_PUNTEO_CONCILIACION);

                }
                _dialogService.ShowNotification("Liquidado correctamente");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Error al puntear:\n" + ex.Message);
            }
        }
        private void RestaurarSeleccionSiguiente()
        {
            // Restaurar selección en ApuntesBanco
            if (_ultimoApunteBancoSeleccionado != null && ApuntesBancoCollectionView != null)
            {
                var elementosVisibles = ApuntesBancoCollectionView.Cast<ApunteBancarioWrapper>()
                    .Where(a => a.Visible).ToList();

                if (elementosVisibles.Any())
                {
                    ApunteBancarioWrapper siguienteElemento = null;

                    // Verificar si el elemento seleccionado original aún está visible
                    if (_ultimoApunteBancoSeleccionado.Visible)
                    {
                        // Si el elemento original sigue visible, mantenerlo seleccionado
                        siguienteElemento = _ultimoApunteBancoSeleccionado;
                    }
                    else
                    {
                        // Si el elemento original desapareció, buscar el siguiente en la posición guardada
                        if (_ultimoIndiceBancoSeleccionado < elementosVisibles.Count)
                        {
                            siguienteElemento = elementosVisibles[_ultimoIndiceBancoSeleccionado];
                        }
                        else if (elementosVisibles.Count > 0)
                        {
                            // Si no hay elemento en esa posición, tomar el último disponible
                            siguienteElemento = elementosVisibles[^1];
                        }
                    }

                    ApunteBancoSeleccionado = siguienteElemento ?? elementosVisibles.FirstOrDefault();
                }
            }

            // Restaurar selección en ApuntesContabilidad
            if (_ultimoApunteContabilidadSeleccionado != null && ApuntesContabilidadCollectionView != null)
            {
                var elementosVisibles = ApuntesContabilidadCollectionView.Cast<ContabilidadWrapper>()
                    .Where(a => a.Visible).ToList();

                if (elementosVisibles.Any())
                {
                    ContabilidadWrapper siguienteElemento = null;

                    // Verificar si el elemento seleccionado original aún está visible
                    if (_ultimoApunteContabilidadSeleccionado.Visible)
                    {
                        // Si el elemento original sigue visible, mantenerlo seleccionado
                        siguienteElemento = _ultimoApunteContabilidadSeleccionado;
                    }
                    else
                    {
                        // Si el elemento original desapareció, buscar el siguiente en la posición guardada
                        if (_ultimoIndiceContabilidadSeleccionado < elementosVisibles.Count)
                        {
                            siguienteElemento = elementosVisibles[_ultimoIndiceContabilidadSeleccionado];
                        }
                        else if (elementosVisibles.Count > 0)
                        {
                            siguienteElemento = elementosVisibles[^1];
                        }
                    }

                    ApunteContabilidadSeleccionado = siguienteElemento ?? elementosVisibles.FirstOrDefault();
                }
            }

            // Limpiar las referencias guardadas
            _ultimoApunteBancoSeleccionado = null;
            _ultimoApunteContabilidadSeleccionado = null;
            _ultimoIndiceBancoSeleccionado = -1;
            _ultimoIndiceContabilidadSeleccionado = -1;
        }
        private static string SerializarDiccionarioFechas(Dictionary<string, string> diccionario)
        {
            return System.Text.Json.JsonSerializer.Serialize(diccionario);
        }

        #endregion


        public override async void OnNavigatedTo(NavigationContext navigationContext)
        {
            string parametroBanco = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.ConciliacionBancariaUltimoBanco);
            string fechasDesdeJson = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.ConciliacionBancariaFechaDesde);
            _fechasDesdePorBanco = LeerDiccionarioFechas(fechasDesdeJson, parametroBanco);

            string fechasHastaJson = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.ConciliacionBancariaFechaHasta);
            _fechasHastaPorBanco = LeerDiccionarioFechas(fechasHastaJson, parametroBanco);

            BancoSeleccionado = ListaBancos.Single(b => b.Banco.Codigo == parametroBanco);
        }
    }
}
