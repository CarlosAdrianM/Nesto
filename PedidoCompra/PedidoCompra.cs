using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoCompra.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace Nesto.Modulos.PedidoCompra
{
    public class PedidoCompra : IModule, IPedidoCompra
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var view = containerProvider.Resolve<PedidoCompraMenuBar>();
            if (view != null)
            {
                containerProvider.Resolve<IServicioCinta>().AnadirMenu(view);
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<object, PedidoCompraView>("PedidoCompraView");
            containerRegistry.Register<IPedidoCompraService, PedidoCompraService>();
            containerRegistry.Register<object, DetallePedidoCompraView>("DetallePedidoCompraView");
            containerRegistry.Register<object, ListaPedidosCompraView>("ListaPedidosCompraView");
        }
    }
}
