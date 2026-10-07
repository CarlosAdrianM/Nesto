using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// NestoAPI#591: mantenimiento de eventos (cursos, masterclass…) con señal reembolsable: título, fecha, importe de la
    /// señal y si está activo. Lo mantiene Tienda online (y Dirección/Informática): el menú ya lo filtra y la API lo
    /// vuelve a comprobar.
    /// </summary>
    public class MantenimientoEventosViewModel : ObservableObject, IReceptorNavegacion
    {
        private readonly IEventosService _servicio;
        private readonly IServicioDialogos _dialogService;

        public MantenimientoEventosViewModel(IEventosService servicio, IConfiguracion configuracion, IServicioDialogos dialogService)
        {
            _servicio = servicio;
            Configuracion = configuracion;
            _dialogService = dialogService;
            Titulo = "Eventos";
            CargarCommand = new AsyncRelayCommand(CargarAsync);
            NuevoCommand = new RelayCommand(OnNuevo);
            GuardarCommand = new AsyncRelayCommand(GuardarAsync, () => Editando);
        }

        public IConfiguracion Configuracion { get; }

        public string Titulo { get; }

        public static bool PuedeMantener(IConfiguracion configuracion) =>
            configuracion != null && (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA));

        public bool TieneAcceso => PuedeMantener(Configuracion);

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            if (!Eventos.Any())
            {
                _ = CargarAsync();
            }
        }

        private ObservableCollection<EventoModel> _eventos = new();
        public ObservableCollection<EventoModel> Eventos
        {
            get => _eventos;
            private set => SetProperty(ref _eventos, value);
        }

        private EventoModel _seleccionado;
        public EventoModel Seleccionado
        {
            get => _seleccionado;
            set
            {
                if (SetProperty(ref _seleccionado, value) && value != null)
                {
                    CargarEdicion(value);
                }
            }
        }

        // Edición: una copia del seleccionado (o un evento nuevo); la fila no se toca hasta guardar con éxito.
        private int _idEdicion;

        private bool _editando;
        public bool Editando
        {
            get => _editando;
            private set
            {
                if (SetProperty(ref _editando, value))
                {
                    GuardarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool EsNuevo => Editando && _idEdicion == 0;

        private string _tituloEdicion;
        public string TituloEdicion { get => _tituloEdicion; set => SetProperty(ref _tituloEdicion, value); }

        private DateTime? _fechaEdicion;
        public DateTime? FechaEdicion { get => _fechaEdicion; set => SetProperty(ref _fechaEdicion, value); }

        private decimal _importeSenalEdicion;
        public decimal ImporteSenalEdicion { get => _importeSenalEdicion; set => SetProperty(ref _importeSenalEdicion, value); }

        private bool _activoEdicion = true;
        public bool ActivoEdicion { get => _activoEdicion; set => SetProperty(ref _activoEdicion, value); }

        private bool _estaOcupado;
        public bool EstaOcupado { get => _estaOcupado; set => SetProperty(ref _estaOcupado, value); }

        public AsyncRelayCommand CargarCommand { get; }

        public async Task CargarAsync()
        {
            try
            {
                EstaOcupado = true;
                int? idSeleccionado = Seleccionado?.Id;
                List<EventoModel> lista = await _servicio.LeerEventos(false);
                Eventos = new ObservableCollection<EventoModel>(lista);
                Seleccionado = Eventos.FirstOrDefault(e => e.Id == idSeleccionado);
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

        public RelayCommand NuevoCommand { get; }

        private void OnNuevo()
        {
            Seleccionado = null;
            _idEdicion = 0;
            TituloEdicion = string.Empty;
            FechaEdicion = DateTime.Today;
            ImporteSenalEdicion = 0;
            ActivoEdicion = true;
            Editando = true;
            OnPropertyChanged(nameof(EsNuevo));
        }

        private void CargarEdicion(EventoModel evento)
        {
            _idEdicion = evento.Id;
            TituloEdicion = evento.Titulo;
            FechaEdicion = evento.Fecha;
            ImporteSenalEdicion = evento.ImporteSenal;
            ActivoEdicion = evento.Activo;
            Editando = true;
            OnPropertyChanged(nameof(EsNuevo));
        }

        public AsyncRelayCommand GuardarCommand { get; }

        public async Task GuardarAsync()
        {
            if (!Editando)
            {
                return;
            }
            if (!TieneAcceso)
            {
                _dialogService.ShowError("Los eventos los mantiene Tienda online.");
                return;
            }
            if (string.IsNullOrWhiteSpace(TituloEdicion))
            {
                _dialogService.ShowError("El evento tiene que tener un título.");
                return;
            }
            if (FechaEdicion == null)
            {
                _dialogService.ShowError("El evento tiene que tener fecha.");
                return;
            }
            if (ImporteSenalEdicion < 0)
            {
                _dialogService.ShowError("El importe de la señal no puede ser negativo.");
                return;
            }
            try
            {
                EstaOcupado = true;
                EventoModel guardado = await _servicio.GuardarEvento(new EventoModel
                {
                    Id = _idEdicion,
                    Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                    Titulo = TituloEdicion.Trim(),
                    Fecha = FechaEdicion.Value.Date,
                    ImporteSenal = ImporteSenalEdicion,
                    Activo = ActivoEdicion
                });
                List<EventoModel> lista = await _servicio.LeerEventos(false);
                Eventos = new ObservableCollection<EventoModel>(lista);
                Seleccionado = Eventos.FirstOrDefault(e => e.Id == guardado?.Id);
                _dialogService.ShowNotification("Evento guardado", $"Guardado el evento «{guardado?.Titulo}» del {guardado?.Fecha:dd/MM/yyyy}.");
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
