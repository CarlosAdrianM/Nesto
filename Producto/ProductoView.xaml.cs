using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

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
            if (((ProductoViewModel)DataContext).PestannaSeleccionada == null)
            {
                ((ProductoViewModel)DataContext).PestannaSeleccionada = (TabItem)tabProducto.Items[0];
            }
            txtFiltroNombre.Focus();
            Keyboard.Focus(txtFiltroNombre);
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

        private void txtFiltroNombre_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                txtFiltroFamilia.Focus();
            }
        }

        private void txtFiltroFamilia_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                txtFiltroSubgrupo.Focus();
            }
        }

        private void txtFiltroSubgrupo_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BindingExpression be = txtFiltroSubgrupo.GetBindingExpression(TextBox.TextProperty);
                be.UpdateSource();
                ProductoViewModel vm = (ProductoViewModel)DataContext;
                vm.BuscarProductoCommand.Execute();
            }
        }
    }

    public class EstadoToBorderBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            if (value.ToString() == "0")
            {
                return Colors.Green;
            }
            else
            {
                return Colors.Honeydew;
            }
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
