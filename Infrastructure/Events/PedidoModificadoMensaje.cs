using Nesto.Models;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Nesto.Infrastructure.Events
{
    /// <summary>Se ha modificado un pedido de venta (detalle o plantilla). Nesto#490 (4C.1): antes PedidoModificadoEvent de Prism.</summary>
    public sealed class PedidoModificadoMensaje : ValueChangedMessage<PedidoVentaDTO>
    {
        public PedidoModificadoMensaje(PedidoVentaDTO value) : base(value) { }
    }
}
