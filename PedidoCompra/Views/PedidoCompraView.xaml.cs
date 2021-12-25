using Prism.Ioc;
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
    /// Lógica de interacción para PedidoCompraView.xaml
    /// </summary>
    public partial class PedidoCompraView : UserControl
    {
        public IRegionManager ScopedRegionManager { get; set; }
        public IContainerProvider ContainerProvider { get; }

        private bool Cargado = false;
        public PedidoCompraView(IContainerProvider containerProvider)
        {
            InitializeComponent();
            ContainerProvider = containerProvider;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!Cargado)
            {
                ListaPedidosCompraView view = ContainerProvider.Resolve<ListaPedidosCompraView>();
                view.CambiarRegionManager(ScopedRegionManager);
                IRegion region = ScopedRegionManager.Regions["ListaPedidosCompraRegion"];
                region.Add(view, "ListaPedidosCompraRegion");
                region.Activate(view);
                Cargado = true;
            }
        }
    }
}
