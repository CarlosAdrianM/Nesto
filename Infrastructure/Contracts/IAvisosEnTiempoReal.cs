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

    /// <summary>
    /// Ariadna#51 (corte 2): avisos de un documento del almacén compartido con Ariadna (el hub «AlmacenHub» de NestoAPI#646, en
    /// la misma conexión que la campana). La pantalla entra en el grupo del documento al abrirlo y sale al cerrarlo; cuando otro
    /// equipo cambia algo, la API manda al grupo solo el aviso (sin datos) y la pantalla pide lo nuevo. Sin sondeo.
    /// Lo implementa <see cref="Shared.AvisosEnTiempoRealSignalR"/>; sin él (o desactivado) no llega ningún aviso.
    /// </summary>
    public interface IGruposAlmacenTiempoReal
    {
        /// <summary>Entra en el grupo (y vuelve a entrar solo al reconectar). Nunca lanza.</summary>
        void EntrarEnGrupo(string grupo);

        /// <summary>Sale del grupo. Nunca lanza.</summary>
        void SalirDeGrupo(string grupo);

        /// <summary>
        /// Otro equipo ha cambiado algo del grupo (el argumento es el grupo). También tras recuperar la conexión, por si se
        /// perdió algún aviso. Llega en un hilo que no es el de la UI.
        /// </summary>
        event EventHandler<string> GrupoHaCambiado;

        /// <summary>La conexión de SignalR de este Nesto (para que la API no le avise de lo suyo). Null si no está conectado.</summary>
        string IdConexion { get; }
    }

    /// <summary>Ariadna#51: los nombres de los grupos del almacén, en un solo sitio (los mismos en la API y en Ariadna).</summary>
    public static class GruposAlmacen
    {
        /// <summary>«recepcion:REPO:81119:REI»: la recepción en curso de un documento en un almacén.</summary>
        public static string Recepcion(string tipo, string documento, string almacen)
            => $"recepcion:{(tipo ?? string.Empty).Trim().ToUpperInvariant()}:{(documento ?? string.Empty).Trim()}:{(almacen ?? string.Empty).Trim().ToUpperInvariant()}";
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
