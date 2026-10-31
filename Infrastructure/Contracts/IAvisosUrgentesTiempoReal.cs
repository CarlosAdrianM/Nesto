using System;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#593 (NestoAPI#775): los avisos de las notificaciones URGENTES (la API decide qué tipos lo son; hoy, la autorización del
    /// repartidor para entregar un reembolso sin cobrar). Llegan por la misma conexión de SignalR que la campana (AvisosHub).
    /// Lo implementa <see cref="Shared.AvisosEnTiempoRealSignalR"/>. Los eventos llegan en un hilo que no es el de la UI.
    /// </summary>
    public interface IAvisosUrgentesTiempoReal
    {
        /// <summary>
        /// Hay que mirar las urgentes pendientes (GET api/Notificaciones/Urgentes): la API ha mandado «hayNotificacionUrgente», o se acaba
        /// de conectar o de recuperar la conexión (pudo llegar alguna mientras no había). Sin datos.
        /// </summary>
        event EventHandler HayNotificacionUrgente;

        /// <summary>«notificacionResuelta»: alguien (este Nesto u otro) ha contestado una urgente; se cierra en todos.</summary>
        event EventHandler<NotificacionResuelta> NotificacionResuelta;
    }

    /// <summary>El evento «notificacionResuelta» de AvisosHub (NestoAPI NotificacionResueltaDTO).</summary>
    public class NotificacionResuelta
    {
        public string Tipo { get; set; }
        public string Referencia { get; set; }
        /// <summary>Quién la ha contestado, sin dominio (en una telefónica, lo que escribió el repartidor).</summary>
        public string Respondio { get; set; }
        /// <summary>Para la autorización de reembolso: «Autorizada», «Denegada» o «Telefonica».</summary>
        public string Resultado { get; set; }
    }

    /// <summary>Una urgente pendiente (GET api/Notificaciones/Urgentes, NestoAPI NotificacionUrgenteDTO).</summary>
    public class NotificacionUrgente
    {
        public int IdBuzon { get; set; }
        public string Tipo { get; set; }
        public string Referencia { get; set; }
        public string Titulo { get; set; }
        public string Cuerpo { get; set; }
        public Dictionary<string, string> Datos { get; set; }
        public DateTime FechaCreacion { get; set; }
        /// <summary>Ha llegado por el escalado a Dirección (nadie de Administración contestó a tiempo).</summary>
        public bool Escalada { get; set; }

        /// <summary>El valor de <see cref="Datos"/> con esa clave (sin distinguir mayúsculas), sin espacios, o null.</summary>
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
                    return string.IsNullOrWhiteSpace(par.Value) ? null : par.Value.Trim();
                }
            }
            return null;
        }
    }
}
