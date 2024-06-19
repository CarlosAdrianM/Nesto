using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using Microsoft.Win32;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.Models.ReglasContabilizacion;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Models;
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
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace Nesto.Modulos.Cajas.ViewModels
{
    public class BancosViewModel : ViewModelBase
    {
        private readonly IBancosService _bancosService;
        private readonly IContabilidadService _contabilidadService;
        private readonly IConfiguracion _configuracion;
        private readonly IDialogService _dialogService;
        private readonly IPedidoCompraService _pedidoCompraService;
        private List<IReglaContabilizacion> _reglasContabilizacion;

        private const string SIMBOLO_PUNTEO_CONCILIACION = "*";

        public BancosViewModel(IBancosService bancosService, IContabilidadService contabilidadService, IConfiguracion configuracion, IDialogService dialogService, IPedidoCompraService pedidoCompraService)
        {
            _bancosService = bancosService;
            _contabilidadService = contabilidadService;
            _configuracion = configuracion;
            _dialogService = dialogService;
            _pedidoCompraService = pedidoCompraService;

            ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>();
            ContenidoCuaderno43 = new ContenidoCuaderno43();
            ContenidoCuaderno43.Cabecera = new RegistroCabeceraCuenta();
            ContenidoCuaderno43.FinalCuenta = new RegistroFinalCuenta();

            CargarArchivoCommand = new DelegateCommand(OnCargarArchivo);
            CargarArchivoTarjetasCommand = new DelegateCommand(OnCargarArchivoTarjetas);
            ContabilizarApunteCommand = new DelegateCommand(OnContabilizarApunte, CanContabilizarApunte);
            CopiarConceptoPortapapelesCommand = new DelegateCommand(OnCopiarConceptoPortapapeles);
            PuntearApuntesCommand = new DelegateCommand(OnPuntearApuntes, CanPuntearApuntes);
            PuntearAutomaticamenteCommand = new DelegateCommand(OnPuntearAutomaticamente, CanPuntearAutomaticamente);
            RegularizarDiferenciaCommand = new DelegateCommand(OnRegularizarDiferencia, CanRegularizarDiferencia);
            SeleccionarApuntesBancoCommand = new DelegateCommand<IList>(OnSeleccionarApuntesBanco);
            SeleccionarApuntesContabilidadCommand = new DelegateCommand<IList>(OnSeleccionarApuntesContabilidad);

            _reglasContabilizacion = new List<IReglaContabilizacion>
            {
                new ReglaAdelantosNomina(),
                new ReglaComisionesBanco(),                
                new ReglaComunidadPropietariosAlcobendas(),
                new ReglaComunidadPropietariosAlgete(),
                new ReglaComunidadPropietariosReina(),                
                new ReglaSegurosSaludPrivados(),
                new ReglaStripe(),
                new ReglaEmbargo(),
                new ReglaAmazonPayComision(),
                new ReglaAplazame(),
                new ReglaAsociacionEsteticistas(),
                new ReglaAyuntamientoAlgete(),
                new ReglaInteresesAplazamientoConfirming(_dialogService),
                new ReglaComisionRemesaRecibos(_bancosService),
                new ReglaPagoProveedor(_bancosService),
                new ReglaGastoPeriodico(_contabilidadService)
            };

            Titulo = "Bancos";
        }
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
                    // Comprobamos las condiciones antes de cargar los movimientos relacionados
                    if (CumpleCondicionesTPV(_apunteBancoSeleccionado))
                    {
                        // Cargar los movimientos relacionados dependiendo del literal Concepto2
                        
                        CargarMovimientosRelacionadosTPV(ApunteBancoSeleccionado.RegistrosConcepto[0].Concepto2.Substring(0, 3));
                    }
                    else
                    {
                        MovimientosRelacionados = new ObservableCollection<MovimientoTPV>();
                        PrepagosPendientes = new();
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
                foreach (var apunte in value)
                {
                    if (apunte.ClaveDebeOHaberMovimiento == "1")
                    {
                        apunte.ImporteMovimiento *= -1;
                    }
                }
                SetProperty(ref _apuntesBanco, value);
            }
        }
        private IEnumerable<ApunteBancarioWrapper> _apuntesBancoSeleccionados;
        public IEnumerable<ApunteBancarioWrapper> ApuntesBancoSeleccionados
        {
            get => _apuntesBancoSeleccionados;
            set
            {
                SetProperty(ref _apuntesBancoSeleccionados, value);
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
                    CargarExtractoProveedorAsiento(value);
                    ((DelegateCommand)ContabilizarApunteCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private ObservableCollection<ContabilidadWrapper> _apuntesContabilidad;
        public ObservableCollection<ContabilidadWrapper> ApuntesContabilidad { 
            get => _apuntesContabilidad;
            set 
            { 
                SetProperty(ref _apuntesContabilidad, value);
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
                SetProperty(ref _apuntesContabilidadSeleccionados, value);
                ((DelegateCommand)RegularizarDiferenciaCommand).RaiseCanExecuteChanged();
                ((DelegateCommand)ContabilizarApunteCommand).RaiseCanExecuteChanged();
            }
        }
        
        private BancoDTO _banco;
        public BancoDTO Banco {
            get => _banco;
            set
            {
                SetProperty(ref _banco, value);
            }
        }
        private ContenidoCuaderno43 _contenidoCuaderno43;
        public ContenidoCuaderno43 ContenidoCuaderno43 
        {
            get => _contenidoCuaderno43;
            set
            {
                SetProperty(ref _contenidoCuaderno43, value);
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
        public List<ExtractoProveedorDTO> ExtractosProveedorAsientoSeleccionado
        {
            get => _extractosProveedorAsientoSeleccionado;
            set => SetProperty(ref _extractosProveedorAsientoSeleccionado, value);
        }
        private DateTime _fechaDesde;
        public DateTime FechaDesde {
            get => _fechaDesde;
            set {
                if (_fechaDesde != value)
                {
                    SetProperty(ref _fechaDesde, value);
                    CargarApuntes(FechaDesde, FechaHasta);
                }                
            }
        }
        private DateTime _fechaHasta;
        public DateTime FechaHasta { 
            get => _fechaHasta;
            set {
                if (_fechaHasta != value)
                {
                    SetProperty(ref _fechaHasta, value);
                    CargarApuntes(FechaDesde, FechaHasta);
                }                
            } 
        }
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

        private bool _mostrarCompletamentePunteado = false;
        public bool MostrarCompletamentePunteado
        {
            get => _mostrarCompletamentePunteado;
            set
            {
                SetProperty(ref _mostrarCompletamentePunteado, value);
                FiltrarRegistros();
            }
        }

        private bool _mostrarParcialmentePunteado = true;
        public bool MostrarParcialmentePunteado
        {
            get => _mostrarParcialmentePunteado;
            set
            {
                SetProperty(ref _mostrarParcialmentePunteado, value);
                FiltrarRegistros();
            }
        }

        private bool _mostrarSinPuntear = true;
        public bool MostrarSinPuntear
        {
            get => _mostrarSinPuntear;
            set
            {
                SetProperty(ref _mostrarSinPuntear, value);
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
                    CargarPrepagosPendientes();
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
        public decimal SaldoFinalBanco { 
            get => _saldoFinalBanco;
            set 
            { 
                SetProperty(ref _saldoFinalBanco, value);
                RaisePropertyChanged(nameof(DescuadreSaldoFinal));
            }
        }
        private decimal _saldoInicialBanco;
        public decimal SaldoInicialBanco
        {
            get => _saldoInicialBanco;
            set
            {
                SetProperty(ref _saldoInicialBanco, value);
                RaisePropertyChanged(nameof(DescuadreSaldoInicial));
            }
        }
        public decimal SaldoFinalContabilidad => (decimal)(SaldoInicialContabilidad + ApuntesContabilidad?.Sum(c => c.Importe));
        private decimal _saldoInicialContabilidad;
        public decimal SaldoInicialContabilidad { 
            get => _saldoInicialContabilidad;
            set
            {
                SetProperty(ref _saldoInicialContabilidad, value);
                RaisePropertyChanged(nameof(DescuadreSaldoInicial));
            } 
        }
        private decimal _saldoPunteoApuntesBanco;
        public decimal SaldoPunteoApuntesBanco
        {
            get => _saldoPunteoApuntesBanco;
            set 
            { 
                SetProperty(ref _saldoPunteoApuntesBanco, value);
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
                SetProperty(ref _saldoPunteoApuntesContabilidad, value);
                RaisePropertyChanged(nameof(DescuadrePunteo));
                ((DelegateCommand)PuntearApuntesCommand).RaiseCanExecuteChanged();
            }
        }
        private Dictionary<string, string> _terminalesUsuarios;
        public Dictionary<string, string> TerminalesUsuarios
        {
            get { return _terminalesUsuarios; }
            set { SetProperty(ref _terminalesUsuarios, value); }
        }









        public ICommand CargarArchivoCommand { get; private set; }
        private async void OnCargarArchivo()
        {
            var ruta = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.PathNorma43);
            // Configura el diálogo para abrir ficheros
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = ruta;
            openFileDialog.Filter = "Archivos de texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                // Obtiene la ruta del fichero seleccionado
                string filePath = openFileDialog.FileName;

                try
                {
                    IsBusyApuntesBanco = true;
                    string fileContent = File.ReadAllText(filePath);
                    ContenidoCuaderno43 = await _bancosService.CargarFicheroCuaderno43(fileContent);
                    Banco = await _bancosService.LeerBanco(ContenidoCuaderno43.Cabecera.ClaveEntidad, ContenidoCuaderno43.Cabecera.ClaveOficina, ContenidoCuaderno43.Cabecera.NumeroCuenta);
                    FechaHasta = ContenidoCuaderno43.Cabecera.FechaInicial;
                    var fechaApunte = ContenidoCuaderno43.Cabecera.FechaFinal;
                    _dialogService.ShowNotification($"Apuntes día {fechaApunte.ToString("dd/MM/yyyy")} cargados correctamente al sistema");
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError("Error al leer el fichero de C43: " + ex.Message);
                }
                finally
                {
                    IsBusyApuntesBanco = false;
                }
            }
        }

        public ICommand CargarArchivoTarjetasCommand { get; private set; }
        private async void OnCargarArchivoTarjetas()
        {
            var ruta = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.PathNormaFB500);
            // Configura el diálogo para abrir ficheros
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = ruta;
            openFileDialog.Filter = "Todos los archivos (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                // Obtiene la ruta del fichero seleccionado
                string filePath = openFileDialog.FileName;

                try
                {
                    // Lee el contenido del fichero
                    string fileContent = File.ReadAllText(filePath);

                    MovimientosTPV = await _bancosService.CargarFicheroTarjetas(fileContent);
                    var fechaApunte = MovimientosTPV.First().FechaOperacion;
                    FechaHasta = fechaApunte;                    
                    _dialogService.ShowNotification($"Movimientos de tarjeta del día {fechaApunte.ToString("dd/MM/yyyy")} cargados correctamente al sistema");
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError("Error al leer el fichero de tarjetas: " + ex.Message);
                }
            }
        }
        public ICommand ContabilizarApunteCommand { get; private set; }
        public bool CanContabilizarApunte()
        {
            foreach (var regla in _reglasContabilizacion)
            {
                var esContabilizable = regla.EsContabilizable(ApuntesBancoSeleccionados?.Select(a => a.Model), ApuntesContabilidadSeleccionados?.Select(c => c.Model));
                if (esContabilizable)
                {
                    return true;
                }
            }
            return false;
        }
        private async void OnContabilizarApunte()
        {
            IReglaContabilizacion reglaContabilizable = null;
            try
            {
                IsBusyApuntesBanco = true;
                IsBusyApuntesContabilidad = true;
                foreach (var regla in _reglasContabilizacion)
                {
                    var esContabilizable = regla.EsContabilizable(ApuntesBancoSeleccionados?.Select(a => a.Model), ApuntesContabilidadSeleccionados?.Select(c => c.Model));
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
            
                var respuesta = reglaContabilizable.ApuntesContabilizar(
                    ApuntesBancoSeleccionados?
                    .Where(b => b.EstadoPunteo != EstadoPunteo.CompletamentePunteado)
                    .Select(b => b.Model), 
                    ApuntesContabilidadSeleccionados?
                    .Where(c => c.EstadoPunteo != EstadoPunteo.CompletamentePunteado)
                    .Select(c => c.Model),
                    Banco);
                string textoMensajeFinal;
                if (respuesta.CrearFacturas)
                {
                    CrearFacturaCmpResponse response = new();
                    foreach (var linea in respuesta.Lineas.Where(r => r.TipoApunte == Constantes.TiposApunte.FACTURA && r.TipoCuenta == Constantes.TiposCuenta.PROVEEDOR))
                    {
                        PedidoCompraDTO pedido = CrearPedidoDesdePreContabilidad(linea);
                        response = await _pedidoCompraService.CrearAlbaranYFactura(
                            new CrearFacturaCmpRequest 
                            { 
                                Pedido = pedido, 
                                CrearPago = true ,
                                ContraPartidaPago = Banco.CuentaContable,
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
                _dialogService.ShowNotification(textoMensajeFinal);
                FiltrarRegistros();
            } 
            catch (Exception ex)
            {
                _dialogService.ShowError("Error al contabilizar el apunte:\n"+ex.Message);
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
            var html = new StringBuilder();
            var plainText = new StringBuilder();
            html.Append(Constantes.Formatos.HTML_BANCO_P_TAG);
            foreach(var concepto in ApunteBancoSeleccionado.RegistrosConcepto)
            {
                html.Append(concepto.ConceptoCompleto);
                html.Append("<br/>");
                plainText.AppendLine(concepto.ConceptoCompleto);
            }
            
            html.Append("</p>");
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
                foreach (var apunteBanco in ApuntesBanco.Where(a => a.EstadoPunteo != EstadoPunteo.CompletamentePunteado))
                {
                    IEnumerable<ContabilidadWrapper> apuntesContabilidadEncontrados = ApuntesContabilidad
                        .Where(c => c.Importe == apunteBanco.ImporteMovimiento && c.Fecha == apunteBanco.FechaOperacion && c.EstadoPunteo != EstadoPunteo.CompletamentePunteado);
                    
                    ContabilidadWrapper apunteContabilidad;

                    if (apuntesContabilidadEncontrados is null || apuntesContabilidadEncontrados.Count() != 1)
                    {
                        continue;
                    }

                    apunteContabilidad = apuntesContabilidadEncontrados.Single();
                    await _bancosService.CrearPunteo(apunteBanco.Id, apunteContabilidad.Id, apunteBanco.ImporteMovimiento, SIMBOLO_PUNTEO_CONCILIACION).ConfigureAwait(true);
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
            if (!_dialogService.ShowConfirmationAnswer("Regularizar", $"¿Desea proceder a la regularización de {DescuadrePunteo.ToString("c")}?"))
            {
                return;
            }

            List<ContabilidadDTO> movimientosApunte;
            ContabilidadDTO apunteGasto = new();
            ContabilidadDTO ultimoApunteIngreso = new();

            foreach (var apunte in ApuntesContabilidadSeleccionados)
            {
                movimientosApunte = await _contabilidadService.LeerAsientoContable(Constantes.Empresas.EMPRESA_DEFECTO, apunte.Asiento);
                apunteGasto = movimientosApunte.Where(m => m.Cuenta.StartsWith("6")).FirstOrDefault();
                if (apunteGasto != null)
                {
                    break;
                }
                ultimoApunteIngreso = movimientosApunte.Where(m => m.Cuenta.StartsWith("7")).FirstOrDefault();
            }

            if (apunteGasto is null && ultimoApunteIngreso is not null)
            {
                apunteGasto = ultimoApunteIngreso;
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
                apunteNuevo.Contrapartida = Banco.CuentaContable;
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
            var selectedItems = apuntes.OfType<ApunteBancarioWrapper>();
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
            var selectedItems = apuntes.OfType<ContabilidadWrapper>();
            ApuntesContabilidadSeleccionados = selectedItems;
            SaldoPunteoApuntesContabilidad = selectedItems
                .Where(s => s.EstadoPunteo != EstadoPunteo.CompletamentePunteado)
                .Select(s => s.Importe)
                .DefaultIfEmpty(0)  
                .Sum();
        }


        private bool ApuntesBancoFilter(object item)
        {
            if (item is ApunteBancarioWrapper apunteBanco)
            {
                return apunteBanco.Visible;
            }
            return false;
        }
        private bool ApuntesContabilidadFilter(object item)
        {
            if (item is ContabilidadWrapper apunteContabilidad)
            {
                return apunteContabilidad.Visible;
            }
            return false;
        }

        private Task _cargaApuntesTask;
        private async Task CargarApuntes(DateTime fechaDesde, DateTime fechaHasta)
        {
            if (fechaDesde > fechaHasta)
            {
                return;
            }
            if (Banco is null)
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
            }).ContinueWith(task => {
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
            await _configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.ConciliacionBancariaFechaDesde, FechaDesde.ToString("dd/MM/yy")).ConfigureAwait(false);
            await _configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.ConciliacionBancariaFechaHasta, FechaHasta.ToString("dd/MM/yy")).ConfigureAwait(false);
        }
        private async Task CargarApuntesBanco(DateTime fechaDesde, DateTime fechaHasta)
        {
            try
            {
                IsBusyApuntesBanco = true;
                var lista = await _bancosService.LeerApuntesBanco(Constantes.Empresas.EMPRESA_DEFECTO, Banco.Codigo, fechaDesde, fechaHasta).ConfigureAwait(false);
                ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>(lista.Select(apunteBancarioDTO => new ApunteBancarioWrapper(apunteBancarioDTO)));
                ApuntesBancoCollectionView = CollectionViewSource.GetDefaultView(ApuntesBanco);
                SaldoInicialBanco = await _bancosService.SaldoBancoInicial(Banco.Entidad, Banco.Oficina, Banco.NumeroCuenta, fechaDesde).ConfigureAwait(false);
                SaldoFinalBanco = await _bancosService.SaldoBancoFinal(Banco.Entidad, Banco.Oficina, Banco.NumeroCuenta, fechaHasta).ConfigureAwait(false);
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
                var lista = await _contabilidadService.LeerApuntesContabilidad(Constantes.Empresas.EMPRESA_DEFECTO, Banco.CuentaContable, fechaDesde, fechaHasta).ConfigureAwait(false);
                ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>(lista.Select(contabilidadDTO => new ContabilidadWrapper(contabilidadDTO)));
                ApuntesContabilidadCollectionView = CollectionViewSource.GetDefaultView(ApuntesContabilidad);
                SaldoInicialContabilidad = await _contabilidadService.SaldoCuenta(Banco.Empresa, Banco.CuentaContable, fechaDesde.AddDays(-1)).ConfigureAwait(false);
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
        private async Task CargarMovimientosRelacionadosTPV(string tipoDatafono)
        {
            string modoCaptura;
            if (tipoDatafono == "WEB")
            {
                modoCaptura = "3";
            } 
            else if (tipoDatafono == "ON ")
            {
                modoCaptura = "1";
            }
            else
            {
                throw new Exception("Tipo de datáfono no contemplado");
            }
            MovimientosTPV = await _bancosService.LeerMovimientosTPV(fechaCaptura: ApunteBancoSeleccionado.FechaOperacion.AddDays(-1), modoCaptura);
            PrepagosPendientes = new();
            MovimientosRelacionados = new ObservableCollection<MovimientoTPV>(
                MovimientosTPV               
            );
            if (ApunteBancoSeleccionado.ImporteMovimiento != MovimientosTPV.Sum(m => m.ImporteOperacion))
            {
                MovimientosTPV = await _bancosService.LeerMovimientosTPV(fechaCaptura: ApunteBancoSeleccionado.FechaOperacion.AddDays(-1), "4"); // terminal price
                MovimientosRelacionados.AddRange(MovimientosTPV);
            }
        }        
        private async Task CargarPrepagosPendientes()
        {
            if (MovimientoTPVSeleccionado is not null)
            {
                PrepagosPendientes = await _bancosService.LeerPrepagosPendientes(MovimientoTPVSeleccionado.ImporteOperacion);
            }
            else
            {
                PrepagosPendientes = null;
            }
        }
        private PedidoCompraDTO CrearPedidoDesdePreContabilidad(PreContabilidadDTO? linea)
        {
            if (linea == null)
            {
                return null;
            }
            PedidoCompraDTO pedido = new()
            {
                Empresa = Banco.Empresa,
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
        private bool CumpleCondicionesTPV(ApunteBancarioWrapper apunteBancoSeleccionado)
        {
            if (ApunteBancoSeleccionado is null)
            {
                return false;
            }
            string concepto2_0 = apunteBancoSeleccionado.RegistrosConcepto[0]?.Concepto2;
            string concepto2_1 = apunteBancoSeleccionado.RegistrosConcepto[1]?.Concepto2;

            return (concepto2_0.StartsWith("WEB") || concepto2_0.StartsWith("ON ")) &&
                   concepto2_1.StartsWith("FACTURAC.COMERCIOS");
        }
        private void FiltrarRegistros()
        {
            if (ApuntesBanco != null)
            {
                foreach (var apunteBanco in ApuntesBanco)
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
                foreach (var apunteContabilidad in ApuntesContabilidad)
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
        private bool MostrarEstadoPunteo(EstadoPunteo estadoPunteo)
        {
            switch (estadoPunteo)
            {
                case EstadoPunteo.SinPuntear:
                    return MostrarSinPuntear;

                case EstadoPunteo.CompletamentePunteado:
                    return MostrarCompletamentePunteado;

                case EstadoPunteo.ParcialmentePunteado:
                    return MostrarParcialmentePunteado;

                default:
                    return true;
            }
        }
        private async Task PuntearMovimientosSoloBanco()
        {
            var grupoPunteo = ApuntesBancoSeleccionados
                .OrderByDescending(apunte => apunte.ImporteMovimiento)
                .First()
                .Id;
            foreach (var apunte in ApuntesBancoSeleccionados)
            {
                await _bancosService.CrearPunteo(apunte.Id, null, apunte.ImporteMovimiento, SIMBOLO_PUNTEO_CONCILIACION, grupoPunteo);
                apunte.EstadoPunteo = EstadoPunteo.CompletamentePunteado;
            }
            _dialogService.ShowNotification("Liquidado correctamente");
        }
        private async Task PuntearMovimientosSoloContabilidad()
        {
            var grupoPunteo = ApuntesContabilidadSeleccionados
                .OrderByDescending(apunte => apunte.Importe)
                .ThenBy(apunte => apunte.Debe)
                .First()
                .Id;
            foreach (var apunte in ApuntesContabilidadSeleccionados)
            {
                await _bancosService.CrearPunteo(null, apunte.Id, apunte.Importe, SIMBOLO_PUNTEO_CONCILIACION, grupoPunteo);
                apunte.EstadoPunteo = EstadoPunteo.CompletamentePunteado;
            }
            _dialogService.ShowNotification("Liquidado correctamente");
        }
        private async Task PuntearMovimientosBancoYContabilidad()
        {
            Queue<ApunteBancarioWrapper> colaBanco = new Queue<ApunteBancarioWrapper>(ApuntesBancoSeleccionados.Where(b => b.EstadoPunteo != EstadoPunteo.CompletamentePunteado));
            Queue<ContabilidadWrapper> colaContabilidad = new Queue<ContabilidadWrapper>(ApuntesContabilidadSeleccionados.Where(c => c.EstadoPunteo != EstadoPunteo.CompletamentePunteado));

            var apunteBanco = colaBanco.Peek();
            var apunteContabilidad = colaContabilidad.Peek();

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
                        colaContabilidad.Dequeue();
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
                        colaBanco.Dequeue();
                        apunteBanco.EstadoPunteo = EstadoPunteo.CompletamentePunteado;
                        apunteContabilidad.EstadoPunteo = EstadoPunteo.ParcialmentePunteado;
                        apunteBanco = colaBanco.Count > 0 ? colaBanco.Peek() : null;
                    }
                    else
                    {
                        importePunteo = importeRestanteBanco; // que es igual a importeRestanteContabilidad
                        importeRestanteContabilidad = 0;
                        importeRestanteBanco = 0;
                        colaBanco.Dequeue();
                        colaContabilidad.Dequeue();
                        apunteBanco.EstadoPunteo = EstadoPunteo.CompletamentePunteado;
                        apunteContabilidad.EstadoPunteo = EstadoPunteo.CompletamentePunteado;
                        apunteBanco = colaBanco.Count > 0 ? colaBanco.Peek() : null;
                        apunteContabilidad = colaContabilidad.Count > 0 ? colaContabilidad.Peek() : null;
                    }                    
                    
                    await _bancosService.CrearPunteo(apunteBancoId, apunteContabilidadId, importePunteo, SIMBOLO_PUNTEO_CONCILIACION);
                    
                }
                _dialogService.ShowNotification("Liquidado correctamente");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Error al puntear:\n" + ex.Message);
            }
        }

        
        public async override void OnNavigatedTo(NavigationContext navigationContext)
        {
            string parametroBanco = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.ConciliacionBancariaUltimoBanco);
            Banco = await _bancosService.LeerBanco(Constantes.Empresas.EMPRESA_DEFECTO, parametroBanco);
                
            string fechaDesdeString = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.ConciliacionBancariaFechaDesde);
            FechaDesde = DateTime.ParseExact(fechaDesdeString, "dd/MM/yy", null);

            string fechaHastaString = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.ConciliacionBancariaFechaHasta);
            FechaHasta = DateTime.ParseExact(fechaHastaString, "dd/MM/yy", null);
        }
    }
}
