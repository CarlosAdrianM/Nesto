using ControlesUsuario.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Interfaz para el servicio de series de facturación.
    /// Carlos 09/12/25: Issue #245
    /// </summary>
    public interface IServicioSeries
    {
        Task<List<SerieItem>> ObtenerSeries();
    }
}
