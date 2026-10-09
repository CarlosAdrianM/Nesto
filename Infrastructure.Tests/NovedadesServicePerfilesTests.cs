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
    /// Sugerencia 551: el servicio pide las novedades de todos los perfiles con ?todas=true, lee los perfiles del
    /// usuario y cambia a quién afecta una novedad.
    /// </summary>
    [TestClass]
    public class NovedadesServicePerfilesTests
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
        public async Task ObtenerTodasLasNovedades_PideConTodasYLeeLosPerfiles()
        {
            handler.Respuesta = "[{\"Id\":1,\"Version\":\"1.10.40.0\",\"Titulo\":\"x\",\"Perfiles\":[\"Almacén\",\"Tiendas\"]},{\"Id\":2,\"Version\":\"1.10.40.0\",\"Titulo\":\"y\",\"Perfiles\":null}]";

            List<NovedadUsuario> novedades = await servicio.ObtenerTodasLasNovedades();

            Assert.AreEqual("/api/Novedades?todas=true", handler.Peticiones[0].Url);
            CollectionAssert.AreEqual(new[] { "Almacén", "Tiendas" }, novedades[0].Perfiles);
            Assert.IsNull(novedades[1].Perfiles);
        }

        [TestMethod]
        public async Task ObtenerTodasLasNovedades_SiFalla_ListaVaciaSinLanzar()
        {
            handler.Codigo = HttpStatusCode.InternalServerError;

            List<NovedadUsuario> novedades = await servicio.ObtenerTodasLasNovedades();

            Assert.AreEqual(0, novedades.Count);
        }

        [TestMethod]
        public async Task ObtenerNovedades_SigueSinTodas()
        {
            _ = await servicio.ObtenerNovedades("1.10.39.1");

            Assert.AreEqual("/api/Novedades?desdeVersion=1.10.39.1", handler.Peticiones[0].Url);
        }

        [TestMethod]
        public async Task LeerMisPerfiles_DeserializaLaRespuesta()
        {
            handler.Respuesta = "{\"Perfiles\":[\"Almacén\"],\"VeTodas\":false,\"PuedeEditar\":false,\"Disponibles\":[\"Vendedores\",\"Almacén\",\"Tiendas\",\"Administración\"]}";

            PerfilesUsuarioNovedades perfiles = await servicio.LeerMisPerfiles();

            Assert.AreEqual("/api/Novedades/MisPerfiles", handler.Peticiones[0].Url);
            CollectionAssert.AreEqual(new[] { "Almacén" }, perfiles.Perfiles);
            Assert.IsFalse(perfiles.VeTodas);
            Assert.AreEqual(4, perfiles.Disponibles.Count);
        }

        [TestMethod]
        public async Task LeerMisPerfiles_SiFalla_Lanza()
        {
            handler.Codigo = HttpStatusCode.NotFound;
            handler.Respuesta = "{\"Message\":\"No se ha encontrado\"}";

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => servicio.LeerMisPerfiles());
        }

        [TestMethod]
        public async Task CambiarPerfiles_HaceElPutConLaLista()
        {
            handler.Codigo = HttpStatusCode.NoContent;
            handler.Respuesta = "";

            await servicio.CambiarPerfiles(7, new[] { "Almacén", "Tiendas" });

            Assert.AreEqual(HttpMethod.Put, handler.Peticiones[0].Metodo);
            Assert.AreEqual("/api/Novedades/7/Perfiles", handler.Peticiones[0].Url);
            CollectionAssert.AreEqual(new[] { "Almacén", "Tiendas" }, JObject.Parse(handler.Peticiones[0].Cuerpo)["Perfiles"].ToObject<string[]>());
        }

        [TestMethod]
        public async Task CambiarPerfiles_SinNinguno_MandaListaVacia()
        {
            handler.Codigo = HttpStatusCode.NoContent;
            handler.Respuesta = "";

            await servicio.CambiarPerfiles(7, null);

            Assert.AreEqual(0, JObject.Parse(handler.Peticiones[0].Cuerpo)["Perfiles"].ToObject<string[]>().Length);
        }
    }
}
