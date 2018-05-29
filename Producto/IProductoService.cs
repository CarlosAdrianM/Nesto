using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    public interface IProductoService
    {
        Task<ProductoModel> LeerProducto(string producto);
    }
}
