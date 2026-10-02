using System.Windows.Controls;
using Nesto.Modulos.Cajas.ViewModels;

namespace Nesto.Modulos.Cajas.Views
{
    /// <summary>
    /// Logica de interaccion para MayorCuentaView.xaml
    /// </summary>
    public partial class MayorCuentaView : UserControl
    {
        public MayorCuentaView(MayorCuentaViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
