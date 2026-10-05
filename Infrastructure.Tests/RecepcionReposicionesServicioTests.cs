using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
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
    /// Recibir una reposición en la tienda (NestoAPI#553): Nesto usa exactamente la misma API que Ariadna
    /// (api/Almacen/Recepciones, tipo REPO). Entra lo leído y el servidor informa de las diferencias.
    /// </summary>
    [TestClass]
    public class RecepcionReposicionesServicioTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> Cuerpos = new List<string>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = "{}";

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.Method + " " + request.RequestUri.PathAndQuery);
                Cuerpos.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync().ConfigureAwait(false));
                return new HttpResponseMessage(Codigo) { Content = new StringContent(Respuesta, Encoding.UTF8, "application/json") };
            }
        }

        private sealed class FactoriaFalsa : IClienteApiFactory
        {
            private readonly HttpMessageHandler _handler;
            public FactoriaFalsa(HttpMessageHandler handler) { _handler = handler; }
            public HttpClient Crear() => new HttpClient(_handler, false) { BaseAddress = new Uri("http://api.test/api/") };
        }

        [TestMethod]
        public async Task LeerPendientes_PideLasDelAlmacenYSoloDevuelveLasReposiciones()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "[{\"Tipo\":\"COMP\",\"Documento\":\"65\",\"Titulo\":\"Maystar\"}," +
                            "{\"Tipo\":\"REPO\",\"Documento\":\"80878\",\"Titulo\":\"Reposición 80878\",\"Lineas\":23,\"Unidades\":40}]"
            };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            List<RecepcionPendiente> pendientes = await servicio.LeerPendientes("1  ", "ALC");

            Assert.AreEqual("GET /api/Almacen/Recepciones?almacen=ALC&empresa=1", handler.Urls[0]);
            Assert.AreEqual(1, pendientes.Count, "Las compras de proveedor no se reciben desde aquí");
            Assert.AreEqual("80878", pendientes[0].Documento);
            Assert.AreEqual(23, pendientes[0].Lineas);
        }

        [TestMethod]
        public async Task LeerRecepcion_PideLaReposicionDeEseTraspaso()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "{\"Tipo\":\"REPO\",\"Documento\":\"80878\",\"PuedeTerminar\":true,\"SeTerminaDesdeAqui\":true," +
                            "\"Lineas\":[{\"Producto\":\"17404\",\"Descripcion\":\"Cera\",\"CodigoBarras\":\"8411\",\"Cantidad\":2}]}"
            };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            RecepcionReposicion recepcion = await servicio.LeerRecepcion("1", "ALC", "80878");

            Assert.AreEqual("GET /api/Almacen/Recepciones/REPO/80878?almacen=ALC&empresa=1", handler.Urls[0]);
            Assert.IsTrue(recepcion.PuedeTerminar);
            Assert.AreEqual("8411", recepcion.Lineas[0].CodigoBarras);
            Assert.AreEqual(2, recepcion.Lineas[0].Cantidad);
        }

        [TestMethod]
        public async Task Terminar_MandaLoLeidoConSuIdentificador()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "{\"Tipo\":\"REPO\",\"Documento\":\"80878\",\"Diferencias\":[{\"Producto\":\"17404\",\"Esperado\":2,\"Leido\":1}]," +
                            "\"AvisadoA\":\"NUEVAVISION\\\\Andre\",\"Avisos\":[\"Ha entrado lo leído\"]}"
            };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));
            var id = Guid.NewGuid();

            ResultadoRecepcionReposicion resultado = await servicio.Terminar("1", "ALC", "80878", new TerminarRecepcionReposicion
            {
                IdRecepcion = id,
                Lecturas = new List<LecturaRecepcionReposicion> { new LecturaRecepcionReposicion { Producto = "17404", Cantidad = 1 } }
            });

            Assert.AreEqual("POST /api/Almacen/Recepciones/REPO/80878/Terminar?almacen=ALC&empresa=1", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual(id.ToString(), (string)cuerpo["IdRecepcion"]);
            Assert.AreEqual("17404", (string)cuerpo["Lecturas"][0]["Producto"]);
            Assert.AreEqual(-1, resultado.Diferencias[0].Diferencia);
            Assert.AreEqual(@"NUEVAVISION\Andre", resultado.AvisadoA);
        }

        [TestMethod]
        public async Task Terminar_SinPermiso_DaElMotivoDelServidor()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.Forbidden, Respuesta = "{\"Message\":\"Solo la puede recibir la tienda de destino.\"}" };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            RecepcionReposicionException ex = await Assert.ThrowsExceptionAsync<RecepcionReposicionException>(() =>
                servicio.Terminar("1", "ALC", "80878", new TerminarRecepcionReposicion { IdRecepcion = Guid.NewGuid() }));

            StringAssert.Contains(ex.Message, "tienda de destino");
        }
    }
}
