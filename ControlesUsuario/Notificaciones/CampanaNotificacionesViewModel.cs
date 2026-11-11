using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Notificaciones.Menciones;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
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
    /// Sugerencia 1183 (Laura Magán, 11/11/26): con una mención sin leer (Datos["mencion"] = "1") la campana cambia de color y lleva
    /// un halo que parpadea suave (<see cref="HayMencionSinLeer"/>); en el panel, las menciones llevan la etiqueta «Le mencionan». Si el
    /// usuario lo enciende (interruptor del panel, parámetro <see cref="Parametros.Claves.AvisoEmergenteMenciones"/>), cada mención
    /// nueva sale además en una ventanita (<see cref="IPresentadorAvisoMencion"/>), una vez por notificación: nunca las que ya
    /// estaban al arrancar ni las que ya se han visto en el panel. Para saberlo, cuando hay no leídas y el contador cambia (o avisa
    /// SignalR) se leen las no leídas.
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
        private readonly IConfiguracion _configuracion;
        private readonly IPresentadorAvisoMencion _presentadorMenciones;
        private DispatcherTimer _timer;
        private bool _refrescando;
        // Sugerencia 1183: llega un aviso mientras se refresca → otra vuelta al terminar (con la lectura de las no leídas)
        private bool _refrescoPendiente;
        private bool _forzarListaPendiente;
        // Sugerencia 1183: las menciones sin leer que se conocen, las que ya se han visto (ventanita o panel) y si ya se sabe
        // lo que había al arrancar (lo de antes no saca la ventanita)
        private readonly HashSet<int> _mencionesSinLeer = new HashSet<int>();
        private readonly HashSet<int> _mencionesVistas = new HashSet<int>();
        private bool _lecturaInicialHecha;
        private int _contadorDeLaUltimaLectura = -1;
        private bool _preferenciasCargadas;
        private DateTime _ultimoRefrescoPorFoco = DateTime.MinValue;

        public CampanaNotificacionesViewModel(IBuzonNotificacionesService buzon, IServicioDialogos dialogService, IAvisosEnTiempoReal avisos,
            IAbridorNovedades abridorNovedades, IServicioNavegacion navegacion, IAbridorPedidos abridorPedidos,
            IConfiguracion configuracion, IPresentadorAvisoMencion presentadorMenciones)
            : this(buzon, dialogService, avisos, abridorNovedades, () => DateTime.Now, new Random(), navegacion, abridorPedidos,
                  configuracion, presentadorMenciones) { }

        internal CampanaNotificacionesViewModel(IBuzonNotificacionesService buzon, IServicioDialogos dialogService, IAvisosEnTiempoReal avisos,
            IAbridorNovedades abridorNovedades, Func<DateTime> ahora, Random azar, IServicioNavegacion navegacion = null,
            IAbridorPedidos abridorPedidos = null, IConfiguracion configuracion = null, IPresentadorAvisoMencion presentadorMenciones = null)
        {
            _configuracion = configuracion;
            _presentadorMenciones = presentadorMenciones;
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
            CerrarAvisoFichaCambiadoCommand = new AsyncRelayCommand<NotificacionBuzonItem>(i => CerrarAvisoFicha(i, NotificacionBuzon.RESULTADO_AVISO_CAMBIADO));
            CerrarAvisoFichaEstabaBienCommand = new AsyncRelayCommand<NotificacionBuzonItem>(i => CerrarAvisoFicha(i, NotificacionBuzon.RESULTADO_AVISO_ESTABA_BIEN));
            AutorizarReembolsoCommand = new AsyncRelayCommand<NotificacionBuzonItem>(i => ResponderAutorizacionReembolso(i, true));
            DenegarReembolsoCommand = new AsyncRelayCommand<NotificacionBuzonItem>(i => ResponderAutorizacionReembolso(i, false));
        }

        /// <summary>Nesto#591: «Autorizar» en el aviso del repartidor que pide entregar sin cobrar el reembolso.</summary>
        public IAsyncRelayCommand<NotificacionBuzonItem> AutorizarReembolsoCommand { get; }
        /// <summary>Nesto#591: «Denegar» (el repartidor tendrá que cobrar o apuntar la incidencia «Reembolso no cobrado»).</summary>
        public IAsyncRelayCommand<NotificacionBuzonItem> DenegarReembolsoCommand { get; }

        /// <summary>
        /// Nesto#591 (NestoAPI#773): contesta en la API (al repartidor le llega a la PDA al momento) y lo da por leído. Si la API no deja
        /// (sin permiso, ya la contestó otro, la autorizaron por teléfono…), se enseña su motivo; si ya está contestada, se cierra igual.
        /// </summary>
        internal async Task ResponderAutorizacionReembolso(NotificacionBuzonItem item, bool autorizar)
        {
            int? autorizacionId = item?.Notificacion.DatoEntero("autorizacionId");
            if (!autorizacionId.HasValue)
            {
                return;
            }
            Mensaje = null;
            try
            {
                await _buzon.ResponderAutorizacionReembolso(autorizacionId.Value, autorizar, null);
                item.AvisoCerrado = true;
                Mensaje = autorizar
                    ? "Autorizado: el repartidor ya puede entregar sin cobrar."
                    : "Denegado: el repartidor tendrá que cobrarlo o apuntar la incidencia «Reembolso no cobrado».";
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
                if ((ex.Message ?? string.Empty).IndexOf(" ya ", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }
                item.AvisoCerrado = true; // ya contestada (por otro o por teléfono): nada más que hacer aquí
            }
            if (!item.Leida)
            {
                try
                {
                    await _buzon.MarcarLeida(item.Id);
                    item.Leida = true;
                    NoLeidas--;
                    QuitarMencion(item.Id);
                }
                catch (Exception)
                {
                    // Lo importante (la respuesta) ya está hecho
                }
                MarcarTodasLeidasCommand.NotifyCanExecuteChanged();
            }
        }

        public ObservableCollection<NotificacionBuzonItem> Notificaciones { get; } = new ObservableCollection<NotificacionBuzonItem>();

        public IAsyncRelayCommand<NotificacionBuzonItem> AbrirNotificacionCommand { get; }
        public IAsyncRelayCommand<NotificacionBuzonItem> BorrarNotificacionCommand { get; }
        public IAsyncRelayCommand MarcarTodasLeidasCommand { get; }
        /// <summary>Nesto#501: el botón de Novedades junto a la campana.</summary>
        public IAsyncRelayCommand AbrirNovedadesCommand { get; }
        /// <summary>Nesto#509: cierra un aviso de dato mal en la ficha (Ariadna#8) porque ya se ha corregido.</summary>
        public IAsyncRelayCommand<NotificacionBuzonItem> CerrarAvisoFichaCambiadoCommand { get; }
        /// <summary>Nesto#509: cierra un aviso de dato mal en la ficha porque el dato estaba bien.</summary>
        public IAsyncRelayCommand<NotificacionBuzonItem> CerrarAvisoFichaEstabaBienCommand { get; }

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

        /// <summary>Sugerencia 1183: hay al menos una mención sin leer (la campana cambia de color y lleva el halo).</summary>
        public bool HayMencionSinLeer => _mencionesSinLeer.Count > 0;
        /// <summary>Lo que pinta el globo rojo (con más de 99 no cabe).</summary>
        public string TextoNoLeidas => NoLeidas > 99 ? "99+" : NoLeidas.ToString();
        public string TextoToolTip => (NoLeidas == 0
            ? "Notificaciones"
            : NoLeidas == 1 ? "Tienes 1 notificación sin leer" : $"Tienes {NoLeidas} notificaciones sin leer")
            + (HayMencionSinLeer ? ". Le han mencionado en Novedades." : string.Empty);

        private bool _avisoEmergenteMenciones;
        /// <summary>
        /// Sugerencia 1183: el interruptor del panel «Avisarme con una ventana cuando me mencionen». Al cambiarlo se guarda en el
        /// parámetro de usuario <see cref="Parametros.Claves.AvisoEmergenteMenciones"/> ("1" / "0"); si no se puede guardar, vuelve
        /// a como estaba y se dice por qué.
        /// </summary>
        public bool AvisoEmergenteMenciones
        {
            get => _avisoEmergenteMenciones;
            set
            {
                if (SetProperty(ref _avisoEmergenteMenciones, value))
                {
                    _ = GuardarAvisoEmergente(value);
                }
            }
        }

        private bool _guardandoPreferencia;
        /// <summary>Sugerencia 1183: el interruptor se puede tocar (hay de dónde leer y guardar, y no se está guardando).</summary>
        public bool PuedeCambiarAvisoEmergente => _configuracion != null && !_guardandoPreferencia;

        /// <summary>
        /// Sugerencia 1183: lee el parámetro del usuario. Sin fila, sin «(defecto)» o si falla = apagado (nunca molesta). Se
        /// pide una vez, al iniciar la campana.
        /// </summary>
        internal async Task CargarPreferencias()
        {
            if (_configuracion == null)
            {
                return;
            }
            bool encendido = false;
            try
            {
                string valor = await _configuracion.leerParametro(EMPRESA_POR_DEFECTO, Parametros.Claves.AvisoEmergenteMenciones);
                encendido = string.Equals(valor?.Trim(), "1", StringComparison.Ordinal);
            }
            catch (Exception)
            {
                // Apagado: es lo de siempre
            }
            _preferenciasCargadas = true;
            if (_avisoEmergenteMenciones != encendido)
            {
                _avisoEmergenteMenciones = encendido; // sin guardar: es lo que ya hay
                OnPropertyChanged(nameof(AvisoEmergenteMenciones));
            }
        }

        private async Task GuardarAvisoEmergente(bool encender)
        {
            if (_configuracion == null)
            {
                return;
            }
            Mensaje = null;
            _guardandoPreferencia = true;
            OnPropertyChanged(nameof(PuedeCambiarAvisoEmergente));
            try
            {
                await _configuracion.GuardarParametro(EMPRESA_POR_DEFECTO, Parametros.Claves.AvisoEmergenteMenciones, encender ? "1" : "0");
                _preferenciasCargadas = true;
                Mensaje = encender
                    ? "Hecho: cuando le mencionen, le avisaremos con una ventanita en la esquina de la pantalla."
                    : "Hecho: ya no le avisaremos con una ventanita; las menciones seguirán resaltadas en la campana.";
            }
            catch (Exception ex)
            {
                _avisoEmergenteMenciones = !encender; // vuelve a como estaba
                OnPropertyChanged(nameof(AvisoEmergenteMenciones));
                Mensaje = "No se ha podido guardar su preferencia: " + ex.Message;
            }
            finally
            {
                _guardandoPreferencia = false;
                OnPropertyChanged(nameof(PuedeCambiarAvisoEmergente));
            }
        }

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
            if (!_preferenciasCargadas)
            {
                _ = CargarPreferencias();
            }
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
            // Sugerencia 1183: con el aviso se leen siempre las no leídas (puede ser una mención aunque el contador no cambie)
            if (_dispatcher == null || _dispatcher.CheckAccess())
            {
                _ = RefrescarContador(true);
            }
            else
            {
                _ = _dispatcher.InvokeAsync(() => RefrescarContador(true));
            }
        }

        /// <summary>
        /// Pide el número de no leídas. Silencioso: si falla (sin red, sin token...), se deja el que había. Sugerencia 1183: si hay
        /// no leídas y el número ha cambiado (o <paramref name="forzarLista"/>, el aviso de SignalR), lee también las no leídas para
        /// saber si hay menciones y si alguna es nueva (ventanita). Si llega otro aviso mientras tanto, se da otra vuelta al terminar.
        /// </summary>
        internal async Task RefrescarContador(bool forzarLista = false)
        {
            if (_refrescando)
            {
                _refrescoPendiente = true;
                _forzarListaPendiente |= forzarLista;
                return;
            }
            _refrescando = true;
            try
            {
                do
                {
                    bool forzar = forzarLista || _forzarListaPendiente;
                    forzarLista = false;
                    _refrescoPendiente = false;
                    _forzarListaPendiente = false;
                    try
                    {
                        int noLeidas = await _buzon.ContarNoLeidas();
                        NoLeidas = noLeidas;
                        await ActualizarMenciones(noLeidas, forzar);
                    }
                    catch (Exception)
                    {
                        // A propósito: un fallo de red no debe molestar ni llenar ELMAH. Ya habrá otro refresco (foco, panel, aviso o sondeo).
                    }
                } while (_refrescoPendiente);
            }
            finally
            {
                _refrescando = false;
            }
        }

        /// <summary>
        /// Sugerencia 1183: con las no leídas, qué menciones quedan sin leer y cuáles son nuevas. La primera lectura (al arrancar)
        /// solo apunta lo que hay: lo de antes no saca la ventanita. Con varias nuevas a la vez sale la más reciente con «y N más».
        /// </summary>
        private async Task ActualizarMenciones(int noLeidas, bool forzar)
        {
            if (noLeidas <= 0)
            {
                _mencionesSinLeer.Clear();
                _contadorDeLaUltimaLectura = 0;
                _lecturaInicialHecha = true;
                NotificarMencion();
                return;
            }
            if (!forzar && _lecturaInicialHecha && noLeidas == _contadorDeLaUltimaLectura)
            {
                return;
            }
            List<NotificacionBuzon> lista = await _buzon.LeerBuzon(true, 1, TAMANO_PANEL) ?? new List<NotificacionBuzon>();
            _contadorDeLaUltimaLectura = noLeidas;
            List<NotificacionBuzon> menciones = lista
                .Where(n => n != null && !n.Leida && n.EsMencion)
                .OrderByDescending(n => n.FechaCreacion)
                .ToList();
            _mencionesSinLeer.Clear();
            foreach (NotificacionBuzon m in menciones)
            {
                _ = _mencionesSinLeer.Add(m.Id);
            }
            List<NotificacionBuzon> nuevas = menciones.Where(m => !_mencionesVistas.Contains(m.Id)).ToList();
            foreach (NotificacionBuzon m in nuevas)
            {
                _ = _mencionesVistas.Add(m.Id);
            }
            bool avisar = _lecturaInicialHecha && nuevas.Count > 0 && AvisoEmergenteMenciones && _presentadorMenciones != null;
            _lecturaInicialHecha = true;
            NotificarMencion();
            if (avisar)
            {
                MostrarAvisoMencion(nuevas[0], nuevas.Count - 1);
            }
        }

        private void MostrarAvisoMencion(NotificacionBuzon notificacion, int otras)
        {
            try
            {
                _presentadorMenciones.Mostrar(new AvisoMencion(notificacion.Id, notificacion.Titulo, notificacion.Cuerpo, otras,
                    () => _ = AbrirNotificacion(new NotificacionBuzonItem(notificacion, _ahora()))));
            }
            catch (Exception)
            {
                // La ventanita no puede romper la campana: la mención sigue resaltada en ella.
            }
        }

        private void QuitarMencion(int id)
        {
            if (_mencionesSinLeer.Remove(id))
            {
                NotificarMencion();
            }
        }

        private void NotificarMencion()
        {
            OnPropertyChanged(nameof(HayMencionSinLeer));
            OnPropertyChanged(nameof(TextoToolTip));
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
                    if (n.EsMencion)
                    {
                        // Sugerencia 1183: vista en el panel = ya no saca la ventanita
                        _ = _mencionesVistas.Add(n.Id);
                        _ = n.Leida ? _mencionesSinLeer.Remove(n.Id) : _mencionesSinLeer.Add(n.Id);
                    }
                }
                NotificarMencion();
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
                    QuitarMencion(item.Id);
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
            // Nesto#529 (NestoAPI#631): el aviso a Compras de propuestas de stock nuevas abre la ventana de propuestas
            if (item.Notificacion.Tipo == NotificacionBuzon.TIPO_PROPUESTAS_STOCK && _navegacion != null)
            {
                PanelAbierto = false;
                _navegacion.RequestNavigate(REGION_PRINCIPAL, NotificacionBuzon.VISTA_PROPUESTAS_STOCK);
                return;
            }
            // Nesto#526 (NestoAPI#634): lo recibido en una reposición no coincide con lo enviado: abre la lista de
            // diferencias de reposición (si ya está abierta, se reutiliza la pestaña y se vuelve a leer).
            if (string.Equals(item.Notificacion.Dato("vista"), NotificacionBuzon.DATO_VISTA_DIFERENCIAS_REPOSICION, StringComparison.OrdinalIgnoreCase)
                && _navegacion != null)
            {
                PanelAbierto = false;
                _navegacion.RequestNavigate(REGION_PRINCIPAL, NotificacionBuzon.VISTA_DIFERENCIAS_REPOSICION);
                return;
            }
            // Nesto#546 (NestoAPI#693): la fecha prometida de un pedido se retrasa o se queda sin fecha: abre «Pedidos con la
            // fecha en riesgo» con ese pedido elegido (ahí se ve el porqué y qué hacer), no el pedido suelto. Va antes del
            // salto genérico al pedido porque este aviso también trae Datos["pedido"].
            if (item.Notificacion.Tipo == NotificacionBuzon.TIPO_PROMESA_EN_RIESGO && _navegacion != null)
            {
                PanelAbierto = false;
                _navegacion.RequestNavigate(REGION_PRINCIPAL, NotificacionBuzon.VISTA_PEDIDOS_PROMESA_RIESGO, ParametrosPromesaRiesgo(item.Notificacion));
                return;
            }
            // Nesto#568 (NestoAPI#734): el almacén pregunta si el pedido sale con o sin reembolso: Administración contesta en el
            // diálogo del pedido. Va antes del salto genérico al pedido porque también trae Datos["pedido"]; la respuesta
            // (accion «respuesta») sí abre el pedido.
            if (item.Notificacion.Tipo == NotificacionBuzon.TIPO_CONSULTA_REEMBOLSO
                && string.Equals(item.Notificacion.Dato("accion"), NotificacionBuzon.ACCION_CONSULTA_REEMBOLSO_CONSULTA, StringComparison.OrdinalIgnoreCase)
                && item.Notificacion.DatoEntero("pedido") is int pedidoConsulta && pedidoConsulta > 0
                && _dialogService != null)
            {
                PanelAbierto = false;
                string empresaConsulta = item.Notificacion.Dato("empresa");
                _dialogService.ShowDialog(NotificacionBuzon.DIALOGO_CONSULTA_REEMBOLSO, new ParametrosDialogo
                {
                    { "empresa", string.IsNullOrWhiteSpace(empresaConsulta) ? EMPRESA_POR_DEFECTO : empresaConsulta.Trim() },
                    { "pedido", pedidoConsulta }
                }, _ => { });
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

        /// <summary>Nesto#546: el pedido y la empresa del aviso, para que la pantalla lo deje elegido. Sin pedido, vacío.</summary>
        internal static ParametrosNavegacion ParametrosPromesaRiesgo(NotificacionBuzon notificacion)
        {
            var parametros = new ParametrosNavegacion();
            int? pedido = notificacion?.DatoEntero(NotificacionBuzon.PARAMETRO_PROMESA_PEDIDO);
            if (pedido.HasValue && pedido.Value > 0)
            {
                parametros.Add(NotificacionBuzon.PARAMETRO_PROMESA_PEDIDO, pedido.Value);
                string empresa = notificacion.Dato(NotificacionBuzon.PARAMETRO_PROMESA_EMPRESA);
                parametros.Add(NotificacionBuzon.PARAMETRO_PROMESA_EMPRESA, string.IsNullOrWhiteSpace(empresa) ? EMPRESA_POR_DEFECTO : empresa.Trim());
            }
            return parametros;
        }

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

        /// <summary>
        /// Nesto#509: cierra el aviso en la API (al mozo que avisó le llega la respuesta en Ariadna) y lo da por
        /// leído. Si la API no deja (no es del equipo, ya estaba cerrado…), se enseña el motivo y sigue abierto.
        /// </summary>
        internal async Task CerrarAvisoFicha(NotificacionBuzonItem item, string resultado)
        {
            int? avisoId = item?.Notificacion.DatoEntero("avisoId");
            if (!avisoId.HasValue)
            {
                return;
            }
            Mensaje = null;
            try
            {
                string respuesta = await _buzon.CerrarAvisoFicha(avisoId.Value, resultado);
                item.AvisoCerrado = true;
                Mensaje = string.IsNullOrWhiteSpace(respuesta) ? "Aviso cerrado." : respuesta;
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
                return;
            }
            if (!item.Leida)
            {
                try
                {
                    await _buzon.MarcarLeida(item.Id);
                    item.Leida = true;
                    NoLeidas--;
                    QuitarMencion(item.Id);
                }
                catch (Exception)
                {
                    // El aviso ya está cerrado, que es lo importante; la marca de leída se pondrá en otro momento.
                }
                MarcarTodasLeidasCommand.NotifyCanExecuteChanged();
            }
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
                QuitarMencion(item.Id);
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
                _mencionesSinLeer.Clear();
                NotificarMencion();
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

        /// <summary>Sugerencia 1183: es el aviso de que le han mencionado (etiqueta «Le mencionan» y borde de acento en el panel).</summary>
        public bool EsMencion => Notificacion.EsMencion;

        /// <summary>Nesto#509: un aviso de dato mal en la ficha (Ariadna#8) que se puede cerrar desde aquí.</summary>
        public bool EsAvisoFichaProducto => Notificacion.Tipo == NotificacionBuzon.TIPO_AVISO_FICHA_PRODUCTO
            && Notificacion.DatoEntero("avisoId").HasValue;

        private bool _avisoCerrado;
        /// <summary>Nesto#509: ya se ha cerrado desde la campana (sin recargar el panel).</summary>
        public bool AvisoCerrado
        {
            get => _avisoCerrado;
            internal set
            {
                if (SetProperty(ref _avisoCerrado, value))
                {
                    OnPropertyChanged(nameof(MostrarBotonesAvisoFicha));
                    OnPropertyChanged(nameof(MostrarBotonesAutorizacionReembolso));
                }
            }
        }

        /// <summary>Nesto#509: los botones «Cambiado» / «Estaba bien».</summary>
        public bool MostrarBotonesAvisoFicha => EsAvisoFichaProducto && !AvisoCerrado;

        /// <summary>Nesto#591: el repartidor pide entregar sin cobrar el reembolso (se contesta aquí).</summary>
        public bool EsAutorizacionReembolso => Notificacion.Tipo == NotificacionBuzon.TIPO_AUTORIZACION_REEMBOLSO_REPARTO
            && Notificacion.DatoEntero("autorizacionId").HasValue;

        /// <summary>Nesto#591: los botones «Autorizar» / «Denegar» (hasta contestar o enterarse de que ya está contestada).</summary>
        public bool MostrarBotonesAutorizacionReembolso => EsAutorizacionReembolso && !AvisoCerrado;

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
