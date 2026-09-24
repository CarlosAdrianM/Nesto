using Nesto.Models.Nesto.Models;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Nesto.Infrastructure.Events
{
    /// <summary>Se ha creado (o modificado) un cliente en CrearCliente. Nesto#490 (4C.1): antes ClienteCreadoEvent de Prism.</summary>
    public sealed class ClienteCreadoMensaje : ValueChangedMessage<Clientes>
    {
        public ClienteCreadoMensaje(Clientes value) : base(value) { }
    }
}
