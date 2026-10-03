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

        /// <summary>
        /// Nesto#501: tipo del aviso de versión nueva (NestoAPI, POST api/Notificaciones/NuevaVersionNesto,
        /// ritual del deploy). Trae la versión en Datos["version"].
        /// </summary>
        public const string TIPO_NUEVA_VERSION_NESTO = "NuevaVersionNesto";

        /// <summary>NestoAPI#522: recordatorio diario a administración de las facturas pendientes de Verifactu.</summary>
        public const string TIPO_FACTURAS_PENDIENTES_VERIFACTU = "FacturasPendientesVerifactu";

        /// <summary>NestoAPI#522: la ventana (módulo Cajas) que abre ese recordatorio al pulsarlo.</summary>
        public const string VISTA_FACTURAS_PENDIENTES_VERIFACTU = "FacturasPendientesVerifactuView";

        /// <summary>
        /// Nesto#509 (Ariadna#8): un mozo dice que un dato de la ficha de un producto está mal. Trae Datos["avisoId"] y
        /// Datos["producto"]; se cierra con <see cref="RESULTADO_AVISO_CAMBIADO"/> o <see cref="RESULTADO_AVISO_ESTABA_BIEN"/>.
        /// </summary>
        public const string TIPO_AVISO_FICHA_PRODUCTO = "AvisoFichaProducto";
        public const string RESULTADO_AVISO_CAMBIADO = "Cambiado";
        public const string RESULTADO_AVISO_ESTABA_BIEN = "EstabaBien";

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

        /// <summary>
        /// Nesto#509: cierra un aviso de dato mal en la ficha (POST api/Almacen/AvisosFicha/{avisoId}/Cerrar) con
        /// <see cref="NotificacionBuzon.RESULTADO_AVISO_CAMBIADO"/> o <see cref="NotificacionBuzon.RESULTADO_AVISO_ESTABA_BIEN"/>.
        /// Devuelve el texto de la API (lo que ha pasado). Lanza con su motivo (no es del equipo, ya cerrado…).
        /// </summary>
        Task<string> CerrarAvisoFicha(int avisoId, string resultado);
    }
}
