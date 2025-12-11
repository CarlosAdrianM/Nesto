using ControlesUsuario.Models;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Servicio para buscar y validar productos.
    /// Issue #258 - Carlos 11/12/25
    /// </summary>
    public interface IServicioProducto
    {
        /// <summary>
        /// Busca un producto por su código.
        /// </summary>
        /// <param name="empresa">Código de empresa.</param>
        /// <param name="producto">Código del producto a buscar.</param>
        /// <param name="cliente">Código del cliente (para precios específicos).</param>
        /// <param name="contacto">Código del contacto del cliente.</param>
        /// <param name="cantidad">Cantidad solicitada (puede afectar el precio).</param>
        /// <returns>ProductoDTO si existe, null si no se encuentra.</returns>
        Task<ProductoDTO> BuscarProducto(string empresa, string producto, string cliente, string contacto, short cantidad);
    }
}
