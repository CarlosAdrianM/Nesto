using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>Nesto#584: una propuesta de la lista «Horarios por revisar», con el horario que se va a guardar (editable).</summary>
    public class PropuestaHorarioFila : ObservableObject
    {
        public PropuestaHorarioFila(PropuestaHorarioPreferibleModel propuesta)
        {
            Propuesta = propuesta ?? throw new ArgumentNullException(nameof(propuesta));
            _horarioAGuardar = propuesta.HorarioPropuesto;
        }

        public PropuestaHorarioPreferibleModel Propuesta { get; }
        public int Id => Propuesta.Id;
        public string Cliente => Propuesta.Cliente?.Trim();
        public string Contacto => Propuesta.Contacto?.Trim();
        public string Nombre => Propuesta.Nombre?.Trim();
        public string Poblacion => Propuesta.Poblacion?.Trim();
        public string Ruta => Propuesta.Ruta?.Trim();
        public string TextoOrigen => (Propuesta.TextoOrigen ?? string.Empty).Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ").Trim();
        public bool TieneHorarioPropuesto => !string.IsNullOrWhiteSpace(Propuesta.HorarioPropuesto);

        private string _horarioAGuardar;
        /// <summary>«L-V 10:00-14:00, 17:00-20:00; S 10:00-13:00»: lo propuesto, o lo que escriba la persona.</summary>
        public string HorarioAGuardar { get => _horarioAGuardar; set => SetProperty(ref _horarioAGuardar, value); }
    }

    /// <summary>
    /// Nesto#584 (NestoAPI#763, corte 3): los horarios preferibles sacados de los comentarios de las fichas que esperan revisión.
    /// Hasta que una persona acepta uno, no cuenta para nada. Se puede corregir el horario antes de aceptarlo (formato
    /// «L-V 10:00-14:00, 17:00-20:00; S 10:00-13:00») o descartarlo si los comentarios no dicen un horario que valga.
    /// </summary>
    public class HorariosPorRevisarViewModel : ObservableObject
    {
        private readonly IHorarioPreferibleService _servicio;
        private readonly IServicioDialogos _dialogos;

        public HorariosPorRevisarViewModel(IHorarioPreferibleService servicio, IServicioDialogos dialogos)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            Titulo = "Horarios por revisar";
            CargarCommand = new AsyncRelayCommand(CargarAsync);
            AceptarCommand = new AsyncRelayCommand(AceptarAsync, () => Seleccionada != null && !EstaOcupado);
            DescartarCommand = new AsyncRelayCommand(DescartarAsync, () => Seleccionada != null && !EstaOcupado);
        }

        public string Titulo { get; }
        public string Aviso => HorarioPreferibleClienteViewModel.AVISO_POR_DEFECTO;

        public AsyncRelayCommand CargarCommand { get; }
        public AsyncRelayCommand AceptarCommand { get; }
        public AsyncRelayCommand DescartarCommand { get; }

        private ObservableCollection<PropuestaHorarioFila> _propuestas = new();
        public ObservableCollection<PropuestaHorarioFila> Propuestas
        {
            get => _propuestas;
            private set => SetProperty(ref _propuestas, value);
        }

        private PropuestaHorarioFila _seleccionada;
        public PropuestaHorarioFila Seleccionada
        {
            get => _seleccionada;
            set
            {
                if (SetProperty(ref _seleccionada, value))
                {
                    AceptarCommand.NotifyCanExecuteChanged();
                    DescartarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    AceptarCommand.NotifyCanExecuteChanged();
                    DescartarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _mensaje;
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        public async Task CargarAsync()
        {
            EstaOcupado = true;
            try
            {
                List<PropuestaHorarioPreferibleModel> lista = await _servicio.Propuestas();
                Propuestas = new ObservableCollection<PropuestaHorarioFila>((lista ?? new List<PropuestaHorarioPreferibleModel>())
                    .Where(p => p != null)
                    .Select(p => new PropuestaHorarioFila(p)));
                Seleccionada = Propuestas.FirstOrDefault();
                ActualizarMensaje();
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private void ActualizarMensaje()
        {
            int sinPropuesta = Propuestas.Count(p => !p.TieneHorarioPropuesto);
            Mensaje = Propuestas.Count == 0
                ? "No hay horarios por revisar."
                : $"{Propuestas.Count} por revisar" + (sinPropuesta > 0 ? $" ({sinPropuesta} sin propuesta: escriba el horario a partir de los comentarios o descártelos)." : ".");
        }

        public async Task AceptarAsync()
        {
            PropuestaHorarioFila fila = Seleccionada;
            if (fila == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(fila.HorarioAGuardar))
            {
                _dialogos?.ShowError("Escriba el horario a guardar (por ejemplo «L-V 10:00-14:00, 17:00-20:00») o descarte la propuesta.");
                return;
            }
            EstaOcupado = true;
            try
            {
                HorarioPreferibleModel guardado = await _servicio.Aceptar(fila.Id, fila.HorarioAGuardar);
                Quitar(fila);
                Mensaje = $"Guardado para el cliente {fila.Cliente}/{fila.Contacto}: {guardado?.Texto ?? fila.HorarioAGuardar}. " + (Mensaje ?? string.Empty);
            }
            catch (Exception ex)
            {
                _dialogos?.ShowError($"No se ha podido aceptar el horario: {ex.Message}");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public async Task DescartarAsync()
        {
            PropuestaHorarioFila fila = Seleccionada;
            if (fila == null)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                await _servicio.Descartar(fila.Id);
                Quitar(fila);
            }
            catch (Exception ex)
            {
                _dialogos?.ShowError($"No se ha podido descartar la propuesta: {ex.Message}");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private void Quitar(PropuestaHorarioFila fila)
        {
            int indice = Propuestas.IndexOf(fila);
            _ = Propuestas.Remove(fila);
            ActualizarMensaje();
            Seleccionada = Propuestas.Count == 0 ? null : Propuestas[Math.Min(Math.Max(indice, 0), Propuestas.Count - 1)];
        }
    }
}
