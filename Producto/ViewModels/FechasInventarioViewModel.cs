using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>Una reposición que toca el inventario, con lo que se puede hacer con ella.</summary>
    public class FilaReposicionInventario
    {
        public FilaReposicionInventario(ReposicionAfectadaInventario reposicion)
        {
            Reposicion = reposicion;
        }

        public ReposicionAfectadaInventario Reposicion { get; }
        public string Texto => Reposicion.Texto;
        public string Motivo => Reposicion.Motivo;
        public string Estado => Reposicion.Estado;
        public string Decision => string.IsNullOrWhiteSpace(Reposicion.DecididoPor)
            ? null
            : Reposicion.FechaDecision.HasValue
                ? $"{Reposicion.DecididoPor.Trim()} ({Reposicion.FechaDecision.Value:dd/MM HH:mm})"
                : Reposicion.DecididoPor.Trim();
        public bool EstaPendiente => Reposicion.Estado == ReposicionAfectadaInventario.PENDIENTE;
        /// <summary>Quitar: aún no ha cerrado y no está quitada.</summary>
        public bool SePuedeQuitar => Reposicion.SePuedeQuitar;
        /// <summary>Mantener: está pendiente (una quitada se vuelve a poner en el calendario, no aquí).</summary>
        public bool SePuedeMantener => EstaPendiente;
    }

    /// <summary>Una fecha de inventario de la lista.</summary>
    public class FilaFechaInventario
    {
        public FilaFechaInventario(FechaInventario fecha)
        {
            Fecha = fecha;
            Reposiciones = new ObservableCollection<FilaReposicionInventario>(
                (fecha.Reposiciones ?? new List<ReposicionAfectadaInventario>()).Select(r => new FilaReposicionInventario(r)));
        }

        public FechaInventario Fecha { get; }
        public int Id => Fecha.Id ?? 0;
        public string Almacen => Fecha.Almacen?.Trim();
        public string NombreAlmacen => FechasInventarioViewModel.NombreAlmacen(Almacen);
        public DateTime Dia => Fecha.Fecha.Date;
        public string Comentario => Fecha.Comentario;
        public string Usuario => Fecha.Usuario?.Trim();
        public DateTime DiaAviso => Fecha.DiaAviso.Date;
        public string Aviso => Fecha.AvisoEnviado.HasValue ? $"Enviado el {Fecha.AvisoEnviado.Value:dd/MM}" : $"Sale el {Fecha.DiaAviso:dd/MM}";
        public int Pendientes => Fecha.Pendientes;
        public ObservableCollection<FilaReposicionInventario> Reposiciones { get; }
    }

    /// <summary>
    /// NestoAPI#883 (corte 1): la ventana «Fechas de inventario» (junto al calendario de reposiciones). Se apunta el día del inventario
    /// de cada almacén y, para cada una, se ven las reposiciones que lo tocan y se decide «Quitar» (ese viaje no sale) o «Mantener» (deja
    /// de avisar). Almacén, Dirección e Informática (la API lo vuelve a comprobar). Sin el script en la base de datos, 503 con su mensaje.
    /// </summary>
    public class FechasInventarioViewModel : ObservableObject, IReceptorNavegacion
    {
        public const string MENSAJE_SIN_GRUPO = "Las fechas de inventario son cosa de Almacén, Dirección e Informática.";
        public const string MENSAJE_SIN_SCRIPT = "Las fechas de inventario todavía no están activadas en el servidor.";
        public const string MENSAJE_FALTA_ALMACEN = "Elija el almacén del inventario.";
        public const string MENSAJE_FALTA_DIA = "Elija el día del inventario.";
        public const string MENSAJE_DIA_PASADO = "No se puede poner una fecha de inventario en un día que ya ha pasado.";
        public const int LONGITUD_COMENTARIO = 200;

        private readonly IServicioFechasInventario _servicio;
        private readonly IServicioDialogos _dialogos;

        public FechasInventarioViewModel(IServicioFechasInventario servicio, IConfiguracion configuracion, IServicioDialogos dialogos)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            PuedeUsar = PuedeUsarFechasInventario(configuracion);
            Mensaje = PuedeUsar ? null : MENSAJE_SIN_GRUPO;
            CargarCommand = new AsyncRelayCommand(CargarAsync, () => PuedeUsar && !EstaOcupado);
            GuardarCommand = new AsyncRelayCommand(GuardarAsync, () => PuedeUsar && !EstaOcupado);
            BorrarCommand = new AsyncRelayCommand(BorrarAsync, () => PuedeUsar && !EstaOcupado && Seleccionada != null);
            QuitarCommand = new AsyncRelayCommand<FilaReposicionInventario>(r => DecidirAsync(r, DecisionReposicionInventario.QUITAR),
                r => PuedeUsar && !EstaOcupado && r != null && r.SePuedeQuitar);
            MantenerCommand = new AsyncRelayCommand<FilaReposicionInventario>(r => DecidirAsync(r, DecisionReposicionInventario.MANTENER),
                r => PuedeUsar && !EstaOcupado && r != null && r.SePuedeMantener);
        }

        /// <summary>Almacén, Dirección e Informática (los que escriben y deciden en la API).</summary>
        public static bool PuedeUsarFechasInventario(IConfiguracion configuracion)
            => configuracion != null
               && (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)
                   || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                   || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA));

        public static IReadOnlyList<string> Almacenes { get; } = new List<string>
        {
            Constantes.Almacenes.ALMACEN_ALGETE, Constantes.Almacenes.ALMACEN_REINA, Constantes.Almacenes.ALMACEN_ALCOBENDAS
        };

        public static string NombreAlmacen(string almacen)
        {
            switch (almacen?.Trim())
            {
                case Constantes.Almacenes.ALMACEN_ALGETE: return "Algete";
                case Constantes.Almacenes.ALMACEN_REINA: return "Reina";
                case Constantes.Almacenes.ALMACEN_ALCOBENDAS: return "Alcobendas";
                default: return almacen?.Trim();
            }
        }

        public string Titulo => "Fechas de inventario";
        public string Ayuda => "Apunte el día del inventario de cada almacén. Tres días laborables antes avisa en la campana de las reposiciones " +
            "que lo tocan (las que llegan la víspera o el día y las que salen y cierran la víspera o el día): decida en cada una si se quita o se mantiene.";

        public bool PuedeUsar { get; }

        public IAsyncRelayCommand CargarCommand { get; }
        public IAsyncRelayCommand GuardarCommand { get; }
        public IAsyncRelayCommand BorrarCommand { get; }
        public IAsyncRelayCommand<FilaReposicionInventario> QuitarCommand { get; }
        public IAsyncRelayCommand<FilaReposicionInventario> MantenerCommand { get; }

        /// <summary>Para los tests (hoy).</summary>
        public Func<DateTime> Hoy { get; set; } = () => DateTime.Today;

        private string _nuevoAlmacen = Constantes.Almacenes.ALMACEN_ALGETE;
        public string NuevoAlmacen { get => _nuevoAlmacen; set => SetProperty(ref _nuevoAlmacen, value); }

        private DateTime? _nuevoDia;
        public DateTime? NuevoDia { get => _nuevoDia; set => SetProperty(ref _nuevoDia, value); }

        private string _nuevoComentario;
        public string NuevoComentario { get => _nuevoComentario; set => SetProperty(ref _nuevoComentario, value); }

        private ObservableCollection<FilaFechaInventario> _fechas = new ObservableCollection<FilaFechaInventario>();
        public ObservableCollection<FilaFechaInventario> Fechas { get => _fechas; private set => SetProperty(ref _fechas, value); }

        private FilaFechaInventario _seleccionada;
        public FilaFechaInventario Seleccionada
        {
            get => _seleccionada;
            set
            {
                if (SetProperty(ref _seleccionada, value))
                {
                    OnPropertyChanged(nameof(HaySeleccionada));
                    Notificar();
                }
            }
        }

        public bool HaySeleccionada => Seleccionada != null;

        private string _mensaje;
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    Notificar();
                }
            }
        }

        private void Notificar()
        {
            CargarCommand?.NotifyCanExecuteChanged();
            GuardarCommand?.NotifyCanExecuteChanged();
            BorrarCommand?.NotifyCanExecuteChanged();
            QuitarCommand?.NotifyCanExecuteChanged();
            MantenerCommand?.NotifyCanExecuteChanged();
        }

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            if (PuedeUsar && Fechas.Count == 0 && !EstaOcupado)
            {
                _ = CargarAsync();
            }
        }

        /// <summary>Lo que falta para apuntar la fecha (de usted), o null.</summary>
        public static string MotivoNoSePuedeGuardar(string almacen, DateTime? dia, string comentario, DateTime hoy)
        {
            if (string.IsNullOrWhiteSpace(almacen) || !Almacenes.Contains(almacen.Trim()))
            {
                return MENSAJE_FALTA_ALMACEN;
            }
            if (!dia.HasValue)
            {
                return MENSAJE_FALTA_DIA;
            }
            if (dia.Value.Date < hoy.Date)
            {
                return MENSAJE_DIA_PASADO;
            }
            if (comentario != null && comentario.Trim().Length > LONGITUD_COMENTARIO)
            {
                return $"El comentario no puede pasar de {LONGITUD_COMENTARIO} caracteres.";
            }
            return null;
        }

        public async Task CargarAsync()
        {
            int? id = Seleccionada?.Id;
            List<FechaInventario> leidas = null;
            await Ocupado(async () => leidas = await _servicio.Leer(null)).ConfigureAwait(true);
            if (leidas == null)
            {
                return;
            }
            Fechas = new ObservableCollection<FilaFechaInventario>(leidas.OrderBy(f => f.Fecha).ThenBy(f => f.Almacen).Select(f => new FilaFechaInventario(f)));
            Seleccionada = Fechas.FirstOrDefault(f => f.Id == id) ?? Fechas.FirstOrDefault();
            Mensaje = Fechas.Count == 0 ? "No hay fechas de inventario de hoy en adelante." : null;
        }

        public async Task GuardarAsync()
        {
            string motivo = MotivoNoSePuedeGuardar(NuevoAlmacen, NuevoDia, NuevoComentario, Hoy());
            if (motivo != null)
            {
                Mensaje = motivo;
                return;
            }
            FechaInventario guardada = null;
            await Ocupado(async () => guardada = await _servicio.Guardar(new FechaInventario
            {
                Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                Almacen = NuevoAlmacen.Trim(),
                Fecha = NuevoDia.Value.Date,
                Comentario = string.IsNullOrWhiteSpace(NuevoComentario) ? null : NuevoComentario.Trim()
            })).ConfigureAwait(true);
            if (guardada == null)
            {
                return;
            }
            Poner(guardada);
            NuevoComentario = null;
            Mensaje = $"Apuntado el inventario de {NombreAlmacen(guardada.Almacen)} del {guardada.Fecha:dd/MM/yyyy}." + TextoPendientes(guardada);
        }

        public async Task BorrarAsync()
        {
            FilaFechaInventario fila = Seleccionada;
            if (fila == null)
            {
                return;
            }
            if (_dialogos != null && !_dialogos.ShowConfirmationAnswer("Borrar fecha de inventario",
                    $"¿Borrar el inventario de {fila.NombreAlmacen} del {fila.Dia:dd/MM/yyyy}? Las reposiciones ya quitadas siguen quitadas en el calendario."))
            {
                return;
            }
            bool borrada = false;
            await Ocupado(async () =>
            {
                await _servicio.Borrar(fila.Id);
                borrada = true;
            }).ConfigureAwait(true);
            if (!borrada)
            {
                return;
            }
            Fechas.Remove(fila);
            Seleccionada = Fechas.FirstOrDefault();
            Mensaje = $"Borrado el inventario de {fila.NombreAlmacen} del {fila.Dia:dd/MM/yyyy}.";
        }

        public async Task DecidirAsync(FilaReposicionInventario reposicion, string decision)
        {
            FilaFechaInventario fila = Seleccionada;
            if (fila == null || reposicion == null)
            {
                return;
            }
            FechaInventario nueva = null;
            await Ocupado(async () => nueva = await _servicio.Decidir(fila.Id, new DecisionReposicionInventario
            {
                Origen = reposicion.Reposicion.Origen,
                Destino = reposicion.Reposicion.Destino,
                FechaLlegada = reposicion.Reposicion.FechaLlegada,
                Decision = decision
            })).ConfigureAwait(true);
            if (nueva == null)
            {
                return;
            }
            Poner(nueva);
            Mensaje = (decision == DecisionReposicionInventario.QUITAR
                ? $"Quitada: {reposicion.Texto}."
                : $"Se mantiene: {reposicion.Texto}.") + TextoPendientes(nueva);
        }

        private static string TextoPendientes(FechaInventario fecha)
            => fecha.Pendientes == 0
                ? " No queda ninguna reposición por decidir."
                : fecha.Pendientes == 1 ? " Queda 1 reposición por decidir." : $" Quedan {fecha.Pendientes} reposiciones por decidir.";

        /// <summary>Pone (o sustituye) en la lista la fecha que devuelve la API y la deja elegida.</summary>
        private void Poner(FechaInventario fecha)
        {
            FilaFechaInventario nueva = new FilaFechaInventario(fecha);
            FilaFechaInventario anterior = Fechas.FirstOrDefault(f => f.Id == nueva.Id && nueva.Id != 0)
                ?? Fechas.FirstOrDefault(f => f.Almacen == nueva.Almacen && f.Dia == nueva.Dia);
            List<FilaFechaInventario> filas = Fechas.Where(f => f != anterior).Append(nueva).OrderBy(f => f.Dia).ThenBy(f => f.Almacen).ToList();
            Fechas = new ObservableCollection<FilaFechaInventario>(filas);
            Seleccionada = nueva;
        }

        private async Task Ocupado(Func<Task> accion)
        {
            EstaOcupado = true;
            try
            {
                await accion().ConfigureAwait(true);
            }
            catch (FechasInventarioApiException ex) when (ex.FaltaElScript)
            {
                Mensaje = $"{MENSAJE_SIN_SCRIPT} {ex.Message}";
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
    }
}
