using ControlesUsuario.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Interfaz para servicios de búsqueda de autocomplete.
    /// Permite implementaciones específicas para productos y cuentas contables.
    /// Issue #263 - Carlos 30/01/26
    /// </summary>
    public interface IServicioBusquedaAutocomplete
    {
        /// <summary>
        /// Busca sugerencias basadas en el texto introducido.
        /// </summary>
        /// <param name="texto">Texto a buscar (código o nombre).</param>
        /// <param name="empresa">Código de empresa.</param>
        /// <param name="maxResultados">Número máximo de resultados a devolver.</param>
        /// <param name="cancellationToken">Token para cancelar la operación.</param>
        /// <returns>Lista de sugerencias ordenadas por relevancia.</returns>
        Task<IList<AutocompleteItem>> BuscarSugerenciasAsync(
            string texto,
            string empresa,
            int maxResultados,
            CancellationToken cancellationToken = default);
    }
}
