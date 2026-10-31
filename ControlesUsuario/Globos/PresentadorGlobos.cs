using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ControlesUsuario.Globos
{
    /// <summary>
    /// Nesto#592 (NestoAPI#774): pinta los globos. UNA sola pieza para toda la aplicación (no se toca cada vista): escucha la carga
    /// de todas las vistas (UserControl cuyo tipo acaba en «View», y la ventana principal) y, al cargarse una, le pregunta a
    /// <see cref="IServicioGlobos"/> qué toca en esa pantalla. Cada globo es un Popup con flecha que apunta a su control (el de
    /// <see cref="Globo.AnclaProperty"/>) o, si no está a la vista, una tarjeta centrada en la ventana. De uno en uno:
    /// «Siguiente» / «Entendido» (los marca vistos) y Esc (lo deja para la próxima vez, sin marcar).
    /// </summary>
    public static class PresentadorGlobos
    {
        private static IServicioGlobos _servicio;
        private static Tanda _actual;

        /// <summary>Una vez, al arrancar, con el shell ya creado.</summary>
        public static void Iniciar(IServicioGlobos servicio)
        {
            if (_servicio != null || servicio == null)
            {
                return;
            }
            _servicio = servicio;
            EventManager.RegisterClassHandler(typeof(UserControl), FrameworkElement.LoadedEvent, new RoutedEventHandler(AlCargarVista));
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(AlCargarVista));
            // La ventana principal ya estaba cargada (o se carga ahora): se mira una vez
            if (Application.Current?.MainWindow is Window principal && principal.IsLoaded)
            {
                Mirar(principal);
            }
        }

        private static void AlCargarVista(object sender, RoutedEventArgs e)
        {
            // Solo el propio elemento (Loaded se propaga a los hijos con su sender)
            if (ReferenceEquals(sender, e.OriginalSource) && sender is FrameworkElement vista)
            {
                Mirar(vista);
            }
        }

        private static async void Mirar(FrameworkElement vista)
        {
            try
            {
                string pantalla = ClavesPantallaGlobos.DeTipo(vista.GetType().Name, Globo.GetPantalla(vista));
                if (pantalla == null || _servicio == null || _actual != null)
                {
                    return;
                }
                // Que la vista termine de pintarse antes de mirar sus anclas
                await vista.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                if (!vista.IsLoaded)
                {
                    return;
                }
                IReadOnlyList<GloboAEnsenar> tanda = await _servicio.QueEnsenarAsync(pantalla, a => RegistroAnclas.Visible(a) != null);
                if (tanda.Count == 0)
                {
                    return;
                }
                if (_actual != null || !vista.IsLoaded)
                {
                    foreach (GloboAEnsenar g in tanda)
                    {
                        _servicio.Soltar(g.Globo);
                    }
                    return;
                }
                _actual = new Tanda(_servicio, tanda, vista, () => _actual = null);
                _actual.Enseñar();
            }
            catch (Exception ex)
            {
                // Un globo nunca rompe una pantalla
                Debug.WriteLine($"[PresentadorGlobos] {ex.Message}");
            }
        }

        /// <summary>Los globos seguidos de una pantalla, de uno en uno.</summary>
        private sealed class Tanda
        {
            private readonly IServicioGlobos _servicio;
            private readonly IReadOnlyList<GloboAEnsenar> _globos;
            private readonly FrameworkElement _vista;
            private readonly Action _alTerminar;
            private readonly Window _ventana;
            private Popup _popup;
            private int _indice;
            private bool _terminada;

            public Tanda(IServicioGlobos servicio, IReadOnlyList<GloboAEnsenar> globos, FrameworkElement vista, Action alTerminar)
            {
                _servicio = servicio;
                _globos = globos;
                _vista = vista;
                _alTerminar = alTerminar;
                _ventana = Window.GetWindow(vista) ?? Application.Current?.MainWindow;
            }

            public void Enseñar()
            {
                _vista.Unloaded += AlDescargarVista;
                if (_ventana != null)
                {
                    _ventana.PreviewKeyDown += AlPulsarEnVentana;
                    _ventana.Deactivated += AlDesactivarVentana;
                    _ventana.Activated += AlActivarVentana;
                    _ventana.LocationChanged += AlMoverVentana;
                    _ventana.SizeChanged += AlMoverVentana;
                }
                EnseñarActual();
            }

            private void EnseñarActual()
            {
                CerrarPopup();
                GloboAEnsenar actual = _globos[_indice];
                FrameworkElement ancla = actual.Centrado ? null : RegistroAnclas.Visible(actual.Globo.Ancla);
                bool ultimo = _indice == _globos.Count - 1;
                bool arriba = ancla != null && VaArriba(ancla);
                var contenido = new VistaGlobo(actual.Globo, ancla == null ? FlechaGlobo.Ninguna : arriba ? FlechaGlobo.Abajo : FlechaGlobo.Arriba,
                    _globos.Count > 1 ? $"{_indice + 1} de {_globos.Count}" : null, ultimo ? "Entendido" : "Siguiente");
                contenido.Aceptado += (_, __) => Avanzar();
                contenido.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Escape)
                    {
                        e.Handled = true;
                        Aplazar();
                    }
                };
                _popup = new Popup
                {
                    Child = contenido,
                    AllowsTransparency = true,
                    StaysOpen = true,
                    PopupAnimation = PopupAnimation.Fade,
                    PlacementTarget = (UIElement)ancla ?? _ventana,
                    Placement = ancla == null ? PlacementMode.Center : arriba ? PlacementMode.Top : PlacementMode.Bottom,
                    // La punta de la flecha, hacia el centro del control (en uno muy ancho, no más allá de 120 px)
                    HorizontalOffset = ancla == null ? 0 : (Math.Min(ancla.ActualWidth, 240) / 2) - VistaGlobo.POSICION_FLECHA,
                };
                _popup.IsOpen = _ventana == null || _ventana.IsActive;
            }

            // Si el control está en la mitad de abajo de la ventana, el globo va encima (flecha hacia abajo)
            private bool VaArriba(FrameworkElement ancla)
            {
                try
                {
                    if (_ventana == null)
                    {
                        return false;
                    }
                    Point arribaIzquierda = ancla.TransformToAncestor(_ventana).Transform(new Point(0, 0));
                    return arribaIzquierda.Y + (ancla.ActualHeight / 2) > _ventana.ActualHeight / 2;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }

            private void Avanzar()
            {
                if (_terminada)
                {
                    return;
                }
                _ = _servicio.EntendidoAsync(_globos[_indice].Globo);
                _indice++;
                if (_indice >= _globos.Count)
                {
                    Terminar(soltarRestantes: false);
                    return;
                }
                EnseñarActual();
            }

            private void Aplazar()
            {
                if (_terminada)
                {
                    return;
                }
                // Esc: el actual y los que faltaban de la tanda, para la próxima vez (sin marcar vistos)
                for (int i = _indice; i < _globos.Count; i++)
                {
                    _servicio.Aplazar(_globos[i].Globo);
                }
                Terminar(soltarRestantes: false);
            }

            private void AlPulsarEnVentana(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    // La vista no recibe este Esc (no cierra la pestaña ni cancela lo que estuviera haciendo)
                    e.Handled = true;
                    Aplazar();
                }
            }

            private void AlDescargarVista(object sender, RoutedEventArgs e)
            {
                // Se cerró (o se cambió de pestaña) sin contestar: siguen pendientes en esta sesión
                Terminar(soltarRestantes: true);
            }

            private void AlDesactivarVentana(object sender, EventArgs e)
            {
                // El Popup está por encima de todo: no se queda encima de otra aplicación
                if (_popup != null)
                {
                    _popup.IsOpen = false;
                }
            }

            private void AlActivarVentana(object sender, EventArgs e)
            {
                if (_popup != null && !_terminada)
                {
                    _popup.IsOpen = true;
                }
            }

            private void AlMoverVentana(object sender, EventArgs e)
            {
                // Recolocar el Popup (WPF no lo mueve solo con la ventana)
                if (_popup != null && _popup.IsOpen)
                {
                    _popup.HorizontalOffset += 0.01;
                    _popup.HorizontalOffset -= 0.01;
                }
            }

            private void Terminar(bool soltarRestantes)
            {
                if (_terminada)
                {
                    return;
                }
                _terminada = true;
                if (soltarRestantes)
                {
                    for (int i = _indice; i < _globos.Count; i++)
                    {
                        _servicio.Soltar(_globos[i].Globo);
                    }
                }
                CerrarPopup();
                _vista.Unloaded -= AlDescargarVista;
                if (_ventana != null)
                {
                    _ventana.PreviewKeyDown -= AlPulsarEnVentana;
                    _ventana.Deactivated -= AlDesactivarVentana;
                    _ventana.Activated -= AlActivarVentana;
                    _ventana.LocationChanged -= AlMoverVentana;
                    _ventana.SizeChanged -= AlMoverVentana;
                }
                _alTerminar();
            }

            private void CerrarPopup()
            {
                if (_popup != null)
                {
                    _popup.IsOpen = false;
                    _popup.Child = null;
                    _popup = null;
                }
            }
        }
    }

    internal enum FlechaGlobo
    {
        Ninguna,
        Arriba,
        Abajo
    }

    /// <summary>Nesto#592: el globo en sí (fondo claro, flecha, título, texto y el botón).</summary>
    internal sealed class VistaGlobo : Border
    {
        /// <summary>Dónde queda la punta de la flecha desde el borde izquierdo del globo.</summary>
        internal const double POSICION_FLECHA = 24;

        private static readonly Brush FONDO = Congelado(new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0xEB)));
        private static readonly Brush BORDE = Congelado(new SolidColorBrush(Color.FromRgb(0xE0, 0xA8, 0x00)));
        private static readonly Brush TEXTO = Congelado(new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)));
        private static readonly Brush TENUE = Congelado(new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)));

        public event EventHandler Aceptado;

        public VistaGlobo(GloboNesto globo, FlechaGlobo flecha, string contador, string textoBoton)
        {
            Background = Brushes.Transparent;
            Padding = new Thickness(8);
            Focusable = true;

            var tarjeta = new Border
            {
                Background = FONDO,
                BorderBrush = BORDE,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                MaxWidth = 360,
                MinWidth = 220,
                Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.3 }
            };
            var pila = new StackPanel();
            if (!string.IsNullOrWhiteSpace(globo.Titulo))
            {
                pila.Children.Add(new TextBlock
                {
                    Text = "💡 " + globo.Titulo.Trim(),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 14,
                    Foreground = TEXTO,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }
            pila.Children.Add(new TextBlock { Text = globo.Texto?.Trim(), FontSize = 13, Foreground = TEXTO, TextWrapping = TextWrapping.Wrap });

            var pie = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false };
            var boton = new Button { Content = textoBoton, Padding = new Thickness(14, 3, 14, 3), IsDefault = true, MinWidth = 90 };
            boton.Click += (_, __) => Aceptado?.Invoke(this, EventArgs.Empty);
            DockPanel.SetDock(boton, Dock.Right);
            _ = pie.Children.Add(boton);
            string ayuda = string.IsNullOrEmpty(contador) ? "Esc: más tarde" : contador + " · Esc: más tarde";
            var textoAyuda = new TextBlock { Text = ayuda, FontSize = 11, Foreground = TENUE, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(textoAyuda, Dock.Left);
            _ = pie.Children.Add(textoAyuda);
            _ = pila.Children.Add(pie);
            tarjeta.Child = pila;

            var raiz = new Grid();
            raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(tarjeta, 1);
            _ = raiz.Children.Add(tarjeta);
            if (flecha != FlechaGlobo.Ninguna)
            {
                Path punta = Flecha(flecha);
                Grid.SetRow(punta, flecha == FlechaGlobo.Arriba ? 0 : 2);
                _ = raiz.Children.Add(punta);
            }
            Child = raiz;
        }

        // Un triángulo que solapa el borde de la tarjeta para que parezcan una sola pieza
        private static Path Flecha(FlechaGlobo flecha)
        {
            const double ANCHO = 18, ALTO = 9;
            Geometry forma = flecha == FlechaGlobo.Arriba
                ? Geometry.Parse($"M0,{ALTO} L{ANCHO / 2},0 L{ANCHO},{ALTO}")
                : Geometry.Parse($"M0,0 L{ANCHO / 2},{ALTO} L{ANCHO},0");
            return new Path
            {
                Data = forma,
                Fill = FONDO,
                Stroke = BORDE,
                StrokeThickness = 1.5,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = flecha == FlechaGlobo.Arriba
                    ? new Thickness(POSICION_FLECHA - 8 - (ANCHO / 2), 0, 0, -1.5)
                    : new Thickness(POSICION_FLECHA - 8 - (ANCHO / 2), -1.5, 0, 0)
            };
        }

        private static Brush Congelado(SolidColorBrush pincel)
        {
            pincel.Freeze();
            return pincel;
        }
    }
}
