using Microsoft.Extensions.Http.Resilience;
using Nesto.Infrastructure.Contracts;
using Polly;
using Polly.Retry;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Shared
{
    public class ClienteApiFactory : IClienteApiFactory
    {
        private readonly string _baseUrl;
        private readonly IServicioAutenticacion _servicioAutenticacion;
        private readonly CanalAvisosBloqueo _canalAvisosBloqueo;

        // NestoAPI#288 (punto 3): pipeline de reintentos compartido por todos los HttpClient de
        // la factoría. Solo reintenta transitorios (5xx, 408, 429, timeouts, errores de red) y
        // SOLO en GET: reintentar un POST/PUT que hizo timeout puede duplicar pedidos o apuntes.
        private static readonly Lazy<ResiliencePipeline<HttpResponseMessage>> PipelineReintentos =
            new(CrearPipelineReintentos);

        public ClienteApiFactory(string baseUrl, IServicioAutenticacion servicioAutenticacion)
        {
            _baseUrl = baseUrl;
            _servicioAutenticacion = servicioAutenticacion;
        }

        /// <summary>Con el canal de los avisos de bloqueo inyectado (tests); el otro constructor usa el común de la aplicación.</summary>
        internal ClienteApiFactory(string baseUrl, IServicioAutenticacion servicioAutenticacion, CanalAvisosBloqueo canalAvisosBloqueo)
            : this(baseUrl, servicioAutenticacion)
        {
            _canalAvisosBloqueo = canalAvisosBloqueo;
        }

        public HttpClient Crear()
        {
            return new HttpClient(CrearCadena(new HttpClientHandler()))
            {
                BaseAddress = new Uri(_baseUrl)
            };
        }

        /// <summary>
        /// La cadena de manejadores, de fuera adentro: avisos de bloqueo → reintentos → token → <paramref name="transporte"/>
        /// (el HttpClientHandler; en los tests, uno de mentira).
        /// </summary>
        internal HttpMessageHandler CrearCadena(HttpMessageHandler transporte)
        {
            var authHandler = new AuthTokenHandler(_servicioAutenticacion)
            {
                InnerHandler = transporte
            };

            // El handler de resiliencia va por FUERA del de auth: cada reintento vuelve a pasar
            // por AuthTokenHandler y sale con token fresco.
            // EXTEXP0001: ResilienceHandler es "experimental" solo porque su API pública puede
            // cambiar entre versiones del paquete; es el mismo handler que usa por dentro
            // AddStandardResilienceHandler. Aquí no hay DI/HttpClientFactory, así que se usa directo.
#pragma warning disable EXTEXP0001
            var resilienceHandler = new ResilienceHandler(PipelineReintentos.Value)
            {
                InnerHandler = authHandler
            };
#pragma warning restore EXTEXP0001

            // Aviso vertical de bloqueos (13/11/26): el que mira la cabecera X-Nesto-Bloqueo va por FUERA de los reintentos,
            // para ver solo la respuesta definitiva de cada llamada: un GET reintentado tres veces da un aviso, no tres.
            return new AvisoBloqueosHandler(_canalAvisosBloqueo)
            {
                InnerHandler = resilienceHandler
            };
        }

        internal static ResiliencePipeline<HttpResponseMessage> CrearPipelineReintentos()
        {
            return new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 2,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    ShouldHandle = EsReintentable
                })
                .Build();
        }

        internal static ValueTask<bool> EsReintentable(RetryPredicateArguments<HttpResponseMessage> args)
        {
            HttpRequestMessage request = args.Context.GetRequestMessage();
            if (request is not null && request.Method != HttpMethod.Get)
            {
                return ValueTask.FromResult(false);
            }

            return ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome));
        }
    }
}
