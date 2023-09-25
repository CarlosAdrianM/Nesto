using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Nesto.Modules.Producto.ViewModels;

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
            ProductoViewModel viewModel = (ProductoViewModel)DataContext;
            if (viewModel.PestannaSeleccionada == null)
            {
                viewModel.PestannaSeleccionada = (TabItem)tabProducto.Items[0];
            }
            txtFiltroNombre.Focus();
            Keyboard.Focus(txtFiltroNombre);
            txtReferencia.SelectAll();

            // En el código de la vista, suscríbete al evento
            viewModel.DatosCargados += (sender, args) =>
            {
                // Realiza la actualización de la interfaz de usuario aquí
                Application.Current.Dispatcher.Invoke(() =>
                {
                    itcControlStock.ItemsSource = viewModel.ControlStock.ControlesStocksAlmacen;
                    grdStockMinimo.DataContext = viewModel.ControlStock;
                });
            };
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));

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
