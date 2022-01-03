using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoCompra.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.RibbonRegionAdapter;

namespace Nesto.Modulos.PedidoCompra
{
    public class PedidoCompra : IModule, IPedidoCompra
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var view = containerProvider.Resolve<PedidoCompraMenuBar>();
            if (view != null)
            {
                var regionAdapter = containerProvider.Resolve<RibbonRegionAdapter>();
                var mainWindow = containerProvider.Resolve<IMainWindow>();
                var region = regionAdapter.Initialize(mainWindow.mainRibbon, "PedidoCompra");

                region.Add(view, "MenuBar");
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
