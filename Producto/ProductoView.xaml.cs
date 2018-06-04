using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Nesto.Modulos.Producto
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class ProductoView : UserControl
    {
        public ProductoView(ProductoViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            //((ProductoViewModel)DataContext).CargarProducto();
            txtReferencia.Focus();
            Keyboard.Focus(txtReferencia);
            txtReferencia.SelectAll();
        }

        private void txtReferencia_GotFocus(object sender, RoutedEventArgs e)
        {
            txtReferencia.SelectAll();
        }

        private void txtReferencia_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BindingExpression be = txtReferencia.GetBindingExpression(TextBox.TextProperty);
                be.UpdateSource();
                txtReferencia.SelectAll();
            }
        }

        private void txtReferencia_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            txtReferencia.SelectAll();
        }
    }
}
