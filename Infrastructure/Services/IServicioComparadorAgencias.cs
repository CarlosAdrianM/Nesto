using Nesto.Infrastructure.Models;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// Cliente del comparador de agencias de NestoAPI (api/Agencias/MasEconomica): para un pedido
    /// (empresa, CP, peso, reembolso) devuelve la agencia más barata con el fuel incluido, o null
    /// si ninguna agencia dada de alta cubre el destino.
    /// </summary>
    public interface IServicioComparadorAgencias
    {
        Task<OpcionEnvioAgencia> MasEconomica(string empresa, string codigoPostal, decimal peso, decimal reembolso);
    }
}
