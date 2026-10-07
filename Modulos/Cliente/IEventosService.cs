using Nesto.Modulos.Cliente.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>NestoAPI#591: eventos con señal reembolsable y sus señales, por API (api/Eventos).</summary>
    public interface IEventosService
    {
        Task<List<EventoModel>> LeerEventos(bool soloActivos);
        Task<EventoModel> GuardarEvento(EventoModel evento);
        /// <param name="estado">Pendiente, Liberada, SinCompra, Consumida; null = todas.</param>
        Task<List<SenalEventoModel>> LeerSenales(string estado, int? eventoId);
        Task<List<SenalEventoModel>> LeerSenalesCliente(string cliente);
        Task<SenalEventoModel> MarcarSenal(int eventoId, MarcarSenalEventoModel peticion);
        Task QuitarSenal(int id);
    }
}
