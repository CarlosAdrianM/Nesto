using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Nesto.Infrastructure.Events
{
    /// <summary>
    /// Nesto#206: el rapport que se estaba creando NO se ha podido guardar. La lista lo había
    /// metido ya como fila (es lo que hace que la pantalla de rapport se abra sobre él) y, sin
    /// este aviso, la fila se quedaba como si el guardado hubiera ido bien. El valor es el
    /// propio rapport (como object, para no atar Infrastructure al módulo de Rapports).
    /// Nesto#490 (4C.1): antes RapportNoGuardadoEvent de Prism.
    /// </summary>
    public sealed class RapportNoGuardadoMensaje : ValueChangedMessage<object>
    {
        public RapportNoGuardadoMensaje(object value) : base(value) { }
    }
}
