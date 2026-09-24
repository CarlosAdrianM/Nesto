using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
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
    /// Nesto#477: el buzón de notificaciones habla con api/Notificaciones/Buzon mandando SIEMPRE
    /// aplicacion=Nesto (sin ella la API usa NestoApp y Nesto vería el buzón de la app).
    /// </summary>
    [TestClass]
    public class BuzonNotificacionesServiceTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<(HttpMethod Metodo, string Url)> Peticiones = new List<(HttpMethod, string)>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = "[]";

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

        private HandlerFalso handler;
        private BuzonNotificacionesService servicio;

        [TestInitialize]
        public void Setup()
        {
            handler = new HandlerFalso();
            servicio = new BuzonNotificacionesService(new FactoriaFalsa(handler));
        }

        [TestMethod]
        public async Task LeerBuzon_MandaAplicacionNestoYDeserializaLosDatos()
        {
            handler.Respuesta = "[{\"Id\":5,\"Titulo\":\"Te han contestado en Novedades\",\"Cuerpo\":\"Claude (asistente IA): hecho\","
                + "\"Datos\":{\"tipo\":\"NovedadComentario\",\"novedadId\":\"338\",\"comentarioId\":\"77\"},"
                + "\"FechaCreacion\":\"2026-09-24T13:55:00\",\"Leida\":false}]";

            List<NotificacionBuzon> lista = await servicio.LeerBuzon();

            Assert.AreEqual(HttpMethod.Get, handler.Peticiones[0].Metodo);
            Assert.AreEqual("/api/Notificaciones/Buzon?aplicacion=Nesto&soloNoLeidas=false&pagina=1&tamanoPagina=20", handler.Peticiones[0].Url);
            Assert.AreEqual(1, lista.Count);
            Assert.AreEqual(NotificacionBuzon.TIPO_NOVEDAD_COMENTARIO, lista[0].Tipo);
            Assert.AreEqual(338, lista[0].DatoEntero("novedadId"));
            Assert.AreEqual(77, lista[0].DatoEntero("comentarioId"));
            Assert.AreEqual(new DateTime(2026, 9, 24, 13, 55, 0), lista[0].FechaCreacion);
            Assert.IsFalse(lista[0].Leida);
        }

        [TestMethod]
        public async Task LeerBuzon_SoloNoLeidasYPagina()
        {
            await servicio.LeerBuzon(true, 2, 50);

            Assert.AreEqual("/api/Notificaciones/Buzon?aplicacion=Nesto&soloNoLeidas=true&pagina=2&tamanoPagina=50", handler.Peticiones[0].Url);
        }

        [TestMethod]
        public async Task ContarNoLeidas_MandaAplicacionNestoYDevuelveElNumero()
        {
            handler.Respuesta = "4";

            int noLeidas = await servicio.ContarNoLeidas();

            Assert.AreEqual("/api/Notificaciones/Buzon/NoLeidas?aplicacion=Nesto", handler.Peticiones[0].Url);
            Assert.AreEqual(4, noLeidas);
        }

        [TestMethod]
        public async Task MarcarLeida_EsPutALaNotificacion()
        {
            handler.Respuesta = "";

            await servicio.MarcarLeida(5);

            Assert.AreEqual(HttpMethod.Put, handler.Peticiones[0].Metodo);
            Assert.AreEqual("/api/Notificaciones/Buzon/5/Leida", handler.Peticiones[0].Url);
        }

        [TestMethod]
        public async Task MarcarTodasLeidas_MandaAplicacionNesto()
        {
            handler.Respuesta = "3";

            int marcadas = await servicio.MarcarTodasLeidas();

            Assert.AreEqual(HttpMethod.Put, handler.Peticiones[0].Metodo);
            Assert.AreEqual("/api/Notificaciones/Buzon/Leidas?aplicacion=Nesto", handler.Peticiones[0].Url);
            Assert.AreEqual(3, marcadas);
        }

        [TestMethod]
        public async Task Eliminar_EsDelete()
        {
            handler.Respuesta = "";

            await servicio.Eliminar(5);

            Assert.AreEqual(HttpMethod.Delete, handler.Peticiones[0].Metodo);
            Assert.AreEqual("/api/Notificaciones/Buzon/5", handler.Peticiones[0].Url);
        }

        [TestMethod]
        public async Task SiLaApiFalla_LanzaConElMotivo()
        {
            handler.Codigo = HttpStatusCode.NotFound;
            handler.Respuesta = "{\"Message\":\"No existe\"}";

            var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => servicio.Eliminar(5));

            StringAssert.StartsWith(ex.Message, "No se pudo borrar la notificación");
        }

        [TestMethod]
        public async Task ContarNoLeidas_SiLaApiFalla_Lanza()
        {
            handler.Codigo = HttpStatusCode.InternalServerError;
            handler.Respuesta = "";

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => servicio.ContarNoLeidas());
        }

        [TestMethod]
        public void Datos_SinDatosOConClaveEnOtroFormato_NoRompe()
        {
            Assert.IsNull(new NotificacionBuzon().Tipo);
            var n = new NotificacionBuzon { Datos = new Dictionary<string, string> { ["Tipo"] = "X", ["novedadId"] = "abc" } };
            Assert.AreEqual("X", n.Tipo);
            Assert.IsNull(n.DatoEntero("novedadId"));
        }
    }
}
