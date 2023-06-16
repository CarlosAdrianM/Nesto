using Nesto.Models;
using Prism.Events;

namespace Nesto.Infrastructure.Events
{
    public class PedidoModificadoEvent : PubSubEvent<PedidoVentaDTO> { }
}
