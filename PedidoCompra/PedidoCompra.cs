using Nesto.Infrastructure.Composicion;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoCompra.ViewModels;
using Nesto.Modulos.PedidoCompra.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace Nesto.Modulos.PedidoCompra
{
    public class PedidoCompra : IModule, IModuloNesto, IPedidoCompra
    {
        // Nesto#490 (4C.5): Prism sigue cargando el módulo por IModule, que delega en IModuloNesto
        void IModule.RegisterTypes(IContainerRegistry containerRegistry) => Registrar(new RegistroPrism(containerRegistry));
        void IModule.OnInitialized(IContainerProvider containerProvider) => Inicializar(new ResolutorPrism(containerProvider));

        public void Registrar(IRegistroNesto registro)
        {
            registro.Vista<PedidoCompraView>("PedidoCompraView");
            registro.Transitorio<IPedidoCompraService, PedidoCompraService>();
            registro.Vista<DetallePedidoCompraView>("DetallePedidoCompraView");
            registro.Vista<ListaPedidosCompraView>("ListaPedidosCompraView");

            // Nesto#490 (4C.5): lo que Unity construía sin registrar. PedidoCompraView y ListaPedidosCompraView también
            // se piden por su tipo (PedidoCompraViewModel y el maestro-detalle de PedidoCompraView).
            registro.Transitorio<PedidoCompraMenuBar>();
            registro.Transitorio<PedidoCompraViewModel>();
            registro.Transitorio<PedidoCompraView>();
            registro.Transitorio<ListaPedidosCompraView>();
        }

        public void Inicializar(IResolutorNesto resolutor)
        {
            var view = resolutor.Resolver<PedidoCompraMenuBar>();
            if (view != null)
            {
                resolutor.Resolver<IServicioCinta>().AnadirMenu(view);
            }
        }
    }
}
