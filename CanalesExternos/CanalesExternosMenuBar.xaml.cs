using System.Windows.Controls;

namespace Nesto.Modulos.CanalesExternos
{
    /// <summary>
    /// Lógica de interacción para CanalesExternosMenuBar.xaml
    /// </summary>
    public partial class CanalesExternosMenuBar : UserControl
    {
        public CanalesExternosMenuBar()
        {
            InitializeComponent();
        }

        public CanalesExternosMenuBar(CanalesExternosViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
