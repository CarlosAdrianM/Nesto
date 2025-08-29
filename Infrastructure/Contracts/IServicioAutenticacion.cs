using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Contracts
{
    public interface IServicioAutenticacion
    {
        /// <summary>
        /// Obtiene un token JWT usando las credenciales de Windows del usuario actual
        /// </summary>
        /// <returns>Token JWT o null si falla la autenticación</returns>
        Task<string> ObtenerTokenWindowsAsync();

        /// <summary>
        /// Verifica si hay un token válido almacenado
        /// </summary>
        /// <returns>True si hay un token válido</returns>
        bool TieneTokenValido();

        /// <summary>
        /// Obtiene el token actual (si existe y es válido)
        /// </summary>
        /// <returns>Token actual o null</returns>
        string ObtenerTokenActual();

        /// <summary>
        /// Limpia el token almacenado
        /// </summary>
        void LimpiarToken();

        /// <summary>
        /// Configura el HttpClient con el token de autorización
        /// </summary>
        /// <param name="httpClient">HttpClient a configurar</param>
        /// <returns>True si se pudo configurar correctamente</returns>
        Task<bool> ConfigurarAutorizacion(HttpClient httpClient);

        /// <summary>
        /// Gestiona la lógica para obtener un token válido, ya sea reutilizando el existente o solicitando uno nuevo si es necesario.
        /// </summary>
        Task<string> ObtenerTokenValidoAsync();

    }
}
