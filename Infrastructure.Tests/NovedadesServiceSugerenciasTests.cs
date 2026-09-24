using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Tests
{
    /// <summary>
    /// Nesto#487 (NestoAPI#526/#527): el servicio habla con los endpoints de sugerencias y buscador
    /// sin ámbito (la API entiende que es el escritorio) y con el mismo cuerpo que los comentarios.
    /// </summary>
    [TestClass]
    public class NovedadesServiceSugerenciasTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<(HttpMethod Metodo, string Url, string Cuerpo)> Peticiones = new List<(HttpMethod, string, string)>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = "[]";

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string cuerpo = request.Content == null ? null : await request.Content.ReadAsStringAsync();
                Peticiones.Add((request.Method, request.RequestUri.PathAndQuery, cuerpo));
                return new HttpResponseMessage(Codigo) { Content = new StringContent(Respuesta, Encoding.UTF8, "application/json") };
            }
        }

        private sealed class FactoriaFalsa : IClienteApiFactory
        {
            private readonly HttpMessageHandler _handler;
            public FactoriaFalsa(HttpMessageHandler handler) { _handler = handler; }
            public HttpClient Crear() => new HttpClient(_handler, false) { BaseAddress = new Uri("http://api.test/api/") };
        }

        private HandlerFalso handler;
        private NovedadesService servicio;

        [TestInitialize]
        public void Setup()
        {
            handler = new HandlerFalso();
            servicio = new NovedadesService(new FactoriaFalsa(handler));
        }

        [TestMethod]
        public async Task LeerSugerencias_PideSinAmbitoYDeserializaLosCamposDeLaSugerencia()
        {
            handler.Respuesta = "[{\"Id\":50,\"Version\":null,\"Titulo\":\"Duplicar\",\"TextoOriginal\":\"poder duplicar\",\"SugeridaNombre\":\"Carlos\",\"Estado\":\"Pendiente\",\"TieneImagen\":true,\"VotosPositivos\":3}]";

            List<NovedadUsuario> sugerencias = await servicio.LeerSugerencias();

            Assert.AreEqual("/api/Novedades/Sugerencias", handler.Peticiones[0].Url);
            Assert.AreEqual(HttpMethod.Get, handler.Peticiones[0].Metodo);
            Assert.AreEqual(1, sugerencias.Count);
            Assert.IsTrue(sugerencias[0].EsSugerencia);
            Assert.AreEqual("poder duplicar", sugerencias[0].TextoOriginal);
            Assert.IsTrue(sugerencias[0].TieneImagen);
            Assert.AreEqual(3, sugerencias[0].VotosPositivos);
        }

        [TestMethod]
        public async Task Sugerir_MandaTextoCapturaYVersion()
        {
            handler.Respuesta = "{\"Id\":60,\"Titulo\":\"Duplicar\",\"TextoOriginal\":\"Duplicar\",\"TieneImagen\":true}";

            NovedadUsuario creada = await servicio.Sugerir("Duplicar", new byte[] { 1, 2 });

            Assert.AreEqual(HttpMethod.Post, handler.Peticiones[0].Metodo);
            Assert.AreEqual("/api/Novedades/Sugerencias", handler.Peticiones[0].Url);
            JObject cuerpo = JObject.Parse(handler.Peticiones[0].Cuerpo);
            Assert.AreEqual("Duplicar", (string)cuerpo["Texto"]);
            Assert.AreEqual(Convert.ToBase64String(new byte[] { 1, 2 }), (string)cuerpo["ImagenBase64"]);
            Assert.AreEqual("image/png", (string)cuerpo["ImagenTipo"]);
            StringAssert.StartsWith((string)cuerpo["VersionCliente"], "Nesto");
            Assert.AreEqual(60, creada.Id);
        }

        [TestMethod]
        public async Task Sugerir_BadRequest_LanzaConElMotivoDeLaApi()
        {
            handler.Codigo = HttpStatusCode.BadRequest;
            handler.Respuesta = "{\"Message\":\"El texto es obligatorio\"}";

            var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => servicio.Sugerir(" ", null));

            Assert.AreEqual("No se pudo enviar la sugerencia: El texto es obligatorio", ex.Message);
        }

        [TestMethod]
        public async Task Buscar_EscapaElTexto()
        {
            await servicio.Buscar("reembolso envío");

            Assert.AreEqual("/api/Novedades/Buscar?texto=reembolso%20env%C3%ADo", handler.Peticiones[0].Url);
        }

        [TestMethod]
        public async Task LeerImagenNovedad_PideLaDeLaNovedad()
        {
            await servicio.LeerImagenNovedad(50);

            Assert.AreEqual("/api/Novedades/50/Imagen", handler.Peticiones[0].Url);
        }

        // Nesto#491 (NestoAPI#537): a quién se puede mencionar con @, sin ámbito (usuarios de Nesto).
        [TestMethod]
        public async Task LeerMencionables_PideSinAmbitoYDeserializa()
        {
            handler.Respuesta = "[{\"Nombre\":\"Alfredo\",\"Clave\":\"NUEVAVISION\\\\Alfredo\",\"Aplicacion\":\"Nesto\"}]";

            List<Mencionable> mencionables = await servicio.LeerMencionables();

            Assert.AreEqual(HttpMethod.Get, handler.Peticiones[0].Metodo);
            Assert.AreEqual("/api/Novedades/Mencionables", handler.Peticiones[0].Url);
            Assert.AreEqual(1, mencionables.Count);
            Assert.AreEqual("Alfredo", mencionables[0].Nombre);
            Assert.AreEqual("NUEVAVISION\\Alfredo", mencionables[0].Clave);
        }

        [TestMethod]
        public async Task LeerMencionables_SiLaApiFalla_Lanza()
        {
            handler.Codigo = HttpStatusCode.InternalServerError;
            handler.Respuesta = "{}";

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => servicio.LeerMencionables());
        }
    }
}
