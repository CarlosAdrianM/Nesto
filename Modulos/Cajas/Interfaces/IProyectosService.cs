using Nesto.Modulos.Cajas.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Interfaces
{
    /// <summary>
    /// Proyectos (gemela de NestoAPI#901): api/Proyectos. Lanza <see cref="Services.ProyectosApiException"/> con el mensaje y el
    /// código de la API ({ Message, Codigo }) si no se puede.
    /// </summary>
    public interface IProyectosService
    {
        Task<List<ProyectoModel>> Listar(string empresa, bool soloAbiertos, string? tipo, string? filtro);
        Task<ProyectoModel> Crear(ProyectoModel proyecto);
        Task<ProyectoModel> Modificar(int id, ProyectoModel proyecto);
        Task<ProyectoModel> Cerrar(int id);
        /// <summary>Solo Dirección (403 con el mensaje de la API).</summary>
        Task<ProyectoModel> Reabrir(int id);
        /// <summary>Solo abierto y sin imputaciones (409 con el mensaje de la API).</summary>
        Task Borrar(int id);

        Task<ApuntesPorImputarModel> ApuntesPorImputar(FiltroApuntesProyectos filtro);
        Task<ImputacionesApunteModel> LeerImputaciones(string empresa, int numeroOrden);
        /// <summary>SUSTITUYE el reparto del apunte (lista vacía = quitarlo).</summary>
        Task<ImputacionesApunteModel> GuardarImputaciones(int numeroOrden, PeticionImputacionesApunte peticion);

        Task<CuentaExplotacionProyectoModel> CuentaExplotacion(int id);
        Task<FicheroProyecto> CuentaExplotacionExcel(int id);
        /// <summary>Por ids o, si no se dan, por producto (todos los proyectos de ese curso).</summary>
        Task<ComparativaProyectosModel> Comparativa(IEnumerable<int>? ids, string? producto, string empresa);
    }
}
