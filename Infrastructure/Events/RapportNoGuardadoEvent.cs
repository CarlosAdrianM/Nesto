using Prism.Events;

namespace Nesto.Infrastructure.Events
{
    /// <summary>
    /// Nesto#206: el rapport que se estaba creando NO se ha podido guardar. La lista lo había
    /// metido ya como fila (es lo que hace que la pantalla de rapport se abra sobre él) y, sin
    /// este aviso, la fila se quedaba como si el guardado hubiera ido bien. El payload es el
    /// propio rapport (como object, para no atar Infrastructure al módulo de Rapports).
    /// </summary>
    public class RapportNoGuardadoEvent : PubSubEvent<object> { }
}
