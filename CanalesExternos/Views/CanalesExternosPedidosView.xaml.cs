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
            // Loaded vuelve a dispararse cada vez que la vista se re-engancha al árbol visual
            // (p. ej. al volver de un pedido abierto en la misma región): el canal solo se
            // inicializa la primera vez, para no resetear a Miravia ni relanzar la descarga.
            if (DataContext is CanalesExternosPedidosViewModel vm && vm.CanalSeleccionado == null)
            {
                vm.CanalSeleccionado = vm.Factory.First().Value;
            }
        }

        // Nesto#374: doble clic sobre la celda de comentarios (donde se ve el pedido) abre el
        // pedido de Nesto asignado. Solo esa columna: en la barra de desplazamiento, la cabecera
        // o los botones de la fila (Crear Pedido / Crear Etiqueta) no debe saltar.
        private void dgPedidos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridCell celda = BuscarAncestro<DataGridCell>(e.OriginalSource as DependencyObject);
            if (celda == null || celda.Column != colComentarios)
            {
                return;
            }

            if (DataContext is CanalesExternosPedidosViewModel vm
                && (sender as DataGrid)?.SelectedItem is PedidoCanalExterno pedido
                && vm.AbrirPedidoNestoCommand.CanExecute(pedido))
            {
                vm.AbrirPedidoNestoCommand.Execute(pedido);
            }
        }

        private static T BuscarAncestro<T>(DependencyObject origen) where T : class
        {
            while (origen != null && !(origen is T))
            {
                origen = VisualTreeHelper.GetParent(origen);
            }
            return origen as T;
        }
    }
}
