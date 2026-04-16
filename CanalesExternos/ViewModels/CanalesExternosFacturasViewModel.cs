using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nesto.Modulos.CanalesExternos.ViewModels
{
    public class CanalesExternosFacturasViewModel : BindableBase
    {
        private const int MARGEN_DIAS_ATRAS = 45;

        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;
        private readonly IDialogService _dialogService;

        public CanalesExternosFacturasViewModel(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion, IDialogService dialogService)
        {
            Titulo = "Facturas canales externos";
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
            _dialogService = dialogService;

            Factory = new Dictionary<string, ICanalExternoFacturas>
            {
                { "Amazon", new CanalExternoFacturasAmazon(configuracion, servicioAutenticacion) }
            };

            CargarFacturasCommand = new DelegateCommand(async () => await OnCargarFacturasAsync(), PuedeCargar);
            ContabilizarTodasCommand = new DelegateCommand(async () => await OnContabilizarTodasAsync(), PuedeContabilizar);
            EmparejarListadoCommand = new DelegateCommand(() => OnEmparejarListado(), PuedeEmparejarListado);
            ContabilizarFacturaCommand = new DelegateCommand<FacturaCanalExterno>(
                async f => await OnContabilizarFacturaAsync(f),
                f => PuedeContabilizarFactura(f));

            Año = DateTime.Today.Year;
            Mes = DateTime.Today.Month == 1 ? 12 : DateTime.Today.Month - 1;
        }

        public Dictionary<string, ICanalExternoFacturas> Factory { get; }

        private string _titulo;
        public string Titulo { get => _titulo; set => SetProperty(ref _titulo, value); }

        public IEnumerable<string> CanalesDisponibles => Factory.Keys;

        private string _canalSeleccionadoNombre = "Amazon";
        public string CanalSeleccionadoNombre
        {
            get => _canalSeleccionadoNombre;
            set
            {
                if (SetProperty(ref _canalSeleccionadoNombre, value))
                {
                    RaiseCanExecuteChanged();
                }
            }
        }

        public ICanalExternoFacturas CanalSeleccionado
            => Factory.TryGetValue(_canalSeleccionadoNombre ?? string.Empty, out var canal) ? canal : null;

        private int _año;
        public int Año { get => _año; set => SetProperty(ref _año, value); }

        private int _mes;
        public int Mes { get => _mes; set => SetProperty(ref _mes, value); }

        private ObservableCollection<FacturaCanalExterno> _facturas = new ObservableCollection<FacturaCanalExterno>();
        public ObservableCollection<FacturaCanalExterno> Facturas
        {
            get => _facturas;
            set => SetProperty(ref _facturas, value);
        }

        public IEnumerable<FacturaCanalExterno> FacturasPendientes
            => Facturas.Where(f => f.Estado == EstadoFacturaCanalExterno.PendienteContabilizar);
        public IEnumerable<FacturaCanalExterno> FacturasYaContabilizadas
            => Facturas.Where(f => f.Estado == EstadoFacturaCanalExterno.YaContabilizada);
        public IEnumerable<FacturaCanalExterno> FacturasHueco
            => Facturas.Where(f => f.Estado == EstadoFacturaCanalExterno.Hueco);

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            set
            {
                SetProperty(ref _estaOcupado, value);
                RaiseCanExecuteChanged();
            }
        }

        private string _mensajeEstado;
        public string MensajeEstado { get => _mensajeEstado; set => SetProperty(ref _mensajeEstado, value); }

        private string _listadoPegado;
        /// Texto copiado de Seller Central → Tax Document Library. Contiene los NºFactura reales
        /// que no devuelve la SP-API, y permite emparejar cada factura reconstruida con su PDF.
        public string ListadoPegado
        {
            get => _listadoPegado;
            set
            {
                if (SetProperty(ref _listadoPegado, value))
                {
                    ((DelegateCommand)EmparejarListadoCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private decimal _totalContabilizadoMes;
        public decimal TotalContabilizadoMes { get => _totalContabilizadoMes; set => SetProperty(ref _totalContabilizadoMes, value); }

        private decimal? _totalLiquidacionesMes;
        public decimal? TotalLiquidacionesMes { get => _totalLiquidacionesMes; set => SetProperty(ref _totalLiquidacionesMes, value); }

        public decimal? DiferenciaCuadre => TotalLiquidacionesMes.HasValue
            ? TotalContabilizadoMes - TotalLiquidacionesMes
            : (decimal?)null;

        public ICommand CargarFacturasCommand { get; }
        public ICommand ContabilizarTodasCommand { get; }
        public ICommand EmparejarListadoCommand { get; }
        public ICommand ContabilizarFacturaCommand { get; }

        private bool PuedeCargar() => !EstaOcupado && CanalSeleccionado != null;
        private bool PuedeContabilizar() => !EstaOcupado && FacturasPendientes.Any();
        private bool PuedeEmparejarListado() => !EstaOcupado && !string.IsNullOrWhiteSpace(_listadoPegado) && Facturas.Any();
        private bool PuedeContabilizarFactura(FacturaCanalExterno f)
            => !EstaOcupado && f != null && f.Estado == EstadoFacturaCanalExterno.PendienteContabilizar;

        private void RaiseCanExecuteChanged()
        {
            ((DelegateCommand)CargarFacturasCommand).RaiseCanExecuteChanged();
            ((DelegateCommand)ContabilizarTodasCommand).RaiseCanExecuteChanged();
            ((DelegateCommand)EmparejarListadoCommand).RaiseCanExecuteChanged();
            ((DelegateCommand<FacturaCanalExterno>)ContabilizarFacturaCommand).RaiseCanExecuteChanged();
        }

        private async Task OnCargarFacturasAsync()
        {
            try
            {
                EstaOcupado = true;
                MensajeEstado = "Descargando facturas...";
                var canal = CanalSeleccionado;
                if (canal == null) return;

                var facturas = await canal.GetFacturasMesAsync(Año, Mes, MARGEN_DIAS_ATRAS);
                Facturas = new ObservableCollection<FacturaCanalExterno>(facturas.OrderBy(f => f.FechaFactura).ThenBy(f => f.InvoiceId));

                RaisePropertyChanged(nameof(FacturasPendientes));
                RaisePropertyChanged(nameof(FacturasYaContabilizadas));
                RaisePropertyChanged(nameof(FacturasHueco));

                MensajeEstado = $"{FacturasPendientes.Count()} pendientes, {FacturasYaContabilizadas.Count()} ya contabilizadas, {FacturasHueco.Count()} huecos.";
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"Error al cargar facturas: {ex.Message}");
                MensajeEstado = $"Error: {ex.Message}";
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private async Task OnContabilizarTodasAsync()
        {
            try
            {
                EstaOcupado = true;
                var canal = CanalSeleccionado;
                if (canal == null) return;

                var aContabilizar = FacturasPendientes.ToList();
                int correctas = 0;
                int errores = 0;
                foreach (var factura in aContabilizar)
                {
                    MensajeEstado = $"Contabilizando {factura.InvoiceId}...";
                    await canal.ContabilizarFacturaAsync(factura);
                    if (factura.Estado == EstadoFacturaCanalExterno.Contabilizada) correctas++;
                    else errores++;
                }

                RaisePropertyChanged(nameof(FacturasPendientes));
                RaisePropertyChanged(nameof(FacturasYaContabilizadas));

                if (canal.SoportaCuadreLiquidacion)
                {
                    await ActualizarCuadreAsync(canal);
                }

                MensajeEstado = $"Terminado. {correctas} contabilizadas, {errores} errores.";
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"Error al contabilizar: {ex.Message}");
                MensajeEstado = $"Error: {ex.Message}";
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private async void OnEmparejarListado()
        {
            try
            {
                var canal = CanalSeleccionado;
                if (canal == null) return;
                var resultado = canal.EmparejarListado(Facturas, _listadoPegado);
                // Tras emparejar, refrescamos estados con NestoAPI: las facturas que el usuario ya
                // contabilizó en sesiones anteriores aparecen ahora como YaContabilizada (en gris).
                int marcadasContabilizadas = await canal.RefrescarEstadosContabilizadasAsync(
                    Facturas, Año, Mes, MARGEN_DIAS_ATRAS);
                MensajeEstado = $"Emparejadas: {resultado.Emparejadas}. " +
                                $"Sintéticas añadidas: {resultado.Sinteticas}. " +
                                $"Reconstruidas sin match: {resultado.NoEmparejadas}. " +
                                $"Detectadas {marcadasContabilizadas} ya contabilizadas en NestoAPI.";
                RaisePropertyChanged(nameof(FacturasPendientes));
                RaisePropertyChanged(nameof(FacturasYaContabilizadas));
                RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"Error al emparejar listado: {ex.Message}");
                MensajeEstado = $"Error: {ex.Message}";
            }
        }

        private async Task OnContabilizarFacturaAsync(FacturaCanalExterno factura)
        {
            if (factura == null) return;
            try
            {
                EstaOcupado = true;
                MensajeEstado = $"Contabilizando {factura.InvoiceId}...";
                var canal = CanalSeleccionado;
                if (canal == null) return;
                await canal.ContabilizarFacturaAsync(factura);
                if (factura.Estado == EstadoFacturaCanalExterno.Contabilizada)
                {
                    string asientos = factura.AsientoPagoNesto.HasValue && factura.AsientoPagoNesto != 0
                        ? $"Asiento Fra. {factura.AsientoNesto} · Pago {factura.AsientoPagoNesto}"
                        : $"Asiento {factura.AsientoNesto}";
                    MensajeEstado = $"{factura.InvoiceId} → Pedido {factura.NumeroPedidoNesto} · Factura {factura.NumeroFacturaNesto} · {asientos}";
                }
                else
                    MensajeEstado = $"Error en {factura.InvoiceId}: {factura.MensajeError}";
                RaisePropertyChanged(nameof(FacturasPendientes));
                RaisePropertyChanged(nameof(FacturasYaContabilizadas));
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"Error al contabilizar: {ex.Message}");
                MensajeEstado = $"Error: {ex.Message}";
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private async Task ActualizarCuadreAsync(ICanalExternoFacturas canal)
        {
            try
            {
                var cuadre = await canal.CuadrarConLiquidacionAsync(Año, Mes);
                TotalContabilizadoMes = cuadre.TotalFacturasContabilizadas;
                TotalLiquidacionesMes = cuadre.TotalComisionesLiquidaciones;
                RaisePropertyChanged(nameof(DiferenciaCuadre));
            }
            catch (Exception ex)
            {
                MensajeEstado += $" | Cuadre no disponible: {ex.Message}";
            }
        }
    }
}
