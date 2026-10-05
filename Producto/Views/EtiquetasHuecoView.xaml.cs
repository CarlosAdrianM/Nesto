using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>Etiquetas de hueco. Sin lógica: todo en <see cref="EtiquetasHuecoViewModel"/>.</summary>
    public partial class EtiquetasHuecoView : UserControl
    {
        public EtiquetasHuecoView(EtiquetasHuecoViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
