using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#372: diálogo "Qué hay de nuevo" con el changelog de usuario.
    /// </summary>
    public partial class NovedadesDialog : UserControl
    {
        public NovedadesDialog()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            // Nesto#516: el AutoWire de Prism pone el VM DENTRO de InitializeComponent, antes de la suscripción
            // de arriba: sin engancharse al que ya trae, la vista no se enteraba nunca de la novedad ni del
            // comentario destacados (salían resaltados, pero la lista no se movía).
            EngancharViewModel(null, DataContext);
            Unloaded += (s, e) => _temporizador?.Stop();
        }

        // NestoAPI#520: Ctrl+V en el cuadro de comentario pega la captura si lo copiado es una imagen;
        // si es texto, la tecla sigue su curso normal y pega el texto.
        private void CuadroComentario_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (EsCtrlV(e)
                && (sender as FrameworkElement)?.DataContext is NovedadItem novedad
                && novedad.PegarImagen())
            {
                e.Handled = true;
            }
        }

        // Nesto#487: lo mismo en el cuadro de sugerir una característica.
        private void CuadroSugerencia_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (EsCtrlV(e) && DataContext is NovedadesDialogViewModel vm && vm.PegarImagenSugerencia())
            {
                e.Handled = true;
            }
        }

        private static bool EsCtrlV(KeyEventArgs e) => e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control;

        // Nesto#487: la novedad elegida en el buscador se hace visible en la lista (cuando ya está pintada).
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
            => EngancharViewModel(e.OldValue, e.NewValue);

        private void EngancharViewModel(object anterior, object nuevo)
        {
            if (anterior is INotifyPropertyChanged vmAnterior)
            {
                vmAnterior.PropertyChanged -= OnViewModelPropertyChanged;
            }
            if (nuevo is INotifyPropertyChanged vmNuevo)
            {
                vmNuevo.PropertyChanged -= OnViewModelPropertyChanged;
                vmNuevo.PropertyChanged += OnViewModelPropertyChanged;
            }
            if (nuevo is NovedadesDialogViewModel vm && vm.NovedadDestacada != null)
            {
                SeguirNovedad(vm.NovedadDestacada);
            }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NovedadesDialogViewModel.NovedadDestacada)
                && sender is NovedadesDialogViewModel vm && vm.NovedadDestacada != null)
            {
                SeguirNovedad(vm.NovedadDestacada);
            }
        }

        // Nesto#477: al llegar desde la campana, el comentario de la notificación se hace visible cuando
        // la novedad destacada termina de cargar sus comentarios.
        private NovedadItem _novedadSeguida;

        private void SeguirNovedad(NovedadItem novedad)
        {
            if (_novedadSeguida != null)
            {
                _novedadSeguida.PropertyChanged -= OnNovedadDestacadaPropertyChanged;
            }
            _novedadSeguida = novedad;
            _novedadSeguida.PropertyChanged += OnNovedadDestacadaPropertyChanged;
            // Nesto#516: si el comentario ya llegó, manda él (la novedad no lo pisa).
            if (novedad.ComentarioDestacado != null)
            {
                _seguimiento.SeguirComentario(novedad.ComentarioDestacado);
            }
            else
            {
                _seguimiento.SeguirNovedad(novedad);
            }
            ArrancarSeguimiento();
        }

        private void OnNovedadDestacadaPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NovedadItem.ComentarioDestacado)
                && sender is NovedadItem novedad && novedad.ComentarioDestacado != null)
            {
                _seguimiento.SeguirComentario(novedad.ComentarioDestacado);
                ArrancarSeguimiento();
            }
        }

        #region Nesto#516: llevar el destacado a la vista (y el comentario, con el foco)

        private readonly SeguimientoDestacado _seguimiento = new SeguimientoDestacado();
        internal SeguimientoDestacado Seguimiento => _seguimiento;
        private DispatcherTimer _temporizador;

        /// <summary>
        /// Cada 100 ms, hasta que el destacado exista, esté centrado y la lista se quede quieta (las miniaturas
        /// que llegan después la mueven), con tope (<see cref="SeguimientoDestacado.MAX_INTENTOS"/>).
        /// </summary>
        private void ArrancarSeguimiento()
        {
            if (_temporizador == null)
            {
                _temporizador = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                _temporizador.Tick += (s, e) => DarPasoSeguimiento();
            }
            _temporizador.Start();
        }

        private void DarPasoSeguimiento()
        {
            FrameworkElement elemento = _seguimiento.Activo ? LocalizarObjetivo() : null;
            bool encontrado = elemento != null && elemento.IsVisible && elemento.ActualHeight > 0
                && ScrollNovedades.ViewportHeight > 0 && ScrollNovedades.IsAncestorOf(elemento);
            double deseado = 0;
            bool yaColocado = false;
            if (encontrado)
            {
                Point posicion = elemento.TransformToVisual(ScrollNovedades).Transform(new Point(0, 0));
                deseado = SeguimientoDestacado.CalcularDesplazamiento(ScrollNovedades.VerticalOffset, posicion.Y,
                    elemento.ActualHeight, ScrollNovedades.ViewportHeight);
                deseado = Math.Min(deseado, ScrollNovedades.ScrollableHeight);
                yaColocado = Math.Abs(deseado - ScrollNovedades.VerticalOffset) < 1;
            }

            PasoSeguimiento paso = _seguimiento.Paso(encontrado, yaColocado);
            if (paso.DarFoco)
            {
                // El foco antes de colocar: al recibirlo, WPF solo lo hace asomar; luego se centra.
                DarFoco(elemento);
            }
            if (paso.Colocar)
            {
                ScrollNovedades.ScrollToVerticalOffset(deseado);
            }
            if (paso.Terminar)
            {
                _temporizador.Stop();
            }
        }

        /// <summary>
        /// El contenedor de la novedad o, si se sigue un comentario, el de ese comentario dentro de ella. Nulo
        /// mientras no exista (lista recién cambiada, comentarios recién cargados): se reintenta.
        /// </summary>
        private FrameworkElement LocalizarObjetivo()
        {
            if (_novedadSeguida == null)
            {
                return null;
            }
            var contenedor = ListaNovedades.ItemContainerGenerator.ContainerFromItem(_novedadSeguida) as FrameworkElement;
            return _seguimiento.EsComentario
                ? BuscarPorDataContext(contenedor, _seguimiento.Objetivo) as FrameworkElement
                : contenedor;
        }

        // Para el teclado y el lector de pantalla: el borde del comentario destacado es enfocable (XAML).
        private static void DarFoco(FrameworkElement presentador)
        {
            UIElement enfocable = VisualTreeHelper.GetChildrenCount(presentador) > 0
                ? VisualTreeHelper.GetChild(presentador, 0) as UIElement
                : null;
            if (enfocable == null || !enfocable.Focusable)
            {
                presentador.Focusable = true;
                enfocable = presentador;
            }
            Keyboard.Focus(enfocable);
        }

        // Si el usuario mueve la lista (rueda, ratón, teclado), se deja de seguir: no se le lleva de vuelta.
        private void ScrollNovedades_InteraccionUsuario(object sender, InputEventArgs e) => _seguimiento.Interrumpir();

        #endregion

        private static DependencyObject BuscarPorDataContext(DependencyObject padre, object dataContext)
        {
            if (padre == null)
            {
                return null;
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(padre); i++)
            {
                DependencyObject hijo = VisualTreeHelper.GetChild(padre, i);
                if (hijo is ContentPresenter presentador && presentador.Content == dataContext)
                {
                    return presentador;
                }
                DependencyObject encontrado = BuscarPorDataContext(hijo, dataContext);
                if (encontrado != null)
                {
                    return encontrado;
                }
            }
            return null;
        }
    }
}
