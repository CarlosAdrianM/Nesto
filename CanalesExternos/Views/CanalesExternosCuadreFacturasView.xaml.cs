using System.Windows.Controls;
using Nesto.Modulos.CanalesExternos.ViewModels;

namespace Nesto.Modulos.CanalesExternos.Views
{
    public partial class CanalesExternosCuadreFacturasView : UserControl
    {
        public CanalesExternosCuadreFacturasView(CanalesExternosCuadreFacturasViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
