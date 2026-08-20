using ControlesUsuario.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Parámetros que el propio usuario autenticado puede cambiarse (catálogo server-side en
    /// NestoAPI, endpoints api/ParametrosUsuario/Editables).
    /// </summary>
    public interface IServicioParametrosEditables
    {
        Task<List<ParametroEditableModel>> LeerEditables();
        Task<ParametroEditableModel> Cambiar(string clave, string valor);
    }
}
