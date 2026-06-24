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
        Task<OpcionEnvioAgencia> MasEconomica(string empresa, string codigoPostal, decimal peso, decimal reembolso, string pais = "ES");

        /// <summary>
        /// Coste de UNA agencia concreta (la realmente usada en el envío), no la más barata, para
        /// rellenar EnviosAgencia.ImporteGasto (NestoAPI#238). Si no se indica servicio, devuelve el
        /// de la tarifa que cubre la zona. Null si esa agencia no tiene tarifa portada o no cubre el destino.
        /// </summary>
        Task<OpcionEnvioAgencia> CosteAgencia(string empresa, int numero, string codigoPostal, decimal peso, decimal reembolso, byte? servicioId = null, string pais = "ES");
    }
}
