using Nesto.Modulos.CanalesExternos.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modulos.CanalesExternos.Views
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class CanalesExternosPedidosView : UserControl
    {
        public CanalesExternosPedidosView()
        {
            InitializeComponent();
        }
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {           
            ((CanalesExternosPedidosViewModel)DataContext).CanalSeleccionado = ((CanalesExternosPedidosViewModel)DataContext).Factory.First().Value;
        }
    }
}
