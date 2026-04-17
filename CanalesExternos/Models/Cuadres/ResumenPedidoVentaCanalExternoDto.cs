using System;

namespace Nesto.Modulos.CanalesExternos.Models.Cuadres
{
    /// <summary>
    /// Deserialización de la respuesta de <c>GET api/PedidosVenta/PorCanalExterno</c>:
    /// resumen de un pedido de venta originado en un canal externo (Amazon), pensado para
    /// el cuadre Nesto ↔ canal por AmazonOrderId.
    /// </summary>
    public class ResumenPedidoVentaCanalExternoDto
    {
        public string Empresa { get; set; }
        public int Numero { get; set; }
        public DateTime Fecha { get; set; }
        public string Cliente { get; set; }

        /// <summary>
        /// Identificador del pedido en el canal externo (p. ej. AmazonOrderId).
        /// Puede ser null si el pedido existe en Nesto pero no se pudo identificar el OrderId.
        /// </summary>
        public string CanalOrderId { get; set; }
    }
}
