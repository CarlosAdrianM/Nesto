using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoCompra.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modulos.PedidoCompra
{
    /// <summary>
    /// Lógica de interacción para PedidoCompraMenuBar.xaml
    /// </summary>
    public partial class PedidoCompraMenuBar : UserControl
    {
        public PedidoCompraMenuBar()
        {
            InitializeComponent();
        }

        public PedidoCompraMenuBar(PedidoCompraViewModel viewModel, IConfiguracion configuracion)
        {
            InitializeComponent();
            DataContext = viewModel;
            Configuracion = configuracion;
        }

        public IConfiguracion Configuracion { get; private set; }

        private void btnComprasPedidos_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN))
            {
                grpCompras.Visibility = System.Windows.Visibility.Visible;
            }
        }
    }
}
