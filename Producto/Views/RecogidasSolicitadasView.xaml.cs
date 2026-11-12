using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// Nesto#635: la ventana «Recogidas solicitadas». La lógica está en <see cref="RecogidasSolicitadasViewModel"/> (lee al llegar,
    /// IReceptorNavegacion; sale del grupo de SignalR al cerrar, ITabCloseConfirmation).
    /// </summary>
    public partial class RecogidasSolicitadasView : UserControl
    {
        public RecogidasSolicitadasView(RecogidasSolicitadasViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
