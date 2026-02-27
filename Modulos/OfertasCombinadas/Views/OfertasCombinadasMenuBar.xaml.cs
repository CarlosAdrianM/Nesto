using Nesto.Modulos.OfertasCombinadas.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modulos.OfertasCombinadas.Views
{
    public partial class OfertasCombinadasMenuBar : UserControl
    {
        public OfertasCombinadasMenuBar()
        {
            InitializeComponent();
        }

        public OfertasCombinadasMenuBar(OfertasCombinadasMenuBarViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
