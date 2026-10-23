using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoCompra.ViewModels;
using Nesto.Modulos.PedidoCompra.Views;

namespace Nesto.Modulos.PedidoCompra
{
    public class PedidoCompra : IModuloNesto, IPedidoCompra
    {
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
            // Los ViewModels de sus vistas
            registro.Transitorio<ListaPedidosCompraViewModel>();
            registro.Transitorio<DetallePedidoCompraViewModel>();
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
