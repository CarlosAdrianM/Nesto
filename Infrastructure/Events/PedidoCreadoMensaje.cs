using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Nesto.Infrastructure.Events
{
    /// <summary>Se ha creado un pedido de venta en el detalle. Nesto#490 (4C.1): antes PedidoCreadoEvent de Prism.</summary>
    public sealed class PedidoCreadoMensaje : ValueChangedMessage<PedidoCreadoEventArgs>
    {
        public PedidoCreadoMensaje(PedidoCreadoEventArgs value) : base(value) { }
    }
}
