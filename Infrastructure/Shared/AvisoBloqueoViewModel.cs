using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Input;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Aviso vertical de bloqueos (13/11/26): lo que enseña la banda de la ventana principal cuando la API dice que la base de
    /// datos está bloqueada (<see cref="CanalAvisosBloqueo"/>). Vale para todas las pantallas y módulos: ningún ViewModel de
    /// módulo tiene que hacer nada.
    /// <list type="bullet">
    /// <item>Una sola banda: si llega otro diagnóstico con la banda a la vista, se cambia el texto, no se abre otra.</item>
    /// <item>No es modal ni roba el foco. Se cierra sola a los <see cref="DURACION"/> del último aviso, o con su botón.</item>
    /// <item>Los avisos llegan en hilos de fondo: todo cambio pasa por <c>enHiloUI</c> (el Dispatcher en Nesto).</item>
    /// </list>
    /// Sin tipos de vista.
    /// </summary>
    public sealed class AvisoBloqueoViewModel : INotifyPropertyChanged, IDisposable
    {
        public const string TITULO = "La base de datos está ocupada.";

        /// <summary>Lo que dura a la vista desde el último aviso.</summary>
        public static readonly TimeSpan DURACION = TimeSpan.FromSeconds(45);

        private readonly CanalAvisosBloqueo _canal;
        private readonly Action<Action> _enHiloUI;
        private readonly Func<TimeSpan, Action, IDisposable> _programar;
        private IDisposable _temporizador;
        private bool _iniciado;
        private bool _liberado;
        private bool _visible;
        private int _turno;
        private string _detalle = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;

        public AvisoBloqueoViewModel(Action<Action> enHiloUI)
            : this(CanalAvisosBloqueo.Comun, enHiloUI, ProgramarConTimer)
        {
        }

        /// <param name="enHiloUI">Ejecuta la acción en el hilo de la interfaz (en los tests, directamente).</param>
        /// <param name="programar">Programa una acción una sola vez tras la espera; el IDisposable la cancela.</param>
        public AvisoBloqueoViewModel(CanalAvisosBloqueo canal, Action<Action> enHiloUI, Func<TimeSpan, Action, IDisposable> programar)
        {
            _canal = canal ?? throw new ArgumentNullException(nameof(canal));
            _enHiloUI = enHiloUI ?? throw new ArgumentNullException(nameof(enHiloUI));
            _programar = programar ?? throw new ArgumentNullException(nameof(programar));
            CerrarCommand = new RelayCommand(Cerrar);
        }

        public string Titulo => TITULO;

        /// <summary>El diagnóstico de la API, tal cual («Puede estar bloqueado por: …»).</summary>
        public string Detalle
        {
            get => _detalle;
            private set
            {
                if (_detalle != value)
                {
                    _detalle = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detalle)));
                }
            }
        }

        /// <summary>La banda está a la vista.</summary>
        public bool Visible
        {
            get => _visible;
            private set
            {
                if (_visible != value)
                {
                    _visible = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Visible)));
                }
            }
        }

        public ICommand CerrarCommand { get; }

        /// <summary>Empieza a escuchar el canal. La segunda vez no hace nada.</summary>
        public void Iniciar()
        {
            if (_iniciado || _liberado)
            {
                return;
            }
            _iniciado = true;
            _canal.BloqueoDetectado += AlDetectarBloqueo;
        }

        public void Dispose()
        {
            if (_liberado)
            {
                return;
            }
            _liberado = true;
            _canal.BloqueoDetectado -= AlDetectarBloqueo;
            CancelarTemporizador();
        }

        /// <summary>La quita de la vista (el botón, o el temporizador). En el hilo de la interfaz.</summary>
        public void Cerrar()
        {
            CancelarTemporizador();
            Visible = false;
        }

        // Llega en el hilo del manejador HTTP (de fondo)
        private void AlDetectarBloqueo(object sender, AvisoBloqueoEventArgs e)
        {
            string diagnostico = e?.Diagnostico;
            if (string.IsNullOrWhiteSpace(diagnostico))
            {
                return;
            }
            _enHiloUI(() => Mostrar(diagnostico));
        }

        private void Mostrar(string diagnostico)
        {
            if (_liberado)
            {
                return;
            }
            Detalle = diagnostico;
            Visible = true;
            CancelarTemporizador();
            int turno = ++_turno;
            _temporizador = _programar(DURACION, () => _enHiloUI(() => AlVencer(turno)));
        }

        // El temporizador de un aviso anterior, que ya estaba en camino al cancelarlo, no cierra el aviso nuevo
        private void AlVencer(int turno)
        {
            if (!_liberado && turno == _turno)
            {
                Cerrar();
            }
        }

        private void CancelarTemporizador()
        {
            _temporizador?.Dispose();
            _temporizador = null;
        }

        private static IDisposable ProgramarConTimer(TimeSpan espera, Action accion)
        {
            return new Timer(_ => accion(), null, espera, Timeout.InfiniteTimeSpan);
        }
    }
}
