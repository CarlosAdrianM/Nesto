using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Nesto.Infrastructure.Events
{
    /// <summary>Se ha sacado un picking. Nesto#490 (4C.1): antes SacarPickingEvent de Prism.</summary>
    public sealed class SacarPickingMensaje : ValueChangedMessage<int>
    {
        public SacarPickingMensaje(int value) : base(value) { }
    }
}
