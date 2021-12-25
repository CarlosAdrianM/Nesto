using Nesto.Modulos.PedidoCompra.ViewModels;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Nesto.Modulos.PedidoCompra.Views
{
    /// <summary>
    /// Lógica de interacción para ListaPedidosCompraView.xaml
    /// </summary>
    public partial class ListaPedidosCompraView : UserControl
    {
        public ListaPedidosCompraView()
        {
            InitializeComponent();
        }

        public void CambiarRegionManager(IRegionManager newRegionManager)
        {
            ListaPedidosCompraViewModel vm = (ListaPedidosCompraViewModel)this.DataContext;
            vm.ScopedRegionManager = newRegionManager;
        }

        private void txtFiltro_GotFocus(object sender, RoutedEventArgs e)
        {
            txtFiltro.SelectAll();
        }

        private void txtFiltro_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            txtFiltro.SelectAll();
        }
    }
}
