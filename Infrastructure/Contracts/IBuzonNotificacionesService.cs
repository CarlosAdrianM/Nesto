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
        /// NestoAPI#631 / Nesto#529: aviso a Compras de que el job mensual ha dejado propuestas de stock pendientes de decidir.
        /// </summary>
        public const string TIPO_PROPUESTAS_STOCK = "PropuestasStock";

        /// <summary>Nesto#529: la ventana (módulo Producto) que abre ese aviso al pulsarlo.</summary>
        public const string VISTA_PROPUESTAS_STOCK = "PropuestasStockView";

        /// <summary>
        /// Nesto#526 (NestoAPI#634): el aviso de que lo recibido en una reposición no coincide con lo enviado. Trae
        /// Datos["vista"] = <see cref="DATO_VISTA_DIFERENCIAS_REPOSICION"/> y abre <see cref="VISTA_DIFERENCIAS_REPOSICION"/>.
        /// </summary>
        public const string TIPO_RECEPCION_REPOSICION = "RecepcionReposicion";
        public const string DATO_VISTA_DIFERENCIAS_REPOSICION = "DiferenciasReposicion";
        /// <summary>Nesto#526: la ventana (módulo Producto) de las diferencias de reposición.</summary>
        public const string VISTA_DIFERENCIAS_REPOSICION = "DiferenciasReposicionView";

        /// <summary>
        /// Nesto#546 (NestoAPI#693): la fecha de entrega a la agencia prometida de un pedido se retrasa o se queda sin fecha
        /// (TextosPromesaRiesgo.TIPO_NOTIFICACION). Trae Datos["pedido"] y Datos["empresa"] y abre
        /// <see cref="VISTA_PEDIDOS_PROMESA_RIESGO"/> con ese pedido elegido, no el pedido suelto.
        /// </summary>
        public const string TIPO_PROMESA_EN_RIESGO = "PromesaEnRiesgo";
        /// <summary>Nesto#546: la ventana (módulo Cliente) «Pedidos con la fecha en riesgo».</summary>
        public const string VISTA_PEDIDOS_PROMESA_RIESGO = "PedidosPromesaRiesgoView";
        /// <summary>Nesto#546: parámetros de navegación con que la campana abre esa ventana en un pedido.</summary>
        public const string PARAMETRO_PROMESA_PEDIDO = "pedido";
        public const string PARAMETRO_PROMESA_EMPRESA = "empresa";

        /// <summary>
        /// Nesto#568 (NestoAPI#734): el almacén pregunta a Administración si un pedido con enlaces de pago pendientes sale con o
        /// sin reembolso (Datos["accion"] = <see cref="ACCION_CONSULTA_REEMBOLSO_CONSULTA"/>: abre <see cref="DIALOGO_CONSULTA_REEMBOLSO"/>
        /// con Datos["empresa"] y Datos["pedido"]), o Administración contesta («respuesta»: abre el pedido, como cualquier aviso de un pedido).
        /// </summary>
        public const string TIPO_CONSULTA_REEMBOLSO = "ConsultaReembolso";
        public const string ACCION_CONSULTA_REEMBOLSO_CONSULTA = "consulta";
        /// <summary>Nesto#568: el diálogo (módulo PedidoVenta) con el que Administración contesta.</summary>
        public const string DIALOGO_CONSULTA_REEMBOLSO = "ConsultaReembolsoDialog";

        /// <summary>
        /// Nesto#509 (Ariadna#8): un mozo dice que un dato de la ficha de un producto está mal. Trae Datos["avisoId"] y
        /// Datos["producto"]; se cierra con <see cref="RESULTADO_AVISO_CAMBIADO"/> o <see cref="RESULTADO_AVISO_ESTABA_BIEN"/>.
        /// </summary>
        public const string TIPO_AVISO_FICHA_PRODUCTO = "AvisoFichaProducto";
        public const string RESULTADO_AVISO_CAMBIADO = "Cambiado";
        public const string RESULTADO_AVISO_ESTABA_BIEN = "EstabaBien";

        /// <summary>
        /// Nesto#591 (NestoAPI#773): el repartidor pide entregar un reembolso sin cobrarlo. Trae Datos["autorizacionId"],
        /// Datos["envio"] y Datos["importe"]; Administración o Dirección lo autoriza o lo deniega desde la campana.
        /// </summary>
        public const string TIPO_AUTORIZACION_REEMBOLSO_REPARTO = "AutorizacionReembolsoReparto";
        /// <summary>Nesto#591: lo autorizó alguien por teléfono (solo aviso, para revisarlo).</summary>
        public const string TIPO_AUTORIZACION_REEMBOLSO_TELEFONICA = "AutorizacionReembolsoTelefonica";

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

        /// <summary>
        /// Nesto#591 (NestoAPI#773): autoriza o deniega que el repartidor deje un reembolso sin cobrar
        /// (POST api/RepartoPropio/Autorizaciones/{id}/Responder). Devuelve el estado que queda («Autorizada» / «Denegada»). Lanza con
        /// el motivo de la API (sin permiso, ya contestada por otro o autorizada por teléfono…).
        /// </summary>
        Task<string> ResponderAutorizacionReembolso(int autorizacionId, bool autorizar, string comentario);
    }
}
