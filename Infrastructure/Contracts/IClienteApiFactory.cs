using System.Net.Http;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Crea instancias de HttpClient ya configuradas para llamar a NestoAPI:
    /// con BaseAddress y con el handler que adjunta el token JWT automáticamente.
    /// Usar esto en vez de 'New HttpClient' para que el usuario salga siempre en ELMAH.
    /// </summary>
    public interface IClienteApiFactory
    {
        HttpClient Crear();
    }
}
