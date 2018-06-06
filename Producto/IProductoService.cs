using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    public interface IProductoService
    {
        Task<ProductoModel> LeerProducto(string producto);
        Task<ICollection<ProductoModel>> BuscarProductos(string filtroNombre, string filtroFamilia, string filtroSubgrupo);
    }
}
