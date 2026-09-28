using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.ViewModels
{
    /// <summary>
    /// NestoAPI#522: facturas que Verifactu todavía no da por buenas (sin registrar, o incorrectas o rechazadas
    /// por la AEAT), con el motivo y qué hacer, para que administración las arregle y reintente el envío.
    /// Se abre desde Contabilidad → Verifactu y desde la nota diaria de la campana. La API solo deja verlas a
    /// Administración, Dirección e Informática. Al volver a navegar a la ventana (p. ej. desde la campana con
    /// la pestaña ya abierta) se recarga.
    /// </summary>
    public class FacturasPendientesVerifactuViewModel : ObservableObject, INavigationAware
    {
        private readonly IFacturasVerifactuService _servicio;
        private readonly IServicioDialogos _dialogService;

        public FacturasPendientesVerifactuViewModel(IFacturasVerifactuService servicio, IServicioDialogos dialogService)
        {
            _servicio = servicio;
            _dialogService = dialogService;
            Titulo = "Facturas Verifactu";
            CargarCommand = new AsyncRelayCommand(CargarAsync);
            ReintentarCommand = new AsyncRelayCommand(ReintentarAsync, CanReintentar);
        }

        public string Titulo { get; }

        private ObservableCollection<FacturaPendienteVerifactuModel> _facturas = new ObservableCollection<FacturaPendienteVerifactuModel>();
        public ObservableCollection<FacturaPendienteVerifactuModel> Facturas
        {
            get => _facturas;
            private set
            {
                if (SetProperty(ref _facturas, value))
                {
                    OnPropertyChanged(nameof(Resumen));
                }
            }
        }

        private FacturaPendienteVerifactuModel? _facturaSeleccionada;
        public FacturaPendienteVerifactuModel? FacturaSeleccionada
        {
            get => _facturaSeleccionada;
            set
            {
                if (SetProperty(ref _facturaSeleccionada, value))
                {
                    OnPropertyChanged(nameof(HayFacturaSeleccionada));
                    ReintentarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>Sin fila seleccionada no se enseña el detalle (motivo, qué hacer, reintentar).</summary>
        public bool HayFacturaSeleccionada => FacturaSeleccionada != null;

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    ReintentarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>Cuántas hay y de qué tipo (la cabecera de la ventana).</summary>
        public string Resumen
        {
            get
            {
                int total = Facturas.Count;
                if (total == 0)
                {
                    return "No hay facturas pendientes de Verifactu.";
                }
                int incorrectas = Facturas.Count(f => f.Situacion == SITUACION_INCORRECTA_AEAT);
                int sinRegistrar = total - incorrectas;
                var partes = new List<string>();
                if (sinRegistrar > 0)
                {
                    partes.Add(sinRegistrar == 1 ? "1 sin registrar" : $"{sinRegistrar} sin registrar");
                }
                if (incorrectas > 0)
                {
                    partes.Add(incorrectas == 1 ? "1 incorrecta en la AEAT" : $"{incorrectas} incorrectas en la AEAT");
                }
                return (total == 1 ? "1 factura" : $"{total} facturas") + $": {string.Join(" y ", partes)}.";
            }
        }

        /// <summary>Texto de situación que devuelve la API para las registradas que la AEAT no acepta.</summary>
        internal const string SITUACION_INCORRECTA_AEAT = "Incorrecta en la AEAT";

        public IAsyncRelayCommand CargarCommand { get; }

        public async Task CargarAsync()
        {
            string? seleccionada = FacturaSeleccionada?.Numero;
            try
            {
                EstaOcupado = true;
                List<FacturaPendienteVerifactuModel> lista = await _servicio.LeerFacturasPendientes();
                Facturas = new ObservableCollection<FacturaPendienteVerifactuModel>(lista ?? new List<FacturaPendienteVerifactuModel>());
                FacturaSeleccionada = Facturas.FirstOrDefault(f => f.Numero == seleccionada);
            }
            catch (Exception ex)
            {
                Facturas = new ObservableCollection<FacturaPendienteVerifactuModel>();
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public IAsyncRelayCommand ReintentarCommand { get; }
        private bool CanReintentar() => FacturaSeleccionada != null && FacturaSeleccionada.PuedeReintentar && !EstaOcupado;

        public async Task ReintentarAsync()
        {
            if (!CanReintentar())
            {
                return;
            }
            FacturaPendienteVerifactuModel factura = FacturaSeleccionada!;
            try
            {
                EstaOcupado = true;
                ResultadoReintentoVerifactuModel resultado = await _servicio.ReintentarFactura(factura.Empresa ?? string.Empty, factura.Numero ?? string.Empty);
                int posicion = Facturas.IndexOf(factura);
                if (resultado.Factura == null)
                {
                    // Ya no está pendiente: sale de la lista
                    Facturas.Remove(factura);
                    FacturaSeleccionada = null;
                }
                else if (posicion >= 0)
                {
                    // Sigue pendiente, con el motivo nuevo
                    Facturas[posicion] = resultado.Factura;
                    FacturaSeleccionada = resultado.Factura;
                }
                OnPropertyChanged(nameof(Resumen));
                if (resultado.Exitoso)
                {
                    _dialogService.ShowNotification("Verifactu", resultado.Mensaje ?? $"Factura {factura.Numero} enviada.");
                }
                else
                {
                    _dialogService.ShowError(resultado.Mensaje ?? $"No se ha podido enviar la factura {factura.Numero}.");
                }
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

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _ = CargarAsync();
        }

        /// <summary>Una sola pestaña: si ya está abierta, se reutiliza (y se recarga).</summary>
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
        }
    }
}
