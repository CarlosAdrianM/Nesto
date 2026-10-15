using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#529: las llamadas a api/PropuestasStock (filtros en la URL, el cuerpo de la decisión, el Message de los errores
    /// y los motivos de silencio con reserva si la API todavía no los da).
    /// </summary>
    [TestClass]
    public class ServicioPropuestasStockTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public List<string> Urls { get; } = new();
            public List<string> Cuerpos { get; } = new();

            public HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.RequestUri!.PathAndQuery);
                Cuerpos.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
                return _responder(request);
            }
        }

        private static ServicioPropuestasStock Crear(HandlerFalso handler)
        {
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() =>
                new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.local/api/") });
            return new ServicioPropuestasStock(factoria);
        }

        private static HttpResponseMessage Json(HttpStatusCode codigo, string json)
            => new(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        [TestMethod]
        public async Task LeerPropuestas_PasaMesEstadoYTop()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK, "[{\"Id\":7,\"Producto\":\"17404\",\"Explicacion\":\"x\"}]"));

            List<PropuestaStockModel> filas = await Crear(handler).LeerPropuestas("2026-10", "Pendiente", 2000);

            Assert.AreEqual("/api/PropuestasStock?top=2000&mes=2026-10&estado=Pendiente", handler.Urls.Single());
            Assert.AreEqual(7, filas.Single().Id);
        }

        [TestMethod]
        public async Task LeerResumen_SinMes_NoLoPasa()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK, "[]"));

            await Crear(handler).LeerResumen(null!);

            Assert.AreEqual("/api/PropuestasStock/Resumen", handler.Urls.Single());
        }

        [TestMethod]
        public async Task Leer_403SinCuerpo_ExcepcionSinPermisoConTextoDeUsted()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

            var ex = await Assert.ThrowsExceptionAsync<PropuestasStockException>(() => Crear(handler).LeerPropuestas(null!, null!, 10));

            Assert.IsTrue(ex.SinPermiso);
            StringAssert.Contains(ex.Message, "Solo Compras y Dirección");
        }

        [TestMethod]
        public async Task Decidir_MandaElCuerpoYDevuelveElResultado()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK,
                "{\"Id\":7,\"Producto\":\"17404\",\"Decision\":\"Aplicar\",\"Estado\":\"Aplicada\",\"Cambios\":[\"ALG: máximo 555 → 364\"],\"Mensaje\":\"Aplicada\"}"));

            ResultadoDecisionPropuestaStock resultado = await Crear(handler).Decidir(7, new DecisionPropuestaStockPeticion
            {
                Decision = "Aplicar", StockMaximo = 300, StockMinimo = 200, Motivo = "Campaña"
            });

            Assert.AreEqual("/api/PropuestasStock/7/Decision", handler.Urls.Single());
            JObject cuerpo = JObject.Parse(handler.Cuerpos.Single());
            Assert.AreEqual("Aplicar", (string?)cuerpo["Decision"]);
            Assert.AreEqual(300, (int?)cuerpo["StockMaximo"]);
            Assert.AreEqual("Aplicada", resultado.Estado);
            CollectionAssert.AreEqual(new[] { "ALG: máximo 555 → 364" }, resultado.Cambios);
        }

        [DataTestMethod]
        [DataRow(HttpStatusCode.BadRequest)]
        [DataRow(HttpStatusCode.NotFound)]
        [DataRow(HttpStatusCode.Conflict)]
        public async Task Decidir_Error_LanzaConElMessageDeLaApi(HttpStatusCode codigo)
        {
            var handler = new HandlerFalso(_ => Json(codigo, "{\"Message\":\"La propuesta ya está aplicada.\"}"));

            var ex = await Assert.ThrowsExceptionAsync<PropuestasStockException>(() =>
                Crear(handler).Decidir(7, new DecisionPropuestaStockPeticion { Decision = "Posponer" }));

            Assert.AreEqual("La propuesta ya está aplicada.", ex.Message);
            Assert.AreEqual((int)codigo, ex.Codigo);
        }

        [TestMethod]
        public async Task MotivosSilencio_DeLaApi()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK, "[\"Nuevo\",\"Campaña\"]"));

            List<string> motivos = await Crear(handler).LeerMotivosSilencio();

            Assert.AreEqual("/api/PropuestasStock/MotivosSilencio", handler.Urls.Single());
            CollectionAssert.AreEqual(new[] { "Nuevo", "Campaña" }, motivos);
        }

        [TestMethod]
        public async Task MotivosSilencio_ApiAnterior404_LosDeReserva()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

            List<string> motivos = await Crear(handler).LeerMotivosSilencio();

            CollectionAssert.AreEqual(new[] { "Nuevo", "Estacional", "Aparato" }, motivos);
        }

        [TestMethod]
        public void LeerListaMotivos_AdmiteObjetosYEnvoltorio()
        {
            CollectionAssert.AreEqual(new[] { "Nuevo", "Aparato" },
                ServicioPropuestasStock.LeerListaMotivos("[{\"Motivo\":\"Nuevo\"},{\"Codigo\":\"Aparato\"}]"));
            CollectionAssert.AreEqual(new[] { "Estacional" },
                ServicioPropuestasStock.LeerListaMotivos("{\"Motivos\":[\"Estacional\",\" estacional \"]}"));
            Assert.AreEqual(0, ServicioPropuestasStock.LeerListaMotivos("").Count);
        }
    }
}
