using Nesto.Modulos.Cajas.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas
{
    public interface IClientesService
    {
        Task<List<ExtractoClienteDTO>> LeerDeudas(string cliente);
    }
}
