using System.Windows.Controls;
using Nesto.Modulos.CanalesExternos.ViewModels;

namespace Nesto.Modulos.CanalesExternos.Views
{
    public partial class CanalesExternosFacturasView : UserControl
    {
        public CanalesExternosFacturasView(CanalesExternosFacturasViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
