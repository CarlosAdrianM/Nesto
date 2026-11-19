using Nesto.Modulos.Cajas.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Interfaces
{
    public interface IClientesService
    {
        Task<List<ExtractoClienteDTO>> LeerDeudas(string cliente);

        /// <summary>19/11/26: las reservas de eventos sin pagar del cliente (todos sus contactos), cobrables en Cajas.</summary>
        Task<List<ReservaEventoCaja>> LeerReservasEventoSinPagar(string cliente);

        /// <summary>19/11/26: cobra la señal de la reserva (cobro en caja, pagada, factura del curso y cupón).</summary>
        Task<ResultadoCobroReservaEventoCaja> CobrarReservaEvento(int inscripcionId, CobroReservaEventoCaja cobro);
    }
}
