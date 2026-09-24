using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#372: lee el changelog de usuario de GET api/Novedades.
    /// Usa IClienteApiFactory (Nesto#369) para que el HttpClient adjunte el JWT.
    /// </summary>
    public class NovedadesService : INovedadesService
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public NovedadesService(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        public async Task<List<NovedadUsuario>> ObtenerNovedades(string desdeVersion = null)
        {
            try
            {
                using (var client = _clienteApiFactory.Crear())
                {
                    string url = string.IsNullOrEmpty(desdeVersion)
                        ? "Novedades"
                        : $"Novedades?desdeVersion={Uri.EscapeDataString(desdeVersion)}";

                    var response = await client.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[NovedadesService] Error HTTP: {response.StatusCode}");
                        return new List<NovedadUsuario>();
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<NovedadUsuario>>(json) ?? new List<NovedadUsuario>();
                }
            }
            catch (Exception ex)
            {
                // Las novedades nunca deben bloquear ni romper el arranque de Nesto
                Debug.WriteLine($"[NovedadesService] Error: {ex.Message}");
                return new List<NovedadUsuario>();
            }
        }

        // ---- NestoAPI#520: feedback (votos y comentarios) ----

        public async Task VotarNovedad(int novedadId, short voto)
        {
            await Enviar(HttpMethod.Put, $"Novedades/{novedadId}/Voto", new { Voto = voto }, "guardar el voto").ConfigureAwait(false);
        }

        public async Task<List<ComentarioNovedad>> LeerComentarios(int novedadId)
        {
            string json = await Enviar(HttpMethod.Get, $"Novedades/{novedadId}/Comentarios", null, "leer los comentarios").ConfigureAwait(false);
            return JsonConvert.DeserializeObject<List<ComentarioNovedad>>(json ?? "[]") ?? new List<ComentarioNovedad>();
        }

        public async Task<ComentarioNovedad> Comentar(int novedadId, string texto, byte[] imagenPng)
        {
            string json = await Enviar(HttpMethod.Post, $"Novedades/{novedadId}/Comentarios", CuerpoConCaptura(texto, imagenPng), "publicar el comentario").ConfigureAwait(false);
            return JsonConvert.DeserializeObject<ComentarioNovedad>(json);
        }

        public Task<byte[]> LeerImagenComentario(int comentarioId)
            => LeerImagen($"Novedades/Comentarios/{comentarioId}/Imagen");

        public async Task BorrarComentario(int comentarioId)
        {
            await Enviar(HttpMethod.Delete, $"Novedades/Comentarios/{comentarioId}", null, "borrar el comentario").ConfigureAwait(false);
        }

        // ---- NestoAPI#526/#527: sugerencias y buscador. Sin ámbito: la API entiende que es el escritorio. ----

        public async Task<List<NovedadUsuario>> LeerSugerencias()
        {
            string json = await Enviar(HttpMethod.Get, "Novedades/Sugerencias", null, "leer las sugerencias").ConfigureAwait(false);
            return JsonConvert.DeserializeObject<List<NovedadUsuario>>(json ?? "[]") ?? new List<NovedadUsuario>();
        }

        public async Task<NovedadUsuario> Sugerir(string texto, byte[] imagenPng)
        {
            string json = await Enviar(HttpMethod.Post, "Novedades/Sugerencias", CuerpoConCaptura(texto, imagenPng), "enviar la sugerencia").ConfigureAwait(false);
            return JsonConvert.DeserializeObject<NovedadUsuario>(json);
        }

        public Task<byte[]> LeerImagenNovedad(int novedadId)
            => LeerImagen($"Novedades/{novedadId}/Imagen");

        public async Task<List<NovedadUsuario>> Buscar(string texto)
        {
            string url = $"Novedades/Buscar?texto={Uri.EscapeDataString(texto ?? string.Empty)}";
            string json = await Enviar(HttpMethod.Get, url, null, "buscar").ConfigureAwait(false);
            return JsonConvert.DeserializeObject<List<NovedadUsuario>>(json ?? "[]") ?? new List<NovedadUsuario>();
        }

        /// <summary>Mismo cuerpo para comentar y para sugerir (NuevoComentarioNovedadDTO en la API).</summary>
        private static object CuerpoConCaptura(string texto, byte[] imagenPng)
        {
            bool conImagen = imagenPng != null && imagenPng.Length > 0;
            return new
            {
                Texto = texto,
                ImagenBase64 = conImagen ? Convert.ToBase64String(imagenPng) : null,
                ImagenTipo = conImagen ? "image/png" : null,
                VersionCliente = VersionNesto()
            };
        }

        private async Task<byte[]> LeerImagen(string url)
        {
            using (var client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(await MensajeDeError(response, "leer la imagen").ConfigureAwait(false));
                }
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        private async Task<string> Enviar(HttpMethod metodo, string url, object cuerpo, string accion)
        {
            using (var client = _clienteApiFactory.Crear())
            using (var request = new HttpRequestMessage(metodo, url))
            {
                if (cuerpo != null)
                {
                    request.Content = new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json");
                }
                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(await MensajeDeError(response, accion).ConfigureAwait(false));
                }
                return response.Content == null ? null : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        /// <summary>El motivo que da la API (formato de GlobalExceptionFilter o {"Message":...}), o uno genérico.</summary>
        internal static async Task<string> MensajeDeError(HttpResponseMessage response, string accion)
        {
            string contenido = response.Content == null ? null : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            string motivo = null;
            if (!string.IsNullOrWhiteSpace(contenido))
            {
                try
                {
                    motivo = HttpErrorHelper.ParsearErrorHttp(JObject.Parse(contenido));
                }
                catch (Exception)
                {
                    motivo = contenido.Trim().Trim('"');
                }
            }
            return string.IsNullOrWhiteSpace(motivo)
                ? $"No se pudo {accion} (error {(int)response.StatusCode})."
                : $"No se pudo {accion}: {motivo}";
        }

        /// <summary>Versión de Nesto que comenta (la de ClickOnce; fuera de ClickOnce, la del ensamblado).</summary>
        internal static string VersionNesto()
        {
            string clickOnce = Environment.GetEnvironmentVariable("ClickOnce_CurrentVersion");
            if (!string.IsNullOrWhiteSpace(clickOnce))
            {
                return "Nesto " + clickOnce;
            }
            Version version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            return version == null ? "Nesto" : "Nesto " + version;
        }
    }
}
