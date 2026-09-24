using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#477: buzón de notificaciones de Nesto (NestoAPI#387). Usa IClienteApiFactory (Nesto#369)
    /// para que el HttpClient adjunte el JWT y la API sepa de quién es el buzón.
    /// </summary>
    public class BuzonNotificacionesService : IBuzonNotificacionesService
    {
        /// <summary>Hay que mandarla siempre: sin ella la API usa NestoApp.</summary>
        internal const string APLICACION = "Nesto";

        private readonly IClienteApiFactory _clienteApiFactory;

        public BuzonNotificacionesService(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        public async Task<List<NotificacionBuzon>> LeerBuzon(bool soloNoLeidas = false, int pagina = 1, int tamanoPagina = 20)
        {
            string url = $"Notificaciones/Buzon?aplicacion={APLICACION}&soloNoLeidas={(soloNoLeidas ? "true" : "false")}&pagina={pagina}&tamanoPagina={tamanoPagina}";
            string json = await Enviar(HttpMethod.Get, url, "leer las notificaciones").ConfigureAwait(false);
            return JsonConvert.DeserializeObject<List<NotificacionBuzon>>(string.IsNullOrWhiteSpace(json) ? "[]" : json) ?? new List<NotificacionBuzon>();
        }

        public async Task<int> ContarNoLeidas()
        {
            string json = await Enviar(HttpMethod.Get, $"Notificaciones/Buzon/NoLeidas?aplicacion={APLICACION}", "contar las notificaciones").ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(json) ? 0 : JsonConvert.DeserializeObject<int>(json);
        }

        public async Task MarcarLeida(int id)
        {
            await Enviar(HttpMethod.Put, $"Notificaciones/Buzon/{id}/Leida", "marcar la notificación como leída").ConfigureAwait(false);
        }

        public async Task<int> MarcarTodasLeidas()
        {
            string json = await Enviar(HttpMethod.Put, $"Notificaciones/Buzon/Leidas?aplicacion={APLICACION}", "marcar las notificaciones como leídas").ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(json) ? 0 : JsonConvert.DeserializeObject<int>(json);
        }

        public async Task Eliminar(int id)
        {
            await Enviar(HttpMethod.Delete, $"Notificaciones/Buzon/{id}", "borrar la notificación").ConfigureAwait(false);
        }

        private async Task<string> Enviar(HttpMethod metodo, string url, string accion)
        {
            using (var client = _clienteApiFactory.Crear())
            using (var request = new HttpRequestMessage(metodo, url))
            {
                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(await NovedadesService.MensajeDeError(response, accion).ConfigureAwait(false));
                }
                return response.Content == null ? null : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }
    }
}
