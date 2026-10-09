using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoCompra.Models;
using Nesto.Modulos.PedidoCompra.Views;
using CommunityToolkit.Mvvm.Input;
using Prism.Ioc;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nesto.Modulos.PedidoCompra.ViewModels
{
    // Nesto#490 (4C.4, 6.º tramo): sin INavigationAware. Lo implementaba vacío (IsNavigationTarget = false) y nadie navega
    // a PedidoCompraView: es el DataContext de la cinta y de la vista que AbrirModulo añade a mano a MainRegion.
    // Nesto#490 (4C.4, 7.º tramo): sin IRegionManager. Cada apertura es una pestaña nueva con su propio ámbito de regiones
    // (IServicioNavegacion.AbrirVistaConAmbito), que recibe la vista por IConAmbitoNavegacion. Este ViewModel lo comparten la
    // cinta y todas las pestañas, así que no guarda el ámbito (antes lo guardaba en ScopedRegionManager y nadie lo leía).
    public class PedidoCompraViewModel : ObservableObject
    {
        private IServicioNavegacion Navegacion { get; }
        public IConfiguracion Configuracion { get; set; }
        private IContainerProvider ContainerProvider { get; }
        

        public PedidoCompraViewModel(IServicioNavegacion navegacion, IConfiguracion configuracion, IContainerProvider containerProvider)
        {
            Navegacion = navegacion;
            Configuracion = configuracion;
            ContainerProvider = containerProvider;

            AbrirModuloCommand = new RelayCommand(OnAbrirModulo);

            Titulo = "Pedido Compra";
        }

        public string Titulo { get; private set; }

        public ICommand AbrirModuloCommand { get; private set; }
        private void OnAbrirModulo()
        {
            var view = ContainerProvider.Resolve<PedidoCompraView>();
            if (view != null)
            {
                _ = Navegacion.AbrirVistaConAmbito("MainRegion", view);
            }
        }

    }
}
