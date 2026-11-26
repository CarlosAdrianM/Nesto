using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>Sugerencia 1482: la ventana «Extracto del producto». La lógica está en <see cref="ExtractoProductoViewModel"/>.</summary>
    public partial class ExtractoProductoView : UserControl
    {
        public ExtractoProductoView(ExtractoProductoViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
