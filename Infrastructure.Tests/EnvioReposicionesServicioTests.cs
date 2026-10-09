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
    /// La tienda prepara y termina la reposición que manda a Algete (NestoAPI#553) con api/Reposiciones: Nesto no
    /// decide nada, solo manda lo que pide el usuario y enseña lo que contesta el servidor.
    /// </summary>
    [TestClass]
    public class EnvioReposicionesServicioTests
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

        private const string REPOSICION = "{\"Empresa\":\"1\",\"Origen\":\"ALC\",\"Destino\":\"ALG\",\"Diario\":\"_RepALC\",\"Fecha\":\"2026-10-06T00:00:00\"," +
            "\"Usuario\":\"NUEVAVISION\\\\Paloma\",\"Lineas\":[{\"NumeroOrden\":561483500,\"Producto\":\"17404\",\"Nombre\":\"Cera\"," +
            "\"CodigoBarras\":\"8411\",\"Cantidad\":3,\"StockOrigen\":7}],\"Unidades\":3}";

        [TestMethod]
        public async Task LeerEnPreparacion_PideLaDelOrigen()
        {
            var handler = new HandlerFalso { Respuesta = REPOSICION };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            ReposicionEnPreparacion reposicion = await servicio.LeerEnPreparacion("1  ", "ALC");

            Assert.AreEqual("GET /api/Reposiciones/EnPreparacion?origen=ALC&empresa=1", handler.Urls[0]);
            Assert.AreEqual(561483500, reposicion.Lineas[0].NumeroOrden);
            Assert.AreEqual(7, reposicion.Lineas[0].StockOrigen);
            Assert.AreEqual(3, reposicion.Unidades);
        }

        [TestMethod]
        public async Task LeerEnPreparacion_SiNoHayNinguna_DevuelveNull()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.NotFound, Respuesta = "" };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            Assert.IsNull(await servicio.LeerEnPreparacion("1", "ALC"));
        }

        [TestMethod]
        public async Task Crear_SinLineas_PideLaPropuestaAlServidor()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.Created, Respuesta = REPOSICION };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            ReposicionEnPreparacion creada = await servicio.Crear(new CrearReposicion { Empresa = "1", Origen = "ALC", Destino = "ALG" });

            Assert.AreEqual("POST /api/Reposiciones", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual("ALC", (string)cuerpo["Origen"]);
            Assert.AreEqual("ALG", (string)cuerpo["Destino"]);
            Assert.IsNull(cuerpo["Lineas"], "Sin líneas el servidor calcula la propuesta");
            Assert.AreEqual(1, creada.Lineas.Count);
        }

        [TestMethod]
        public async Task Crear_ConInventarioEnCurso_DaElMotivoDelServidor()
        {
            var handler = new HandlerFalso
            {
                Codigo = HttpStatusCode.Conflict,
                Respuesta = "{\"Message\":\"El almacén ALC tiene un inventario en curso: termínalo antes de crear una reposición.\"}"
            };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            EnvioReposicionException ex = await Assert.ThrowsExceptionAsync<EnvioReposicionException>(() =>
                servicio.Crear(new CrearReposicion { Empresa = "1", Origen = "ALC", Destino = "ALG" }));

            StringAssert.Contains(ex.Message, "inventario en curso");
        }

        [TestMethod]
        public async Task CambiarCantidad_MandaLaCantidadDeEsaLinea()
        {
            var handler = new HandlerFalso { Respuesta = REPOSICION };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            await servicio.CambiarCantidad("1", "ALC", 561483500, 0);

            Assert.AreEqual("PUT /api/Reposiciones/EnPreparacion/Lineas/561483500?origen=ALC&empresa=1", handler.Urls[0]);
            Assert.AreEqual(0, (int)JObject.Parse(handler.Cuerpos[0])["Cantidad"]);
        }

        [TestMethod]
        public async Task Terminar_DevuelveElTraspaso()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "{\"NumTraspaso\":80999,\"Origen\":\"ALC\",\"Destino\":\"ALG\",\"Lineas\":[{\"Producto\":\"17404\",\"Nombre\":\"Cera\",\"Cantidad\":3}],\"Unidades\":3}"
            };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            ResultadoTerminarReposicion resultado = await servicio.Terminar("1", "ALC");

            Assert.AreEqual("POST /api/Reposiciones/EnPreparacion/Terminar?origen=ALC&empresa=1", handler.Urls[0]);
            Assert.AreEqual(80999, resultado.NumTraspaso);
            Assert.AreEqual(3, resultado.Unidades);
        }

        [TestMethod]
        public async Task Terminar_SiFallaLaContabilizacion_DaElMotivoDelServidor()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.BadRequest, Respuesta = "\"El producto 17404 quedaría con stock negativo en ALC.\"" };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            EnvioReposicionException ex = await Assert.ThrowsExceptionAsync<EnvioReposicionException>(() => servicio.Terminar("1", "ALC"));

            StringAssert.Contains(ex.Message, "stock negativo");
        }

        [TestMethod]
        public async Task Crear_MandaQueLaCreaNesto()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.Created, Respuesta = REPOSICION };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            await servicio.Crear(new CrearReposicion { Empresa = "1", Origen = "ALC", Destino = "ALG", Herramienta = "Nesto" });

            Assert.AreEqual("Nesto", (string)JObject.Parse(handler.Cuerpos[0])["Herramienta"]);
        }

        [TestMethod]
        public async Task Crear_SinPermiso_DaElMotivoDelServidorYQueEsUn403()
        {
            const string motivo = "Solo el proceso automático y las personas autorizadas pueden rellenar reposiciones a mano.";
            var handler = new HandlerFalso { Codigo = HttpStatusCode.Forbidden, Respuesta = "{\"Message\":\"" + motivo + "\"}" };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            EnvioReposicionException ex = await Assert.ThrowsExceptionAsync<EnvioReposicionException>(() =>
                servicio.Crear(new CrearReposicion { Empresa = "1", Origen = "ALC", Destino = "ALG" }));

            Assert.AreEqual(motivo, ex.Message);
            Assert.IsTrue(ex.EsSinPermiso);
        }

        [TestMethod]
        [DataRow("true", true)]
        [DataRow("false", false)]
        public async Task PuedeRellenarManual_DevuelveLoQueDiceElServidor(string respuesta, bool esperado)
        {
            var handler = new HandlerFalso { Respuesta = respuesta };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            bool puede = await servicio.PuedeRellenarManual();

            Assert.AreEqual("GET /api/Reposiciones/PuedeRellenarManual", handler.Urls[0]);
            Assert.AreEqual(esperado, puede);
        }

        [TestMethod]
        public async Task PuedeRellenarManual_ConUnaApiAnteriorA577_NoLoRestringe()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.NotFound, Respuesta = "" };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            Assert.IsTrue(await servicio.PuedeRellenarManual());
        }

        [TestMethod]
        public async Task PuedeRellenarManual_SiElServidorFalla_NoDejaRellenar()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.InternalServerError, Respuesta = "" };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            Assert.IsFalse(await servicio.PuedeRellenarManual());
        }

        [TestMethod]
        public async Task LeerProximaLlegada_PideLaDeLaRutaYLeeElCierre()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "{\"Origen\":\"ALC\",\"Destino\":\"ALG\",\"CierraEl\":\"2026-10-09T10:00:00\",\"LlegaEl\":\"2026-10-09T16:00:00\"," +
                    "\"PedidoSaleEl\":\"2026-10-12T00:00:00\",\"DiasHastaSalida\":4}"
            };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            ProximaReposicion proxima = await servicio.LeerProximaLlegada("1  ", "ALC", "ALG");

            Assert.AreEqual("GET /api/Reposiciones/ProximaLlegada?origen=ALC&destino=ALG&empresa=1", handler.Urls[0]);
            Assert.AreEqual(new DateTime(2026, 10, 9, 10, 0, 0), proxima.CierraEl);
            Assert.AreEqual(4, proxima.DiasHastaSalida);
        }

        [TestMethod]
        public async Task LeerProximaLlegada_SinCalendario_DevuelveNull()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.NotFound, Respuesta = "\"No hay calendario de reposiciones activo de ALC a ALG.\"" };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            Assert.IsNull(await servicio.LeerProximaLlegada("1", "ALC", "ALG"));
        }

        // ---- Sugerencia 564: la reposición en PDF ----

        [TestMethod]
        public async Task DescargarListadoPdf_PideElDeLaEnPreparacionDelOrigen()
        {
            var handler = new HandlerFalso { Respuesta = "%PDF" };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            byte[] pdf = await servicio.DescargarListadoPdf("1", "ALC");

            Assert.AreEqual("GET /api/Reposiciones/EnPreparacion/Pdf?origen=ALC&empresa=1", handler.Urls[0]);
            Assert.AreEqual("%PDF", Encoding.ASCII.GetString(pdf));
        }

        [TestMethod]
        public async Task DescargarListadoPdf_SinNingunaEnPreparacion_LanzaConElMotivoDeLaApi()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.NotFound, Respuesta = "{\"Message\":\"ALC no tiene ninguna reposición en preparación.\"}" };
            var servicio = new ServicioEnvioReposiciones(new FactoriaFalsa(handler));

            EnvioReposicionException ex = await Assert.ThrowsExceptionAsync<EnvioReposicionException>(() => servicio.DescargarListadoPdf("1", "ALC"));

            Assert.AreEqual("ALC no tiene ninguna reposición en preparación.", ex.Message);
        }

        [TestMethod]
        public async Task DescargarListadoPdfRecepcion_PideElDelTraspasoEnElAlmacen()
        {
            var handler = new HandlerFalso { Respuesta = "%PDF" };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            byte[] pdf = await servicio.DescargarListadoPdf("1", "ALC", " 80893 ");

            Assert.AreEqual("GET /api/Reposiciones/Recepcion/80893/Pdf?almacen=ALC&empresa=1", handler.Urls[0]);
            Assert.AreEqual("%PDF", Encoding.ASCII.GetString(pdf));
        }

        [TestMethod]
        public async Task DescargarListadoPdfRecepcion_QueYaNoEstaPendiente_Lanza()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.NotFound, Respuesta = "{\"Message\":\"La reposición 80893 no está pendiente de recibir en ALC.\"}" };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            RecepcionReposicionException ex = await Assert.ThrowsExceptionAsync<RecepcionReposicionException>(() => servicio.DescargarListadoPdf("1", "ALC", "80893"));

            Assert.AreEqual("La reposición 80893 no está pendiente de recibir en ALC.", ex.Message);
        }
    }
}
