using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// Propuestas de stock (Nesto#529, NestoAPI#631). La lógica está en <see cref="PropuestasStockViewModel"/>; se carga al
    /// navegar a ella (<see cref="Nesto.Infrastructure.Contracts.IReceptorNavegacion"/>).
    /// </summary>
    public partial class PropuestasStockView : UserControl
    {
        public PropuestasStockView(PropuestasStockViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
