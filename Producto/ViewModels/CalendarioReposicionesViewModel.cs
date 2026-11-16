using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>Una opción de un desplegable del calendario (día de la semana, antelación, almacén).</summary>
    public class OpcionCalendario
    {
        public OpcionCalendario(object valor, string texto)
        {
            Valor = valor;
            Texto = texto;
        }

        public object Valor { get; }
        public string Texto { get; }
    }

    /// <summary>Una fila de la tabla del calendario, editable. Las horas van como texto «HH:mm» para teclearlas.</summary>
    public class FilaCalendarioEditable : ObservableObject
    {
        private bool _cargando = true;

        public FilaCalendarioEditable(FilaCalendarioReposicion fila)
        {
            Id = fila.Id;
            Origen = fila.Origen?.Trim().ToUpperInvariant();
            Destino = fila.Destino?.Trim().ToUpperInvariant();
            DiaSemana = fila.DiaSemana;
            HoraCierre = CalendarioReposicionesViewModel.TextoHora(fila.HoraCierre);
            HoraLlegada = CalendarioReposicionesViewModel.TextoHora(fila.HoraLlegadaHabitual);
            Antelacion = fila.LaborablesAntelacionCierre;
            Activo = fila.Activo;
            Usuario = fila.Usuario?.Trim();
            FechaModificacion = fila.FechaModificacion;
            _cargando = false;
            Modificada = !Id.HasValue;
        }

        public int? Id { get; }
        public string Origen { get; }
        public string Destino { get; }
        public string Ruta => $"{Origen} → {Destino}";
        public string Usuario { get; }
        public DateTime? FechaModificacion { get; }
        public bool EsNueva => !Id.HasValue;

        private byte _diaSemana;
        public byte DiaSemana { get => _diaSemana; set => Cambiar(ref _diaSemana, value, nameof(DiaSemana)); }

        private string _horaCierre;
        public string HoraCierre { get => _horaCierre; set => Cambiar(ref _horaCierre, value, nameof(HoraCierre)); }

        private string _horaLlegada;
        public string HoraLlegada { get => _horaLlegada; set => Cambiar(ref _horaLlegada, value, nameof(HoraLlegada)); }

        private byte _antelacion;
        public byte Antelacion { get => _antelacion; set => Cambiar(ref _antelacion, value, nameof(Antelacion)); }

        private bool _activo;
        public bool Activo { get => _activo; set => Cambiar(ref _activo, value, nameof(Activo)); }

        private bool _modificada;
        /// <summary>Cambiada (o nueva) desde que se leyó: es lo que se manda al guardar.</summary>
        public bool Modificada { get => _modificada; private set => SetProperty(ref _modificada, value); }

        private void Cambiar<T>(ref T campo, T valor, string propiedad)
        {
            if (SetProperty(ref campo, valor, propiedad) && !_cargando)
            {
                Modificada = true;
            }
        }

        /// <summary>La fila como la espera la API; null y <paramref name="error"/> si una hora no se entiende.</summary>
        internal FilaCalendarioReposicion AFila(string empresa, out string error)
        {
            error = null;
            if (!CalendarioReposicionesViewModel.LeerHora(HoraCierre, out TimeSpan cierre))
            {
                error = $"La hora de cierre «{HoraCierre}» de {Ruta} ({CalendarioReposicionesViewModel.NombreDia(DiaSemana)}) no es una hora: escríbela como 09:30.";
                return null;
            }
            if (!CalendarioReposicionesViewModel.LeerHora(HoraLlegada, out TimeSpan llegada))
            {
                error = $"La hora de llegada «{HoraLlegada}» de {Ruta} ({CalendarioReposicionesViewModel.NombreDia(DiaSemana)}) no es una hora: escríbela como 13:30.";
                return null;
            }
            return new FilaCalendarioReposicion
            {
                Id = Id,
                Empresa = empresa,
                Origen = Origen,
                Destino = Destino,
                DiaSemana = DiaSemana,
                HoraCierre = cierre,
                HoraLlegadaHabitual = llegada,
                LaborablesAntelacionCierre = Antelacion,
                Activo = Activo
            };
        }
    }

    /// <summary>NestoAPI#867: una excepción por fecha, como se enseña en la lista (solo lectura).</summary>
    public class ExcepcionCalendarioFila
    {
        private static readonly CultureInfo _castellano = new CultureInfo("es-ES");

        public ExcepcionCalendarioFila(ExcepcionCalendarioReposicion excepcion)
        {
            Excepcion = excepcion ?? throw new ArgumentNullException(nameof(excepcion));
        }

        public ExcepcionCalendarioReposicion Excepcion { get; }
        public int? Id => Excepcion.Id;
        public DateTime FechaLlegada => Excepcion.FechaLlegada.Date;
        public string Fecha => FechaLlegada.ToString("dddd dd/MM/yyyy", _castellano);
        public string Ruta => $"{Excepcion.Origen?.Trim().ToUpperInvariant()} → {Excepcion.Destino?.Trim().ToUpperInvariant()}";
        public bool EsDiaExtra => Excepcion.Tipo == TiposExcepcionCalendario.REPOSICION_EXTRA;

        public string TipoTexto => EsDiaExtra
            ? "Día extra" + (Excepcion.HoraCierre.HasValue && Excepcion.HoraLlegadaHabitual.HasValue
                ? $" (cierra a las {CalendarioReposicionesViewModel.TextoHora(Excepcion.HoraCierre.Value)}, llega a las {CalendarioReposicionesViewModel.TextoHora(Excepcion.HoraLlegadaHabitual.Value)})"
                : string.Empty)
            : "Sin reposición";

        public string Motivo => Excepcion.Motivo;
        public string Usuario => Excepcion.Usuario?.Trim();

        /// <summary>«jueves 19/11 16:00»: desde entonces ya no se puede cambiar ni borrar.</summary>
        public string CierraEl => Excepcion.CierraEl?.ToString("dddd dd/MM HH:mm", _castellano);
    }

    /// <summary>
    /// NestoAPI#577 (09/10/26): el calendario de reposiciones de tiendas (qué días llega cada ruta, a qué hora se cierra y
    /// a qué hora llega), que antes se cambiaba con scripts. Lo ve cualquiera; lo cambian quienes la API deja (los mismos
    /// que rellenan reposiciones a mano). Se guardan solo las filas cambiadas o nuevas; las reglas las pone la API y su
    /// mensaje se enseña tal cual. Nada se borra: un día que ya no toca se desactiva.
    /// </summary>
    public class CalendarioReposicionesViewModel : ObservableObject
    {
        public const string AYUDA = "Cambiar la hora de cierre de hoy no repite la reposición de hoy; vale desde la siguiente.";

        private static readonly string[] _nombresDias = { "lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo" };

        private readonly IServicioCalendarioReposiciones _servicio;

        public CalendarioReposicionesViewModel(IServicioCalendarioReposiciones servicio)
        {
            _servicio = servicio;
            CargarCommand = new AsyncRelayCommand(CargarAsync);
            GuardarCommand = new AsyncRelayCommand(GuardarAsync, () => PuedeEditar && HayCambios && !EstaOcupado);
            AnadirCommand = new RelayCommand(Anadir, () => PuedeEditar && !EstaOcupado);
            QuitarDiaCommand = new AsyncRelayCommand(() => GuardarExcepcionAsync(TiposExcepcionCalendario.SIN_REPOSICION), () => PuedeEditar && !EstaOcupado);
            AnadirDiaExtraCommand = new AsyncRelayCommand(() => GuardarExcepcionAsync(TiposExcepcionCalendario.REPOSICION_EXTRA), () => PuedeEditar && !EstaOcupado);
            BorrarExcepcionCommand = new AsyncRelayCommand(BorrarExcepcionAsync, () => PuedeEditar && !EstaOcupado && ExcepcionSeleccionada?.Id != null);
            Filas.CollectionChanged += (_, e) =>
            {
                foreach (FilaCalendarioEditable fila in e.NewItems?.OfType<FilaCalendarioEditable>() ?? Enumerable.Empty<FilaCalendarioEditable>())
                {
                    fila.PropertyChanged += Fila_PropertyChanged;
                }
                foreach (FilaCalendarioEditable fila in e.OldItems?.OfType<FilaCalendarioEditable>() ?? Enumerable.Empty<FilaCalendarioEditable>())
                {
                    fila.PropertyChanged -= Fila_PropertyChanged;
                }
                OnPropertyChanged(nameof(HayCambios));
                GuardarCommand.NotifyCanExecuteChanged();
            };
        }

        public static IReadOnlyList<OpcionCalendario> Dias { get; } =
            Enumerable.Range(1, 7).Select(d => new OpcionCalendario((byte)d, _nombresDias[d - 1])).ToList();

        public static IReadOnlyList<OpcionCalendario> Antelaciones { get; } = new List<OpcionCalendario>
        {
            new OpcionCalendario((byte)0, "el mismo día"),
            new OpcionCalendario((byte)1, "1 laborable antes"),
            new OpcionCalendario((byte)2, "2 laborables antes"),
            new OpcionCalendario((byte)3, "3 laborables antes"),
            new OpcionCalendario((byte)4, "4 laborables antes"),
            new OpcionCalendario((byte)5, "5 laborables antes")
        };

        public static IReadOnlyList<string> Almacenes { get; } = new List<string>
        {
            Constantes.Almacenes.ALMACEN_ALGETE, Constantes.Almacenes.ALMACEN_REINA, Constantes.Almacenes.ALMACEN_ALCOBENDAS
        };

        public string Titulo => "Calendario de reposiciones";
        public string Ayuda => AYUDA;
        public string Empresa { get; set; } = Constantes.Empresas.EMPRESA_DEFECTO;

        public ObservableCollection<FilaCalendarioEditable> Filas { get; } = new ObservableCollection<FilaCalendarioEditable>();

        private bool _puedeEditar;
        public bool PuedeEditar
        {
            get => _puedeEditar;
            private set
            {
                if (SetProperty(ref _puedeEditar, value))
                {
                    OnPropertyChanged(nameof(SoloLectura));
                    NotificarComandos();
                }
            }
        }

        private void NotificarComandos()
        {
            GuardarCommand.NotifyCanExecuteChanged();
            AnadirCommand.NotifyCanExecuteChanged();
            QuitarDiaCommand.NotifyCanExecuteChanged();
            AnadirDiaExtraCommand.NotifyCanExecuteChanged();
            BorrarExcepcionCommand.NotifyCanExecuteChanged();
        }

        public bool SoloLectura => !PuedeEditar;

        public bool HayCambios => Filas.Any(f => f.Modificada);

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    NotificarComandos();
                }
            }
        }

        private string _mensaje;
        public string Mensaje { get => _mensaje; set => SetProperty(ref _mensaje, value); }

        private string _nuevoOrigen = Constantes.Almacenes.ALMACEN_REINA;
        public string NuevoOrigen { get => _nuevoOrigen; set => SetProperty(ref _nuevoOrigen, value); }

        private string _nuevoDestino = Constantes.Almacenes.ALMACEN_ALGETE;
        public string NuevoDestino { get => _nuevoDestino; set => SetProperty(ref _nuevoDestino, value); }

        private byte _nuevoDia = 1;
        public byte NuevoDia { get => _nuevoDia; set => SetProperty(ref _nuevoDia, value); }

        public IAsyncRelayCommand CargarCommand { get; }
        public IAsyncRelayCommand GuardarCommand { get; }
        public IRelayCommand AnadirCommand { get; }

        #region Excepciones por fecha (NestoAPI#867)

        /// <summary>Las excepciones de hoy en adelante (días quitados y días extra).</summary>
        public ObservableCollection<ExcepcionCalendarioFila> Excepciones { get; } = new ObservableCollection<ExcepcionCalendarioFila>();

        private ExcepcionCalendarioFila _excepcionSeleccionada;
        public ExcepcionCalendarioFila ExcepcionSeleccionada
        {
            get => _excepcionSeleccionada;
            set
            {
                if (SetProperty(ref _excepcionSeleccionada, value))
                {
                    BorrarExcepcionCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _excepcionOrigen = Constantes.Almacenes.ALMACEN_ALGETE;
        public string ExcepcionOrigen { get => _excepcionOrigen; set => SetProperty(ref _excepcionOrigen, value); }

        private string _excepcionDestino = Constantes.Almacenes.ALMACEN_REINA;
        public string ExcepcionDestino { get => _excepcionDestino; set => SetProperty(ref _excepcionDestino, value); }

        private DateTime? _excepcionFecha;
        /// <summary>El día de LLEGADA de la reposición que se quita o se añade.</summary>
        public DateTime? ExcepcionFecha { get => _excepcionFecha; set => SetProperty(ref _excepcionFecha, value); }

        private string _excepcionMotivo;
        public string ExcepcionMotivo { get => _excepcionMotivo; set => SetProperty(ref _excepcionMotivo, value); }

        private string _excepcionHoraCierre;
        /// <summary>Solo para un día extra; en blanco, la de la ruta.</summary>
        public string ExcepcionHoraCierre { get => _excepcionHoraCierre; set => SetProperty(ref _excepcionHoraCierre, value); }

        private string _excepcionHoraLlegada;
        /// <summary>Solo para un día extra; en blanco, la de la ruta.</summary>
        public string ExcepcionHoraLlegada { get => _excepcionHoraLlegada; set => SetProperty(ref _excepcionHoraLlegada, value); }

        private string _mensajeExcepciones;
        /// <summary>Lo que pasa con las excepciones (aparte del mensaje del calendario).</summary>
        public string MensajeExcepciones { get => _mensajeExcepciones; set => SetProperty(ref _mensajeExcepciones, value); }

        public IAsyncRelayCommand QuitarDiaCommand { get; }
        public IAsyncRelayCommand AnadirDiaExtraCommand { get; }
        public IAsyncRelayCommand BorrarExcepcionCommand { get; }

        private async Task CargarExcepcionesAsync()
        {
            await Ocupado(async () =>
            {
                PonerExcepciones(await _servicio.LeerExcepciones(Empresa).ConfigureAwait(true));
            }, m => MensajeExcepciones = m).ConfigureAwait(true);
        }

        private void PonerExcepciones(IEnumerable<ExcepcionCalendarioReposicion> excepciones)
        {
            Excepciones.Clear();
            foreach (ExcepcionCalendarioReposicion excepcion in (excepciones ?? Enumerable.Empty<ExcepcionCalendarioReposicion>())
                .Where(e => e != null)
                .OrderBy(e => e.FechaLlegada).ThenBy(e => e.Origen).ThenBy(e => e.Destino))
            {
                Excepciones.Add(new ExcepcionCalendarioFila(excepcion));
            }
            ExcepcionSeleccionada = null;
        }

        /// <summary>
        /// Quitar un día (<see cref="TiposExcepcionCalendario.SIN_REPOSICION"/>) o añadir uno extra: la ruta y el día de
        /// llegada elegidos. Las reglas (día que exista, que no haya cerrado…) las pone la API y su mensaje se enseña tal cual.
        /// </summary>
        private async Task GuardarExcepcionAsync(string tipo)
        {
            MensajeExcepciones = null;
            string origen = ExcepcionOrigen?.Trim().ToUpperInvariant();
            string destino = ExcepcionDestino?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(origen) || string.IsNullOrEmpty(destino) || origen == destino)
            {
                MensajeExcepciones = "Elija un almacén de origen y otro de destino distintos.";
                return;
            }
            if (!ExcepcionFecha.HasValue)
            {
                MensajeExcepciones = "Elija el día de llegada.";
                return;
            }
            if (string.IsNullOrWhiteSpace(ExcepcionMotivo))
            {
                MensajeExcepciones = "Indique el motivo (por ejemplo, «Inventario del sábado»).";
                return;
            }
            bool extra = tipo == TiposExcepcionCalendario.REPOSICION_EXTRA;
            TimeSpan? cierre = null;
            TimeSpan? llegada = null;
            if (extra)
            {
                if (!string.IsNullOrWhiteSpace(ExcepcionHoraCierre))
                {
                    if (!LeerHora(ExcepcionHoraCierre, out TimeSpan hora))
                    {
                        MensajeExcepciones = $"La hora de cierre «{ExcepcionHoraCierre}» no es una hora: escríbala como 09:30 (o déjela en blanco para usar la de la ruta).";
                        return;
                    }
                    cierre = hora;
                }
                if (!string.IsNullOrWhiteSpace(ExcepcionHoraLlegada))
                {
                    if (!LeerHora(ExcepcionHoraLlegada, out TimeSpan hora))
                    {
                        MensajeExcepciones = $"La hora de llegada «{ExcepcionHoraLlegada}» no es una hora: escríbala como 13:30 (o déjela en blanco para usar la de la ruta).";
                        return;
                    }
                    llegada = hora;
                }
            }
            var excepcion = new ExcepcionCalendarioReposicion
            {
                Empresa = Empresa,
                Origen = origen,
                Destino = destino,
                FechaLlegada = ExcepcionFecha.Value.Date,
                Tipo = tipo,
                HoraCierre = cierre,
                HoraLlegadaHabitual = llegada,
                Motivo = ExcepcionMotivo.Trim()
            };
            bool guardada = false;
            await Ocupado(async () =>
            {
                _ = await _servicio.GuardarExcepcion(excepcion).ConfigureAwait(true);
                guardada = true;
            }, m => MensajeExcepciones = m).ConfigureAwait(true);
            if (!guardada)
            {
                return;
            }
            string dia = new ExcepcionCalendarioFila(excepcion).Fecha;
            await CargarExcepcionesAsync().ConfigureAwait(true);
            ExcepcionMotivo = null;
            MensajeExcepciones ??= extra
                ? $"Añadida una reposición extra de {origen} a {destino} que llega el {dia}."
                : $"Quitada la reposición de {origen} a {destino} que llega el {dia}: ese día no se rellenará ni se avisará.";
        }

        private async Task BorrarExcepcionAsync()
        {
            ExcepcionCalendarioFila seleccionada = ExcepcionSeleccionada;
            if (seleccionada?.Id == null)
            {
                return;
            }
            MensajeExcepciones = null;
            bool borrada = false;
            await Ocupado(async () =>
            {
                await _servicio.BorrarExcepcion(seleccionada.Id.Value).ConfigureAwait(true);
                borrada = true;
            }, m => MensajeExcepciones = m).ConfigureAwait(true);
            if (!borrada)
            {
                return;
            }
            await CargarExcepcionesAsync().ConfigureAwait(true);
            MensajeExcepciones ??= $"Borrada la excepción del {seleccionada.Fecha} ({seleccionada.Ruta}): ese día vuelve a ser como dice el calendario.";
        }

        #endregion

        public async Task CargarAsync()
        {
            Mensaje = null;
            MensajeExcepciones = null;
            await Ocupado(async () =>
            {
                PuedeEditar = await _servicio.PuedeEditar().ConfigureAwait(true);
                Poner(await _servicio.LeerCalendario(Empresa).ConfigureAwait(true));
                if (!PuedeEditar)
                {
                    Mensaje = "Solo lectura: el calendario lo pueden cambiar las personas autorizadas a rellenar reposiciones a mano.";
                }
            }).ConfigureAwait(true);
            // Aparte: si las excepciones fallan (API anterior, falta el script), el calendario se ve igual.
            await CargarExcepcionesAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Añade una fila nueva de la ruta y el día elegidos, con las horas y la antelación de esa ruta (o 10:00 y 13:30 si
        /// es nueva). Si la ruta ya tiene ese día, no se añade otra: se dice (para volver a usar uno desactivado, se activa).
        /// </summary>
        private void Anadir()
        {
            string origen = NuevoOrigen?.Trim().ToUpperInvariant();
            string destino = NuevoDestino?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(origen) || string.IsNullOrEmpty(destino) || origen == destino)
            {
                Mensaje = "Elige un almacén de origen y otro de destino distintos.";
                return;
            }
            FilaCalendarioEditable existente = Filas.FirstOrDefault(f => f.Origen == origen && f.Destino == destino && f.DiaSemana == NuevoDia);
            if (existente != null)
            {
                Mensaje = existente.Activo
                    ? $"La reposición de {origen} a {destino} ya llega el {NombreDia(NuevoDia)}: cambia esa fila."
                    : $"La reposición de {origen} a {destino} del {NombreDia(NuevoDia)} está desactivada: márcala como activa en vez de añadir otra.";
                return;
            }
            FilaCalendarioEditable modelo = Filas.FirstOrDefault(f => f.Origen == origen && f.Destino == destino && f.Activo)
                ?? Filas.FirstOrDefault(f => f.Origen == origen && f.Destino == destino);
            var nueva = new FilaCalendarioEditable(new FilaCalendarioReposicion
            {
                Origen = origen,
                Destino = destino,
                DiaSemana = NuevoDia,
                HoraCierre = modelo != null && LeerHora(modelo.HoraCierre, out TimeSpan cierre) ? cierre : new TimeSpan(10, 0, 0),
                HoraLlegadaHabitual = modelo != null && LeerHora(modelo.HoraLlegada, out TimeSpan llegada) ? llegada : new TimeSpan(13, 30, 0),
                LaborablesAntelacionCierre = modelo?.Antelacion ?? 0,
                Activo = true
            });
            Filas.Add(nueva);
            Mensaje = $"Añadida la reposición de {origen} a {destino} del {NombreDia(NuevoDia)}: revisa las horas y pulsa Guardar.";
        }

        private async Task GuardarAsync()
        {
            Mensaje = null;
            var peticion = new GuardarCalendarioReposiciones { Empresa = Empresa };
            foreach (FilaCalendarioEditable fila in Filas.Where(f => f.Modificada))
            {
                FilaCalendarioReposicion dto = fila.AFila(Empresa, out string error);
                if (dto == null)
                {
                    Mensaje = error;
                    return;
                }
                peticion.Filas.Add(dto);
            }
            if (peticion.Filas.Count == 0)
            {
                return;
            }
            bool guardado = false;
            await Ocupado(async () =>
            {
                Poner(await _servicio.Guardar(peticion).ConfigureAwait(true));
                guardado = true;
            }).ConfigureAwait(true);
            if (guardado)
            {
                Mensaje = peticion.Filas.Count == 1 ? "Guardado (1 fila)." : $"Guardado ({peticion.Filas.Count} filas).";
            }
        }

        private void Poner(IEnumerable<FilaCalendarioReposicion> filas)
        {
            Filas.Clear();
            foreach (FilaCalendarioReposicion fila in (filas ?? Enumerable.Empty<FilaCalendarioReposicion>())
                .OrderBy(f => f.Origen).ThenBy(f => f.Destino).ThenBy(f => f.DiaSemana))
            {
                Filas.Add(new FilaCalendarioEditable(fila));
            }
        }

        private void Fila_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilaCalendarioEditable.Modificada))
            {
                OnPropertyChanged(nameof(HayCambios));
                GuardarCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>Lo que falla al hablar con la API se enseña en <see cref="Mensaje"/> tal cual (lo escribe el servidor).</summary>
        private async Task Ocupado(Func<Task> accion, Action<string> alFallar = null)
        {
            alFallar ??= m => Mensaje = m;
            EstaOcupado = true;
            try
            {
                await accion().ConfigureAwait(true);
            }
            catch (CalendarioReposicionesException ex)
            {
                alFallar(ex.Message);
            }
            catch (Exception ex)
            {
                alFallar("No se ha podido hablar con el servidor: " + ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public static string NombreDia(byte dia) => dia >= 1 && dia <= 7 ? _nombresDias[dia - 1] : dia.ToString(CultureInfo.InvariantCulture);

        internal static string TextoHora(TimeSpan hora) => hora.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

        /// <summary>«9:30», «09:30» o «0930»; entre las 00:00 y las 23:59.</summary>
        internal static bool LeerHora(string texto, out TimeSpan hora)
        {
            hora = TimeSpan.Zero;
            string limpio = texto?.Trim().Replace('.', ':');
            if (string.IsNullOrEmpty(limpio))
            {
                return false;
            }
            if (!limpio.Contains(':') && limpio.Length == 4 && limpio.All(char.IsDigit))
            {
                limpio = limpio.Substring(0, 2) + ":" + limpio.Substring(2);
            }
            string[] partes = limpio.Split(':');
            if (partes.Length != 2
                || !int.TryParse(partes[0], NumberStyles.None, CultureInfo.InvariantCulture, out int horas)
                || !int.TryParse(partes[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minutos)
                || horas > 23 || minutos > 59 || partes[1].Length != 2)
            {
                return false;
            }
            hora = new TimeSpan(horas, minutos, 0);
            return true;
        }
    }
}
