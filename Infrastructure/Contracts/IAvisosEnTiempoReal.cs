using System;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#477: la API avisa de que hay notificaciones nuevas en el buzón del usuario, sin que Nesto
    /// tenga que preguntar cada poco (no se quiere cargar el servidor con sondeos de todos los puestos).
    /// La implementación real es el push por SignalR contra NestoAPI (NestoAPI#536,
    /// <see cref="Shared.AvisosEnTiempoRealSignalR"/>); <see cref="AvisosEnTiempoRealNulo"/> nunca avisa.
    /// El evento puede llegar desde un hilo que no es el de la UI.
    /// </summary>
    public interface IAvisosEnTiempoReal
    {
        event EventHandler HayNotificacionesNuevas;
    }

    /// <summary>Nesto#477: sin push: nunca avisa (la campana se refresca por sus otras vías).</summary>
    public sealed class AvisosEnTiempoRealNulo : IAvisosEnTiempoReal
    {
        public event EventHandler HayNotificacionesNuevas
        {
            add { }
            remove { }
        }
    }
}
