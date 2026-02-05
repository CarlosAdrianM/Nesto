using Nesto.Modulos.Ganavisiones.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modulos.Ganavisiones.Views
{
    public partial class GanavisionesMenuBar : UserControl
    {
        public GanavisionesMenuBar()
        {
            InitializeComponent();
        }

        public GanavisionesMenuBar(GanavisionesMenuBarViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
