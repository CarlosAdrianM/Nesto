using Nesto.Modulos.Cajas.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Interfaces
{
    /// <summary>
    /// Inmovilizados (Carlos 25/11/26): la consulta (GET api/Inmovilizados) y la etiqueta para pegar en la máquina
    /// (POST api/Inmovilizados/Etiquetas/Imprimir). Lanza con el mensaje de la API si no se puede.
    /// </summary>
    public interface IInmovilizadosService
    {
        /// <param name="filtro">Número, «I/1823» o parte de la descripción (vacío: todos, 500 como mucho).</param>
        /// <param name="delegacion">ALG, REI, ALC, VAR o null para todas.</param>
        /// <param name="soloVivos">False para ver también los anulados.</param>
        Task<List<InmovilizadoModel>> Buscar(string empresa, string? filtro, string? delegacion, bool soloVivos);

        Task<RespuestaImprimirEtiquetasInmovilizado> ImprimirEtiquetas(PeticionImprimirEtiquetasInmovilizado peticion);
    }
}
