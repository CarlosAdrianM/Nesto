using FakeItEasy;
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
    /// NestoAPI#581: sustitución temporal de referencias. La API decide cuándo hay que avisar; Nesto pregunta al meter el
    /// producto (plantilla y detalle) y no vuelve a preguntar por un producto al que el usuario ya ha dicho que no.
    /// </summary>
    [TestClass]
    public class SustitucionesProductoTests
    {
        private const string VIGENTE = "{\"Id\":7,\"Empresa\":\"1\",\"Producto\":\"25539\",\"NombreProducto\":\"PANTALON PRESOTERAPIA LOTE RT\"," +
            "\"ProductoSustituto\":\"45685\",\"NombreSustituto\":\"PANTALON PRESOTERAPIA COD040308\",\"MientrasNoHayaStock\":true," +
            "\"Estado\":\"Vigente\",\"Vigente\":true,\"Aviso\":\"Compras pide servir la 45685 (PANTALON PRESOTERAPIA COD040308) en lugar de la 25539 mientras no haya stock.\"}";

        #region Servicio

        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> Cuerpos = new List<string>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = "null";
            public bool Lanzar;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.Method + " " + request.RequestUri.PathAndQuery);
                Cuerpos.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync().ConfigureAwait(false));
                if (Lanzar)
                {
                    throw new HttpRequestException("Sin conexión");
                }
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
        public async Task LeerVigente_PideConLaCantidad_YLaDevuelve()
        {
            var handler = new HandlerFalso { Respuesta = VIGENTE };
            var servicio = new ServicioSustitucionesProducto(new FactoriaFalsa(handler));

            SustitucionProductoDTO vigente = await servicio.LeerVigente("1", " 25539 ", 100);

            Assert.AreEqual("GET /api/Productos/25539/Sustitucion?empresa=1&cantidad=100", handler.Urls[0]);
            Assert.AreEqual("45685", vigente.ProductoSustituto);
            Assert.IsTrue(vigente.Vigente);
        }

        [TestMethod]
        public async Task LeerVigente_NullErrorO404_EsNull_SinLanzar()
        {
            var conNull = new ServicioSustitucionesProducto(new FactoriaFalsa(new HandlerFalso { Respuesta = "null" }));
            var api404 = new ServicioSustitucionesProducto(new FactoriaFalsa(new HandlerFalso { Codigo = HttpStatusCode.NotFound, Respuesta = "" }));
            var sinConexion = new ServicioSustitucionesProducto(new FactoriaFalsa(new HandlerFalso { Lanzar = true }));

            Assert.IsNull(await conNull.LeerVigente("1", "25539", 1));
            Assert.IsNull(await api404.LeerVigente("1", "25539", 1), "API anterior al endpoint: no se avisa");
            Assert.IsNull(await sinConexion.LeerVigente("1", "25539", 1), "Es una ayuda: si falla, no se avisa");
        }

        [TestMethod]
        public async Task Listar_404_EsNull_YLaLista()
        {
            var api404 = new ServicioSustitucionesProducto(new FactoriaFalsa(new HandlerFalso { Codigo = HttpStatusCode.NotFound, Respuesta = "" }));
            var handler = new HandlerFalso { Respuesta = "[" + VIGENTE + "]" };
            var servicio = new ServicioSustitucionesProducto(new FactoriaFalsa(handler));

            Assert.IsNull(await api404.Listar("1", "25539"));
            List<SustitucionProductoDTO> lista = await servicio.Listar("1", "25539");
            Assert.AreEqual("GET /api/Productos/25539/Sustituciones?empresa=1", handler.Urls[0]);
            Assert.AreEqual(1, lista.Count);
        }

        [TestMethod]
        public async Task Crear_PostConElDto_YSiNoValeLanzaConElMotivoDeLaApi()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.Created, Respuesta = VIGENTE };
            var servicio = new ServicioSustitucionesProducto(new FactoriaFalsa(handler));
            var malo = new ServicioSustitucionesProducto(new FactoriaFalsa(new HandlerFalso
            {
                Codigo = HttpStatusCode.BadRequest,
                Respuesta = "{\"Message\":\"La fecha «hasta» ya ha pasado.\"}"
            }));

            await servicio.Crear("25539", new NuevaSustitucionProductoDTO { Empresa = "1", ProductoSustituto = "45685", FechaHasta = new DateTime(2026, 10, 31) });
            var ex = await Assert.ThrowsExceptionAsync<Exception>(() => malo.Crear("25539", new NuevaSustitucionProductoDTO { ProductoSustituto = "45685" }));

            Assert.AreEqual("POST /api/Productos/25539/Sustituciones", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual("45685", (string)cuerpo["ProductoSustituto"]);
            Assert.AreEqual(true, (bool)cuerpo["MientrasNoHayaStock"]);
            StringAssert.Contains(ex.Message, "La fecha «hasta» ya ha pasado.");
        }

        [TestMethod]
        public async Task Anular_Delete()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.NoContent, Respuesta = "" };
            var servicio = new ServicioSustitucionesProducto(new FactoriaFalsa(handler));

            await servicio.Anular("1", "25539", 7);

            Assert.AreEqual("DELETE /api/Productos/25539/Sustituciones/7?empresa=1", handler.Urls[0]);
        }

        #endregion

        #region Comprobador

        private static SustitucionProductoDTO Sustitucion(string producto = "25539", string sustituto = "45685") => new SustitucionProductoDTO
        {
            Producto = producto,
            ProductoSustituto = sustituto,
            Vigente = true,
            Aviso = $"Compras pide servir la {sustituto} en lugar de la {producto} mientras no haya stock."
        };

        [TestMethod]
        public async Task Comprobador_SinCantidadOSinProducto_NiPregunta()
        {
            var servicio = A.Fake<IServicioSustitucionesProducto>();
            var comprobador = new ComprobadorSustitucionProducto(servicio);

            Assert.IsNull(await comprobador.SustitucionAOfrecer("1", "25539", 0));
            Assert.IsNull(await comprobador.SustitucionAOfrecer("1", "  ", 5));
            A.CallTo(() => servicio.LeerVigente(A<string>._, A<string>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Comprobador_Rechazada_NoVuelveAPreguntar_HastaOlvidar()
        {
            var servicio = A.Fake<IServicioSustitucionesProducto>();
            A.CallTo(() => servicio.LeerVigente("1", "25539", A<int>._)).Returns(Task.FromResult(Sustitucion()));
            var comprobador = new ComprobadorSustitucionProducto(servicio);

            Assert.IsNotNull(await comprobador.SustitucionAOfrecer("1", "25539", 100));
            comprobador.Rechazar("25539 ");
            Assert.IsNull(await comprobador.SustitucionAOfrecer("1", "25539", 200), "Ya ha dicho que no");
            comprobador.Olvidar();
            Assert.IsNotNull(await comprobador.SustitucionAOfrecer("1", "25539", 200), "Otro pedido: se vuelve a avisar");
            A.CallTo(() => servicio.LeerVigente("1", "25539", A<int>._)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task Comprobador_SustitutoVacioOElMismo_NoSeOfrece()
        {
            var servicio = A.Fake<IServicioSustitucionesProducto>();
            A.CallTo(() => servicio.LeerVigente("1", "11111", A<int>._)).Returns(Task.FromResult(Sustitucion("11111", " ")));
            A.CallTo(() => servicio.LeerVigente("1", "22222", A<int>._)).Returns(Task.FromResult(Sustitucion("22222", "22222")));
            var comprobador = new ComprobadorSustitucionProducto(servicio);

            Assert.IsNull(await comprobador.SustitucionAOfrecer("1", "11111", 1));
            Assert.IsNull(await comprobador.SustitucionAOfrecer("1", "22222", 1));
        }

        [TestMethod]
        public void Pregunta_ElAvisoDeLaApiYLaOferta()
        {
            string pregunta = ComprobadorSustitucionProducto.Pregunta(Sustitucion());

            Assert.AreEqual("Compras pide servir la 45685 en lugar de la 25539 mientras no haya stock." + Environment.NewLine + Environment.NewLine +
                "¿Poner la 45685 en su lugar?", pregunta);
        }

        [TestMethod]
        public void TextoHastaCuando_LasTresFormas()
        {
            Assert.AreEqual("Mientras no haya stock", new SustitucionProductoDTO { MientrasNoHayaStock = true }.TextoHastaCuando);
            Assert.AreEqual("Hasta el 31/10/2026", new SustitucionProductoDTO { FechaHasta = new DateTime(2026, 10, 31) }.TextoHastaCuando);
            Assert.AreEqual("Mientras no haya stock (como mucho hasta el 31/10/2026)",
                new SustitucionProductoDTO { MientrasNoHayaStock = true, FechaHasta = new DateTime(2026, 10, 31) }.TextoHastaCuando);
        }

        #endregion
    }
}
