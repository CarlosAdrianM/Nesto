using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    }
}
