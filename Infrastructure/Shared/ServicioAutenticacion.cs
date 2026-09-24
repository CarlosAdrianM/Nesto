using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Shared
{
    public class ServicioAutenticacion : IServicioAutenticacion
    {
        private readonly string _baseUrl;
        private string _tokenActual;
        private DateTime _expiracionToken;
        private readonly SemaphoreSlim _tokenSemaphore = new SemaphoreSlim(1, 1);
        private static readonly TimeSpan MARGEN_CADUCIDAD = TimeSpan.FromMinutes(5);

        /// <summary>Nesto#492: se ha obtenido, renovado o limpiado el token.</summary>
        public event EventHandler TokenCambiado;

        public ServicioAutenticacion(string baseUrl)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        }

        public async Task<string> ObtenerTokenWindowsAsync()
        {
            try
            {
                var handler = new HttpClientHandler()
                {
                    UseDefaultCredentials = true
                };
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "WPF-Client");

                var response = await client.PostAsync($"{_baseUrl}/auth/windows-token", null);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(json);
                    if (!string.IsNullOrEmpty(tokenResponse?.Token))
                    {
                        _tokenActual = tokenResponse.Token;
                        _expiracionToken = ExtraerExpiracionToken(_tokenActual);
                        string token = _tokenActual;
                        AvisarTokenCambiado();
                        return token;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo token Windows: {ex.Message}");
            }
            return null;
        }

        public bool TieneTokenValido()
        {
            return !string.IsNullOrEmpty(_tokenActual) &&
                   _expiracionToken > DateTime.UtcNow.Add(MARGEN_CADUCIDAD); // margen de 5 min
        }

        public DateTime? TokenValidoHastaUtc
        {
            get
            {
                if (string.IsNullOrEmpty(_tokenActual))
                {
                    return null;
                }
                DateTime expiracion = _expiracionToken;
                return expiracion - DateTime.MinValue < MARGEN_CADUCIDAD ? DateTime.MinValue : expiracion - MARGEN_CADUCIDAD;
            }
        }

        public string ObtenerTokenActual()
        {
            return TieneTokenValido() ? _tokenActual : null;
        }

        public void LimpiarToken()
        {
            _tokenActual = null;
            _expiracionToken = DateTime.MinValue;
            AvisarTokenCambiado();
        }

        private void AvisarTokenCambiado()
        {
            try
            {
                TokenCambiado?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // Quien escucha no debe romper la obtención del token.
            }
        }

        // Nuevo método
        public async Task<string> ObtenerTokenValidoAsync()
        {
            await _tokenSemaphore.WaitAsync();
            try
            {
                if (!TieneTokenValido())
                {
                    await ObtenerTokenWindowsAsync();
                }
                return ObtenerTokenActual();
            }
            finally
            {
                _tokenSemaphore.Release();
            }
        }

        // Ahora es async y siempre asegura token
        public async Task<bool> ConfigurarAutorizacion(HttpClient httpClient)
        {
            var token = await ObtenerTokenValidoAsync();
            if (!string.IsNullOrEmpty(token))
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                return true;
            }

            return false;
        }

        private DateTime ExtraerExpiracionToken(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(token);
                return jsonToken.ValidTo;
            }
            catch
            {
                return DateTime.UtcNow.AddHours(1);
            }
        }

        private class TokenResponse
        {
            [JsonProperty("token")]
            public string Token { get; set; }
        }
    }
}
