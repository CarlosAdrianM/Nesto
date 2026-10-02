using System.Windows.Controls;
using Nesto.Modulos.CanalesExternos.ViewModels;

namespace Nesto.Modulos.CanalesExternos.Views
{
    /// <summary>
    /// Lógica de interacción para PoisonPillsView.xaml
    /// </summary>
    public partial class PoisonPillsView : UserControl
    {
        public PoisonPillsView(PoisonPillsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
