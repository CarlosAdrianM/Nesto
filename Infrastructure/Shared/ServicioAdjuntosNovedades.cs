using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#519 (NestoAPI#616): descarga, sube y borra los adjuntos de las novedades. Usa IClienteApiFactory
    /// para que el HttpClient lleve el JWT. Lanza con el mensaje de la API (la ventana se lo cuenta al usuario).
    /// </summary>
    public class ServicioAdjuntosNovedades : IServicioAdjuntosNovedades
    {
        /// <summary>Mismo tope que la API: se avisa antes de subir.</summary>
        public const long TAMANO_MAXIMO = 10 * 1024 * 1024;

        private static readonly Dictionary<string, string> TiposPorExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".pdf", "application/pdf" },
            { ".png", "image/png" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".gif", "image/gif" },
            { ".webp", "image/webp" }
        };

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioAdjuntosNovedades(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        /// <summary>El Content-Type de un fichero que se puede adjuntar, o null si su extensión no se admite.</summary>
        public static string TipoDeFichero(string ruta)
        {
            string extension = Path.GetExtension(ruta ?? string.Empty);
            return TiposPorExtension.TryGetValue(extension, out string tipo) ? tipo : null;
        }

        public async Task<byte[]> Descargar(int adjuntoId)
        {
            using (var client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync($"Novedades/Adjuntos/{adjuntoId}").ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(await NovedadesService.MensajeDeError(response, "descargar el adjunto").ConfigureAwait(false));
                }
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        public async Task<List<AdjuntoNovedad>> Subir(int novedadId, IEnumerable<string> rutasFicheros)
        {
            List<string> rutas = (rutasFicheros ?? Enumerable.Empty<string>()).Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
            if (rutas.Count == 0)
            {
                return new List<AdjuntoNovedad>();
            }
            foreach (string ruta in rutas)
            {
                if (TipoDeFichero(ruta) == null)
                {
                    throw new InvalidOperationException($"«{Path.GetFileName(ruta)}» no se puede adjuntar: solo PDF e imágenes (png, jpg, gif, webp).");
                }
                if (new FileInfo(ruta).Length > TAMANO_MAXIMO)
                {
                    throw new InvalidOperationException($"«{Path.GetFileName(ruta)}» pasa de 10 MB: no se puede adjuntar.");
                }
            }

            using (var client = _clienteApiFactory.Crear())
            using (var contenido = new MultipartFormDataContent())
            {
                foreach (string ruta in rutas)
                {
                    var fichero = new ByteArrayContent(File.ReadAllBytes(ruta));
                    fichero.Headers.ContentType = new MediaTypeHeaderValue(TipoDeFichero(ruta));
                    contenido.Add(fichero, "fichero", Path.GetFileName(ruta));
                }
                HttpResponseMessage response = await client.PostAsync($"Novedades/{novedadId}/Adjuntos", contenido).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(await NovedadesService.MensajeDeError(response, "subir el adjunto").ConfigureAwait(false));
                }
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<List<AdjuntoNovedad>>(string.IsNullOrWhiteSpace(json) ? "[]" : json) ?? new List<AdjuntoNovedad>();
            }
        }

        public async Task Borrar(int adjuntoId)
        {
            using (var client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.DeleteAsync($"Novedades/Adjuntos/{adjuntoId}").ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(await NovedadesService.MensajeDeError(response, "borrar el adjunto").ConfigureAwait(false));
                }
            }
        }
    }
}
