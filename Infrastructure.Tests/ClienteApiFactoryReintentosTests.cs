using Microsoft.Extensions.Http.Resilience;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Shared;
using Polly;
using Polly.Retry;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// NestoAPI#288 (punto 3): el pipeline de reintentos de ClienteApiFactory solo debe
    /// reintentar transitorios y SOLO en GET (reintentar un POST/PUT que hizo timeout
    /// puede duplicar pedidos o apuntes).
    /// </summary>
    [TestClass]
    public class ClienteApiFactoryReintentosTests
    {
        private static RetryPredicateArguments<HttpResponseMessage> CrearArgs(HttpMethod metodo, Outcome<HttpResponseMessage> outcome)
        {
            ResilienceContext contexto = ResilienceContextPool.Shared.Get();
            if (metodo is not null)
            {
                contexto.SetRequestMessage(new HttpRequestMessage(metodo, "http://localhost/api/test"));
            }
            return new RetryPredicateArguments<HttpResponseMessage>(contexto, outcome, 0);
        }

        [TestMethod]
        public async Task EsReintentable_GetConError500_SeReintenta()
        {
            var args = CrearArgs(HttpMethod.Get,
                Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

            Assert.IsTrue(await ClienteApiFactory.EsReintentable(args));
        }

        [TestMethod]
        public async Task EsReintentable_GetConExcepcionDeRed_SeReintenta()
        {
            var args = CrearArgs(HttpMethod.Get,
                Outcome.FromException<HttpResponseMessage>(new HttpRequestException("connection reset")));

            Assert.IsTrue(await ClienteApiFactory.EsReintentable(args));
        }

        [TestMethod]
        public async Task EsReintentable_GetConRespuestaCorrecta_NoSeReintenta()
        {
            var args = CrearArgs(HttpMethod.Get,
                Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            Assert.IsFalse(await ClienteApiFactory.EsReintentable(args));
        }

        [TestMethod]
        public async Task EsReintentable_GetConError404_NoSeReintenta()
        {
            // Un 404 no es transitorio: reintentarlo solo alarga la espera
            var args = CrearArgs(HttpMethod.Get,
                Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

            Assert.IsFalse(await ClienteApiFactory.EsReintentable(args));
        }

        [TestMethod]
        public async Task EsReintentable_PostConError500_NoSeReintenta()
        {
            var args = CrearArgs(HttpMethod.Post,
                Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

            Assert.IsFalse(await ClienteApiFactory.EsReintentable(args));
        }

        [TestMethod]
        public async Task EsReintentable_PutConExcepcionDeRed_NoSeReintenta()
        {
            var args = CrearArgs(HttpMethod.Put,
                Outcome.FromException<HttpResponseMessage>(new HttpRequestException("timeout")));

            Assert.IsFalse(await ClienteApiFactory.EsReintentable(args));
        }

        [TestMethod]
        public async Task PipelineCompleto_GetTransitorio_ReintentaHastaDosVeces()
        {
            // Verifica el pipeline real: 2 reintentos sobre un GET que siempre devuelve 500
            ResiliencePipeline<HttpResponseMessage> pipeline = ClienteApiFactory.CrearPipelineReintentos();
            int intentos = 0;
            ResilienceContext contexto = ResilienceContextPool.Shared.Get();
            contexto.SetRequestMessage(new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/test"));

            HttpResponseMessage respuesta = await pipeline.ExecuteAsync(_ =>
            {
                intentos++;
                return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }, contexto);

            Assert.AreEqual(3, intentos); // 1 intento + 2 reintentos
            Assert.AreEqual(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        }
    }
}
