using ControlesUsuario.Models;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Interfaz para el servicio de cuentas contables.
    /// Issue #258 - Carlos 11/12/25
    /// </summary>
    public interface IServicioCuentaContable
    {
        /// <summary>
        /// Busca una cuenta contable por empresa y número de cuenta.
        /// </summary>
        /// <param name="empresa">Código de empresa</param>
        /// <param name="cuenta">Número de cuenta (8 dígitos)</param>
        /// <returns>Datos de la cuenta o null si no existe</returns>
        Task<CuentaContableDTO> BuscarCuenta(string empresa, string cuenta);
    }
}
