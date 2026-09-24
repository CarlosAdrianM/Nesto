using Microsoft.AspNet.SignalR.Client;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
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
    /// El evento <see cref="HayNotificacionesNuevas"/> llega en un hilo que no es el de la UI.
    /// </summary>
    public sealed class AvisosEnTiempoRealSignalR : IAvisosEnTiempoReal, IDisposable
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

        public event EventHandler HayNotificacionesNuevas;

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
                    var cerrada = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    conexion.HayNotificacionesNuevas += (s, e) => Avisar();
                    conexion.Reconectada += (s, e) => Avisar();
                    conexion.Cerrada += (s, e) => cerrada.TrySetResult(true);

                    await conexion.Iniciar().ConfigureAwait(false);
                    fallos = 0;
                    if (avisarAlConectar)
                    {
                        Avisar();
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
                    DetenerConexion(conexion);
                }

                if (ct.IsCancellationRequested)
                {
                    break;
                }
                if (renovarToken)
                {
                    // Se reconecta enseguida con el token nuevo; no se ha perdido nada.
                    avisarAlConectar = false;
                    continue;
                }
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
        /// <summary>SignalR ha recuperado la conexión por su cuenta (pudo perderse algún aviso).</summary>
        event EventHandler Reconectada;
        /// <summary>La conexión se ha cerrado del todo (SignalR ya no reintenta).</summary>
        event EventHandler Cerrada;
        Task Iniciar();
    }

    /// <summary>NestoAPI#536: <see cref="IConexionAvisos"/> con el cliente de ASP.NET SignalR 2.</summary>
    internal sealed class ConexionAvisosSignalR : IConexionAvisos
    {
        internal const string HUB = "AvisosHub";
        internal const string METODO_AVISO = "hayNotificacionesNuevas";

        private readonly HubConnection _conexion;

        public event EventHandler HayNotificacionesNuevas;
        public event EventHandler Reconectada;
        public event EventHandler Cerrada;

        public ConexionAvisosSignalR(string urlHub, string token)
        {
            _conexion = new HubConnection(urlHub, new Dictionary<string, string> { { "access_token", token } }, useDefaultUrl: false);
            IHubProxy proxy = _conexion.CreateHubProxy(HUB);
            _ = proxy.On(METODO_AVISO, () => HayNotificacionesNuevas?.Invoke(this, EventArgs.Empty));
            _conexion.Reconnected += () => Reconectada?.Invoke(this, EventArgs.Empty);
            _conexion.Closed += () => Cerrada?.Invoke(this, EventArgs.Empty);
        }

        public Task Iniciar() => _conexion.Start();

        public void Dispose()
        {
            // Stop con tope: al cerrar Nesto no se quiere esperar a un servidor que no contesta.
            _conexion.Stop(TimeSpan.FromSeconds(2));
            _conexion.Dispose();
        }
    }
}
