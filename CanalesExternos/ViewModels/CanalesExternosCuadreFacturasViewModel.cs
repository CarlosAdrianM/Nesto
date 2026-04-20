using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos.Models;
using Nesto.Modulos.CanalesExternos.Models.Cuadres;
using Nesto.Modulos.CanalesExternos.Models.Cuadres.Saldo555;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nesto.Modulos.CanalesExternos.ViewModels
{
    public class CanalesExternosCuadreFacturasViewModel : BindableBase
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;
        private readonly IDialogService _dialogService;

        public CanalesExternosCuadreFacturasViewModel(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion, IDialogService dialogService)
        {
            Titulo = "Cuadre Canales Externos";
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
            _dialogService = dialogService;

            Factory = new Dictionary<string, ICanalExternoFacturas>
            {
                { "Amazon", new CanalExternoFacturasAmazon(configuracion, servicioAutenticacion) }
            };

            CalcularCuadreCommand = new DelegateCommand(async () => await OnCalcularCuadreAsync(), PuedeCalcular);

            Año = DateTime.Today.Year;
            Mes = DateTime.Today.Month == 1 ? 12 : DateTime.Today.Month - 1;
        }

        public Dictionary<string, ICanalExternoFacturas> Factory { get; }
        public IEnumerable<string> CanalesDisponibles => Factory.Keys;

        private string _titulo;
        public string Titulo { get => _titulo; set => SetProperty(ref _titulo, value); }

        private string _canalSeleccionadoNombre = "Amazon";
        public string CanalSeleccionadoNombre
        {
            get => _canalSeleccionadoNombre;
            set
            {
                if (SetProperty(ref _canalSeleccionadoNombre, value))
                {
                    ((DelegateCommand)CalcularCuadreCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public ICanalExternoFacturas CanalSeleccionado
            => Factory.TryGetValue(_canalSeleccionadoNombre ?? string.Empty, out var canal) ? canal : null;

        private int _año;
        public int Año { get => _año; set => SetProperty(ref _año, value); }

        private int _mes;
        public int Mes { get => _mes; set => SetProperty(ref _mes, value); }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            set
            {
                SetProperty(ref _estaOcupado, value);
                ((DelegateCommand)CalcularCuadreCommand).RaiseCanExecuteChanged();
            }
        }

        private string _mensajeProceso = "Calculando cuadre...";
        public string MensajeProceso
        {
            get => _mensajeProceso;
            set => SetProperty(ref _mensajeProceso, value);
        }

        private CuadreLiquidacionCanalExterno _cuadre;
        public CuadreLiquidacionCanalExterno Cuadre
        {
            get => _cuadre;
            set
            {
                SetProperty(ref _cuadre, value);
                RaisePropertyChanged(nameof(DetalleCuadre));
            }
        }

        public ObservableCollection<CuadreLiquidacionDetalle> DetalleCuadre
            => _cuadre == null ? new ObservableCollection<CuadreLiquidacionDetalle>()
                               : new ObservableCollection<CuadreLiquidacionDetalle>(_cuadre.Detalle);

        // Issue #349 Fase 1: cuadre Amazon ↔ Nesto por InvoiceId
        private ResultadoCuadre<string> _cuadreFacturas;
        public ResultadoCuadre<string> CuadreFacturas
        {
            get => _cuadreFacturas;
            set
            {
                SetProperty(ref _cuadreFacturas, value);
                RaisePropertyChanged(nameof(FacturasCuadradas));
                RaisePropertyChanged(nameof(FacturasSoloEnNesto));
                RaisePropertyChanged(nameof(FacturasSoloEnAmazon));
            }
        }

        public ObservableCollection<ElementoCuadre<string>> FacturasCuadradas
            => _cuadreFacturas == null ? new ObservableCollection<ElementoCuadre<string>>()
                                       : new ObservableCollection<ElementoCuadre<string>>(_cuadreFacturas.Cuadrados);

        public ObservableCollection<ElementoCuadre<string>> FacturasSoloEnNesto
            => _cuadreFacturas == null ? new ObservableCollection<ElementoCuadre<string>>()
                                       : new ObservableCollection<ElementoCuadre<string>>(_cuadreFacturas.SoloEnNesto);

        public ObservableCollection<ElementoCuadre<string>> FacturasSoloEnAmazon
            => _cuadreFacturas == null ? new ObservableCollection<ElementoCuadre<string>>()
                                       : new ObservableCollection<ElementoCuadre<string>>(_cuadreFacturas.SoloEnAmazon);

        // Issue #349 Fase 2: cuadre Amazon ↔ Nesto por FinancialEventGroupId (liquidaciones)
        private ResultadoCuadre<string> _cuadreLiquidaciones;
        public ResultadoCuadre<string> CuadreLiquidaciones
        {
            get => _cuadreLiquidaciones;
            set
            {
                SetProperty(ref _cuadreLiquidaciones, value);
                RaisePropertyChanged(nameof(LiquidacionesCuadradas));
                RaisePropertyChanged(nameof(LiquidacionesSoloEnNesto));
                RaisePropertyChanged(nameof(LiquidacionesSoloEnAmazon));
                RaisePropertyChanged(nameof(LiquidacionesImportesDistintos));
            }
        }

        public ObservableCollection<ElementoCuadre<string>> LiquidacionesCuadradas
            => _cuadreLiquidaciones == null ? new ObservableCollection<ElementoCuadre<string>>()
                                            : new ObservableCollection<ElementoCuadre<string>>(_cuadreLiquidaciones.Cuadrados);

        public ObservableCollection<ElementoCuadre<string>> LiquidacionesSoloEnNesto
            => _cuadreLiquidaciones == null ? new ObservableCollection<ElementoCuadre<string>>()
                                            : new ObservableCollection<ElementoCuadre<string>>(_cuadreLiquidaciones.SoloEnNesto);

        public ObservableCollection<ElementoCuadre<string>> LiquidacionesSoloEnAmazon
            => _cuadreLiquidaciones == null ? new ObservableCollection<ElementoCuadre<string>>()
                                            : new ObservableCollection<ElementoCuadre<string>>(_cuadreLiquidaciones.SoloEnAmazon);

        public ObservableCollection<ElementoCuadre<string>> LiquidacionesImportesDistintos
            => _cuadreLiquidaciones == null ? new ObservableCollection<ElementoCuadre<string>>()
                                            : new ObservableCollection<ElementoCuadre<string>>(_cuadreLiquidaciones.ImportesDistintos);

        // Issue #349 Fase 3: cuadre Amazon ↔ Nesto por AmazonOrderId (pedidos)
        private ResultadoCuadre<string> _cuadrePedidos;
        public ResultadoCuadre<string> CuadrePedidos
        {
            get => _cuadrePedidos;
            set
            {
                SetProperty(ref _cuadrePedidos, value);
                RaisePropertyChanged(nameof(PedidosCuadrados));
                RaisePropertyChanged(nameof(PedidosSoloEnNesto));
                RaisePropertyChanged(nameof(PedidosSoloEnAmazon));
            }
        }

        public ObservableCollection<ElementoCuadre<string>> PedidosCuadrados
            => _cuadrePedidos == null ? new ObservableCollection<ElementoCuadre<string>>()
                                      : new ObservableCollection<ElementoCuadre<string>>(_cuadrePedidos.Cuadrados);

        public ObservableCollection<ElementoCuadre<string>> PedidosSoloEnNesto
            => _cuadrePedidos == null ? new ObservableCollection<ElementoCuadre<string>>()
                                      : new ObservableCollection<ElementoCuadre<string>>(_cuadrePedidos.SoloEnNesto);

        public ObservableCollection<ElementoCuadre<string>> PedidosSoloEnAmazon
            => _cuadrePedidos == null ? new ObservableCollection<ElementoCuadre<string>>()
                                      : new ObservableCollection<ElementoCuadre<string>>(_cuadrePedidos.SoloEnAmazon);

        public ICommand CalcularCuadreCommand { get; }

        // Issue #349 Fase 4 / NestoAPI#164: saldo de cuentas 555 al corte (último día del mes seleccionado).
        private ObservableCollection<ResumenSaldoCuentaDto> _saldosCuentas555;
        public ObservableCollection<ResumenSaldoCuentaDto> SaldosCuentas555
        {
            get => _saldosCuentas555;
            set
            {
                SetProperty(ref _saldosCuentas555, value);
                RaisePropertyChanged(nameof(GruposCuentaSeleccionada));
            }
        }

        private ResumenSaldoCuentaDto _cuentaSaldo555Seleccionada;
        public ResumenSaldoCuentaDto CuentaSaldo555Seleccionada
        {
            get => _cuentaSaldo555Seleccionada;
            set
            {
                SetProperty(ref _cuentaSaldo555Seleccionada, value);
                RaisePropertyChanged(nameof(GruposCuentaSeleccionada));
            }
        }

        public ObservableCollection<GrupoAbiertoDto> GruposCuentaSeleccionada
            => _cuentaSaldo555Seleccionada?.Resultado?.GruposAbiertos == null
                ? new ObservableCollection<GrupoAbiertoDto>()
                : new ObservableCollection<GrupoAbiertoDto>(_cuentaSaldo555Seleccionada.Resultado.GruposAbiertos);

        private bool PuedeCalcular() => !EstaOcupado && CanalSeleccionado != null && CanalSeleccionado.SoportaCuadreLiquidacion;

        private async Task OnCalcularCuadreAsync()
        {
            try
            {
                EstaOcupado = true;
                MensajeProceso = "Cuadrando totales del mes...";
                Cuadre = await CanalSeleccionado.CuadrarConLiquidacionAsync(Año, Mes);
                MensajeProceso = "Cuadrando facturas (Amazon ↔ Nesto)...";
                CuadreFacturas = await CanalSeleccionado.CuadrarFacturasAsync(Año, Mes);
                MensajeProceso = "Cuadrando liquidaciones...";
                CuadreLiquidaciones = await CanalSeleccionado.CuadrarLiquidacionesAsync(Año, Mes);
                MensajeProceso = "Cuadrando pedidos (Amazon ↔ Nesto)...";
                CuadrePedidos = await CanalSeleccionado.CuadrarPedidosAsync(Año, Mes);

                DateTime fechaCorte = new DateTime(Año, Mes, DateTime.DaysInMonth(Año, Mes));
                var progreso = new Progress<string>(msg => MensajeProceso = msg);
                var saldos = await CanalSeleccionado.CalcularSaldos555Async(fechaCorte, progreso);
                SaldosCuentas555 = new ObservableCollection<ResumenSaldoCuentaDto>(saldos);
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"Error al calcular cuadre: {ex.Message}");
            }
            finally
            {
                MensajeProceso = "Calculando cuadre...";
                EstaOcupado = false;
            }
        }
    }
}
