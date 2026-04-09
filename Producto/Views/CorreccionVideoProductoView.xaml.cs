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
        /// Issue #342: engancha los eventos de los behaviours del TextBox de Referencia
        /// para actualizar NombreProducto en el ProductoEditable cuando el usuario
        /// selecciona una sugerencia del autocomplete o cuando la validación LostFocus
        /// resuelve un producto contra la API.
        ///
        /// ProductoBehavior actualiza el TextBox.Text (y por tanto Referencia vía binding),
        /// pero sus SetProperty por reflexión apuntan a nombres de PedidoVenta (Producto,
        /// texto, PrecioUnitario...) que no existen en ProductoEditable, así que son no-ops.
        /// Por eso necesitamos actualizar NombreProducto manualmente desde aquí.
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
                            item.NombreProducto = args.Nombre;
                        }
                    };
                }
                else if (behavior is AutocompleteBehavior autocompleteBehavior)
                {
                    autocompleteBehavior.ItemSeleccionado += (s, args) =>
                    {
                        if (textBox.DataContext is ProductoEditable item && !string.IsNullOrEmpty(args.Item?.Texto))
                        {
                            item.NombreProducto = args.Item.Texto;
                        }
                    };
                }
            }
        }
    }
}