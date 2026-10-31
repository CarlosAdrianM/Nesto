using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ControlesUsuario.Notificaciones.Urgentes
{
    /// <summary>
    /// Nesto#593 (NestoAPI#775): lo que hace la vista con una notificación urgente. Lo implementa <see cref="PresentadorAvisosUrgentesWpf"/>
    /// (la ventana roja, el pitido y el parpadeo de la barra de tareas); el gestor no conoce tipos de vista.
    /// </summary>
    public interface IPresentadorAvisosUrgentes
    {
        /// <summary>Abre su ventana flotante (no modal, siempre encima, abajo a la derecha). Cuando se cierre, llama a <paramref name="alCerrarse"/>.</summary>
        void Abrir(AvisoUrgenteViewModel aviso, Action<AvisoUrgenteViewModel> alCerrarse);

        /// <summary>Cierra su ventana pasado <paramref name="retraso"/> (para que se lea el «Lo ha autorizado X»).</summary>
        void Cerrar(AvisoUrgenteViewModel aviso, TimeSpan retraso);

        void Pitar();

        /// <summary>Si Nesto no está delante (minimizado o sin foco), parpadea su icono en la barra de tareas.</summary>
        void LlamarLaAtencion();
    }

    /// <summary>
    /// Nesto#593 (NestoAPI#775): las notificaciones URGENTES (la API decide qué tipos lo son: hoy, la autorización del repartidor para
    /// entregar un reembolso sin cobrar). No es la campana: cada urgente abre su propia ventana roja con pitido.
    /// <list type="bullet">
    /// <item>Pide las pendientes (GET api/Notificaciones/Urgentes) al arrancar, con cada conexión o reconexión de SignalR y con cada
    /// «hayNotificacionUrgente». Sin sondeo.</item>
    /// <item>Mientras haya alguna abierta sin contestar, pita cada <see cref="INTERVALO_PITIDO"/>, como mucho durante <see cref="MAXIMO_PITANDO"/>
    /// desde que salió (o desde que se escaló a Dirección), y parpadea la barra de tareas si Nesto no está delante.</item>
    /// <item>Con «notificacionResuelta» (la ha contestado otro, o este mismo Nesto) enseña «Lo ha autorizado X» y se cierra a los
    /// <see cref="RETRASO_CIERRE"/>, en todos los Nesto.</item>
    /// <item>«Más tarde» la cierra aquí; si sigue pendiente vuelve a salir al reconectar, al volver a abrir Nesto o si se escala.</item>
    /// </list>
    /// </summary>
    public sealed class GestorNotificacionesUrgentes
    {
        public static readonly TimeSpan INTERVALO_PITIDO = TimeSpan.FromSeconds(20);
        public static readonly TimeSpan MAXIMO_PITANDO = TimeSpan.FromMinutes(3);
        public static readonly TimeSpan RETRASO_CIERRE = TimeSpan.FromSeconds(5);
        /// <summary>Margen del temporizador: un tic que llega un poco antes de los 20 s también pita.</summary>
        private static readonly TimeSpan HOLGURA = TimeSpan.FromSeconds(1);

        private readonly IBuzonNotificacionesService _buzon;
        private readonly IAvisosUrgentesTiempoReal _avisos;
        private readonly IPresentadorAvisosUrgentes _presentador;
        private readonly Func<DateTime> _ahora;
        private readonly Dispatcher _dispatcher;
        private readonly Dictionary<string, AvisoUrgenteViewModel> _abiertos = new Dictionary<string, AvisoUrgenteViewModel>(StringComparer.Ordinal);
        private DispatcherTimer _timer;
        private bool _iniciado;
        private bool _refrescando;
        private bool _refrescarOtraVez;
        private DateTime _ultimoPitido = DateTime.MinValue;

        public GestorNotificacionesUrgentes(IBuzonNotificacionesService buzon, IAvisosEnTiempoReal avisos, IPresentadorAvisosUrgentes presentador)
            : this(buzon, avisos as IAvisosUrgentesTiempoReal, presentador, () => DateTime.Now)
        {
        }

        internal GestorNotificacionesUrgentes(IBuzonNotificacionesService buzon, IAvisosUrgentesTiempoReal avisos, IPresentadorAvisosUrgentes presentador,
            Func<DateTime> ahora)
        {
            _buzon = buzon ?? throw new ArgumentNullException(nameof(buzon));
            _avisos = avisos;
            _presentador = presentador ?? throw new ArgumentNullException(nameof(presentador));
            _ahora = ahora ?? (() => DateTime.Now);
            // Null en los tests (sin Application): entonces todo se atiende en el hilo que llega y el pitido lo da Tic().
            _dispatcher = System.Windows.Application.Current?.Dispatcher;
        }

        /// <summary>Las ventanas abiertas ahora (para los tests y el diagnóstico).</summary>
        public IReadOnlyList<AvisoUrgenteViewModel> Abiertos => _abiertos.Values.ToList();

        /// <summary>Se suscribe a los avisos y mira las pendientes. Se puede llamar desde cualquier hilo; la segunda vez no hace nada.</summary>
        public void Iniciar() => EnUi(() =>
        {
            if (_iniciado)
            {
                return;
            }
            _iniciado = true;
            if (_avisos != null)
            {
                _avisos.HayNotificacionUrgente += (s, e) => EnUi(() => _ = Refrescar());
                _avisos.NotificacionResuelta += (s, resuelta) => EnUi(() => AlResolverse(resuelta));
            }
            _ = Refrescar();
        });

        /// <summary>Pide las urgentes pendientes y abre las que no estén ya abiertas. Silencioso: si falla, ya habrá otro aviso.</summary>
        internal async Task Refrescar()
        {
            if (_refrescando)
            {
                _refrescarOtraVez = true;
                return;
            }
            _refrescando = true;
            try
            {
                do
                {
                    _refrescarOtraVez = false;
                    List<NotificacionUrgente> pendientes;
                    try
                    {
                        pendientes = await _buzon.LeerUrgentes() ?? new List<NotificacionUrgente>();
                    }
                    catch (Exception)
                    {
                        // A propósito: un fallo de red no debe molestar. Lo urgente sigue también en la campana.
                        return;
                    }
                    Mostrar(pendientes);
                } while (_refrescarOtraVez);
            }
            finally
            {
                _refrescando = false;
            }
        }

        private void Mostrar(IEnumerable<NotificacionUrgente> pendientes)
        {
            DateTime ahora = _ahora();
            bool nuevas = false;
            foreach (NotificacionUrgente n in pendientes.Where(n => n != null && !string.IsNullOrWhiteSpace(n.Referencia)))
            {
                string clave = AvisoUrgenteViewModel.ClaveDe(n.Tipo, n.Referencia);
                if (_abiertos.TryGetValue(clave, out AvisoUrgenteViewModel abierto))
                {
                    bool escalaAhora = n.Escalada && !abierto.Escalada;
                    abierto.Actualizar(n, ahora);
                    nuevas |= escalaAhora;
                    continue;
                }
                var aviso = new AvisoUrgenteViewModel(n, _buzon, ahora, AlTerminar, Quitar);
                _abiertos[clave] = aviso;
                _presentador.Abrir(aviso, Quitar);
                nuevas = true;
            }
            if (nuevas)
            {
                Sonar(ahora);
            }
            ArrancarOPararTimer();
        }

        /// <summary>Cada <see cref="INTERVALO_PITIDO"/> (lo llama el temporizador; en los tests, a mano con el reloj inyectado).</summary>
        internal void Tic()
        {
            DateTime ahora = _ahora();
            bool algunaSuena = _abiertos.Values.Any(a => !a.Resuelta && ahora - a.Desde <= MAXIMO_PITANDO);
            if (algunaSuena && ahora - _ultimoPitido >= INTERVALO_PITIDO - HOLGURA)
            {
                Sonar(ahora);
            }
            ArrancarOPararTimer();
        }

        private void Sonar(DateTime ahora)
        {
            _ultimoPitido = ahora;
            try
            {
                _presentador.Pitar();
                _presentador.LlamarLaAtencion();
            }
            catch (Exception)
            {
                // Sin sonido no pasa nada: la ventana ya está a la vista
            }
        }

        internal void AlResolverse(NotificacionResuelta resuelta)
        {
            if (resuelta == null)
            {
                return;
            }
            if (_abiertos.TryGetValue(AvisoUrgenteViewModel.ClaveDe(resuelta.Tipo, resuelta.Referencia), out AvisoUrgenteViewModel aviso))
            {
                aviso.Resolver(resuelta);
            }
        }

        /// <summary>Contestada (aquí o fuera): deja de pitar y se cierra tras <see cref="RETRASO_CIERRE"/>.</summary>
        private void AlTerminar(AvisoUrgenteViewModel aviso)
        {
            _presentador.Cerrar(aviso, RETRASO_CIERRE);
            ArrancarOPararTimer();
        }

        /// <summary>Su ventana se ha cerrado (sola tras contestarse, o con «Más tarde» / la X).</summary>
        private void Quitar(AvisoUrgenteViewModel aviso)
        {
            if (aviso != null && _abiertos.TryGetValue(aviso.Clave, out AvisoUrgenteViewModel actual) && ReferenceEquals(actual, aviso))
            {
                _ = _abiertos.Remove(aviso.Clave);
                _presentador.Cerrar(aviso, TimeSpan.Zero);
            }
            ArrancarOPararTimer();
        }

        private void ArrancarOPararTimer()
        {
            if (_dispatcher == null)
            {
                return;
            }
            DateTime ahora = _ahora();
            bool hace = _abiertos.Values.Any(a => !a.Resuelta && ahora - a.Desde <= MAXIMO_PITANDO);
            if (hace && _timer == null)
            {
                _timer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher) { Interval = INTERVALO_PITIDO };
                _timer.Tick += (s, e) => Tic();
                _timer.Start();
            }
            else if (!hace && _timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
        }

        private void EnUi(Action accion)
        {
            if (_dispatcher == null || _dispatcher.CheckAccess())
            {
                accion();
            }
            else
            {
                _ = _dispatcher.InvokeAsync(accion);
            }
        }
    }
}
