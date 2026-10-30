using Microsoft.AspNet.SignalR.Client;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// NestoAPI#536: la API avisa al momento, por SignalR (ASP.NET SignalR 2, hub «AvisosHub» en /signalr),
    /// de que hay algo nuevo en el buzón del usuario. Así la campana (Nesto#477) no tiene que sondear cada poco.
    /// - El JWT va en la query string (access_token): es lo que lee el servidor en /signalr.
    /// - SignalR ya reconecta solo durante un rato; si la conexión se cierra del todo, se reintenta con
    ///   esperas crecientes (<see cref="EsperaTrasFallos"/>) sin bloquear ni molestar. Solo se registra en
    ///   ELMAH un error por sesión, y solo si el fallo dura (se llega a la espera máxima).
    /// - El token de empleado caduca (8 h): cada <see cref="INTERVALO_REVISION_TOKEN"/> se mira si el
    ///   servicio de autenticación tiene uno nuevo y, si lo tiene, se reconecta con él.
    /// - Tras recuperar una conexión perdida se avisa una vez (pudo llegar algo mientras no había conexión).
    /// - Nesto#492: <see cref="Estado"/> sigue a la conexión real (conectando, conectada, reintentando al
    ///   perderla o no conseguirla, desactivada si no se ha arrancado o se ha detenido). Renovar el token
    ///   no cambia el estado: se reconecta enseguida y no se ha perdido nada.
    /// - Ariadna#51 (corte 2): en la misma conexión, los grupos del almacén compartidos con Ariadna («AlmacenHub»,
    ///   <see cref="IGruposAlmacenTiempoReal"/>). Se recuerdan los grupos en los que se ha entrado y se vuelve a entrar en cada
    ///   conexión nueva; al recuperar una perdida se avisa de cada grupo (pudo cambiar algo mientras no había conexión).
    /// - Nesto#589 (NestoAPI#768): escáner remoto. En cada conexión (y al recuperarla) Nesto se presenta con
    ///   «ActividadEscanerRemoto(equipo, conFoco)» y vuelve a llamarlo cada vez que se activa su ventana
    ///   (<see cref="NotificarActividad"/>); la API le manda «lecturaEscanerRemoto» con lo leído en Ariadna por el mismo usuario
    ///   (<see cref="LecturaRecibida"/>).
    /// Los eventos <see cref="HayNotificacionesNuevas"/>, <see cref="EstadoCambiado"/>, <see cref="GrupoHaCambiado"/> y
    /// <see cref="LecturaRecibida"/> llegan en un hilo que no es el de la UI.
    /// </summary>
    public sealed class AvisosEnTiempoRealSignalR : IAvisosEnTiempoReal, IGruposAlmacenTiempoReal, IEscanerRemoto, IDisposable
    {
        public const string RUTA_HUB = "/signalr";
        public static readonly TimeSpan INTERVALO_REVISION_TOKEN = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan[] ESPERAS = {
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)
        };
        public static readonly TimeSpan ESPERA_MAXIMA = TimeSpan.FromMinutes(5);

        private readonly string _urlHub;
        private readonly IServicioAutenticacion _autenticacion;
        private readonly Func<string, string, IConexionAvisos> _crearConexion;
        private readonly Func<TimeSpan, CancellationToken, Task> _esperar;
        private readonly Action<Exception> _registrarError;
        private readonly object _candado = new object();
        private CancellationTokenSource _cancelacion;
        private Task _bucle;
        private bool _errorRegistrado;
        private int _estado = (int)EstadoConexionTiempoReal.Desactivado;
        private IConexionAvisos _conexionActual;
        /// <summary>Ariadna#51: los grupos del almacén en los que se está, con cuántas pantallas lo han pedido.</summary>
        private readonly ConcurrentDictionary<string, int> _grupos = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public event EventHandler HayNotificacionesNuevas;

        /// <summary>Nesto#589: lo leído con Ariadna por el mismo usuario. Llega en un hilo que no es el de la UI.</summary>
        public event EventHandler<LecturaEscanerRemoto> LecturaRecibida;

        /// <summary>Nesto#589: si alguna ventana de Nesto está delante (se dice al presentarse en cada conexión).</summary>
        private volatile bool _conFoco;

        /// <summary>Nesto#589: el equipo que se dice a la API («RDS2016»), para que Ariadna enseñe a qué Nesto manda.</summary>
        internal string Equipo { get; set; } = Environment.MachineName;

        public void NotificarActividad(bool conFoco)
        {
            _conFoco = conFoco;
            if (!conFoco)
            {
                // Perder el foco no se dice: manda el último que lo tuvo.
                return;
            }
            IConexionAvisos conexion = Volatile.Read(ref _conexionActual);
            if (conexion != null)
            {
                _ = PresentarSinFallar(conexion, true);
            }
        }

        private async Task PresentarSinFallar(IConexionAvisos conexion, bool conFoco)
        {
            try
            {
                _ = await conexion.ActividadEscanerRemoto(Equipo, conFoco).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Una API sin el escáner remoto (o sin conexión): no pasa nada, simplemente no llegan lecturas.
            }
        }

        private void AvisarLectura(LecturaEscanerRemoto lectura)
        {
            if (string.IsNullOrWhiteSpace(lectura?.Codigo))
            {
                return;
            }
            try
            {
                LecturaRecibida?.Invoke(this, lectura);
            }
            catch (Exception)
            {
                // Quien escucha no debe tumbar la conexión.
            }
        }

        /// <summary>Ariadna#51: otro equipo ha cambiado algo de un grupo del almacén. Llega en un hilo que no es el de la UI.</summary>
        public event EventHandler<string> GrupoHaCambiado;

        /// <summary>Ariadna#51: la conexión de SignalR actual (null si no está conectado).</summary>
        public string IdConexion
        {
            get
            {
                try
                {
                    return Volatile.Read(ref _conexionActual)?.IdConexion;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public void EntrarEnGrupo(string grupo)
        {
            string limpio = grupo?.Trim();
            if (string.IsNullOrEmpty(limpio))
            {
                return;
            }
            if (_grupos.AddOrUpdate(limpio, 1, (_, n) => n + 1) == 1)
            {
                IConexionAvisos conexion = Volatile.Read(ref _conexionActual);
                if (conexion != null)
                {
                    _ = EntrarSinFallar(conexion, limpio);
                }
            }
        }

        public void SalirDeGrupo(string grupo)
        {
            string limpio = grupo?.Trim();
            if (string.IsNullOrEmpty(limpio) || !_grupos.TryGetValue(limpio, out int veces))
            {
                return;
            }
            if (veces > 1)
            {
                _grupos[limpio] = veces - 1;
                return;
            }
            _ = _grupos.TryRemove(limpio, out _);
            IConexionAvisos conexion = Volatile.Read(ref _conexionActual);
            if (conexion != null)
            {
                _ = SalirSinFallar(conexion, limpio);
            }
        }

        /// <summary>Para los tests: los grupos en los que se está.</summary>
        internal IReadOnlyCollection<string> Grupos => _grupos.Keys.ToList();

        private static async Task EntrarSinFallar(IConexionAvisos conexion, string grupo)
        {
            try
            {
                _ = await conexion.EntrarEnGrupo(grupo, Environment.MachineName).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // El aviso es un extra: la pantalla también recarga al abrirla y al volver a ella
            }
        }

        private static async Task SalirSinFallar(IConexionAvisos conexion, string grupo)
        {
            try
            {
                await conexion.SalirDeGrupo(grupo).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // nada
            }
        }

        private async Task EntrarEnTodos(IConexionAvisos conexion)
        {
            foreach (string grupo in _grupos.Keys.ToList())
            {
                await EntrarSinFallar(conexion, grupo).ConfigureAwait(false);
            }
        }

        private void AvisarGrupo(string grupo)
        {
            if (string.IsNullOrWhiteSpace(grupo))
            {
                return;
            }
            try
            {
                GrupoHaCambiado?.Invoke(this, grupo.Trim());
            }
            catch (Exception)
            {
                // Quien escucha no debe tumbar la conexión.
            }
        }

        private void AvisarTodosLosGrupos()
        {
            foreach (string grupo in _grupos.Keys.ToList())
            {
                AvisarGrupo(grupo);
            }
        }

        /// <summary>Nesto#492: cambia <see cref="Estado"/>. Llega en un hilo que no es el de la UI.</summary>
        public event EventHandler EstadoCambiado;

        /// <summary>Nesto#492: estado actual de la conexión en tiempo real.</summary>
        public EstadoConexionTiempoReal Estado => (EstadoConexionTiempoReal)Volatile.Read(ref _estado);

        public AvisosEnTiempoRealSignalR(string servidorApi, IServicioAutenticacion autenticacion, Action<Exception> registrarError)
            : this(UrlDelHub(servidorApi), autenticacion, (url, token) => new ConexionAvisosSignalR(url, token),
                  (espera, ct) => Task.Delay(espera, ct), registrarError)
        { }

        internal AvisosEnTiempoRealSignalR(string urlHub, IServicioAutenticacion autenticacion,
            Func<string, string, IConexionAvisos> crearConexion, Func<TimeSpan, CancellationToken, Task> esperar,
            Action<Exception> registrarError)
        {
            _urlHub = urlHub ?? throw new ArgumentNullException(nameof(urlHub));
            _autenticacion = autenticacion ?? throw new ArgumentNullException(nameof(autenticacion));
            _crearConexion = crearConexion ?? throw new ArgumentNullException(nameof(crearConexion));
            _esperar = esperar ?? throw new ArgumentNullException(nameof(esperar));
            _registrarError = registrarError;
        }

        /// <summary>«http://api.nuevavision.es/api/» → «http://api.nuevavision.es/signalr» (el hub cuelga de la raíz del sitio).</summary>
        public static string UrlDelHub(string servidorApi)
        {
            if (string.IsNullOrWhiteSpace(servidorApi))
            {
                throw new ArgumentNullException(nameof(servidorApi));
            }
            return new Uri(new Uri(servidorApi), RUTA_HUB).ToString();
        }

        /// <summary>Espera antes del siguiente intento tras <paramref name="fallosSeguidos"/> fallos: 5 s, 15 s, 30 s, 60 s, 2 min y luego 5 min.</summary>
        public static TimeSpan EsperaTrasFallos(int fallosSeguidos)
        {
            if (fallosSeguidos <= 0)
            {
                return TimeSpan.Zero;
            }
            return ESPERAS[Math.Min(fallosSeguidos, ESPERAS.Length) - 1];
        }

        /// <summary>Arranca la conexión en segundo plano (no espera a que conecte). Si ya estaba arrancada, no hace nada.</summary>
        public void Iniciar()
        {
            lock (_candado)
            {
                if (_bucle != null)
                {
                    return;
                }
                _cancelacion = new CancellationTokenSource();
                CancellationToken ct = _cancelacion.Token;
                CambiarEstado(EstadoConexionTiempoReal.Conectando);
                _bucle = Task.Run(() => Bucle(ct));
            }
        }

        /// <summary>Para la conexión y los reintentos (espera como mucho un par de segundos a que se cierre).</summary>
        public void Detener()
        {
            Task bucle;
            lock (_candado)
            {
                _cancelacion?.Cancel();
                _cancelacion = null;
                bucle = _bucle;
                _bucle = null;
            }
            try
            {
                _ = bucle?.Wait(TimeSpan.FromSeconds(3));
            }
            catch (Exception)
            {
                // nada
            }
            if (bucle != null)
            {
                CambiarEstado(EstadoConexionTiempoReal.Desactivado);
            }
        }

        public void Dispose() => Detener();

        /// <summary>Para los tests: la tarea del bucle de conexión.</summary>
        internal Task BucleEnCurso => _bucle;

        private async Task Bucle(CancellationToken ct)
        {
            int fallos = 0;
            bool avisarAlConectar = false;
            while (!ct.IsCancellationRequested)
            {
                IConexionAvisos conexion = null;
                bool renovarToken = false;
                try
                {
                    string token = await _autenticacion.ObtenerTokenValidoAsync().ConfigureAwait(false);
                    if (string.IsNullOrEmpty(token))
                    {
                        throw new InvalidOperationException("No hay token para conectar a los avisos en tiempo real");
                    }
                    conexion = _crearConexion(_urlHub, token);
                    IConexionAvisos esta = conexion;
                    var cerrada = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    conexion.HayNotificacionesNuevas += (s, e) => Avisar();
                    conexion.GrupoHaCambiado += (s, grupo) => AvisarGrupo(grupo);
                    conexion.LecturaEscanerRemoto += (s, lectura) => AvisarLectura(lectura);
                    conexion.Reconectando += (s, e) => CambiarEstadoSiEsLaActual(esta, EstadoConexionTiempoReal.Reintentando, ct);
                    conexion.Reconectada += (s, e) =>
                    {
                        CambiarEstadoSiEsLaActual(esta, EstadoConexionTiempoReal.Conectado, ct);
                        Avisar();
                        // Ariadna#51: por si la API se ha reciclado y ha olvidado los grupos; y pudo perderse algún aviso
                        _ = EntrarEnTodos(esta).ContinueWith(_ => AvisarTodosLosGrupos(), TaskScheduler.Default);
                        // Nesto#589: por si la API se ha reciclado y ha olvidado que este Nesto recibe lecturas
                        _ = PresentarSinFallar(esta, _conFoco);
                    };
                    conexion.Cerrada += (s, e) => cerrada.TrySetResult(true);

                    await conexion.Iniciar().ConfigureAwait(false);
                    Volatile.Write(ref _conexionActual, conexion);
                    if (!ct.IsCancellationRequested)
                    {
                        CambiarEstado(EstadoConexionTiempoReal.Conectado);
                    }
                    fallos = 0;
                    // Ariadna#51: en cada conexión nueva se vuelve a entrar en los grupos del almacén
                    await EntrarEnTodos(conexion).ConfigureAwait(false);
                    // Nesto#589: en cada conexión nueva, Nesto se presenta para el escáner remoto
                    await PresentarSinFallar(conexion, _conFoco).ConfigureAwait(false);
                    if (avisarAlConectar)
                    {
                        Avisar();
                        AvisarTodosLosGrupos();
                    }
                    avisarAlConectar = true;

                    renovarToken = await EsperarCierreOTokenNuevo(cerrada.Task, token, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // Se está deteniendo.
                }
                catch (Exception ex)
                {
                    fallos++;
                    RegistrarSiDura(ex, fallos);
                }
                finally
                {
                    Volatile.Write(ref _conexionActual, null);
                    DetenerConexion(conexion);
                }

                if (ct.IsCancellationRequested)
                {
                    break;
                }
                if (renovarToken)
                {
                    // Se reconecta enseguida con el token nuevo; no se ha perdido nada (el estado no cambia).
                    avisarAlConectar = false;
                    continue;
                }
                // Nesto#492: no conectó o se cerró: se va a reintentar tras la espera.
                CambiarEstado(EstadoConexionTiempoReal.Reintentando);
                if (fallos == 0)
                {
                    // Estaba conectada y se cerró: cuenta como fallo para espaciar el siguiente intento.
                    fallos = 1;
                }
                try
                {
                    await _esperar(EsperaTrasFallos(fallos), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>Devuelve true si hay que reconectar con un token nuevo; false si la conexión se cerró.</summary>
        private async Task<bool> EsperarCierreOTokenNuevo(Task cerrada, string tokenConectado, CancellationToken ct)
        {
            while (true)
            {
                Task espera = _esperar(INTERVALO_REVISION_TOKEN, ct);
                Task primera = await Task.WhenAny(cerrada, espera).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                if (primera == cerrada)
                {
                    return false;
                }
                string token = null;
                try
                {
                    token = await _autenticacion.ObtenerTokenValidoAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Sin token nuevo se sigue con la conexión que hay.
                }
                if (!string.IsNullOrEmpty(token) && token != tokenConectado)
                {
                    return true;
                }
            }
        }

        private void Avisar()
        {
            try
            {
                HayNotificacionesNuevas?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // Quien escucha no debe tumbar la conexión.
            }
        }

        private void CambiarEstadoSiEsLaActual(IConexionAvisos conexion, EstadoConexionTiempoReal nuevo, CancellationToken ct)
        {
            // Una conexión ya sustituida o que se está cerrando no debe pisar el estado de la actual.
            if (!ct.IsCancellationRequested && ReferenceEquals(Volatile.Read(ref _conexionActual), conexion))
            {
                CambiarEstado(nuevo);
            }
        }

        private void CambiarEstado(EstadoConexionTiempoReal nuevo)
        {
            if (Interlocked.Exchange(ref _estado, (int)nuevo) == (int)nuevo)
            {
                return;
            }
            try
            {
                EstadoCambiado?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // Quien escucha no debe tumbar la conexión.
            }
        }

        private void RegistrarSiDura(Exception ex, int fallos)
        {
            if (_errorRegistrado || EsperaTrasFallos(fallos) < ESPERA_MAXIMA || _registrarError == null)
            {
                return;
            }
            _errorRegistrado = true;
            try
            {
                _registrarError(new Exception($"[SignalR] Los avisos en tiempo real no conectan tras {fallos} intentos (la campana sigue con su sondeo): {ex.Message}", ex));
            }
            catch (Exception)
            {
                // nada
            }
        }

        private static void DetenerConexion(IConexionAvisos conexion)
        {
            if (conexion == null)
            {
                return;
            }
            try
            {
                conexion.Dispose();
            }
            catch (Exception)
            {
                // nada
            }
        }
    }

    /// <summary>NestoAPI#536: la conexión con el hub de avisos, tras una interfaz pequeña para poder falsearla en los tests.</summary>
    public interface IConexionAvisos : IDisposable
    {
        /// <summary>El servidor ha llamado a «hayNotificacionesNuevas».</summary>
        event EventHandler HayNotificacionesNuevas;
        /// <summary>Nesto#492: SignalR ha perdido la conexión y está intentando recuperarla por su cuenta.</summary>
        event EventHandler Reconectando;
        /// <summary>SignalR ha recuperado la conexión por su cuenta (pudo perderse algún aviso).</summary>
        event EventHandler Reconectada;
        /// <summary>La conexión se ha cerrado del todo (SignalR ya no reintenta).</summary>
        event EventHandler Cerrada;
        /// <summary>Ariadna#51: el servidor ha llamado a «haCambiado(grupo, dispositivo)» del hub del almacén (el argumento es el grupo).</summary>
        event EventHandler<string> GrupoHaCambiado;
        /// <summary>Ariadna#51: la conexión de SignalR (null antes de conectar).</summary>
        string IdConexion { get; }
        Task Iniciar();
        /// <summary>Ariadna#51: AlmacenHub.Entrar(grupo, dispositivo). False si el servidor no admite el grupo.</summary>
        Task<bool> EntrarEnGrupo(string grupo, string dispositivo);
        /// <summary>Ariadna#51: AlmacenHub.Salir(grupo).</summary>
        Task SalirDeGrupo(string grupo);
        /// <summary>Nesto#589: el servidor ha llamado a «lecturaEscanerRemoto(lectura)» (lo leído en Ariadna por el mismo usuario).</summary>
        event EventHandler<LecturaEscanerRemoto> LecturaEscanerRemoto;
        /// <summary>Nesto#589: AvisosHub.ActividadEscanerRemoto(equipo, conFoco). False si el servidor no lo admite.</summary>
        Task<bool> ActividadEscanerRemoto(string equipo, bool conFoco);
    }

    /// <summary>NestoAPI#536: <see cref="IConexionAvisos"/> con el cliente de ASP.NET SignalR 2.</summary>
    internal sealed class ConexionAvisosSignalR : IConexionAvisos
    {
        internal const string HUB = "AvisosHub";
        internal const string METODO_AVISO = "hayNotificacionesNuevas";

        // Ariadna#51 (corte 2): el hub del almacén de NestoAPI#646, el mismo que usa Ariadna. Si la API cambia el nombre del
        // aviso de las recepciones en curso, se cambia solo aquí.
        internal const string HUB_ALMACEN = "AlmacenHub";
        internal const string METODO_ENTRAR = "Entrar";
        internal const string METODO_SALIR = "Salir";
        /// <summary>«haCambiado(grupo, dispositivo)»: otro equipo ha cambiado algo del grupo (sin datos).</summary>
        internal const string METODO_HA_CAMBIADO = "haCambiado";

        // Nesto#589 (NestoAPI#768): el escáner remoto, en el hub de la campana
        internal const string METODO_LECTURA_ESCANER = "lecturaEscanerRemoto";
        internal const string METODO_ACTIVIDAD_ESCANER = "ActividadEscanerRemoto";

        private readonly HubConnection _conexion;
        private readonly IHubProxy _avisos;
        private readonly IHubProxy _almacen;

        public event EventHandler HayNotificacionesNuevas;
        public event EventHandler<string> GrupoHaCambiado;
        public event EventHandler<LecturaEscanerRemoto> LecturaEscanerRemoto;
        public event EventHandler Reconectando;
        public event EventHandler Reconectada;
        public event EventHandler Cerrada;

        public ConexionAvisosSignalR(string urlHub, string token)
        {
            _conexion = new HubConnection(urlHub, new Dictionary<string, string> { { "access_token", token } }, useDefaultUrl: false);
            IHubProxy proxy = _conexion.CreateHubProxy(HUB);
            _avisos = proxy;
            _ = proxy.On(METODO_AVISO, () => HayNotificacionesNuevas?.Invoke(this, EventArgs.Empty));
            _ = proxy.On<LecturaEscanerRemoto>(METODO_LECTURA_ESCANER, lectura => LecturaEscanerRemoto?.Invoke(this, lectura));
            // Ariadna#51: el proxy se crea antes de Start (SignalR 2 se suscribe a los hubs al conectar)
            _almacen = _conexion.CreateHubProxy(HUB_ALMACEN);
            _ = _almacen.On<string, string>(METODO_HA_CAMBIADO, (grupo, dispositivo) => GrupoHaCambiado?.Invoke(this, grupo));
            _conexion.Reconnecting += () => Reconectando?.Invoke(this, EventArgs.Empty);
            _conexion.Reconnected += () => Reconectada?.Invoke(this, EventArgs.Empty);
            _conexion.Closed += () => Cerrada?.Invoke(this, EventArgs.Empty);
        }

        public Task Iniciar() => _conexion.Start();

        public string IdConexion => _conexion.ConnectionId;

        public Task<bool> EntrarEnGrupo(string grupo, string dispositivo) => _almacen.Invoke<bool>(METODO_ENTRAR, grupo, dispositivo);

        public Task SalirDeGrupo(string grupo) => _almacen.Invoke(METODO_SALIR, grupo);

        public Task<bool> ActividadEscanerRemoto(string equipo, bool conFoco) => _avisos.Invoke<bool>(METODO_ACTIVIDAD_ESCANER, equipo, conFoco);

        public void Dispose()
        {
            // Stop con tope: al cerrar Nesto no se quiere esperar a un servidor que no contesta.
            _conexion.Stop(TimeSpan.FromSeconds(2));
            _conexion.Dispose();
        }
    }
}
