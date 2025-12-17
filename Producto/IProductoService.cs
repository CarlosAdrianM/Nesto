using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        Task<List<VideoLookupModel>> BuscarVideosRelacionados(string producto);
        Task<VideoModel> CargarVideoCompleto(int videoId);
        Task<List<KitContienePerteneceModel>> LeerKitsContienePertenece(string producto);
        Task ActualizarVideoProducto(int id, ActualizacionVideoProductoDto dto);
        Task EliminarVideoProducto(int id, string observaciones = null);
        // TO-DO: implementar
        //Task<List<HistorialCambioDto>> ObtenerHistorialCambios(int videoProductoId);
        Task DeshacerCambio(int videoProductoId, int logId, string observaciones = null);
        Task<List<ProductoControlStockModel>> LeerProductosProveedorControlStock(string proveedorId, string almacen);
    }
}
