using ControlesUsuario.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Interfaz para el servicio de almacenes.
    /// Carlos 09/12/25: Issue #253/#52
    /// </summary>
    public interface IServicioAlmacenes
    {
        Task<List<AlmacenItem>> ObtenerAlmacenes();
    }
}
