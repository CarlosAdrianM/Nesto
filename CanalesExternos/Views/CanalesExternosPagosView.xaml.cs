using System.Windows.Controls;
using Nesto.Modulos.CanalesExternos.ViewModels;

namespace Nesto.Modulos.CanalesExternos.Views
{
    /// <summary>
    /// Lógica de interacción para CanalesExternosPagosView.xaml
    /// </summary>
    public partial class CanalesExternosPagosView : UserControl
    {
        public CanalesExternosPagosView(CanalesExternosPagosViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
