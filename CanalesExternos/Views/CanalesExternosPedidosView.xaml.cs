using Nesto.Modulos.CanalesExternos.Models;
using Nesto.Modulos.CanalesExternos.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Nesto.Modulos.CanalesExternos.Views
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class CanalesExternosPedidosView : UserControl
    {
        public CanalesExternosPedidosView()
        {
            InitializeComponent();
        }
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ((CanalesExternosPedidosViewModel)DataContext).CanalSeleccionado = ((CanalesExternosPedidosViewModel)DataContext).Factory.First().Value;
        }

        // Nesto#374: doble clic en una fila abre el pedido de Nesto asignado (si lo tiene).
        private void dgPedidos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Ignorar el doble clic sobre los botones de la fila (Crear Pedido / Crear Etiqueta).
            DependencyObject origen = e.OriginalSource as DependencyObject;
            while (origen != null)
            {
                if (origen is Button) return;
                origen = VisualTreeHelper.GetParent(origen);
            }

            if (DataContext is CanalesExternosPedidosViewModel vm
                && (sender as DataGrid)?.SelectedItem is PedidoCanalExterno pedido
                && vm.AbrirPedidoNestoCommand.CanExecute(pedido))
            {
                vm.AbrirPedidoNestoCommand.Execute(pedido);
            }
        }
    }
}
