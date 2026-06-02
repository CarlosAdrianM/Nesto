using System;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Envía a NestoAPI los errores no controlados del cliente para que queden
    /// registrados de forma centralizada en ELMAH (Nesto no tiene ELMAH propio).
    /// </summary>
    public interface IServicioRegistroErrores
    {
        /// <summary>
        /// Registra una excepción en el servidor. Nunca lanza: si falla el envío, lo ignora.
        /// </summary>
        /// <param name="excepcion">Excepción a registrar.</param>
        /// <param name="contexto">Dónde se produjo (ventana, comando, manejador global...).</param>
        Task RegistrarErrorAsync(Exception excepcion, string contexto = null);
    }
}
