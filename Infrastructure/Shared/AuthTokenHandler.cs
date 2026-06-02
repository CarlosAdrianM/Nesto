using Nesto.Infrastructure.Contracts;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// DelegatingHandler que adjunta el token JWT a cada petición saliente (si no lo trae ya).
    /// Así no depende de que cada llamada se acuerde de llamar a ConfigurarAutorizacion, y el
    /// usuario sale en ELMAH siempre que la app esté autenticada (en endpoints anónimos el token
    /// es opcional, pero si va, el servidor lo registra).
    /// </summary>
    public class AuthTokenHandler : DelegatingHandler
    {
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public AuthTokenHandler(IServicioAutenticacion servicioAutenticacion)
        {
            _servicioAutenticacion = servicioAutenticacion;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization == null && _servicioAutenticacion != null)
            {
                string token = await _servicioAutenticacion.ObtenerTokenValidoAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
