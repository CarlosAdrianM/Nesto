using Nesto.Models;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Nesto.Infrastructure.Events
{
    /// <summary>Se ha modificado un pedido de venta (detalle o plantilla). Nesto#490 (4C.1): antes PedidoModificadoEvent de Prism.</summary>
    public sealed class PedidoModificadoMensaje : ValueChangedMessage<PedidoVentaDTO>
    {
        public PedidoModificadoMensaje(PedidoVentaDTO value) : base(value) { }

        /// <summary>
        /// Nesto#588 (revisión 30/10): true si el pedido solo se ha cargado en el detalle (no ha cambiado). La lista entonces se
        /// queda con el «fechas futuras» que ya le mandó la API, sin pedir la fecha mínima por cada pedido que se selecciona.
        /// </summary>
        public bool EsCarga { get; }

        public PedidoModificadoMensaje(PedidoVentaDTO value, bool esCarga) : base(value)
        {
            EsCarga = esCarga;
        }
    }
}
