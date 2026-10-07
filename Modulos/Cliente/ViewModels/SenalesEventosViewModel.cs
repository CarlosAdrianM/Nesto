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
    /// <summary>Un estado del filtro de «Señales de eventos»: lo que se ve y lo que se manda a la API (null = todas).</summary>
    public class FiltroEstadoSenal
    {
        public FiltroEstadoSenal(string texto, string valor)
        {
            Texto = texto;
            Valor = valor;
        }

        public string Texto { get; }
        public string Valor { get; }

        public override string ToString() => Texto;
    }

    /// <summary>
    /// NestoAPI#591: lista de las señales de los eventos (Pendiente / Liberada / Sin compra / Consumida) para Administración,
    /// con filtro por estado y por evento. Doble clic abre el extracto del cliente.
    /// </summary>
    public class SenalesEventosViewModel : ObservableObject, IReceptorNavegacion
    {
        public const string TODOS_LOS_EVENTOS = "(Todos los eventos)";

        private readonly IEventosService _servicio;
        private readonly IServicioDialogos _dialogService;
        private readonly IServicioNavegacion _navegacion;

        public SenalesEventosViewModel(IEventosService servicio, IServicioDialogos dialogService, IServicioNavegacion navegacion)
        {
            _servicio = servicio;
            _dialogService = dialogService;
            _navegacion = navegacion;
            Titulo = "Señales de eventos";
            Estados = new List<FiltroEstadoSenal>
            {
                new("Todas", null),
                new("Pendiente", SenalEventoModel.PENDIENTE),
                new("Liberada", SenalEventoModel.LIBERADA),
                new("Sin compra", SenalEventoModel.SIN_COMPRA),
                new("Consumida", SenalEventoModel.CONSUMIDA)
            };
            _estadoSeleccionado = Estados[0];
            CargarCommand = new AsyncRelayCommand(CargarAsync);
            AbrirExtractoCommand = new RelayCommand<SenalEventoModel>(AbrirExtracto, s => !string.IsNullOrWhiteSpace(s?.Cliente));
        }

        public string Titulo { get; }

        public static bool PuedeVer(IConfiguracion configuracion) =>
            configuracion != null && (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA));

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            if (!_cargadoAlgunaVez)
            {
                _ = CargarAsync();
            }
        }

        private bool _cargadoAlgunaVez;

        public List<FiltroEstadoSenal> Estados { get; }

        private FiltroEstadoSenal _estadoSeleccionado;
        public FiltroEstadoSenal EstadoSeleccionado
        {
            get => _estadoSeleccionado;
            set
            {
                if (SetProperty(ref _estadoSeleccionado, value) && _cargadoAlgunaVez)
                {
                    _ = CargarSenalesAsync();
                }
            }
        }

        private ObservableCollection<EventoModel> _eventos = new();
        /// <summary>El primero es «(Todos los eventos)» (Id 0).</summary>
        public ObservableCollection<EventoModel> Eventos
        {
            get => _eventos;
            private set => SetProperty(ref _eventos, value);
        }

        private EventoModel _eventoSeleccionado;
        public EventoModel EventoSeleccionado
        {
            get => _eventoSeleccionado;
            set
            {
                if (SetProperty(ref _eventoSeleccionado, value) && _cargadoAlgunaVez)
                {
                    _ = CargarSenalesAsync();
                }
            }
        }

        private ObservableCollection<SenalEventoModel> _senales = new();
        public ObservableCollection<SenalEventoModel> Senales
        {
            get => _senales;
            private set
            {
                if (SetProperty(ref _senales, value))
                {
                    OnPropertyChanged(nameof(TotalPendiente));
                }
            }
        }

        public decimal TotalPendiente => Senales?.Sum(s => s.ImportePendiente) ?? 0;

        private SenalEventoModel _seleccionada;
        public SenalEventoModel Seleccionada { get => _seleccionada; set => SetProperty(ref _seleccionada, value); }

        private bool _estaOcupado;
        public bool EstaOcupado { get => _estaOcupado; set => SetProperty(ref _estaOcupado, value); }

        public AsyncRelayCommand CargarCommand { get; }

        /// <summary>Carga los eventos del filtro y las señales.</summary>
        public async Task CargarAsync()
        {
            try
            {
                EstaOcupado = true;
                int? eventoAnterior = EventoSeleccionado?.Id;
                List<EventoModel> eventos = await _servicio.LeerEventos(false);
                var todos = new EventoModel { Id = 0, Titulo = TODOS_LOS_EVENTOS };
                Eventos = new ObservableCollection<EventoModel>(new[] { todos }.Concat(eventos));
                _eventoSeleccionado = Eventos.FirstOrDefault(e => e.Id == eventoAnterior) ?? todos;
                OnPropertyChanged(nameof(EventoSeleccionado));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
            _cargadoAlgunaVez = true;
            await CargarSenalesAsync();
        }

        public async Task CargarSenalesAsync()
        {
            try
            {
                EstaOcupado = true;
                int? eventoId = EventoSeleccionado == null || EventoSeleccionado.Id == 0 ? null : EventoSeleccionado.Id;
                List<SenalEventoModel> lista = await _servicio.LeerSenales(EstadoSeleccionado?.Valor, eventoId);
                Senales = new ObservableCollection<SenalEventoModel>(lista);
            }
            catch (Exception ex)
            {
                Senales = new ObservableCollection<SenalEventoModel>();
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public RelayCommand<SenalEventoModel> AbrirExtractoCommand { get; }

        private void AbrirExtracto(SenalEventoModel senal)
        {
            if (string.IsNullOrWhiteSpace(senal?.Cliente) || _navegacion == null)
            {
                return;
            }
            ParametrosNavegacion parametros = new();
            parametros.Add("cliente", senal.Cliente.Trim());
            _navegacion.RequestNavigate("MainRegion", "ExtractoClienteView", parametros);
        }
    }
}
