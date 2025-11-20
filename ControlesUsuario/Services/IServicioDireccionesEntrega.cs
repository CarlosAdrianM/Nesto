using ControlesUsuario.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Servicio para obtener direcciones de entrega de clientes desde la API.
    /// Carlos 20/11/2024: Creado para hacer SelectorDireccionEntrega testeable (FASE 3).
    /// </summary>
    public interface IServicioDireccionesEntrega
    {
        /// <summary>
        /// Obtiene las direcciones de entrega para un cliente específico.
        /// </summary>
        /// <param name="empresa">Empresa del cliente</param>
        /// <param name="cliente">Número de cliente</param>
        /// <param name="totalPedido">Total del pedido (opcional, puede afectar a direcciones disponibles)</param>
        /// <returns>Lista de direcciones de entrega del cliente</returns>
        /// <exception cref="System.ArgumentException">Si empresa o cliente son null/vacíos</exception>
        /// <exception cref="System.Exception">Si hay error en la llamada HTTP</exception>
        Task<IEnumerable<DireccionesEntregaCliente>> ObtenerDireccionesEntrega(
            string empresa,
            string cliente,
            decimal? totalPedido = null
        );
    }
}
