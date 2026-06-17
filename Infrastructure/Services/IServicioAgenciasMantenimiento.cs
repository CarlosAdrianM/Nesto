using Nesto.Infrastructure.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// Cliente del CRUD de agencias de NestoAPI (api/Agencias) para la ventana de mantenimiento
    /// (Nesto#340): leer todas, dar de alta una nueva y editar una existente. Sin borrado (las
    /// agencias tienen movimientos). La cuarentena se gestiona aparte vía parámetro.
    /// </summary>
    public interface IServicioAgenciasMantenimiento
    {
        Task<List<AgenciaMantenimiento>> LeerAgencias();
        Task CrearAgencia(AgenciaMantenimiento agencia);
        Task GuardarAgencia(AgenciaMantenimiento agencia);
    }
}
