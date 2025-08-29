// CorreccionVideoProductoView.xaml.cs

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
    }
}