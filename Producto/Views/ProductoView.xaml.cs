using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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
            if (DataContext is ProductoViewModel vm)
            {
                vm.VideoCompletoSeleccionadoCambiado += video =>
                    ActualizarDescripcionYProtocolo(video?.Descripcion, video?.Protocolo);
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ProductoViewModel viewModel = (ProductoViewModel)DataContext;
            _ = txtFiltroNombre.Focus();
            _ = Keyboard.Focus(txtFiltroNombre);
            txtReferencia.SelectAll();

            viewModel.DatosCargados += (sender, args) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    itcControlStock.ItemsSource = viewModel.ControlStock.ControlesStocksAlmacen;
                    grdStockMinimo.DataContext = viewModel.ControlStock;
                });
            };
            _ = Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));

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
                _ = txtFiltroFamilia.Focus();
            }
        }

        private void txtFiltroFamilia_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _ = txtFiltroSubgrupo.Focus();
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

        // Llama a este método cuando cambie el VideoCompletoSeleccionado
        private void ActualizarDescripcionYProtocolo(string descripcion, string protocolo)
        {
            DescripcionRichTextBox.Document = HtmlHelper.PlainTextToFlowDocument(descripcion);
            ProtocoloRichTextBox.Document = HtmlHelper.ConvertHtmlToFlowDocument(protocolo);
        }

        private void RichTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var richTextBox = sender as RichTextBox;
            var pointer = richTextBox.GetPositionFromPoint(e.GetPosition(richTextBox), true);
            if (pointer != null)
            {
                var inline = pointer.Parent as Inline;
                while (inline is not null and not Hyperlink)
                {
                    inline = inline.Parent as Inline;
                }
                if (inline is Hyperlink hyperlink && hyperlink.NavigateUri != null)
                {
                    _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(hyperlink.NavigateUri.AbsoluteUri) { UseShellExecute = true });
                    e.Handled = true;
                }
            }
        }

        private void RichTextBox_MouseMove(object sender, MouseEventArgs e)
        {
            var richTextBox = sender as RichTextBox;
            var pointer = richTextBox.GetPositionFromPoint(e.GetPosition(richTextBox), true);
            if (pointer != null)
            {
                var inline = pointer.Parent as Inline;
                while (inline is not null and not Hyperlink)
                {
                    inline = inline.Parent as Inline;
                }
                if (inline is Hyperlink)
                {
                    richTextBox.Cursor = Cursors.Hand;
                    return;
                }
            }
            richTextBox.Cursor = Cursors.IBeam;
        }

        private void RichTextBox_MouseLeave(object sender, MouseEventArgs e)
        {
            var richTextBox = sender as RichTextBox;
            richTextBox.Cursor = Cursors.IBeam;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ProductoViewModel vm && vm.VideoCompletoSeleccionado != null && !string.IsNullOrWhiteSpace(vm.VideoRelacionadoSeleccionado.UrlVideo))
            {
                _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = vm.VideoRelacionadoSeleccionado.UrlVideo,
                    UseShellExecute = true // esto abre el navegador predeterminado
                });
            }
        }

        private void dgrProductosKit_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem is KitContienePerteneceModel productoKit)
            {
                var vm = DataContext as ProductoViewModel;
                vm?.AbrirProductoCommand.Execute(productoKit.ProductoId);
            }
        }
    }

    public class EstadoToBorderBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return value.ToString() == "0" ? Colors.Green : Colors.Honeydew;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class TabConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (int)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (Pestannas)value;
        }
    }
}
