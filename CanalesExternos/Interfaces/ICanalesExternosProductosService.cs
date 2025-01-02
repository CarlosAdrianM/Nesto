using Nesto.Modulos.CanalesExternos.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Interfaces
{
    public interface ICanalesExternosProductosService
    {
        Task<ProductoCanalExterno> GetProductoAsync(string productoId);
        Task<IEnumerable<ProductoCanalExterno>> GetProductosSinVistoBuenoNumeroAsync();
        Task SaveProductoAsync(ProductoCanalExterno producto);
        Task<ProductoCanalExterno> AddProductoAsync(string productoId);
    }
}
