using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Nesto.Infrastructure.Events
{
    /// <summary>Se ha guardado un rapport. Nesto#490 (4C.1): antes RapportGuardadoEvent de Prism.</summary>
    public sealed class RapportGuardadoMensaje : ValueChangedMessage<int>
    {
        public RapportGuardadoMensaje(int value) : base(value) { }
    }
}
