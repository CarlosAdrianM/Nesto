using Nesto.Infrastructure.Contracts;
using System;
using System.Net.Http;

namespace Nesto.Infrastructure.Shared
{
    public class ClienteApiFactory : IClienteApiFactory
    {
        private readonly string _baseUrl;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public ClienteApiFactory(string baseUrl, IServicioAutenticacion servicioAutenticacion)
        {
            _baseUrl = baseUrl;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public HttpClient Crear()
        {
            var handler = new AuthTokenHandler(_servicioAutenticacion)
            {
                InnerHandler = new HttpClientHandler()
            };

            return new HttpClient(handler)
            {
                BaseAddress = new Uri(_baseUrl)
            };
        }
    }
}
