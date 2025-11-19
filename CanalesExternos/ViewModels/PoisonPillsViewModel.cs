using CanalesExternos.Models;
using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos.Interfaces;
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
    public class PoisonPillsViewModel : BindableBase
    {
        private readonly IPoisonPillsService _servicio;
        private readonly IDialogService _dialogService;
        private readonly IConfiguracion _configuracion;

        public PoisonPillsViewModel(IPoisonPillsService servicio, IDialogService dialogService, IConfiguracion configuracion)
        {
            _servicio = servicio;
            _dialogService = dialogService;
            _configuracion = configuracion;

            Titulo = "Gestión de Poison Pills";

            // Inicializar listas
            EstadosDisponibles = new ObservableCollection<string>
            {
                "Todos",
                "PoisonPill",
                "Retrying",
                "Reprocess",
                "Resolved",
                "PermanentFailure"
            };

            TablasDisponibles = new ObservableCollection<string>
            {
                "Todas",
                "Clientes",
                "Productos",
                "Pedidos",
                "Pagos"
            };

            EstadoSeleccionado = "PoisonPill"; // Por defecto mostrar solo poison pills
            TablaSeleccionada = "Todas";

            // Inicializar comandos
            CargarPoisonPillsCommand = new DelegateCommand(async () => await OnCargarPoisonPillsAsync());
            ReprocesarCommand = new DelegateCommand(async () => await OnReprocesarAsync(), CanExecuteActionCommand);
            MarcarComoResueltoCommand = new DelegateCommand(async () => await OnMarcarComoResueltoAsync(), CanExecuteActionCommand);
            MarcarComoFalloPermanenteCommand = new DelegateCommand(async () => await OnMarcarComoFalloPermanenteAsync(), CanExecuteActionCommand);
            VerDetalleCommand = new DelegateCommand(OnVerDetalle, CanExecuteActionCommand);
        }

        #region Propiedades

        private string _titulo;
        public string Titulo
        {
            get => _titulo;
            set => SetProperty(ref _titulo, value);
        }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            set => SetProperty(ref _estaOcupado, value);
        }

        private ObservableCollection<PoisonPillModel> _listaPoisonPills;
        public ObservableCollection<PoisonPillModel> ListaPoisonPills
        {
            get => _listaPoisonPills;
            set => SetProperty(ref _listaPoisonPills, value);
        }

        private PoisonPillModel _poisonPillSeleccionado;
        public PoisonPillModel PoisonPillSeleccionado
        {
            get => _poisonPillSeleccionado;
            set
            {
                SetProperty(ref _poisonPillSeleccionado, value);
                RaiseCanExecuteChanged();
            }
        }

        private ObservableCollection<string> _estadosDisponibles;
        public ObservableCollection<string> EstadosDisponibles
        {
            get => _estadosDisponibles;
            set => SetProperty(ref _estadosDisponibles, value);
        }

        private string _estadoSeleccionado;
        public string EstadoSeleccionado
        {
            get => _estadoSeleccionado;
            set
            {
                SetProperty(ref _estadoSeleccionado, value);
            }
        }

        private ObservableCollection<string> _tablasDisponibles;
        public ObservableCollection<string> TablasDisponibles
        {
            get => _tablasDisponibles;
            set => SetProperty(ref _tablasDisponibles, value);
        }

        private string _tablaSeleccionada;
        public string TablaSeleccionada
        {
            get => _tablaSeleccionada;
            set
            {
                SetProperty(ref _tablaSeleccionada, value);
            }
        }

        private int _totalPoisonPills;
        public int TotalPoisonPills
        {
            get => _totalPoisonPills;
            set => SetProperty(ref _totalPoisonPills, value);
        }

        #endregion

        #region Comandos

        public ICommand CargarPoisonPillsCommand { get; private set; }
        public ICommand ReprocesarCommand { get; private set; }
        public ICommand MarcarComoResueltoCommand { get; private set; }
        public ICommand MarcarComoFalloPermanenteCommand { get; private set; }
        public ICommand VerDetalleCommand { get; private set; }

        #endregion

        #region Métodos de Comandos

        private async Task OnCargarPoisonPillsAsync()
        {
            try
            {
                EstaOcupado = true;

                // Preparar filtros
                string filtroEstado = EstadoSeleccionado == "Todos" ? null : EstadoSeleccionado;
                string filtroTabla = TablaSeleccionada == "Todas" ? null : TablaSeleccionada;

                // Cargar poison pills
                var lista = await _servicio.ObtenerPoisonPillsAsync(filtroEstado, filtroTabla, 100);

                ListaPoisonPills = new ObservableCollection<PoisonPillModel>(lista);
                TotalPoisonPills = lista.Count;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Error al cargar poison pills: {ex.Message}");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private async Task OnReprocesarAsync()
        {
            if (PoisonPillSeleccionado == null) return;

            try
            {
                bool continuar = _dialogService.ShowConfirmationAnswer(
                    "Reprocesar mensaje",
                    $"¿Está seguro de que desea reprocesar el mensaje {PoisonPillSeleccionado.DisplayId}?\n\n" +
                    "El contador de intentos se reseteará y el mensaje se procesará en el próximo envío de Pub/Sub."
                );

                if (!continuar)
                {
                    return;
                }

                EstaOcupado = true;

                var exito = await _servicio.CambiarEstadoAsync(PoisonPillSeleccionado.MessageId, "Reprocess");

                if (exito)
                {
                    _dialogService.ShowNotification($"Mensaje {PoisonPillSeleccionado.DisplayId} marcado para reprocesar");
                    await OnCargarPoisonPillsAsync(); // Recargar la lista
                }
                else
                {
                    _dialogService.ShowError("No se pudo cambiar el estado del mensaje");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Error al reprocesar: {ex.Message}");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private async Task OnMarcarComoResueltoAsync()
        {
            if (PoisonPillSeleccionado == null) return;

            try
            {
                bool continuar = _dialogService.ShowConfirmationAnswer(
                    "Marcar como resuelto",
                    $"¿Está seguro de que desea marcar el mensaje {PoisonPillSeleccionado.DisplayId} como resuelto?\n\n" +
                    "El mensaje ya no se procesará automáticamente."
                );

                if (!continuar)
                {
                    return;
                }

                EstaOcupado = true;

                var exito = await _servicio.CambiarEstadoAsync(PoisonPillSeleccionado.MessageId, "Resolved");

                if (exito)
                {
                    _dialogService.ShowNotification($"Mensaje {PoisonPillSeleccionado.DisplayId} marcado como resuelto");
                    await OnCargarPoisonPillsAsync(); // Recargar la lista
                }
                else
                {
                    _dialogService.ShowError("No se pudo cambiar el estado del mensaje");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Error al marcar como resuelto: {ex.Message}");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private async Task OnMarcarComoFalloPermanenteAsync()
        {
            if (PoisonPillSeleccionado == null) return;

            try
            {
                bool continuar = _dialogService.ShowConfirmationAnswer(
                    "Marcar como fallo permanente",
                    $"¿Está seguro de que desea marcar el mensaje {PoisonPillSeleccionado.DisplayId} como fallo permanente?\n\n" +
                    "El mensaje ya no se procesará nunca más."
                );

                if (!continuar)
                {
                    return;
                }

                EstaOcupado = true;

                var exito = await _servicio.CambiarEstadoAsync(PoisonPillSeleccionado.MessageId, "PermanentFailure");

                if (exito)
                {
                    _dialogService.ShowNotification($"Mensaje {PoisonPillSeleccionado.DisplayId} marcado como fallo permanente");
                    await OnCargarPoisonPillsAsync(); // Recargar la lista
                }
                else
                {
                    _dialogService.ShowError("No se pudo cambiar el estado del mensaje");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Error al marcar como fallo permanente: {ex.Message}");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private void OnVerDetalle()
        {
            if (PoisonPillSeleccionado == null) return;

            var detalle = $"ID de Mensaje: {PoisonPillSeleccionado.MessageId}\n" +
                         $"Tabla: {PoisonPillSeleccionado.Tabla}\n" +
                         $"Entidad: {PoisonPillSeleccionado.EntityId}\n" +
                         $"Origen: {PoisonPillSeleccionado.Source}\n" +
                         $"Intentos: {PoisonPillSeleccionado.AttemptCount}\n" +
                         $"Primer intento: {PoisonPillSeleccionado.FirstAttemptDate}\n" +
                         $"Último intento: {PoisonPillSeleccionado.LastAttemptDate}\n" +
                         $"Estado: {PoisonPillSeleccionado.Status}\n\n" +
                         $"Último error:\n{PoisonPillSeleccionado.LastError}\n\n" +
                         $"Datos del mensaje:\n{PoisonPillSeleccionado.MessageData}";

            _dialogService.ShowNotification(detalle, "Detalle del Poison Pill");
        }

        private bool CanExecuteActionCommand()
        {
            return PoisonPillSeleccionado != null && !EstaOcupado;
        }

        private void RaiseCanExecuteChanged()
        {
            ((DelegateCommand)ReprocesarCommand).RaiseCanExecuteChanged();
            ((DelegateCommand)MarcarComoResueltoCommand).RaiseCanExecuteChanged();
            ((DelegateCommand)MarcarComoFalloPermanenteCommand).RaiseCanExecuteChanged();
            ((DelegateCommand)VerDetalleCommand).RaiseCanExecuteChanged();
        }

        #endregion
    }
}
