using Nesto.Modulos.Cajas.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Interfaces
{
    /// <summary>
    /// Nesto#261: auditoría de enlaces de pago (GET api/Pagos/Auditoria, solo Administración y Dirección).
    /// Lanza con el mensaje de la API si no se puede.
    /// </summary>
    public interface IAuditoriaEnlacesPagoService
    {
        Task<List<EnlacePagoAuditoriaModel>> Buscar(FiltroAuditoriaEnlacesPago filtro);
    }
}
