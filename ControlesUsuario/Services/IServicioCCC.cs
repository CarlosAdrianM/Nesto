using ControlesUsuario.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Servicio para obtener CCCs (cuentas bancarias) de clientes desde la API.
    /// Carlos 20/11/2024: Creado para el nuevo control SelectorCCC con inyección de dependencias.
    /// </summary>
    public interface IServicioCCC
    {
        /// <summary>
        /// Obtiene los CCCs (cuentas bancarias) para un cliente/contacto específico.
        /// </summary>
        /// <param name="empresa">Empresa del cliente</param>
        /// <param name="cliente">Número de cliente</param>
        /// <param name="contacto">Contacto del cliente</param>
        /// <returns>Lista de CCCs del cliente/contacto. Incluye CCCs válidos (estado >= 0) e inválidos (estado < 0)</returns>
        /// <exception cref="System.ArgumentException">Si empresa, cliente o contacto son null/vacíos</exception>
        /// <exception cref="System.Exception">Si hay error en la llamada HTTP</exception>
        Task<IEnumerable<CCCItem>> ObtenerCCCs(
            string empresa,
            string cliente,
            string contacto
        );
    }
}
