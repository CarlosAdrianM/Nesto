using CanalesExternos.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Interfaces
{
    /// <summary>
    /// Servicio para gestionar poison pills de mensajes de sincronización
    /// </summary>
    public interface IPoisonPillsService
    {
        /// <summary>
        /// Obtiene la lista de poison pills con filtros opcionales
        /// </summary>
        /// <param name="status">Estado para filtrar (opcional): PoisonPill, Retrying, etc.</param>
        /// <param name="tabla">Tabla para filtrar (opcional): Clientes, Productos, etc.</param>
        /// <param name="limit">Límite de resultados (default: 100)</param>
        /// <returns>Lista de poison pills</returns>
        Task<List<PoisonPillModel>> ObtenerPoisonPillsAsync(string status = null, string tabla = null, int limit = 100);

        /// <summary>
        /// Cambia el estado de un poison pill
        /// </summary>
        /// <param name="messageId">ID del mensaje</param>
        /// <param name="newStatus">Nuevo estado: Reprocess, Resolved, o PermanentFailure</param>
        /// <returns>True si el cambio fue exitoso</returns>
        Task<bool> CambiarEstadoAsync(string messageId, string newStatus);
    }
}
