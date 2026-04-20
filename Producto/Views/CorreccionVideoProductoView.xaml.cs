// CorreccionVideoProductoView.xaml.cs

using ControlesUsuario.Behaviors;
using Microsoft.Xaml.Behaviors;
using Nesto.Modules.Producto.ViewModels;
using Prism.Services.Dialogs;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nesto.Modulos.Producto.Views
{
    public partial class CorreccionVideoProductoView : UserControl
    {
        public CorreccionVideoProductoView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        /// <summary>
        /// Issue #347 problema 2: con muchos VideoProducto la ventana se salía de pantalla
        /// (MaxHeight fijo a 700 px no cabe en portátiles de 13" a 1366×768). Al cargar
        /// ajustamos el MaxHeight de la Window contenedora al 90% del WorkArea para que
        /// los botones inferiores siempre queden accesibles y el ScrollViewer interno
        /// gestione el overflow.
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.MaxHeight = SystemParameters.WorkArea.Height * 0.9;
            }
        }

        private void Hyperlink_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TextBlock textBlock = sender as TextBlock;
            string url = textBlock?.Tag?.ToString();
            if (!string.IsNullOrEmpty(url))
            {
                _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        private void Button_Cancelar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CorreccionVideoProductoViewModel vm)
            {
                vm.CerrarDialogo(ButtonResult.Cancel);
            }
        }

        /// <summary>
        /// Issues #342/#347: engancha los eventos de los behaviours del TextBox de Referencia
        /// para actualizar NombreProductoAsociado en el ProductoEditable cuando el usuario
        /// selecciona una sugerencia del autocomplete o cuando la validación LostFocus
        /// resuelve un producto contra la API. NombreProducto (el nombre del VideoProducto
        /// tal como aparece en el vídeo) no se toca, para que ambos nombres queden visibles
        /// y el usuario pueda detectar referencias equivocadas.
        /// </summary>
        private void TextBoxReferencia_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            // Evitar doble suscripción si Loaded se dispara más de una vez
            if (textBox.Tag is true)
            {
                return;
            }
            textBox.Tag = true;

            var behaviors = Interaction.GetBehaviors(textBox);
            foreach (var behavior in behaviors)
            {
                if (behavior is ProductoBehavior productoBehavior)
                {
                    productoBehavior.ProductoEncontrado += (s, args) =>
                    {
                        if (textBox.DataContext is ProductoEditable item && !string.IsNullOrEmpty(args.Nombre))
                        {
                            item.NombreProductoAsociado = args.Nombre;
                        }
                    };
                }
                else if (behavior is AutocompleteBehavior autocompleteBehavior)
                {
                    autocompleteBehavior.ItemSeleccionado += (s, args) =>
                    {
                        if (textBox.DataContext is ProductoEditable item && !string.IsNullOrEmpty(args.Item?.Texto))
                        {
                            item.NombreProductoAsociado = args.Item.Texto;
                        }
                    };
                }
            }
        }
    }
}