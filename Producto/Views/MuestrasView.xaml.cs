using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>Nesto#650: muestras (etiquetas de cajón o gaveta, código interno y hoja). Sin lógica: todo en <see cref="MuestrasViewModel"/>.</summary>
    public partial class MuestrasView : UserControl
    {
        public MuestrasView(MuestrasViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
