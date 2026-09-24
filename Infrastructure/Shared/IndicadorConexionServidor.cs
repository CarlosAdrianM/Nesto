using Nesto.Infrastructure.Contracts;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>Nesto#492: cómo se pinta la raya de la ventana principal.</summary>
    public enum NivelConexionServidor
    {
        /// <summary>Verde: sesión válida y conexión en tiempo real establecida (o tiempo real desactivado).</summary>
        Correcto,
        /// <summary>Ámbar: hay sesión, pero la conexión en tiempo real con el servidor no está establecida.</summary>
        Aviso,
        /// <summary>Rojo: no hay sesión válida con el servidor.</summary>
        Error
    }

    /// <summary>Nesto#492: nivel y texto (tooltip) del indicador.</summary>
    public readonly struct EstadoIndicadorConexion
    {
        public EstadoIndicadorConexion(NivelConexionServidor nivel, string descripcion)
        {
            Nivel = nivel;
            Descripcion = descripcion;
        }

        public NivelConexionServidor Nivel { get; }
        public string Descripcion { get; }
    }

    /// <summary>
    /// Nesto#492: estado de la conexión con el servidor que pinta la raya bajo el nombre del usuario.
    /// Sin sondeos: se recalcula cuando cambia el token (<see cref="IServicioAutenticacion.TokenCambiado"/>),
    /// cuando cambia la conexión en tiempo real (<see cref="IAvisosEnTiempoReal.EstadoCambiado"/>) y con UN
    /// único temporizador programado para el momento en que el token deja de ser válido; en ese momento se
    /// intenta además renovarlo, para que la raya no se ponga roja solo porque no se ha usado el servidor.
    /// Los eventos llegan en cualquier hilo: todo cambio pasa por <c>enHiloUI</c> (el Dispatcher en Nesto).
    /// </summary>
    public sealed class IndicadorConexionServidor : INotifyPropertyChanged, IDisposable
    {
        public const string TEXTO_CONECTADO = "Conectado con el servidor";
        public const string TEXTO_SIN_TIEMPO_REAL = "Conectado con el servidor (avisos en tiempo real desactivados)";
        public const string TEXTO_CONECTANDO = "Conectando con el servidor…";
        public const string TEXTO_REINTENTANDO = "Sin conexión con el servidor: reintentando…";
        public const string TEXTO_SIN_SESION = "Sin sesión con el servidor";
        public const string TEXTO_SESION_CADUCADA = "Sesión caducada con el servidor";

        /// <summary>System.Threading.Timer no admite esperas de más de ~49 días; si el token durase más, se reprograma al vencer.</summary>
        internal static readonly TimeSpan ESPERA_MAXIMA_TEMPORIZADOR = TimeSpan.FromDays(24);
        /// <summary>Se programa un poco después de la caducidad para que al saltar el token ya no sea válido.</summary>
        internal static readonly TimeSpan HOLGURA_TEMPORIZADOR = TimeSpan.FromSeconds(1);

        private readonly IServicioAutenticacion _autenticacion;
        private readonly IAvisosEnTiempoReal _avisos;
        private readonly Action<Action> _enHiloUI;
        private readonly Func<DateTime> _ahoraUtc;
        private readonly Func<TimeSpan, Action, IDisposable> _programar;
        private IDisposable _temporizador;
        private DateTime? _programadoPara;
        private bool _iniciado;
        private bool _liberado;
        private NivelConexionServidor _nivel = NivelConexionServidor.Error;
        private string _descripcion = TEXTO_SIN_SESION;

        public event PropertyChangedEventHandler PropertyChanged;

        public IndicadorConexionServidor(IServicioAutenticacion autenticacion, IAvisosEnTiempoReal avisos, Action<Action> enHiloUI)
            : this(autenticacion, avisos, enHiloUI, () => DateTime.UtcNow, ProgramarConTimer)
        { }

        /// <param name="enHiloUI">Ejecuta la acción en el hilo de la interfaz (en los tests, directamente).</param>
        /// <param name="programar">Programa una acción una sola vez tras la espera; el IDisposable la cancela.</param>
        public IndicadorConexionServidor(IServicioAutenticacion autenticacion, IAvisosEnTiempoReal avisos, Action<Action> enHiloUI,
            Func<DateTime> ahoraUtc, Func<TimeSpan, Action, IDisposable> programar)
        {
            _autenticacion = autenticacion;
            _avisos = avisos ?? new AvisosEnTiempoRealNulo();
            _enHiloUI = enHiloUI ?? throw new ArgumentNullException(nameof(enHiloUI));
            _ahoraUtc = ahoraUtc ?? throw new ArgumentNullException(nameof(ahoraUtc));
            _programar = programar ?? throw new ArgumentNullException(nameof(programar));
        }

        public NivelConexionServidor Nivel
        {
            get => _nivel;
            private set
            {
                if (_nivel != value)
                {
                    _nivel = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Nivel)));
                }
            }
        }

        /// <summary>Texto para el usuario (tooltip de la raya).</summary>
        public string Descripcion
        {
            get => _descripcion;
            private set
            {
                if (_descripcion != value)
                {
                    _descripcion = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Descripcion)));
                }
            }
        }

        /// <summary>
        /// Qué se pinta según el token y la conexión en tiempo real:
        /// - Sin token válido: rojo («Sin sesión» si no lo hay, «Sesión caducada» si venció).
        /// - Con token y tiempo real conectado o desactivado (parámetro AvisosTiempoReal = NO): verde.
        /// - Con token y tiempo real conectando o reintentando: ámbar.
        /// </summary>
        public static EstadoIndicadorConexion Calcular(DateTime? tokenValidoHastaUtc, DateTime ahoraUtc, EstadoConexionTiempoReal estadoTiempoReal)
        {
            if (!tokenValidoHastaUtc.HasValue)
            {
                return new EstadoIndicadorConexion(NivelConexionServidor.Error, TEXTO_SIN_SESION);
            }
            if (tokenValidoHastaUtc.Value <= ahoraUtc)
            {
                return new EstadoIndicadorConexion(NivelConexionServidor.Error, TEXTO_SESION_CADUCADA);
            }
            switch (estadoTiempoReal)
            {
                case EstadoConexionTiempoReal.Conectado:
                    return new EstadoIndicadorConexion(NivelConexionServidor.Correcto, TEXTO_CONECTADO);
                case EstadoConexionTiempoReal.Conectando:
                    return new EstadoIndicadorConexion(NivelConexionServidor.Aviso, TEXTO_CONECTANDO);
                case EstadoConexionTiempoReal.Reintentando:
                    return new EstadoIndicadorConexion(NivelConexionServidor.Aviso, TEXTO_REINTENTANDO);
                default:
                    return new EstadoIndicadorConexion(NivelConexionServidor.Correcto, TEXTO_SIN_TIEMPO_REAL);
            }
        }

        /// <summary>Se suscribe a los eventos y calcula el estado inicial. Llamar desde el hilo de la interfaz.</summary>
        public void Iniciar()
        {
            if (_iniciado || _liberado)
            {
                return;
            }
            _iniciado = true;
            if (_autenticacion != null)
            {
                _autenticacion.TokenCambiado += AlCambiarAlgo;
            }
            _avisos.EstadoCambiado += AlCambiarAlgo;
            Recalcular();
        }

        public void Dispose()
        {
            if (_liberado)
            {
                return;
            }
            _liberado = true;
            if (_autenticacion != null)
            {
                _autenticacion.TokenCambiado -= AlCambiarAlgo;
            }
            _avisos.EstadoCambiado -= AlCambiarAlgo;
            CancelarTemporizador();
        }

        private void AlCambiarAlgo(object sender, EventArgs e) => _enHiloUI(Recalcular);

        private void Recalcular()
        {
            if (_liberado)
            {
                return;
            }
            DateTime? hasta = _autenticacion?.TokenValidoHastaUtc;
            DateTime ahora = _ahoraUtc();
            EstadoIndicadorConexion estado = Calcular(hasta, ahora, _avisos.Estado);
            Nivel = estado.Nivel;
            Descripcion = estado.Descripcion;
            ProgramarCaducidad(hasta, ahora);
        }

        private void ProgramarCaducidad(DateTime? hasta, DateTime ahora)
        {
            if (!hasta.HasValue || hasta.Value <= ahora)
            {
                CancelarTemporizador();
                return;
            }
            if (_temporizador != null && _programadoPara == hasta)
            {
                return;
            }
            CancelarTemporizador();
            TimeSpan espera = hasta.Value - ahora + HOLGURA_TEMPORIZADOR;
            if (espera > ESPERA_MAXIMA_TEMPORIZADOR)
            {
                espera = ESPERA_MAXIMA_TEMPORIZADOR;
            }
            _programadoPara = hasta;
            _temporizador = _programar(espera, () => _enHiloUI(AlVencerElToken));
        }

        private void CancelarTemporizador()
        {
            _temporizador?.Dispose();
            _temporizador = null;
            _programadoPara = null;
        }

        private void AlVencerElToken()
        {
            if (_liberado)
            {
                return;
            }
            CancelarTemporizador();
            Recalcular();
            if (Nivel == NivelConexionServidor.Error && _autenticacion != null)
            {
                _ = RenovarTokenAsync();
            }
        }

        /// <summary>Al vencer, se pide un token nuevo; si llega, <see cref="IServicioAutenticacion.TokenCambiado"/> vuelve a pintar la raya.</summary>
        private async Task RenovarTokenAsync()
        {
            try
            {
                _ = await _autenticacion.ObtenerTokenValidoAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Sin servidor se queda en rojo; la siguiente operación (o la conexión en tiempo real) lo reintentará.
            }
        }

        private static IDisposable ProgramarConTimer(TimeSpan espera, Action accion)
        {
            return new Timer(_ => accion(), null, espera, Timeout.InfiniteTimeSpan);
        }
    }
}
