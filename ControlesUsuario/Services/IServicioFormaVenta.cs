using ControlesUsuario.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Servicio para obtener Formas de Venta desde la API.
    /// Carlos 04/12/2024: Issue #252 - Creado para el nuevo control SelectorFormaVenta.
    /// </summary>
    public interface IServicioFormaVenta
    {
        /// <summary>
        /// Obtiene las formas de venta disponibles para una empresa.
        /// </summary>
        /// <param name="empresa">Código de la empresa</param>
        /// <returns>Lista de formas de venta de la empresa</returns>
        /// <exception cref="System.ArgumentException">Si empresa es null/vacía</exception>
        /// <exception cref="System.Exception">Si hay error en la llamada HTTP</exception>
        Task<IEnumerable<FormaVentaItem>> ObtenerFormasVenta(string empresa);
    }
}
