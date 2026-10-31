using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Globos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Tests
{
    /// <summary>Nesto#592 (NestoAPI#774): el cliente HTTP de api/Globos de Nesto.</summary>
    [TestClass]
    public class ClienteGlobosTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<(HttpMethod Metodo, string Url)> Peticiones = new List<(HttpMethod, string)>();
            public string Respuesta = "[]";
            public HttpStatusCode Codigo = HttpStatusCode.OK;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Peticiones.Add((request.Method, request.RequestUri.PathAndQuery));
                return Task.FromResult(new HttpResponseMessage(Codigo) { Content = new StringContent(Respuesta, Encoding.UTF8, "application/json") });
            }
        }

        private sealed class FactoriaFalsa : IClienteApiFactory
        {
            private readonly HttpMessageHandler _handler;
            public FactoriaFalsa(HttpMessageHandler handler) { _handler = handler; }
            public HttpClient Crear() => new HttpClient(_handler, false) { BaseAddress = new Uri("http://api.test/api/") };
        }

        [TestMethod]
        public async Task PideConAplicacionPantallaYVersion_YMarcaVisto()
        {
            var handler = new HandlerFalso { Respuesta = "[{\"Id\":7,\"Pantalla\":\"EnviarReposicion\",\"Ancla\":\"EnviarReposicion.EstadoRecogida\",\"Texto\":\"Hola\"}]" };
            var cliente = new ClienteGlobos(new FactoriaFalsa(handler), () => "1.10.54.0");

            List<DatosGlobo> globos = await cliente.LeerPendientesAsync("EnviarReposicion");
            await cliente.MarcarVistoAsync(7);

            Assert.AreEqual("/api/Globos?aplicacion=Nesto&pantalla=EnviarReposicion&version=1.10.54.0", handler.Peticiones[0].Url);
            Assert.AreEqual("EnviarReposicion.EstadoRecogida", globos.Single().Ancla);
            Assert.AreEqual(HttpMethod.Post, handler.Peticiones[1].Metodo);
            Assert.AreEqual("/api/Globos/7/Visto", handler.Peticiones[1].Url);
        }

        [TestMethod]
        public async Task SinVersion_NoLaManda_YUnErrorLanza_ParaQueElNucleoLoTrateComoNinguno()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.InternalServerError };
            var cliente = new ClienteGlobos(new FactoriaFalsa(handler), () => null);

            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => cliente.LeerPendientesAsync("Principal"));
            Assert.AreEqual("/api/Globos?aplicacion=Nesto&pantalla=Principal", handler.Peticiones[0].Url);
        }
    }
}
