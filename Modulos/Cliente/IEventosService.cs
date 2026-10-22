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

        // Nesto#517 (NestoAPI#611/#612): inscripciones sin pedido, pago a mano, lista de asistencia y facturación del curso.

        /// <summary>Los inscritos del evento (todas, también caducadas y anuladas). Lista vacía con una API sin inscripciones.</summary>
        Task<List<InscripcionEventoModel>> LeerInscripciones(int eventoId);
        /// <summary>Las inscripciones del cliente (de todos sus contactos). Lista vacía con una API sin inscripciones.</summary>
        Task<List<InscripcionEventoModel>> LeerInscripcionesCliente(string empresa, string cliente);
        Task<InscripcionEventoModel> Inscribir(int eventoId, InscribirEventoModel peticion);
        /// <summary>Anula una inscripción sin pagar (Reservada).</summary>
        Task<InscripcionEventoModel> AnularInscripcion(int id);
        Task<InscripcionEventoModel> MarcarPagada(int id, MarcarPagoInscripcionModel peticion);
        /// <summary>Las pagadas que vienen en <paramref name="asistentes"/> pasan a Asistió y el resto a No asistió.</summary>
        Task<List<InscripcionEventoModel>> PasarLista(int eventoId, IEnumerable<int> asistentes);
        /// <summary>NestoAPI#612: relanza la facturación del curso desde donde se quedó.</summary>
        Task<InscripcionEventoModel> FacturarCurso(int id);
        /// <summary>La referencia del curso (nombre e IVA de la ficha); null si no existe.</summary>
        Task<ProductoCursoModel> LeerProductoCurso(string producto);

        // NestoAPI#690: confirmación de asistencia (la apunta la tienda) e imagen del evento.

        /// <param name="respuesta">Confirma o NoPodraIr.</param>
        Task<InscripcionEventoModel> ConfirmarAsistencia(int id, string respuesta);
        /// <summary>Sube (o sustituye) la imagen del evento: PNG, JPEG, GIF o WebP hasta 10 MB. Devuelve el evento con su ImagenUrl.</summary>
        Task<EventoModel> SubirImagen(int eventoId, string ruta);
        Task BorrarImagen(int eventoId);
    }
}
