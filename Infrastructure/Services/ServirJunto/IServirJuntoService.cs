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
        Task<ValidarServirJuntoResponse> Validar(
            string almacen,
            List<ProductoBonificadoConCantidadRequest> productosBonificados,
            List<ProductoBonificadoConCantidadRequest> lineasPedido);
    }
}
