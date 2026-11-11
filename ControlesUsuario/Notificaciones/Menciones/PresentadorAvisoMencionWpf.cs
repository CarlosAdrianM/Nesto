using System;
using System.Windows;
using System.Windows.Threading;

namespace ControlesUsuario.Notificaciones.Menciones
{
    /// <summary>
    /// Sugerencia 1183: la ventanita de «le han mencionado» de verdad. Una sola a la vez: si llega otra con una abierta, se sustituye
    /// su contenido y vuelve a contar el tiempo (no se apilan). Arriba a la derecha del área de trabajo, debajo de la cabecera de Nesto
    /// (donde está la campana) y lejos de la esquina de abajo, que es la de las ventanas rojas de las urgentes. Sin sonido, sin dueño
    /// (se ve aunque Nesto esté detrás), no se activa al abrirse y se cierra sola a los <see cref="DURACION"/> (mientras el ratón esté
    /// encima, espera).
    /// </summary>
    public sealed class PresentadorAvisoMencionWpf : IPresentadorAvisoMencion
    {
        public static readonly TimeSpan DURACION = TimeSpan.FromSeconds(9);
        private const double MARGEN_DERECHO = 8;
        private const double MARGEN_SUPERIOR = 64;

        private AvisoMencionWindow _ventana;
        private DispatcherTimer _temporizador;

        public void Mostrar(AvisoMencion aviso)
        {
            if (aviso == null)
            {
                return;
            }
            Dispatcher dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }
            if (!dispatcher.CheckAccess())
            {
                _ = dispatcher.InvokeAsync(() => Mostrar(aviso));
                return;
            }
            try
            {
                if (_ventana == null)
                {
                    CrearVentana();
                }
                _ventana.DataContext = aviso;
                if (!_ventana.IsVisible)
                {
                    _ventana.Left = -10000; // fuera de la vista hasta saber su alto
                    _ventana.Show();
                }
                Colocar();
                ReiniciarTemporizador();
            }
            catch (Exception)
            {
                // La ventanita no puede romper nada: la mención sigue en la campana.
                Cerrar();
            }
        }

        private void CrearVentana()
        {
            var ventana = new AvisoMencionWindow { WindowStartupLocation = WindowStartupLocation.Manual };
            ventana.Pulsada += OnPulsada;
            ventana.CerradaAMano += OnCerradaAMano;
            ventana.SizeChanged += OnSizeChanged;
            ventana.MouseEnter += OnMouseEnter;
            ventana.MouseLeave += OnMouseLeave;
            ventana.Closed += OnClosed;
            _ventana = ventana;
            _temporizador = new DispatcherTimer { Interval = DURACION };
            _temporizador.Tick += OnTick;
            // Sin dueño: si se cierra Nesto con la ventanita abierta, se cierra con él (si no, Nesto seguiría vivo hasta que se cerrara)
            _principal = Application.Current?.MainWindow;
            if (_principal != null)
            {
                _principal.Closed += OnPrincipalCerrada;
            }
        }

        private Window _principal;

        private void OnPrincipalCerrada(object sender, EventArgs e) => Cerrar();

        private void OnPulsada(object sender, EventArgs e)
        {
            Action alPulsar = (_ventana?.DataContext as AvisoMencion)?.AlPulsar;
            Cerrar();
            try
            {
                alPulsar?.Invoke();
            }
            catch (Exception)
            {
                // Lo mismo que la campana: si no se puede abrir, la notificación sigue ahí.
            }
        }

        private void OnCerradaAMano(object sender, EventArgs e) => Cerrar();

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Colocar();

        private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => _temporizador?.Stop();

        private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e) => ReiniciarTemporizador();

        private void OnTick(object sender, EventArgs e) => Cerrar();

        private void ReiniciarTemporizador()
        {
            if (_temporizador == null)
            {
                return;
            }
            _temporizador.Stop();
            _temporizador.Start();
        }

        private void Colocar()
        {
            if (_ventana == null)
            {
                return;
            }
            Rect area = SystemParameters.WorkArea;
            double ancho = _ventana.ActualWidth > 0 ? _ventana.ActualWidth : _ventana.Width;
            _ventana.Left = area.Right - ancho - MARGEN_DERECHO;
            _ventana.Top = area.Top + MARGEN_SUPERIOR;
        }

        private void Cerrar()
        {
            AvisoMencionWindow ventana = _ventana;
            if (ventana == null)
            {
                return;
            }
            try
            {
                ventana.Close(); // OnClosed suelta los eventos y el temporizador
            }
            catch (Exception)
            {
                OnClosed(ventana, EventArgs.Empty);
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (sender is AvisoMencionWindow ventana)
            {
                ventana.Pulsada -= OnPulsada;
                ventana.CerradaAMano -= OnCerradaAMano;
                ventana.SizeChanged -= OnSizeChanged;
                ventana.MouseEnter -= OnMouseEnter;
                ventana.MouseLeave -= OnMouseLeave;
                ventana.Closed -= OnClosed;
            }
            if (_principal != null)
            {
                _principal.Closed -= OnPrincipalCerrada;
                _principal = null;
            }
            if (_temporizador != null)
            {
                _temporizador.Stop();
                _temporizador.Tick -= OnTick;
                _temporizador = null;
            }
            if (ReferenceEquals(sender, _ventana))
            {
                _ventana = null;
            }
        }
    }
}
