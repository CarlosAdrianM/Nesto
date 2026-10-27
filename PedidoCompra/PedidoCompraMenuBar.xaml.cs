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
            // Nesto#490 (panel lateral, tramo 1): antes se miraba en el Loaded del botón, que con la cinta colapsada (menú
            // lateral) no llega nunca y el botón no salía para nadie. El grupo ALMACEN ya lo ha consultado (y cacheado) el
            // MenuBar al arrancar, así que mirarlo aquí no cuesta nada. Tramo 4: pasará a una propiedad del ViewModel.
            AplicarPermisos();
        }

        public IConfiguracion Configuracion { get; private set; }

        private void AplicarPermisos()
        {
            if (Configuracion?.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN) == true)
            {
                grpCompras.Visibility = System.Windows.Visibility.Visible;
            }
        }
    }
}
