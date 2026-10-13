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
                    GuardarCommand.NotifyCanExecuteChanged();
                    AnadirCommand.NotifyCanExecuteChanged();
                }
            }
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
                    GuardarCommand.NotifyCanExecuteChanged();
                    AnadirCommand.NotifyCanExecuteChanged();
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

        public async Task CargarAsync()
        {
            Mensaje = null;
            await Ocupado(async () =>
            {
                PuedeEditar = await _servicio.PuedeEditar().ConfigureAwait(true);
                Poner(await _servicio.LeerCalendario(Empresa).ConfigureAwait(true));
                if (!PuedeEditar)
                {
                    Mensaje = "Solo lectura: el calendario lo pueden cambiar las personas autorizadas a rellenar reposiciones a mano.";
                }
            }).ConfigureAwait(true);
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
        private async Task Ocupado(Func<Task> accion)
        {
            EstaOcupado = true;
            try
            {
                await accion().ConfigureAwait(true);
            }
            catch (CalendarioReposicionesException ex)
            {
                Mensaje = ex.Message;
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido hablar con el servidor: " + ex.Message;
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
