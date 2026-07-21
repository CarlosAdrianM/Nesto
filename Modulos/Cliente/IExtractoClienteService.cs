using Nesto.Modulos.Cliente.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#419: acceso por API al extracto de cliente. Sin EF en el cliente (Fase 1C).
    /// </summary>
    public interface IExtractoClienteService
    {
        /// <summary>Movimientos PENDIENTES del cliente (ImportePdte != 0), que es lo que
        /// interesa para liquidar. GET api/ExtractosCliente?cliente=</summary>
        Task<List<ExtractoClienteModel>> LeerExtractoPendiente(string cliente);

        /// <summary>Liquida dos movimientos (NestoAPI#333, prdLiquidar con validaciones
        /// adelantadas server-side). Lanza Exception con el motivo legible si no se puede.</summary>
        Task<ResultadoLiquidacionModel> LiquidarEfectos(string empresa, int origen, int destino);
    }
}
