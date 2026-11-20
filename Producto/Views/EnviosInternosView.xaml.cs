using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// Sugerencia 1277: la ventana «Envíos internos» entre sedes. La lógica está en <see cref="EnviosInternosViewModel"/> (lee al llegar,
    /// IReceptorNavegacion).
    /// </summary>
    public partial class EnviosInternosView : UserControl
    {
        public EnviosInternosView(EnviosInternosViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
