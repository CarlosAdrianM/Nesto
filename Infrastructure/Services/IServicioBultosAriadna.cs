using Nesto.Infrastructure.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// Nesto#507: los bultos del packing de Ariadna de un pedido y el enlace para ver la foto de cada uno.
    /// </summary>
    public interface IServicioBultosAriadna
    {
        /// <summary>Los bultos del pedido (lista vacía si se preparó a la antigua, sin Ariadna).</summary>
        Task<List<BultoAriadna>> LeerBultosDelPedido(string empresa, int pedido);

        /// <summary>Un enlace temporal para ver la foto del bulto, o null si no tiene.</summary>
        Task<string> EnlaceFoto(int idBulto);

        /// <summary>
        /// Nesto#522: la imagen de la foto del bulto (JPG). Pide cada vez un enlace temporal nuevo, así que nunca se
        /// usa uno caducado. Null si el bulto no tiene foto; lanza si la API o la descarga fallan.
        /// </summary>
        Task<byte[]> DescargarFoto(int idBulto);
    }
}
