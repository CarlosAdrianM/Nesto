using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>NestoAPI#883 (corte 1): la ventana «Fechas de inventario». La lógica está en <see cref="FechasInventarioViewModel"/> (lee al llegar).</summary>
    public partial class FechasInventarioView : UserControl
    {
        public FechasInventarioView(FechasInventarioViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
