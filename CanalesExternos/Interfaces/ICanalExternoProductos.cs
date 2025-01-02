using Nesto.Modulos.CanalesExternos.Models;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Interfaces
{
    public interface ICanalExternoProductos
    {        
        Task ActualizarProducto(ProductoCanalExterno producto);
        string Nombre { get; }
    }
}
