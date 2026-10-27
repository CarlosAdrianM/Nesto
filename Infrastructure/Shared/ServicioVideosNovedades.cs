using Nesto.Infrastructure.Contracts;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// NestoAPI#711: el vídeo de las novedades. Usa IClienteApiFactory para que el HttpClient lleve el JWT (el
    /// MediaElement de WPF no sabe mandar la cabecera Authorization: por eso se descarga entero y se reproduce
    /// desde un fichero temporal; son 10 MB como mucho).
    /// </summary>
    public class ServicioVideosNovedades : IServicioVideosNovedades
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioVideosNovedades(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        public async Task<byte[]> Descargar(int novedadId)
        {
            using (var client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync($"Novedades/{novedadId}/Video").ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(await NovedadesService.MensajeDeError(response, "descargar el vídeo").ConfigureAwait(false));
                }
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        public async Task RegistrarReproduccion(int novedadId)
        {
            try
            {
                using (var client = _clienteApiFactory.Crear())
                {
                    using (HttpResponseMessage _ = await client.PostAsync($"Novedades/{novedadId}/Video/Reproduccion", new StringContent(string.Empty)).ConfigureAwait(false))
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                // Contar las reproducciones nunca estorba al que está viendo el vídeo
                Debug.WriteLine($"NestoAPI#711: no se pudo apuntar la reproducción del vídeo de la novedad {novedadId}: {ex.Message}");
            }
        }
    }
}
