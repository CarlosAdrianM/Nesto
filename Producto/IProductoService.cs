using System.Collections.Generic;
using System.Threading.Tasks;
using Nesto.Modules.Producto.Models;

namespace Nesto.Modules.Producto
{
    public interface IProductoService
    {
        Task<ProductoModel> LeerProducto(string producto);
        Task<ICollection<ProductoModel>> BuscarProductos(string filtroNombre, string filtroFamilia, string filtroSubgrupo);
        Task<ICollection<ProductoClienteModel>> BuscarClientes(string producto);
        Task<ControlStockProductoModel> LeerControlStock(string producto);
        Task<List<DiarioProductoModel>> LeerDiariosProducto();
        Task GuardarControlStock(ControlStock controlStock);
        Task CrearControlStock(ControlStock controlStock);
        Task<bool> TraspasarDiario(string id1, string id2, string almacenOrigen);
        Task<int> MontarKit(string almacen, string producto, int cantidad);
    }
}
