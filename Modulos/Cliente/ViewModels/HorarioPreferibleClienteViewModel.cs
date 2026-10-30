using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>Nesto#584: un día del editor del horario preferible (mañana y tarde, «HH:mm»; vacío = esa franja no).</summary>
    public class DiaHorarioPreferibleFila : ObservableObject
    {
        public static readonly string[] NOMBRES = { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };

        public DiaHorarioPreferibleFila(int diaSemana)
        {
            DiaSemana = diaSemana;
        }

        /// <summary>1 = lunes … 7 = domingo.</summary>
        public int DiaSemana { get; }
        public string Nombre => NOMBRES[DiaSemana - 1];

        private string _mananaDesde;
        public string MananaDesde { get => _mananaDesde; set => SetProperty(ref _mananaDesde, value); }
        private string _mananaHasta;
        public string MananaHasta { get => _mananaHasta; set => SetProperty(ref _mananaHasta, value); }
        private string _tardeDesde;
        public string TardeDesde { get => _tardeDesde; set => SetProperty(ref _tardeDesde, value); }
        private string _tardeHasta;
        public string TardeHasta { get => _tardeHasta; set => SetProperty(ref _tardeHasta, value); }

        public void Vaciar()
        {
            MananaDesde = MananaHasta = TardeDesde = TardeHasta = null;
        }

        public void CopiarDe(DiaHorarioPreferibleFila otro)
        {
            MananaDesde = otro.MananaDesde;
            MananaHasta = otro.MananaHasta;
            TardeDesde = otro.TardeDesde;
            TardeHasta = otro.TardeHasta;
        }
    }

    /// <summary>
    /// Nesto#584 (NestoAPI#763): pestaña «Horario preferible» de la ficha del cliente. Por cliente y contacto, cada día con una o
    /// dos franjas (mañana y tarde). Deja claro que SOLO lo respeta nuestra ruta propia: las agencias externas no tienen
    /// obligación. Si hay una propuesta sacada de los comentarios por revisar, la enseña y se puede usar. Las reglas (franjas que
    /// se solapan, horas al revés…) las comprueba la API.
    /// </summary>
    public class HorarioPreferibleClienteViewModel : ObservableObject
    {
        public const string AVISO_POR_DEFECTO = "Horario preferible: solo lo respeta nuestra ruta propia. Las agencias externas no tienen obligación de respetarlo.";

        private readonly IHorarioPreferibleService _servicio;
        private readonly IServicioDialogos _dialogos;
        private int _version;

        public HorarioPreferibleClienteViewModel(IHorarioPreferibleService servicio, IServicioDialogos dialogos)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            Dias = new ObservableCollection<DiaHorarioPreferibleFila>(Enumerable.Range(1, 7).Select(d => new DiaHorarioPreferibleFila(d)));
            GuardarCommand = new AsyncRelayCommand(GuardarAsync, () => HayCliente && !EstaOcupado);
            QuitarCommand = new AsyncRelayCommand(QuitarAsync, () => HayCliente && !EstaOcupado && TieneHorario);
            RecargarCommand = new AsyncRelayCommand(RecargarAsync, () => HayCliente);
            CopiarLunesCommand = new RelayCommand(CopiarLunes);
            UsarPropuestaCommand = new RelayCommand(UsarPropuesta, () => HayPropuesta);
        }

        public AsyncRelayCommand GuardarCommand { get; }
        public AsyncRelayCommand QuitarCommand { get; }
        public AsyncRelayCommand RecargarCommand { get; }
        public RelayCommand CopiarLunesCommand { get; }
        public RelayCommand UsarPropuestaCommand { get; }

        public ObservableCollection<DiaHorarioPreferibleFila> Dias { get; }

        public string Empresa { get; private set; }
        public string Cliente { get; private set; }
        public string Contacto { get; private set; }
        public bool HayCliente => !string.IsNullOrWhiteSpace(Cliente);

        private string _aviso = AVISO_POR_DEFECTO;
        /// <summary>Siempre a la vista: solo lo respeta nuestra ruta propia.</summary>
        public string Aviso { get => _aviso; private set => SetProperty(ref _aviso, string.IsNullOrWhiteSpace(value) ? AVISO_POR_DEFECTO : value); }

        private string _textoGuardado;
        /// <summary>«L-V 10:00-14:00, 17:00-20:00» tal como está guardado, o vacío.</summary>
        public string TextoGuardado
        {
            get => _textoGuardado;
            private set
            {
                if (SetProperty(ref _textoGuardado, value))
                {
                    OnPropertyChanged(nameof(TieneHorario));
                    OnPropertyChanged(nameof(Resumen));
                    QuitarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool TieneHorario => !string.IsNullOrWhiteSpace(TextoGuardado);

        private string _ultimaModificacion;
        public string Resumen => TieneHorario
            ? $"Guardado: {TextoGuardado}{_ultimaModificacion}"
            : "Este contacto no tiene horario preferible.";

        private PropuestaHorarioPreferibleModel _propuesta;
        public PropuestaHorarioPreferibleModel Propuesta
        {
            get => _propuesta;
            private set
            {
                if (SetProperty(ref _propuesta, value))
                {
                    OnPropertyChanged(nameof(HayPropuesta));
                    OnPropertyChanged(nameof(TextoPropuesta));
                    UsarPropuestaCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HayPropuesta => Propuesta != null;

        public string TextoPropuesta => Propuesta == null
            ? null
            : string.IsNullOrWhiteSpace(Propuesta.HorarioPropuesto)
                ? $"Hay un horario por revisar sacado de los comentarios, pero no se ha podido leer: «{Recortar(Propuesta.TextoOrigen)}». Escríbalo usted y guarde."
                : $"Propuesta sacada de los comentarios: {Propuesta.HorarioPropuesto}. Revísela, pulse «Usar la propuesta» y guarde.";

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    GuardarCommand.NotifyCanExecuteChanged();
                    QuitarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _mensaje;
        public string Mensaje
        {
            get => _mensaje;
            private set
            {
                if (SetProperty(ref _mensaje, value))
                {
                    OnPropertyChanged(nameof(HayMensaje));
                }
            }
        }

        public bool HayMensaje => !string.IsNullOrWhiteSpace(Mensaje);

        /// <summary>Cambia el cliente de la ficha y lee su horario (la respuesta de un cliente anterior se descarta).</summary>
        public async Task CargarAsync(string empresa, string cliente, string contacto)
        {
            Empresa = string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa.Trim();
            Cliente = cliente?.Trim();
            Contacto = string.IsNullOrWhiteSpace(contacto) ? "0" : contacto.Trim();
            OnPropertyChanged(nameof(HayCliente));
            GuardarCommand.NotifyCanExecuteChanged();
            RecargarCommand.NotifyCanExecuteChanged();
            await RecargarAsync();
        }

        public async Task RecargarAsync()
        {
            int version = ++_version;
            Mensaje = null;
            if (!HayCliente || _servicio == null)
            {
                Aplicar(null);
                return;
            }
            EstaOcupado = true;
            try
            {
                HorarioPreferibleModel horario = await _servicio.Leer(Empresa, Cliente, Contacto);
                if (version == _version)
                {
                    Aplicar(horario);
                }
            }
            catch (Exception ex)
            {
                if (version == _version)
                {
                    Aplicar(null);
                    Mensaje = ex.Message;
                }
            }
            finally
            {
                if (version == _version)
                {
                    EstaOcupado = false;
                }
            }
        }

        private void Aplicar(HorarioPreferibleModel horario)
        {
            PonerFranjas(horario?.Franjas);
            Aviso = horario?.Aviso;
            _ultimaModificacion = horario?.FechaModificacion == null
                ? string.Empty
                : $" ({horario.Usuario?.Trim()}, {horario.FechaModificacion.Value.ToString("g", CultureInfo.GetCultureInfo("es-ES"))})";
            TextoGuardado = horario?.Texto;
            OnPropertyChanged(nameof(Resumen));
            Propuesta = horario?.PropuestaPendiente;
        }

        /// <summary>Rellena el editor con las franjas (la primera de cada día, mañana; la segunda, tarde).</summary>
        internal void PonerFranjas(IEnumerable<FranjaHorarioPreferibleModel> franjas)
        {
            foreach (DiaHorarioPreferibleFila dia in Dias)
            {
                dia.Vaciar();
            }
            foreach (IGrouping<int, FranjaHorarioPreferibleModel> grupo in (franjas ?? Enumerable.Empty<FranjaHorarioPreferibleModel>())
                .Where(f => f != null && f.DiaSemana >= 1 && f.DiaSemana <= 7)
                .GroupBy(f => f.DiaSemana))
            {
                DiaHorarioPreferibleFila dia = Dias[grupo.Key - 1];
                List<FranjaHorarioPreferibleModel> delDia = grupo.OrderBy(f => f.Desde, StringComparer.Ordinal).ToList();
                dia.MananaDesde = delDia[0].Desde;
                dia.MananaHasta = delDia[0].Hasta;
                if (delDia.Count > 1)
                {
                    dia.TardeDesde = delDia[1].Desde;
                    dia.TardeHasta = delDia[1].Hasta;
                }
            }
        }

        /// <summary>
        /// Lo escrito en el editor → franjas. Null y <paramref name="error"/> si una franja tiene solo una de las dos horas o una
        /// hora no se entiende. Las horas se aceptan como «10», «10:30», «10.30» o «10,30».
        /// </summary>
        internal List<FranjaHorarioPreferibleModel> LeerFranjas(out string error)
        {
            error = null;
            var franjas = new List<FranjaHorarioPreferibleModel>();
            foreach (DiaHorarioPreferibleFila dia in Dias)
            {
                foreach ((string desde, string hasta, string parte) in new[] { (dia.MananaDesde, dia.MananaHasta, "por la mañana"), (dia.TardeDesde, dia.TardeHasta, "por la tarde") })
                {
                    bool hayDesde = !string.IsNullOrWhiteSpace(desde);
                    bool hayHasta = !string.IsNullOrWhiteSpace(hasta);
                    if (!hayDesde && !hayHasta)
                    {
                        continue;
                    }
                    if (hayDesde != hayHasta)
                    {
                        error = $"Al {dia.Nombre.ToLowerInvariant()} {parte} le falta la hora de {(hayDesde ? "fin" : "inicio")}.";
                        return null;
                    }
                    string d = NormalizarHora(desde);
                    string h = NormalizarHora(hasta);
                    if (d == null || h == null)
                    {
                        error = $"No se entiende la hora del {dia.Nombre.ToLowerInvariant()} {parte} («{(d == null ? desde : hasta).Trim()}»): escríbala como 10:00.";
                        return null;
                    }
                    franjas.Add(new FranjaHorarioPreferibleModel { DiaSemana = dia.DiaSemana, Desde = d, Hasta = h });
                }
            }
            return franjas;
        }

        /// <summary>«9», «9:30», «9.30», «9,30», «9h» → «09:30»; null si no es una hora de 00:00 a 23:59.</summary>
        internal static string NormalizarHora(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return null;
            }
            string limpio = texto.Trim().ToLowerInvariant().TrimEnd('h').Trim().Replace('.', ':').Replace(',', ':');
            string[] partes = limpio.Split(':');
            if (partes.Length > 2 || !int.TryParse(partes[0], NumberStyles.None, CultureInfo.InvariantCulture, out int horas))
            {
                return null;
            }
            int minutos = 0;
            if (partes.Length == 2 && (partes[1].Length != 2 || !int.TryParse(partes[1], NumberStyles.None, CultureInfo.InvariantCulture, out minutos)))
            {
                return null;
            }
            return horas <= 23 && minutos <= 59 ? $"{horas:00}:{minutos:00}" : null;
        }

        public async Task GuardarAsync()
        {
            if (!HayCliente || _servicio == null)
            {
                return;
            }
            List<FranjaHorarioPreferibleModel> franjas = LeerFranjas(out string error);
            if (franjas == null)
            {
                _dialogos?.ShowError(error);
                return;
            }
            if (franjas.Count == 0 && TieneHorario
                && _dialogos?.ShowConfirmationAnswer("Horario preferible", "No ha dejado ninguna franja: ¿quiere quitar el horario preferible de este contacto?") != true)
            {
                return;
            }
            await GuardarFranjasAsync(franjas, "Horario preferible guardado. Recuerde: solo lo respeta nuestra ruta propia.");
        }

        public async Task QuitarAsync()
        {
            if (!HayCliente || _servicio == null || !TieneHorario)
            {
                return;
            }
            if (_dialogos?.ShowConfirmationAnswer("Horario preferible", "¿Quiere quitar el horario preferible de este contacto?") != true)
            {
                return;
            }
            await GuardarFranjasAsync(new List<FranjaHorarioPreferibleModel>(), "Horario preferible quitado.");
        }

        private async Task GuardarFranjasAsync(List<FranjaHorarioPreferibleModel> franjas, string aviso)
        {
            int version = ++_version;
            EstaOcupado = true;
            try
            {
                HorarioPreferibleModel guardado = await _servicio.Guardar(Empresa, Cliente, Contacto, franjas);
                if (version == _version)
                {
                    Aplicar(guardado);
                    Mensaje = aviso;
                }
            }
            catch (Exception ex)
            {
                _dialogos?.ShowError($"No se ha podido guardar el horario preferible: {ex.Message}");
            }
            finally
            {
                if (version == _version)
                {
                    EstaOcupado = false;
                }
            }
        }

        /// <summary>Copia el lunes a martes, miércoles, jueves y viernes (lo más habitual: el mismo horario de lunes a viernes).</summary>
        public void CopiarLunes()
        {
            for (int d = 1; d <= 4; d++)
            {
                Dias[d].CopiarDe(Dias[0]);
            }
        }

        /// <summary>Pone en el editor lo que propone la propuesta pendiente (hay que guardar para que valga).</summary>
        public void UsarPropuesta()
        {
            if (Propuesta?.Franjas == null || Propuesta.Franjas.Count == 0)
            {
                _dialogos?.ShowNotification("Horario preferible", "La propuesta no trae franjas: escriba usted el horario a partir de los comentarios.");
                return;
            }
            PonerFranjas(Propuesta.Franjas);
            Mensaje = "Propuesta puesta en el editor: revísela y pulse «Guardar».";
        }

        private static string Recortar(string texto)
        {
            string limpio = (texto ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            return limpio.Length <= 160 ? limpio : limpio.Substring(0, 160) + "…";
        }
    }
}
