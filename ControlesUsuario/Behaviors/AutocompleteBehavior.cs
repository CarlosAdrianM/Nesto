using ControlesUsuario.Models;
using ControlesUsuario.Services;
using Microsoft.Xaml.Behaviors;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ControlesUsuario.Behaviors
{
    /// <summary>
    /// Behavior reutilizable para TextBox que muestra sugerencias de autocomplete mientras el usuario escribe.
    /// Soporta búsqueda de productos y cuentas contables según el TipoBusqueda configurado.
    ///
    /// Issue #263 - Carlos 30/01/26
    ///
    /// Uso en XAML:
    /// <code>
    /// xmlns:behaviors="clr-namespace:ControlesUsuario.Behaviors;assembly=ControlesUsuario"
    /// xmlns:i="http://schemas.microsoft.com/xaml/behaviors"
    ///
    /// &lt;TextBox Text="{Binding Producto}"&gt;
    ///     &lt;i:Interaction.Behaviors&gt;
    ///         &lt;behaviors:AutocompleteBehavior
    ///             Empresa="{Binding DataContext.pedido.empresa, RelativeSource={RelativeSource AncestorType=DataGrid}}"
    ///             TipoLineaActual="{Binding tipoLinea}"
    ///             TipoLineaRequerido="1"
    ///             TipoBusqueda="Producto"
    ///             MinChars="2"
    ///             DebounceMs="300"/&gt;
    ///     &lt;/i:Interaction.Behaviors&gt;
    /// &lt;/TextBox&gt;
    /// </code>
    /// </summary>
    public class AutocompleteBehavior : Behavior<TextBox>
    {
        #region Enums

        /// <summary>
        /// Tipo de búsqueda a realizar.
        /// </summary>
        public enum TipoBusquedaEnum
        {
            /// <summary>
            /// Buscar productos usando el buscador Lucene.
            /// </summary>
            Producto = 1,

            /// <summary>
            /// Buscar cuentas contables por prefijo o nombre.
            /// </summary>
            CuentaContable = 2
        }

        #endregion

        #region Dependency Properties

        /// <summary>
        /// Mínimo de caracteres para iniciar la búsqueda.
        /// </summary>
        public static readonly DependencyProperty MinCharsProperty =
            DependencyProperty.Register(
                nameof(MinChars),
                typeof(int),
                typeof(AutocompleteBehavior),
                new PropertyMetadata(2));

        public int MinChars
        {
            get => (int)GetValue(MinCharsProperty);
            set => SetValue(MinCharsProperty, value);
        }

        /// <summary>
        /// Milisegundos de espera después de la última tecla antes de buscar (debounce).
        /// </summary>
        public static readonly DependencyProperty DebounceMsProperty =
            DependencyProperty.Register(
                nameof(DebounceMs),
                typeof(int),
                typeof(AutocompleteBehavior),
                new PropertyMetadata(300));

        public int DebounceMs
        {
            get => (int)GetValue(DebounceMsProperty);
            set => SetValue(DebounceMsProperty, value);
        }

        /// <summary>
        /// Máximo número de resultados a mostrar.
        /// </summary>
        public static readonly DependencyProperty MaxResultsProperty =
            DependencyProperty.Register(
                nameof(MaxResults),
                typeof(int),
                typeof(AutocompleteBehavior),
                new PropertyMetadata(10));

        public int MaxResults
        {
            get => (int)GetValue(MaxResultsProperty);
            set => SetValue(MaxResultsProperty, value);
        }

        /// <summary>
        /// Empresa para la búsqueda.
        /// </summary>
        public static readonly DependencyProperty EmpresaProperty =
            DependencyProperty.Register(
                nameof(Empresa),
                typeof(string),
                typeof(AutocompleteBehavior),
                new PropertyMetadata(null));

        public string Empresa
        {
            get => (string)GetValue(EmpresaProperty);
            set => SetValue(EmpresaProperty, value);
        }

        /// <summary>
        /// Tipo de línea actual (bindeado al campo tipoLinea de la fila).
        /// </summary>
        public static readonly DependencyProperty TipoLineaActualProperty =
            DependencyProperty.Register(
                nameof(TipoLineaActual),
                typeof(byte?),
                typeof(AutocompleteBehavior),
                new PropertyMetadata(null));

        public byte? TipoLineaActual
        {
            get => (byte?)GetValue(TipoLineaActualProperty);
            set => SetValue(TipoLineaActualProperty, value);
        }

        /// <summary>
        /// Tipo de línea que activa este behavior.
        /// Por defecto 1 (Producto).
        /// </summary>
        public static readonly DependencyProperty TipoLineaRequeridoProperty =
            DependencyProperty.Register(
                nameof(TipoLineaRequerido),
                typeof(byte),
                typeof(AutocompleteBehavior),
                new PropertyMetadata((byte)1));

        public byte TipoLineaRequerido
        {
            get => (byte)GetValue(TipoLineaRequeridoProperty);
            set => SetValue(TipoLineaRequeridoProperty, value);
        }

        /// <summary>
        /// Tipo de búsqueda: Producto o CuentaContable.
        /// </summary>
        public static readonly DependencyProperty TipoBusquedaProperty =
            DependencyProperty.Register(
                nameof(TipoBusqueda),
                typeof(TipoBusquedaEnum),
                typeof(AutocompleteBehavior),
                new PropertyMetadata(TipoBusquedaEnum.Producto));

        public TipoBusquedaEnum TipoBusqueda
        {
            get => (TipoBusquedaEnum)GetValue(TipoBusquedaProperty);
            set => SetValue(TipoBusquedaProperty, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Evento que se dispara cuando el usuario selecciona un item del autocomplete.
        /// </summary>
        public event EventHandler<AutocompleteSeleccionEventArgs> ItemSeleccionado;

        #endregion

        #region Private Fields

        private Popup _popup;
        private ListBox _listBox;
        private DispatcherTimer _debounceTimer;
        private CancellationTokenSource _cts;
        private bool _isSelecting;
        private string _ultimoTextoBuscado;

        #endregion

        #region Behavior Overrides

        protected override void OnAttached()
        {
            base.OnAttached();

            // Crear el popup programáticamente
            CrearPopup();

            // Suscribir a eventos
            AssociatedObject.TextChanged += OnTextChanged;
            AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
            AssociatedObject.LostFocus += OnLostFocus;

            System.Diagnostics.Debug.WriteLine($"[AutocompleteBehavior] OnAttached: Behavior attached, TipoBusqueda={TipoBusqueda}");
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            // Cancelar búsquedas pendientes
            _cts?.Cancel();
            _debounceTimer?.Stop();

            // Desuscribir eventos
            if (AssociatedObject != null)
            {
                AssociatedObject.TextChanged -= OnTextChanged;
                AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
                AssociatedObject.LostFocus -= OnLostFocus;
            }

            // Limpiar popup
            if (_listBox != null)
            {
                _listBox.PreviewMouseLeftButtonUp -= OnListBoxItemSelected;
            }

            _popup = null;
            _listBox = null;
        }

        #endregion

        #region Popup Creation

        private void CrearPopup()
        {
            _listBox = new ListBox
            {
                MaxHeight = 200,
                MinWidth = 200,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(171, 173, 179)),
                Background = Brushes.White,
                DisplayMemberPath = "TextoMostrar"
            };
            _listBox.PreviewMouseLeftButtonUp += OnListBoxItemSelected;

            _popup = new Popup
            {
                PlacementTarget = AssociatedObject,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                Child = _listBox
            };
        }

        #endregion

        #region Event Handlers

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            // Si estamos seleccionando un item, no volver a buscar
            if (_isSelecting)
            {
                return;
            }

            // Verificar si debemos activar según TipoLinea
            if (!DebeActivar())
            {
                CerrarPopup();
                return;
            }

            var texto = AssociatedObject.Text;

            // Verificar mínimo de caracteres
            if (string.IsNullOrEmpty(texto) || texto.Length < MinChars)
            {
                CerrarPopup();
                return;
            }

            // Si el texto no ha cambiado, no buscar de nuevo
            if (texto == _ultimoTextoBuscado)
            {
                return;
            }

            // Cancelar búsqueda anterior
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            // Iniciar debounce timer
            if (_debounceTimer == null)
            {
                _debounceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(DebounceMs)
                };
                _debounceTimer.Tick += OnDebounceTimerTick;
            }

            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private async void OnDebounceTimerTick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();

            if (AssociatedObject == null) return;

            var texto = AssociatedObject.Text;
            if (string.IsNullOrEmpty(texto) || texto.Length < MinChars)
            {
                CerrarPopup();
                return;
            }

            _ultimoTextoBuscado = texto;
            await BuscarSugerencias(texto, _cts.Token);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_popup == null || !_popup.IsOpen)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Down:
                    if (_listBox.Items.Count > 0)
                    {
                        if (_listBox.SelectedIndex < _listBox.Items.Count - 1)
                        {
                            _listBox.SelectedIndex++;
                        }
                        else
                        {
                            _listBox.SelectedIndex = 0;
                        }
                        _listBox.ScrollIntoView(_listBox.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (_listBox.Items.Count > 0)
                    {
                        if (_listBox.SelectedIndex > 0)
                        {
                            _listBox.SelectedIndex--;
                        }
                        else
                        {
                            _listBox.SelectedIndex = _listBox.Items.Count - 1;
                        }
                        _listBox.ScrollIntoView(_listBox.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.Enter:
                    if (_listBox.SelectedItem != null)
                    {
                        SeleccionarItem(_listBox.SelectedItem as AutocompleteItem);
                    }
                    else
                    {
                        // Si no hay item seleccionado, cerrar popup igualmente
                        CerrarPopup();
                    }
                    // NO marcar e.Handled - dejar que Enter propague al DataGrid
                    // para que haga commit de la celda y baje a la siguiente fila
                    break;

                case Key.Escape:
                    CerrarPopup();
                    e.Handled = true;
                    break;

                case Key.Tab:
                    // Si hay un item seleccionado, usarlo
                    if (_listBox.SelectedItem != null)
                    {
                        SeleccionarItem(_listBox.SelectedItem as AutocompleteItem);
                    }
                    // No marcar handled para permitir navegación Tab normal
                    break;
            }
        }

        private void OnListBoxItemSelected(object sender, MouseButtonEventArgs e)
        {
            if (_listBox.SelectedItem is AutocompleteItem item)
            {
                SeleccionarItem(item);
            }
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            // Pequeño delay para permitir que el clic en el popup se procese
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_popup != null && !_popup.IsMouseOver && !AssociatedObject.IsFocused)
                {
                    CerrarPopup();
                }
            }), DispatcherPriority.Background);
        }

        #endregion

        #region Private Methods

        private bool DebeActivar()
        {
            if (!TipoLineaActual.HasValue)
            {
                return false;
            }

            return TipoLineaActual.Value == TipoLineaRequerido;
        }

        private async Task BuscarSugerencias(string texto, CancellationToken token)
        {
            try
            {
                var servicio = ResolverServicio();
                if (servicio == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AutocompleteBehavior] No se pudo resolver el servicio de búsqueda");
                    return;
                }

                var resultados = await servicio.BuscarSugerenciasAsync(texto, Empresa, MaxResults, token);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (resultados != null && resultados.Count > 0)
                {
                    MostrarPopup(resultados);
                }
                else
                {
                    CerrarPopup();
                }
            }
            catch (OperationCanceledException)
            {
                // Búsqueda cancelada, ignorar
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutocompleteBehavior] Error buscando: {ex.Message}");
                CerrarPopup();
            }
        }

        private IServicioBusquedaAutocomplete ResolverServicio()
        {
            try
            {
                if (ContainerLocator.Container == null)
                {
                    return null;
                }

                switch (TipoBusqueda)
                {
                    case TipoBusquedaEnum.Producto:
                        return ContainerLocator.Container.Resolve<ServicioBusquedaProductos>();

                    case TipoBusquedaEnum.CuentaContable:
                        return ContainerLocator.Container.Resolve<ServicioBusquedaCuentas>();

                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutocompleteBehavior] Error resolviendo servicio: {ex.Message}");
                return null;
            }
        }

        private void MostrarPopup(IList<AutocompleteItem> resultados)
        {
            if (_listBox == null || _popup == null)
            {
                return;
            }

            _listBox.ItemsSource = resultados;
            _listBox.SelectedIndex = 0;

            // Ajustar ancho del popup al TextBox
            if (AssociatedObject != null)
            {
                _listBox.MinWidth = AssociatedObject.ActualWidth;
            }

            _popup.IsOpen = true;
        }

        private void CerrarPopup()
        {
            if (_popup != null)
            {
                _popup.IsOpen = false;
            }
            _ultimoTextoBuscado = null;
        }

        private void SeleccionarItem(AutocompleteItem item)
        {
            if (item == null || AssociatedObject == null)
            {
                return;
            }

            _isSelecting = true;

            try
            {
                // Actualizar el TextBox con el código del item
                AssociatedObject.Text = item.Id;
                AssociatedObject.CaretIndex = AssociatedObject.Text.Length;

                // Forzar actualización del binding al ViewModel
                // (el binding tiene UpdateSourceTrigger=LostFocus, pero necesitamos
                // que se actualice antes de que Enter propague al DataGrid)
                var binding = AssociatedObject.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();

                System.Diagnostics.Debug.WriteLine($"[AutocompleteBehavior] Item seleccionado: {item.Id} - {item.Texto}");

                // Disparar evento
                ItemSeleccionado?.Invoke(this, new AutocompleteSeleccionEventArgs { Item = item });
            }
            finally
            {
                _isSelecting = false;
                CerrarPopup();
            }
        }

        #endregion
    }

    /// <summary>
    /// Argumentos del evento de selección de item en autocomplete.
    /// </summary>
    public class AutocompleteSeleccionEventArgs : EventArgs
    {
        public AutocompleteItem Item { get; set; }
    }
}
