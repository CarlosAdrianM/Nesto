using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Rapports;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RapportsTests
{
    [TestClass]
    public class RapportServiceTests
    {
        [TestMethod]
        public void RapportService_AlCrearUnRapport_LaIdYaNoEsCero()
        {

        }

        /// <summary>
        /// Nesto#381: CargarClientesProbabilidad devolvía Nothing cuando la API respondía vacío o
        /// sin éxito (DeserializeObject("") => null), y el ViewModel petaba con ArgumentNullException
        /// al construir un ObservableCollection con null. Debe devolver una lista vacía, nunca null.
        /// </summary>
        [TestMethod]
        public async Task CargarClientesProbabilidad_RespuestaVacia_DevuelveListaVaciaNoNull()
        {
            var handler = new RespuestaFalsaHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("")
            });
            var factory = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factory.Crear())
                .Returns(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/") });

            var servicio = new RapportService(
                A.Fake<IConfiguracion>(), null, A.Fake<IServicioAutenticacion>(), factory);

            var resultado = await servicio.CargarClientesProbabilidad("AM", "Visita", "PEL");

            Assert.IsNotNull(resultado,
                "Nunca debe devolver Nothing: el ViewModel construye un ObservableCollection con el resultado");
            Assert.AreEqual(0, resultado.Count);
        }

        // Devuelve siempre la misma respuesta canned, para simular el endpoint sin red.
        private sealed class RespuestaFalsaHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _respuesta;

            public RespuestaFalsaHandler(HttpResponseMessage respuesta) => _respuesta = respuesta;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_respuesta);
        }
    }
}
