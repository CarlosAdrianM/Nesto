using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos.Models;
using Nesto.Modulos.CanalesExternos.Models.Cuadres;
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

        public ICommand CalcularCuadreCommand { get; }

        private bool PuedeCalcular() => !EstaOcupado && CanalSeleccionado != null && CanalSeleccionado.SoportaCuadreLiquidacion;

        private async Task OnCalcularCuadreAsync()
        {
            try
            {
                EstaOcupado = true;
                Cuadre = await CanalSeleccionado.CuadrarConLiquidacionAsync(Año, Mes);
                CuadreFacturas = await CanalSeleccionado.CuadrarFacturasAsync(Año, Mes);
                CuadreLiquidaciones = await CanalSeleccionado.CuadrarLiquidacionesAsync(Año, Mes);
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"Error al calcular cuadre: {ex.Message}");
            }
            finally
            {
                EstaOcupado = false;
            }
        }
    }
}
