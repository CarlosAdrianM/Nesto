using Nesto.Infrastructure.Contracts;
using Prism.Ioc;
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
using Nesto.Modulos.PedidoCompra.ViewModels;

namespace Nesto.Modulos.PedidoCompra.Views
{
    /// <summary>
    /// Lógica de interacción para PedidoCompraView.xaml
    /// </summary>
    // Nesto#490 (4C.4, 7.º tramo): la navegación de su ámbito (sus regiones de lista y detalle) le llega por
    // IConAmbitoNavegacion al abrirla con AbrirVistaConAmbito, en vez del IRegionManager con ámbito de Prism.
    public partial class PedidoCompraView : UserControl, IConAmbitoNavegacion
    {
        public IServicioNavegacion NavegacionAmbito { get; set; }
        public IContainerProvider ContainerProvider { get; }

        private bool Cargado = false;
        public PedidoCompraView(IContainerProvider containerProvider, PedidoCompraViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            ContainerProvider = containerProvider;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!Cargado && NavegacionAmbito != null)
            {
                // La lista recibe esta misma navegación (IConAmbitoNavegacion) al entrar en la región: navega al detalle de esta pestaña
                ListaPedidosCompraView view = ContainerProvider.Resolve<ListaPedidosCompraView>();
                NavegacionAmbito.AbrirVistaNueva("ListaPedidosCompraRegion", view, "ListaPedidosCompraRegion");
                Cargado = true;
            }
        }
    }
}
