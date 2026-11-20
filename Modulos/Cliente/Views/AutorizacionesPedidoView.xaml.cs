using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Novedades 1258: «Autorizaciones de pedidos». La lógica está en <see cref="AutorizacionesPedidoViewModel"/>, que se lee a sí mismo
    /// al llegar (IReceptorNavegacion): desde el menú de Pedidos o desde la campana.
    /// </summary>
    public partial class AutorizacionesPedidoView : UserControl
    {
        public AutorizacionesPedidoView(AutorizacionesPedidoViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
