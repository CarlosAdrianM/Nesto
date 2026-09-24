using Nesto.Infrastructure.Contracts;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#491: engancha a un TextBox el desplegable de @menciones (<see cref="AutocompletadoMenciones"/>):
    /// un Popup con la lista bajo el cuadro, flechas/Enter/Tab/Esc y clic. En XAML:
    /// <c>local:MencionesTextBox.Autocompletado="{Binding MencionesComentario}"</c>.
    /// </summary>
    public static class MencionesTextBox
    {
        public static readonly DependencyProperty AutocompletadoProperty = DependencyProperty.RegisterAttached(
            "Autocompletado", typeof(AutocompletadoMenciones), typeof(MencionesTextBox),
            new PropertyMetadata(null, OnAutocompletadoChanged));

        public static AutocompletadoMenciones GetAutocompletado(DependencyObject d) => (AutocompletadoMenciones)d.GetValue(AutocompletadoProperty);
        public static void SetAutocompletado(DependencyObject d, AutocompletadoMenciones value) => d.SetValue(AutocompletadoProperty, value);

        private static readonly DependencyProperty PopupProperty = DependencyProperty.RegisterAttached(
            "Popup", typeof(Popup), typeof(MencionesTextBox));

        private static void OnAutocompletadoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is TextBox cuadro))
            {
                return;
            }
            if (e.OldValue == null)
            {
                cuadro.TextChanged += Cuadro_TextChanged;
                cuadro.SelectionChanged += Cuadro_SelectionChanged;
                cuadro.PreviewKeyDown += Cuadro_PreviewKeyDown;
                cuadro.LostKeyboardFocus += Cuadro_LostKeyboardFocus;
                cuadro.IsVisibleChanged += Cuadro_IsVisibleChanged;
            }
            if (e.NewValue == null)
            {
                cuadro.TextChanged -= Cuadro_TextChanged;
                cuadro.SelectionChanged -= Cuadro_SelectionChanged;
                cuadro.PreviewKeyDown -= Cuadro_PreviewKeyDown;
                cuadro.LostKeyboardFocus -= Cuadro_LostKeyboardFocus;
                cuadro.IsVisibleChanged -= Cuadro_IsVisibleChanged;
            }
            (e.OldValue as AutocompletadoMenciones)?.Cerrar();
            Popup popup = (Popup)cuadro.GetValue(PopupProperty);
            if (popup == null && e.NewValue != null)
            {
                popup = CrearPopup(cuadro);
                cuadro.SetValue(PopupProperty, popup);
            }
            if (popup != null)
            {
                popup.DataContext = e.NewValue;
            }
        }

        private static Popup CrearPopup(TextBox cuadro)
        {
            var itemStyle = new Style(typeof(ListBoxItem));
            // Sin foco: el teclado se queda en el cuadro (se sigue escribiendo mientras se elige).
            itemStyle.Setters.Add(new Setter(UIElement.FocusableProperty, false));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 2, 6, 2)));
            itemStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));

            var lista = new ListBox
            {
                Focusable = false,
                DisplayMemberPath = nameof(Mencionable.Nombre),
                MaxHeight = 180,
                MinWidth = 160,
                BorderThickness = new Thickness(0),
                ItemContainerStyle = itemStyle
            };
            lista.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(AutocompletadoMenciones.Sugerencias)));
            lista.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(AutocompletadoMenciones.Seleccionada)) { Mode = BindingMode.TwoWay });
            lista.SelectionChanged += (s, e) =>
            {
                if (lista.SelectedItem != null)
                {
                    lista.ScrollIntoView(lista.SelectedItem);
                }
            };
            lista.PreviewMouseLeftButtonDown += (s, e) =>
            {
                ListBoxItem item = BuscarAncestro<ListBoxItem>(e.OriginalSource as DependencyObject);
                if (item?.DataContext is Mencionable elegido)
                {
                    Insertar(cuadro, elegido);
                    e.Handled = true;
                }
            };

            var popup = new Popup
            {
                PlacementTarget = cuadro,
                Placement = PlacementMode.Bottom,
                StaysOpen = true,
                AllowsTransparency = false,
                Child = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xD9)),
                    BorderThickness = new Thickness(1),
                    Child = lista
                }
            };
            popup.SetBinding(Popup.IsOpenProperty, new Binding(nameof(AutocompletadoMenciones.Abierto)) { Mode = BindingMode.OneWay });
            return popup;
        }

        private static T BuscarAncestro<T>(DependencyObject desde) where T : DependencyObject
        {
            while (desde != null && !(desde is T))
            {
                desde = desde is Visual || desde is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(desde)
                    : LogicalTreeHelper.GetParent(desde);
            }
            return desde as T;
        }

        private static void Actualizar(TextBox cuadro)
        {
            AutocompletadoMenciones autocompletado = GetAutocompletado(cuadro);
            if (autocompletado != null && cuadro.IsKeyboardFocusWithin)
            {
                _ = autocompletado.Actualizar(cuadro.Text, cuadro.CaretIndex);
            }
        }

        private static void Cuadro_TextChanged(object sender, TextChangedEventArgs e) => Actualizar((TextBox)sender);

        private static void Cuadro_SelectionChanged(object sender, RoutedEventArgs e) => Actualizar((TextBox)sender);

        private static void Cuadro_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
            => GetAutocompletado((TextBox)sender)?.Cerrar();

        private static void Cuadro_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
            {
                GetAutocompletado((TextBox)sender)?.Cerrar();
            }
        }

        private static void Cuadro_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var cuadro = (TextBox)sender;
            AutocompletadoMenciones autocompletado = GetAutocompletado(cuadro);
            if (autocompletado == null || !autocompletado.Abierto)
            {
                return;
            }
            switch (e.Key)
            {
                case Key.Down:
                    autocompletado.Mover(1);
                    e.Handled = true;
                    break;
                case Key.Up:
                    autocompletado.Mover(-1);
                    e.Handled = true;
                    break;
                case Key.Enter:
                case Key.Tab:
                    e.Handled = Insertar(cuadro, null);
                    break;
                case Key.Escape:
                    autocompletado.Descartar();
                    e.Handled = true;
                    break;
            }
        }

        private static bool Insertar(TextBox cuadro, Mencionable elegido)
        {
            (string Texto, int Cursor)? resultado = GetAutocompletado(cuadro)?.Elegir(elegido);
            if (resultado == null)
            {
                return false;
            }
            cuadro.Text = resultado.Value.Texto;
            cuadro.CaretIndex = resultado.Value.Cursor;
            _ = cuadro.Focus();
            return true;
        }
    }

    /// <summary>
    /// Nesto#491: pinta el texto de un comentario con las @menciones resaltadas (un Run en negrita y color de
    /// acento por mención; el resto, tal cual). En XAML: <c>local:TextoConMenciones.Texto="{Binding Texto}"</c>.
    /// </summary>
    public static class TextoConMenciones
    {
        private static readonly Brush COLOR_MENCION = CrearColorMencion();

        private static Brush CrearColorMencion()
        {
            var brush = new SolidColorBrush(Color.FromRgb(0x2E, 0x6D, 0xA4));
            brush.Freeze();
            return brush;
        }

        public static readonly DependencyProperty TextoProperty = DependencyProperty.RegisterAttached(
            "Texto", typeof(string), typeof(TextoConMenciones), new PropertyMetadata(null, OnTextoChanged));

        public static string GetTexto(DependencyObject d) => (string)d.GetValue(TextoProperty);
        public static void SetTexto(DependencyObject d, string value) => d.SetValue(TextoProperty, value);

        private static void OnTextoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is TextBlock bloque))
            {
                return;
            }
            bloque.Inlines.Clear();
            foreach (TrozoTexto trozo in Menciones.Trocear(e.NewValue as string))
            {
                var run = new Run(trozo.Texto);
                if (trozo.EsMencion)
                {
                    run.FontWeight = FontWeights.SemiBold;
                    run.Foreground = COLOR_MENCION;
                }
                bloque.Inlines.Add(run);
            }
        }
    }
}
