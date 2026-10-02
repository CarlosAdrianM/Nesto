using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ControlesUsuario.Notificaciones
{
    /// <summary>
    /// Nesto#477: la campana de la barra con el buzón de notificaciones del usuario (NestoAPI#387).
    /// Para no cargar el servidor (incidente del 23/09) NO se sondea cada poco: el contador se refresca al
    /// arrancar, al abrir o cerrar el panel, al recuperar el foco la ventana principal (como mucho uno cada
    /// <see cref="MINIMO_ENTRE_REFRESCOS_POR_FOCO"/>), cuando <see cref="IAvisosEnTiempoReal"/> avisa de que hay
    /// nuevas y, como red de seguridad, cada <see cref="INTERVALO_SONDEO"/> (con un desfase aleatorio de hasta
    /// <see cref="DESFASE_MAXIMO_SONDEO"/> para que no coincidan todos los puestos). Un fallo al refrescar se
    /// calla (sin avisos ni ELMAH: la campana no debe molestar).
    /// Al pulsar una notificación se marca leída y, si es la respuesta a un comentario de Novedades,
    /// se abre la ventana de Novedades en ese comentario; si es el aviso de versión nueva (Nesto#501), en esa
    /// versión; si es el recordatorio de facturas pendientes de Verifactu (NestoAPI#522), se abre esa ventana.
    /// Nesto#501: junto a la campana, un botón que abre Novedades (antes solo estaba en el menú de la
    /// cinta y los usuarios no sabían volver a abrirla).
    /// </summary>
    public class CampanaNotificacionesViewModel : ObservableObject
    {
        public static readonly TimeSpan INTERVALO_SONDEO = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan DESFASE_MAXIMO_SONDEO = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan MINIMO_ENTRE_REFRESCOS_POR_FOCO = TimeSpan.FromMinutes(5);
        internal const int TAMANO_PANEL = 50;
        internal const string DIALOGO_NOVEDADES = "NovedadesDialog";
        internal const string REGION_PRINCIPAL = "MainRegion";

        private readonly IBuzonNotificacionesService _buzon;
        private readonly IServicioDialogos _dialogService;
        private readonly IAbridorNovedades _abridorNovedades;
        private readonly IServicioNavegacion _navegacion;
        private readonly IAbridorPedidos _abridorPedidos;
        private readonly Func<DateTime> _ahora;
        private readonly Random _azar;
        private readonly Dispatcher _dispatcher;
        private DispatcherTimer _timer;
        private bool _refrescando;
        private DateTime _ultimoRefrescoPorFoco = DateTime.MinValue;

        public CampanaNotificacionesViewModel(IBuzonNotificacionesService buzon, IServicioDialogos dialogService, IAvisosEnTiempoReal avisos,
            IAbridorNovedades abridorNovedades, IServicioNavegacion navegacion, IAbridorPedidos abridorPedidos)
            : this(buzon, dialogService, avisos, abridorNovedades, () => DateTime.Now, new Random(), navegacion, abridorPedidos) { }

        internal CampanaNotificacionesViewModel(IBuzonNotificacionesService buzon, IServicioDialogos dialogService, IAvisosEnTiempoReal avisos,
            IAbridorNovedades abridorNovedades, Func<DateTime> ahora, Random azar, IServicioNavegacion navegacion = null,
            IAbridorPedidos abridorPedidos = null)
        {
            _buzon = buzon ?? throw new ArgumentNullException(nameof(buzon));
            _dialogService = dialogService;
            _abridorNovedades = abridorNovedades;
            _navegacion = navegacion;
            _abridorPedidos = abridorPedidos;
            _ahora = ahora ?? (() => DateTime.Now);
            _azar = azar ?? new Random();
            // Null en los tests (sin Application): entonces el aviso se atiende en el hilo que llega.
            _dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (avisos != null)
            {
                avisos.HayNotificacionesNuevas += OnHayNotificacionesNuevas;
            }

            AbrirNotificacionCommand = new AsyncRelayCommand<NotificacionBuzonItem>(AbrirNotificacion);
            BorrarNotificacionCommand = new AsyncRelayCommand<NotificacionBuzonItem>(BorrarNotificacion);
            MarcarTodasLeidasCommand = new AsyncRelayCommand(MarcarTodasLeidas, () => Notificaciones.Any(n => !n.Leida));
            AbrirNovedadesCommand = new AsyncRelayCommand(() => AbrirNovedades(null), () => _abridorNovedades != null);
        }

        public ObservableCollection<NotificacionBuzonItem> Notificaciones { get; } = new ObservableCollection<NotificacionBuzonItem>();

        public IAsyncRelayCommand<NotificacionBuzonItem> AbrirNotificacionCommand { get; }
        public IAsyncRelayCommand<NotificacionBuzonItem> BorrarNotificacionCommand { get; }
        public IAsyncRelayCommand MarcarTodasLeidasCommand { get; }
        /// <summary>Nesto#501: el botón de Novedades junto a la campana.</summary>
        public IAsyncRelayCommand AbrirNovedadesCommand { get; }

        private int _noLeidas;
        public int NoLeidas
        {
            get => _noLeidas;
            private set
            {
                if (SetProperty(ref _noLeidas, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(HayNoLeidas));
                    OnPropertyChanged(nameof(TextoNoLeidas));
                    OnPropertyChanged(nameof(TextoToolTip));
                }
            }
        }
        public bool HayNoLeidas => NoLeidas > 0;
        /// <summary>Lo que pinta el globo rojo (con más de 99 no cabe).</summary>
        public string TextoNoLeidas => NoLeidas > 99 ? "99+" : NoLeidas.ToString();
        public string TextoToolTip => NoLeidas == 0
            ? "Notificaciones"
            : NoLeidas == 1 ? "Tienes 1 notificación sin leer" : $"Tienes {NoLeidas} notificaciones sin leer";

        private bool _panelAbierto;
        /// <summary>Al abrir el panel se carga la lista; al abrirlo y al cerrarlo se refresca el contador.</summary>
        public bool PanelAbierto
        {
            get => _panelAbierto;
            set
            {
                if (SetProperty(ref _panelAbierto, value))
                {
                    _ = value ? CargarPanel() : RefrescarContador();
                }
            }
        }

        private bool _cargando;
        public bool Cargando { get => _cargando; private set => SetProperty(ref _cargando, value); }

        private string _mensaje;
        /// <summary>Aviso del panel (error al cargar o al marcar, bandeja vacía). Solo se ve con el panel abierto.</summary>
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

        /// <summary>
        /// Primer refresco y sondeo de seguridad (cada <see cref="INTERVALO_SONDEO"/>, el primero con desfase
        /// aleatorio). Lo llama la vista al cargarse; si ya estaba iniciado no hace nada.
        /// </summary>
        public void Iniciar()
        {
            if (_timer != null)
            {
                return;
            }
            _timer = new DispatcherTimer { Interval = PrimerSondeo(_azar) };
            _timer.Tick += async (s, e) =>
            {
                _timer.Interval = INTERVALO_SONDEO;
                await RefrescarContador();
            };
            _timer.Start();
            // El refresco de arranque cuenta como el del foco: la ventana se activa justo después de cargarse.
            _ultimoRefrescoPorFoco = _ahora();
            _ = RefrescarContador();
        }

        public void Detener()
        {
            _timer?.Stop();
            _timer = null;
        }

        /// <summary>El primer sondeo: el intervalo más un desfase aleatorio entre 0 y <see cref="DESFASE_MAXIMO_SONDEO"/>.</summary>
        internal static TimeSpan PrimerSondeo(Random azar)
            => INTERVALO_SONDEO + TimeSpan.FromSeconds(azar.NextDouble() * DESFASE_MAXIMO_SONDEO.TotalSeconds);

        /// <summary>
        /// La ventana principal ha recuperado el foco: se refresca, pero como mucho una vez cada
        /// <see cref="MINIMO_ENTRE_REFRESCOS_POR_FOCO"/> (el usuario cambia de ventana muchas veces al día).
        /// </summary>
        public Task AlActivarseLaVentana()
        {
            DateTime ahora = _ahora();
            if (ahora - _ultimoRefrescoPorFoco < MINIMO_ENTRE_REFRESCOS_POR_FOCO)
            {
                return Task.CompletedTask;
            }
            _ultimoRefrescoPorFoco = ahora;
            return RefrescarContador();
        }

        /// <summary>La API avisa (push) de que hay notificaciones nuevas: se refresca sin esperar a nada.</summary>
        private void OnHayNotificacionesNuevas(object sender, EventArgs e)
        {
            if (_dispatcher == null || _dispatcher.CheckAccess())
            {
                _ = RefrescarContador();
            }
            else
            {
                _ = _dispatcher.InvokeAsync(() => RefrescarContador());
            }
        }

        /// <summary>Pide el número de no leídas. Silencioso: si falla (sin red, sin token...), se deja el que había.</summary>
        internal async Task RefrescarContador()
        {
            if (_refrescando)
            {
                return;
            }
            _refrescando = true;
            try
            {
                NoLeidas = await _buzon.ContarNoLeidas();
            }
            catch (Exception)
            {
                // A propósito: un fallo de red no debe molestar ni llenar ELMAH. Ya habrá otro refresco (foco, panel, aviso o sondeo).
            }
            finally
            {
                _refrescando = false;
            }
        }

        internal async Task CargarPanel()
        {
            Cargando = true;
            Mensaje = null;
            try
            {
                List<NotificacionBuzon> lista = await _buzon.LeerBuzon(false, 1, TAMANO_PANEL) ?? new List<NotificacionBuzon>();
                DateTime ahora = _ahora();
                Notificaciones.Clear();
                foreach (NotificacionBuzon n in lista.Where(n => n != null).OrderByDescending(n => n.FechaCreacion))
                {
                    Notificaciones.Add(new NotificacionBuzonItem(n, ahora));
                }
                if (Notificaciones.Count == 0)
                {
                    Mensaje = "No tienes notificaciones.";
                }
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
            }
            finally
            {
                Cargando = false;
                MarcarTodasLeidasCommand.NotifyCanExecuteChanged();
            }
            await RefrescarContador();
        }

        /// <summary>
        /// Marca la notificación leída y la «abre»: la respuesta a un comentario de Novedades abre la ventana
        /// de Novedades en ese comentario; el aviso de versión nueva (Nesto#501), en esa versión; cualquier
        /// otra se despliega para leerla entera.
        /// </summary>
        internal async Task AbrirNotificacion(NotificacionBuzonItem item)
        {
            if (item == null)
            {
                return;
            }
            if (!item.Leida)
            {
                try
                {
                    await _buzon.MarcarLeida(item.Id);
                    item.Leida = true;
                    NoLeidas--;
                    MarcarTodasLeidasCommand.NotifyCanExecuteChanged();
                }
                catch (Exception ex)
                {
                    // Se abre igual: lo importante es que el usuario vea a dónde lleva.
                    Mensaje = ex.Message;
                }
            }

            int? novedadId = item.Notificacion.DatoEntero("novedadId");
            if (item.Notificacion.Tipo == NotificacionBuzon.TIPO_NOVEDAD_COMENTARIO && novedadId.HasValue && _dialogService != null)
            {
                PanelAbierto = false;
                var parametros = new ParametrosDialogo
                {
                    { NovedadesDialogViewModel.PARAMETRO_NOVEDAD_ID, novedadId.Value }
                };
                int? comentarioId = item.Notificacion.DatoEntero("comentarioId");
                if (comentarioId.HasValue)
                {
                    parametros.Add(NovedadesDialogViewModel.PARAMETRO_COMENTARIO_ID, comentarioId.Value);
                }
                _dialogService.ShowDialog(DIALOGO_NOVEDADES, parametros, _ => { });
                return;
            }
            if (item.Notificacion.Tipo == NotificacionBuzon.TIPO_NUEVA_VERSION_NESTO && _abridorNovedades != null)
            {
                await AbrirNovedades(item.Notificacion.Dato("version"));
                return;
            }
            // NestoAPI#522: el recordatorio diario de administración abre la ventana de facturas pendientes de
            // Verifactu (módulo Cajas). Si ya está abierta, se reutiliza la pestaña y se recarga.
            if (item.Notificacion.Tipo == NotificacionBuzon.TIPO_FACTURAS_PENDIENTES_VERIFACTU && _navegacion != null)
            {
                PanelAbierto = false;
                _navegacion.RequestNavigate(REGION_PRINCIPAL, NotificacionBuzon.VISTA_FACTURAS_PENDIENTES_VERIFACTU);
                return;
            }
            // NestoAPI#555: cualquier aviso que hable de un pedido (el picking con importe, el NIF incorrecto...)
            // abre ese pedido. Va detrás de los tipos con destino propio.
            int? pedido = item.Notificacion.DatoEntero("pedido");
            if (pedido.HasValue && pedido.Value > 0 && _abridorPedidos != null)
            {
                PanelAbierto = false;
                string empresa = item.Notificacion.Dato("empresa");
                _abridorPedidos.Abrir(string.IsNullOrWhiteSpace(empresa) ? EMPRESA_POR_DEFECTO : empresa.Trim(), pedido.Value);
                return;
            }
            item.Desplegada = !item.Desplegada;
        }

        internal const string EMPRESA_POR_DEFECTO = "1";

        /// <summary>Nesto#501: cierra el panel y abre Novedades (en <paramref name="version"/> si se indica).</summary>
        private Task AbrirNovedades(string version)
        {
            if (_abridorNovedades == null)
            {
                return Task.CompletedTask;
            }
            PanelAbierto = false;
            return _abridorNovedades.Abrir(version);
        }

        internal async Task BorrarNotificacion(NotificacionBuzonItem item)
        {
            if (item == null)
            {
                return;
            }
            Mensaje = null;
            try
            {
                await _buzon.Eliminar(item.Id);
                Notificaciones.Remove(item);
                if (!item.Leida)
                {
                    NoLeidas--;
                }
                if (Notificaciones.Count == 0)
                {
                    Mensaje = "No tienes notificaciones.";
                }
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
            }
            MarcarTodasLeidasCommand.NotifyCanExecuteChanged();
        }

        internal async Task MarcarTodasLeidas()
        {
            Mensaje = null;
            try
            {
                await _buzon.MarcarTodasLeidas();
                foreach (NotificacionBuzonItem n in Notificaciones)
                {
                    n.Leida = true;
                }
                NoLeidas = 0;
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
            }
            MarcarTodasLeidasCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Nesto#477: una notificación del panel de la campana.</summary>
    public class NotificacionBuzonItem : ObservableObject
    {
        public NotificacionBuzonItem(NotificacionBuzon notificacion, DateTime ahora)
        {
            Notificacion = notificacion ?? throw new ArgumentNullException(nameof(notificacion));
            _leida = notificacion.Leida;
            FechaRelativa = CalcularFechaRelativa(notificacion.FechaCreacion, ahora);
        }

        public NotificacionBuzon Notificacion { get; }
        public int Id => Notificacion.Id;
        public string Titulo => Notificacion.Titulo;
        public string Cuerpo => Notificacion.Cuerpo;
        public DateTime FechaCreacion => Notificacion.FechaCreacion;
        public string FechaRelativa { get; }

        private bool _leida;
        public bool Leida { get => _leida; internal set => SetProperty(ref _leida, value); }

        private bool _desplegada;
        /// <summary>El cuerpo entero (plegada se ven dos líneas).</summary>
        public bool Desplegada { get => _desplegada; internal set => SetProperty(ref _desplegada, value); }

        /// <summary>«ahora», «hace 5 min», «hace 3 h», «ayer» o la fecha.</summary>
        internal static string CalcularFechaRelativa(DateTime fecha, DateTime ahora)
        {
            TimeSpan hace = ahora - fecha;
            if (hace < TimeSpan.FromMinutes(1))
            {
                return "ahora";
            }
            if (hace < TimeSpan.FromHours(1))
            {
                return $"hace {(int)hace.TotalMinutes} min";
            }
            if (fecha.Date == ahora.Date)
            {
                return $"hace {(int)hace.TotalHours} h";
            }
            if (fecha.Date == ahora.Date.AddDays(-1))
            {
                return "ayer a las " + fecha.ToString("HH:mm", CultureInfo.InvariantCulture);
            }
            return fecha.ToString(fecha.Year == ahora.Year ? "dd/MM HH:mm" : "dd/MM/yyyy", CultureInfo.InvariantCulture);
        }
    }
}
