using Nesto.Infrastructure.Contracts;
using System.Collections.Generic;
using System.Threading.Tasks;
using static ControlesUsuario.Models.SelectorProveedorModel;

namespace ControlesUsuario.Services
{
    public interface ISelectorProveedorService
    {
        Task<IEnumerable<IFiltrableItem>> BuscarProveedores(string empresa, string filtro);
        Task<ProveedorDTO> CargarProveedor(string empresa, string proveedor, string contacto);
    }
}
