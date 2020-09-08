using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modulos.CanalesExternos
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class CanalesExternosPedidosView : UserControl
    {
        public CanalesExternosPedidosView(CanalesExternosPedidosViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            
            ((CanalesExternosPedidosViewModel)DataContext).CanalSeleccionado = ((CanalesExternosPedidosViewModel)DataContext).Factory.First().Value;
        }
    }
}
