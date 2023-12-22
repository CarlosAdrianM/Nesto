using Nesto.Modulos.Cajas.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas
{
    public interface IContabilidadService
    {
        Task<List<CuentaContableDTO>> LeerCuentas(string empresa, string grupo);
        Task<int> Contabilizar(List<PreContabilidadDTO> lineas);
        Task<int> Contabilizar(PreContabilidadDTO linea);
    }

}
