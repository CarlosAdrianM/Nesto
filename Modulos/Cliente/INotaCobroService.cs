using Nesto.Modulos.Cliente.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>Nesto#618 (NestoAPI#786): crear notas de solo cobro.</summary>
    public interface INotaCobroService
    {
        /// <summary>
        /// POST api/NotasEntrega/Cobro. Si la API dice que no (400, 403, 404, 409), lanza Exception con su «Message» tal cual
        /// (ya viene de usted).
        /// </summary>
        Task<NotaCobroCreadaModel> CrearNotaCobro(CrearNotaCobroModel nota);

        /// <summary>
        /// NestoAPI#786: GET api/RepartoPropio/Repartidores, a quién se puede asignar la nota (los que han repartido en los últimos
        /// 90 días y el propio usuario). Si la API dice que no, lanza Exception con su «Message».
        /// </summary>
        Task<List<RepartidorModel>> LeerRepartidores();
    }
}
