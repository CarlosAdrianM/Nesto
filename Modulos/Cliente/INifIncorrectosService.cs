using Nesto.Modulos.Cliente.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#417: clientes con NIF incorrecto para Verifactu y su corrección rápida.
    /// </summary>
    public interface INifIncorrectosService
    {
        /// <param name="vendedor">Null = sin filtro (administración/dirección).</param>
        Task<List<ClienteNifIncorrectoModel>> LeerNifIncorrectos(string vendedor);

        /// <summary>Corrección centralizada: revalida contra la AEAT y, solo si es correcto,
        /// lo propaga a la ficha, los contactos y las facturas sin declarar.</summary>
        Task<ResultadoCorreccionNifModel> CorregirNif(string cliente, string nif);
    }
}
