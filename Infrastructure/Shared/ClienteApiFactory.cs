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

        public HttpClient Crear()
        {
            var authHandler = new AuthTokenHandler(_servicioAutenticacion)
            {
                InnerHandler = new HttpClientHandler()
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

            return new HttpClient(resilienceHandler)
            {
                BaseAddress = new Uri(_baseUrl)
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
