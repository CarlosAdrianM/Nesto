using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#545 (NestoAPI#692, Carlos 22/10/26): «Anunciar otra vez». Enseña cuándo se anunció la última vez y pide el
    /// reclamo del nuevo aviso: uno de los que propone la API para ese evento (GET api/Eventos/{id}/Reclamos: «¡Últimas
    /// plazas disponibles!», «¡Solo faltan dos días!»…) o uno escrito a mano, de hasta 60 caracteres. Obligatorio. Al
    /// aceptar devuelve «reclamo»; el anuncio lo manda quien lo abre.
    /// </summary>
    public class AnunciarEventoDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "AnunciarEventoDialog";
        public const string PARAMETRO_EVENTO_ID = "eventoId";
        public const string PARAMETRO_EVENTO = "evento";
        public const string PARAMETRO_ULTIMO_ANUNCIO = "ultimoAnuncio";
        public const string PARAMETRO_RECLAMO = "reclamo";

        private readonly IEventosService _servicio;

        public AnunciarEventoDialogViewModel(IEventosService servicio)
        {
            _servicio = servicio;
            Title = "Anunciar otra vez";
            AnunciarCommand = new RelayCommand(Anunciar, () => ValidarReclamo(Reclamo) == null);
            CancelarCommand = new RelayCommand(() => RaiseRequestClose(ResultadoBoton.Cancel));
        }

        /// <summary>Null si el reclamo vale; si no, por qué no.</summary>
        public static string ValidarReclamo(string reclamo)
        {
            string limpio = reclamo?.Trim();
            if (string.IsNullOrEmpty(limpio))
            {
                return "Para volver a anunciar el evento hace falta un reclamo: elija uno de la lista o escríbalo.";
            }
            if (limpio.Length > AnunciarEventoModel.LONGITUD_MAXIMA_RECLAMO)
            {
                return $"El reclamo no puede pasar de {AnunciarEventoModel.LONGITUD_MAXIMA_RECLAMO} caracteres (tiene {limpio.Length}).";
            }
            return null;
        }

        public int EventoId { get; private set; }

        private string _evento;
        public string Evento { get => _evento; private set => SetProperty(ref _evento, value); }

        private string _ultimoAnuncio;
        /// <summary>«Anunciado el 22/10/2026 a las 10:15 por carlos».</summary>
        public string UltimoAnuncio { get => _ultimoAnuncio; private set => SetProperty(ref _ultimoAnuncio, value); }

        public ObservableCollection<string> Reclamos { get; } = new();

        private string _reclamo;
        public string Reclamo
        {
            get => _reclamo;
            set
            {
                if (SetProperty(ref _reclamo, value))
                {
                    OnPropertyChanged(nameof(TextoCaracteres));
                    OnPropertyChanged(nameof(Aviso));
                    AnunciarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public int LongitudMaxima => AnunciarEventoModel.LONGITUD_MAXIMA_RECLAMO;

        /// <summary>«23 / 60».</summary>
        public string TextoCaracteres => $"{Reclamo?.Trim().Length ?? 0} / {AnunciarEventoModel.LONGITUD_MAXIMA_RECLAMO}";

        /// <summary>Por qué no se puede anunciar todavía (solo si se ha escrito algo); vacío si vale.</summary>
        public string Aviso => string.IsNullOrEmpty(Reclamo) ? string.Empty : ValidarReclamo(Reclamo) ?? string.Empty;

        private string _error;
        /// <summary>Si no se pudieron cargar los reclamos (se puede escribir uno igualmente).</summary>
        public string Error { get => _error; private set => SetProperty(ref _error, value); }

        public RelayCommand AnunciarCommand { get; }
        public RelayCommand CancelarCommand { get; }

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            EventoId = parameters.ContainsKey(PARAMETRO_EVENTO_ID) ? parameters.GetValue<int>(PARAMETRO_EVENTO_ID) : 0;
            Evento = parameters.ContainsKey(PARAMETRO_EVENTO) ? parameters.GetValue<string>(PARAMETRO_EVENTO) : null;
            UltimoAnuncio = parameters.ContainsKey(PARAMETRO_ULTIMO_ANUNCIO) ? parameters.GetValue<string>(PARAMETRO_ULTIMO_ANUNCIO) : null;
            if (!string.IsNullOrWhiteSpace(Evento))
            {
                Title = $"Anunciar otra vez «{Evento.Trim()}»";
            }
            _ = CargarReclamosAsync();
        }

        public async Task CargarReclamosAsync()
        {
            if (EventoId <= 0)
            {
                return;
            }
            try
            {
                List<string> reclamos = await _servicio.LeerReclamos(EventoId) ?? new List<string>();
                Reclamos.Clear();
                foreach (string reclamo in reclamos.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct())
                {
                    Reclamos.Add(reclamo);
                }
                if (string.IsNullOrEmpty(Reclamo) && Reclamos.Any())
                {
                    Reclamo = Reclamos.First();
                }
            }
            catch (Exception ex)
            {
                Error = $"{ex.Message}. Puede escribir el reclamo a mano.";
            }
        }

        private void Anunciar()
        {
            if (ValidarReclamo(Reclamo) != null)
            {
                return;
            }
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { PARAMETRO_RECLAMO, Reclamo.Trim() } }));
        }
    }
}
