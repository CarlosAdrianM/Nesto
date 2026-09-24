using System;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#477: la API avisa de que hay notificaciones nuevas en el buzón del usuario, sin que Nesto
    /// tenga que preguntar cada poco (no se quiere cargar el servidor con sondeos de todos los puestos).
    /// La implementación real es el push por SignalR contra NestoAPI (NestoAPI#536,
    /// <see cref="Shared.AvisosEnTiempoRealSignalR"/>); <see cref="AvisosEnTiempoRealNulo"/> nunca avisa.
    /// Nesto#492: además expone el estado de la conexión (la raya de la ventana principal lo pinta).
    /// Los eventos pueden llegar desde un hilo que no es el de la UI.
    /// </summary>
    public interface IAvisosEnTiempoReal
    {
        event EventHandler HayNotificacionesNuevas;

        /// <summary>Nesto#492: estado actual de la conexión en tiempo real con la API.</summary>
        EstadoConexionTiempoReal Estado { get; }

        /// <summary>Nesto#492: cambia <see cref="Estado"/>. Puede llegar desde otro hilo.</summary>
        event EventHandler EstadoCambiado;
    }

    /// <summary>Nesto#492: estado de la conexión en tiempo real con la API.</summary>
    public enum EstadoConexionTiempoReal
    {
        /// <summary>No se usa el tiempo real: implementación nula, sin arrancar o apagado con el parámetro AvisosTiempoReal = "NO".</summary>
        Desactivado,
        /// <summary>Primer intento de conexión en curso.</summary>
        Conectando,
        /// <summary>Conexión establecida con el hub.</summary>
        Conectado,
        /// <summary>Se ha perdido la conexión (o no se consigue) y se está reintentando.</summary>
        Reintentando
    }

    /// <summary>Nesto#477: sin push: nunca avisa (la campana se refresca por sus otras vías).</summary>
    public sealed class AvisosEnTiempoRealNulo : IAvisosEnTiempoReal
    {
        public event EventHandler HayNotificacionesNuevas
        {
            add { }
            remove { }
        }

        public EstadoConexionTiempoReal Estado => EstadoConexionTiempoReal.Desactivado;

        public event EventHandler EstadoCambiado
        {
            add { }
            remove { }
        }
    }
}
