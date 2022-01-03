using Nesto.Models.Nesto.Models;
using Prism.Events;

namespace Nesto.Infrastructure.Events
{
    public class ClienteModificadoEvent : PubSubEvent<Clientes> { }
}
