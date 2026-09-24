using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Nesto.Infrastructure.Events
{
    /// <summary>Se ha elegido un producto en la pantalla de Productos (el valor es su código). Nesto#490 (4C.1): antes ProductoSeleccionadoEvent de Prism.</summary>
    public sealed class ProductoSeleccionadoMensaje : ValueChangedMessage<string>
    {
        public ProductoSeleccionadoMensaje(string value) : base(value) { }
    }
}
