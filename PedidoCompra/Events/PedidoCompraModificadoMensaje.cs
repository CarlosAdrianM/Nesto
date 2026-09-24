using CommunityToolkit.Mvvm.Messaging.Messages;
using Nesto.Modulos.PedidoCompra.Models;

namespace Nesto.Modulos.PedidoCompra.Events
{
    /// <summary>Se ha guardado un pedido de compra. Nesto#490 (4C.1): antes PedidoCompraModificadoEvent de Prism.</summary>
    public sealed class PedidoCompraModificadoMensaje : ValueChangedMessage<PedidoCompraDTO>
    {
        public PedidoCompraModificadoMensaje(PedidoCompraDTO value) : base(value) { }
    }
}
