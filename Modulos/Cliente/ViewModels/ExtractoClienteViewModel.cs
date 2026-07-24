using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Events;
using Nesto.Modulos.Cliente.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#419 v1: ventana de Extracto de Cliente (contenedor que irá creciendo). Esta
    /// primera versión: consultar los movimientos pendientes y LIQUIDAR dos entre sí
    /// (NestoAPI#333; la lógica y las validaciones viven en la API, aquí solo se pinta y se
    /// pide). Driver: el paso de revisión de #332 exige poder liquidar antes de remesar.
    /// </summary>
    public class ExtractoClienteViewModel : BindableBase, INavigationAware
    {
        private readonly IExtractoClienteService _servicio;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;

        public ExtractoClienteViewModel(IExtractoClienteService servicio, IDialogService dialogService,
            IEventAggregator eventAggregator)
        {
            _servicio = servicio;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            Titulo = "Extracto de Cliente";
            CargarCommand = new DelegateCommand(OnCargar, CanCargar);
            LiquidarCommand = new DelegateCommand(OnLiquidar, CanLiquidar);
        }

        public string Titulo { get; }

        // Nesto#419: al navegar aquí desde Remesas (doble clic en un efecto) se recibe el cliente
        // como parámetro y se cargan sus movimientos automáticamente. IsNavigationTarget = true
        // reutiliza la MISMA pestaña de Extracto y le cambia el cliente, en vez de abrir otra.
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            string cliente = navigationContext?.Parameters?.GetValue<string>("cliente");
            if (!string.IsNullOrWhiteSpace(cliente))
            {
                ClienteSeleccionado = cliente.Trim();
                _ = CargarAsync();
            }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext) { }

        private string _clienteSeleccionado;
        public string ClienteSeleccionado
        {
            get => _clienteSeleccionado;
            set
            {
                if (SetProperty(ref _clienteSeleccionado, value))
                {
                    CargarCommand.RaiseCanExecuteChanged();
                    // Cambiar de cliente invalida lo que hubiera en pantalla
                    Movimientos = new ObservableCollection<ExtractoClienteModel>();
                }
            }
        }

        private ObservableCollection<ExtractoClienteModel> _movimientos = new ObservableCollection<ExtractoClienteModel>();
        public ObservableCollection<ExtractoClienteModel> Movimientos
        {
            get => _movimientos;
            private set
            {
                if (_movimientos != null)
                {
                    foreach (ExtractoClienteModel movimiento in _movimientos)
                    {
                        movimiento.PropertyChanged -= MovimientoCambiado;
                    }
                }
                _ = SetProperty(ref _movimientos, value);
                foreach (ExtractoClienteModel movimiento in _movimientos)
                {
                    movimiento.PropertyChanged += MovimientoCambiado;
                }
                RaisePropertyChanged(nameof(TotalPendiente));
                LiquidarCommand.RaiseCanExecuteChanged();
            }
        }

        private void MovimientoCambiado(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExtractoClienteModel.Seleccionado))
            {
                LiquidarCommand.RaiseCanExecuteChanged();
            }
        }

        public decimal TotalPendiente => Movimientos?.Sum(m => m.ImportePendiente) ?? 0;

        public List<ExtractoClienteModel> Seleccionados =>
            Movimientos?.Where(m => m.Seleccionado).ToList() ?? new List<ExtractoClienteModel>();

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            set => SetProperty(ref _estaOcupado, value);
        }

        public DelegateCommand CargarCommand { get; }
        private bool CanCargar() => !string.IsNullOrWhiteSpace(ClienteSeleccionado);
        private async void OnCargar() => await CargarAsync();

        // Function As Task para poder esperarla en los tests (patrón Fase 1C).
        public async Task CargarAsync()
        {
            if (!CanCargar())
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                List<ExtractoClienteModel> movimientos = await _servicio.LeerExtractoPendiente(ClienteSeleccionado);
                Movimientos = new ObservableCollection<ExtractoClienteModel>(movimientos);
            }
            catch (Exception ex)
            {
                Movimientos = new ObservableCollection<ExtractoClienteModel>();
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public DelegateCommand LiquidarCommand { get; }

        // Exactamente dos movimientos marcados; el resto de reglas (mismo cliente, signos
        // opuestos, remesas, estados bloqueados) las valida la API con mensaje claro.
        private bool CanLiquidar() => Seleccionados.Count == 2;

        private async void OnLiquidar() => await LiquidarAsync();

        public async Task LiquidarAsync()
        {
            List<ExtractoClienteModel> seleccionados = Seleccionados;
            if (seleccionados.Count != 2)
            {
                return;
            }
            ExtractoClienteModel origen = seleccionados[0];
            ExtractoClienteModel destino = seleccionados[1];

            if (!string.Equals(origen.Empresa?.Trim(), destino.Empresa?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _dialogService.ShowError("Los dos movimientos deben ser de la misma empresa " +
                    $"(uno es de la {origen.Empresa?.Trim()} y otro de la {destino.Empresa?.Trim()}).");
                return;
            }

            bool confirmado = _dialogService.ShowConfirmationAnswer("Liquidar movimientos",
                $"¿Liquidar el movimiento {origen.Id} ({origen.ImportePendiente:C} pendiente) " +
                $"contra el {destino.Id} ({destino.ImportePendiente:C} pendiente)?" + Environment.NewLine +
                "Si los importes no coinciden, la diferencia quedará pendiente en el de mayor importe.");
            if (!confirmado)
            {
                return;
            }

            try
            {
                EstaOcupado = true;
                ResultadoLiquidacionModel resultado = await _servicio.LiquidarEfectos(
                    origen.Empresa?.Trim(), origen.Id, destino.Id);
                _dialogService.ShowNotification("Liquidación realizada",
                    $"Movimiento {origen.Id}: quedan {resultado.ImportePdteOrigen:C} pendientes. " +
                    $"Movimiento {destino.Id}: quedan {resultado.ImportePdteDestino:C} pendientes.");
                await CargarAsync(); // refrescar pendientes (los saldados a 0 desaparecen)

                // Avisar a la ventana de Remesas para que actualice EN SITIO esos efectos (sin
                // recargar candidatos, que perdería las marcas del usuario). Se envían los nuevos
                // importes pendientes y si el cliente sigue teniendo negativos (Movimientos ya
                // está refrescado por CargarAsync).
                _eventAggregator?.GetEvent<EfectosLiquidadosEvent>().Publish(new EfectosLiquidadosPayload
                {
                    Empresa = origen.Empresa?.Trim(),
                    Cliente = ClienteSeleccionado?.Trim(),
                    NuevosImportesPendientes = new Dictionary<int, decimal>
                    {
                        [origen.Id] = resultado.ImportePdteOrigen,
                        [destino.Id] = resultado.ImportePdteDestino
                    },
                    ClienteSigueConNegativos = Movimientos.Any(m => m.ImportePendiente < 0)
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }
    }
}
