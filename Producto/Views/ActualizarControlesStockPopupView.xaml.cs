using Nesto.Modules.Producto.ViewModels;
using Prism.Services.Dialogs;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modulos.Producto.Views
{
    public partial class ActualizarControlesStockPopupView : UserControl
    {
        public ActualizarControlesStockPopupView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Conectar el selector de proveedor con el ViewModel
            if (DataContext is ActualizarControlesStockPopupViewModel vm)
            {
                vm.SetSelectorProveedor(selectorProveedor);
            }

            // Configurar la ventana para que sea redimensionable
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.SizeToContent = SizeToContent.Manual;
                window.ResizeMode = ResizeMode.CanResize;
                window.Width = 800;
                window.Height = 550;
            }
        }

        private void Button_Cancelar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ActualizarControlesStockPopupViewModel vm)
            {
                vm.CerrarDialogo(ButtonResult.Cancel);
            }
        }
    }
}
