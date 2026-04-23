using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services.ServirJunto
{
    /// <summary>
    /// Cliente del endpoint canónico api/PedidosVenta/ValidarServirJunto.
    /// Usado tanto por PlantillaVenta como por DetallePedido.
    /// </summary>
    public interface IServirJuntoService
    {
        /// <summary>
        /// Valida si se puede desmarcar "Servir junto". Los parámetros adicionales
        /// (NestoAPI#187) dan al backend los datos necesarios para calcular si el
        /// pedido aplica comisión contra reembolso y devolver el aviso en la
        /// respuesta; si se omiten, el campo Aviso puede no reflejar correctamente
        /// la situación del pedido.
        /// </summary>
        Task<ValidarServirJuntoResponse> Validar(
            string almacen,
            List<ProductoBonificadoConCantidadRequest> productosBonificados,
            List<ProductoBonificadoConCantidadRequest> lineasPedido,
            string formaPago = null,
            string plazosPago = null,
            string ccc = null,
            string periodoFacturacion = null,
            bool? notaEntrega = null);
    }
}
