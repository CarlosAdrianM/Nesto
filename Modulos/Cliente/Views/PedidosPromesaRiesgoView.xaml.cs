using System.Windows.Controls;
using System.Windows.Input;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#546 (NestoAPI#693): «Pedidos con la fecha en riesgo». La lógica está en <see cref="PedidosPromesaRiesgoViewModel"/>,
    /// que se lee a sí mismo al llegar (IReceptorNavegacion).
    /// </summary>
    public partial class PedidosPromesaRiesgoView : UserControl
    {
        public PedidosPromesaRiesgoView(PedidosPromesaRiesgoViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>Doble clic en una fila: abre ese pedido (el resto de la fila sigue seleccionando sin más).</summary>
        private void Fila_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow fila || fila.Item is not FilaPedidoPromesaRiesgo pedido || DataContext is not PedidosPromesaRiesgoViewModel vm)
            {
                return;
            }
            if (vm.AbrirPedidoCommand?.CanExecute(pedido) != true)
            {
                return;
            }
            e.Handled = true;
            vm.AbrirPedidoCommand.Execute(pedido);
        }
    }
}
