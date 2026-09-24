using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#477: una notificación del buzón tal y como la devuelve la API (NotificacionBuzonDTO de
    /// NestoAPI#387). <see cref="Datos"/> dice a dónde lleva al pulsarla.
    /// </summary>
    public class NotificacionBuzon
    {
        /// <summary>Tipo que deja la API cuando el asistente contesta un comentario de Novedades.</summary>
        public const string TIPO_NOVEDAD_COMENTARIO = "NovedadComentario";

        public int Id { get; set; }
        public string Titulo { get; set; }
        public string Cuerpo { get; set; }
        public Dictionary<string, string> Datos { get; set; }
        public DateTime FechaCreacion { get; set; }
        public bool Leida { get; set; }

        public string Tipo => Dato("tipo");

        /// <summary>El valor de <see cref="Datos"/> con esa clave (sin distinguir mayúsculas) o null.</summary>
        public string Dato(string clave)
        {
            if (Datos == null || clave == null)
            {
                return null;
            }
            foreach (KeyValuePair<string, string> par in Datos)
            {
                if (string.Equals(par.Key, clave, StringComparison.OrdinalIgnoreCase))
                {
                    return par.Value;
                }
            }
            return null;
        }

        /// <summary>El dato como número, o null si no está o no es un número.</summary>
        public int? DatoEntero(string clave)
            => int.TryParse(Dato(clave), out int valor) ? valor : (int?)null;
    }

    /// <summary>
    /// Nesto#477: el buzón de notificaciones del usuario de Nesto (api/Notificaciones/Buzon). Manda
    /// SIEMPRE aplicacion=Nesto (sin ella la API entiende NestoApp). El usuario es el del token.
    /// Todos lanzan con el mensaje de la API; es quien llama el que decide si lo calla.
    /// </summary>
    public interface IBuzonNotificacionesService
    {
        /// <summary>Las notificaciones, de la más reciente a la más antigua.</summary>
        Task<List<NotificacionBuzon>> LeerBuzon(bool soloNoLeidas = false, int pagina = 1, int tamanoPagina = 20);
        Task<int> ContarNoLeidas();
        Task MarcarLeida(int id);
        /// <summary>Devuelve cuántas se han marcado.</summary>
        Task<int> MarcarTodasLeidas();
        Task Eliminar(int id);
    }
}
