using Nesto.Modulos.Ganavisiones.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Ganavisiones.Interfaces
{
    public interface IGanavisionesService
    {
        Task<List<GanavisionModel>> GetGanavisiones(string empresa, string productoId = null, bool soloActivos = false);
        Task<GanavisionModel> GetGanavision(int id);
        Task<GanavisionModel> CreateGanavision(GanavisionCreateModel ganavision);
        Task<GanavisionModel> UpdateGanavision(int id, GanavisionCreateModel ganavision);
        Task<GanavisionModel> DeleteGanavision(int id);
    }
}
