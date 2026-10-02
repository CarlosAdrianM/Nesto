using System.Windows.Controls;
using Nesto.Modulos.CanalesExternos.ViewModels;

namespace Nesto.Modulos.CanalesExternos.Views
{
    /// <summary>
    /// Lógica de interacción para CanalesExternosProductosView.xaml
    /// </summary>
    public partial class CanalesExternosProductosView : UserControl
    {
        public CanalesExternosProductosView(CanalesExternosProductosViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
