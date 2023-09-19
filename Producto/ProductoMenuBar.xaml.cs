using System.Windows.Controls;
using Nesto.Modules.Producto;

namespace Nesto.Modulos.Producto
{
    /// <summary>
    /// Lógica de interacción para ProductoMenuBar.xaml
    /// </summary>
    public partial class ProductoMenuBar : UserControl
    {
        public ProductoMenuBar()
        {
            InitializeComponent();
        }

        public ProductoMenuBar(ProductoMenuBarViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
