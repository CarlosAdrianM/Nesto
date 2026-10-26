using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// Sugerencia 648 (NestoAPI#658): el Excel de lo facturado en la tienda. La lógica está en
    /// <see cref="FacturacionTiendaViewModel"/> (al llegar lee la tienda del usuario, IReceptorNavegacion).
    /// </summary>
    public partial class FacturacionTiendaView : UserControl
    {
        public FacturacionTiendaView(FacturacionTiendaViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
