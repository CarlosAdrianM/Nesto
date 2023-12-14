using Nesto.Modules.Producto.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    public interface IUbicacionService
    {
        Task<List<UbicacionProductoDTO>> LeerUbicacionesProducto(string producto);
    }
}
