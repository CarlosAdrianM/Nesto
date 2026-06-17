using Nesto.Infrastructure.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// Cliente de los endpoints api/Agencias/RecargosCombustible de NestoAPI (Nesto#340):
    /// leer y actualizar el % de fuel por agencia.
    /// </summary>
    public interface IServicioRecargosCombustible
    {
        Task<List<RecargoCombustibleAgencia>> LeerRecargos();
        Task GuardarRecargo(int numero, decimal recargoCombustible);
    }
}
