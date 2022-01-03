using Prism.Events;
using static Nesto.Models.PedidoVenta;

namespace Nesto.Infrastructure.Events
{
    public class PedidoCreadoEvent : PubSubEvent<PedidoVentaDTO> { }
}
