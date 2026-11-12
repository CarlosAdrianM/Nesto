using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
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
    /// <summary>NestoAPI#495: el aviso de novedades al arrancar por última novedad vista (Id), junto al de versión de siempre.</summary>
    [TestClass]
    public class ComprobadorNovedadesArranqueTests
    {
        private const string VERSION = "1.10.60.0";
        private IConfiguracion configuracion;
        private INovedadesService servicio;
        private Dictionary<string, string> parametros;
        private Dictionary<string, string> guardados;

        [TestInitialize]
        public void Setup()
        {
            parametros = new Dictionary<string, string>();
            guardados = new Dictionary<string, string>();
            configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, A<string>._)).ReturnsLazily((string e, string clave) =>
                parametros.TryGetValue(clave, out string valor) ? Task.FromResult(valor) : throw new Exception("No se puede leer el parámetro"));
            A.CallTo(() => configuracion.GuardarParametro(A<string>._, A<string>._, A<string>._)).Invokes((string e, string clave, string valor) =>
                guardados[clave] = valor).Returns(Task.CompletedTask);
            servicio = A.Fake<INovedadesService>();
            A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).Returns(new List<NovedadUsuario>());
            A.CallTo(() => servicio.ObtenerNovedadesDesdeId(A<int>._)).Returns(new List<NovedadUsuario>());
        }

        private static NovedadUsuario Novedad(int id, string ambito = "NestoAPI", string version = VERSION) =>
            new NovedadUsuario { Id = id, Ambito = ambito, Version = version, Titulo = "N" + id };

        private Task<List<NovedadUsuario>> Comprobar(string version = VERSION) =>
            new ComprobadorNovedadesArranque(configuracion, servicio, version).Comprobar();

        [TestMethod]
        public async Task SinCambiarDeVersion_UnaNovedadDeLaApiNueva_SeEnsenaYSeGuardaSuId()
        {
            parametros[Parametros.Claves.UltimaVersionNovedades] = VERSION;
            parametros[Parametros.Claves.UltimaNovedadVista] = "100";
            A.CallTo(() => servicio.ObtenerNovedadesDesdeId(100)).Returns(new List<NovedadUsuario> { Novedad(101) });

            List<NovedadUsuario> mostrar = await Comprobar();

            CollectionAssert.AreEqual(new[] { 101 }, mostrar.Select(n => n.Id).ToArray());
            Assert.AreEqual("101", guardados[Parametros.Claves.UltimaNovedadVista]);
            A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).MustNotHaveHappened();
            Assert.IsFalse(guardados.ContainsKey(Parametros.Claves.UltimaVersionNovedades));
        }

        [TestMethod]
        public async Task YaVista_NoSeRepite()
        {
            parametros[Parametros.Claves.UltimaVersionNovedades] = VERSION;
            parametros[Parametros.Claves.UltimaNovedadVista] = "101";

            List<NovedadUsuario> mostrar = await Comprobar();

            Assert.AreEqual(0, mostrar.Count);
            A.CallTo(() => servicio.ObtenerNovedadesDesdeId(101)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(guardados.ContainsKey(Parametros.Claves.UltimaNovedadVista));
        }

        [TestMethod]
        public async Task PrimeraVez_NoEnsenaElHistorico_PeroApuntaPorDondeVa()
        {
            parametros[Parametros.Claves.UltimaVersionNovedades] = VERSION;
            A.CallTo(() => servicio.ObtenerNovedadesDesdeId(0)).Returns(new List<NovedadUsuario> { Novedad(5), Novedad(90, "Nesto", "1.10.50.0") });

            List<NovedadUsuario> mostrar = await Comprobar();

            Assert.AreEqual(0, mostrar.Count);
            Assert.AreEqual("90", guardados[Parametros.Claves.UltimaNovedadVista]);
        }

        [TestMethod]
        public async Task PrimeraVez_TrasActualizar_SoloLasDeLaVersionComoAntes()
        {
            parametros[Parametros.Claves.UltimaVersionNovedades] = "1.10.59.0";
            A.CallTo(() => servicio.ObtenerNovedades("1.10.59.0")).Returns(new List<NovedadUsuario> { Novedad(90, "Nesto") });
            A.CallTo(() => servicio.ObtenerNovedadesDesdeId(0)).Returns(new List<NovedadUsuario> { Novedad(5, "Nesto", "1.9.0.0"), Novedad(90, "Nesto") });

            List<NovedadUsuario> mostrar = await Comprobar();

            CollectionAssert.AreEqual(new[] { 90 }, mostrar.Select(n => n.Id).ToArray());
            Assert.AreEqual("90", guardados[Parametros.Claves.UltimaNovedadVista]);
            Assert.AreEqual(VERSION, guardados[Parametros.Claves.UltimaVersionNovedades]);
        }

        [TestMethod]
        public async Task TrasActualizar_JuntaLasDeLaVersionYLasDeIdSinRepetir_IncluidaLaSugerenciaConIdAntiguo()
        {
            parametros[Parametros.Claves.UltimaVersionNovedades] = "1.10.59.0";
            parametros[Parametros.Claves.UltimaNovedadVista] = "100";
            // La 40 es una sugerencia implementada en la 1.10.60.0: conserva su Id antiguo y solo sale por versión
            A.CallTo(() => servicio.ObtenerNovedades("1.10.59.0")).Returns(new List<NovedadUsuario> { Novedad(40, "Nesto"), Novedad(102, "Nesto") });
            A.CallTo(() => servicio.ObtenerNovedadesDesdeId(100)).Returns(new List<NovedadUsuario> { Novedad(101, "NestoAPI", "1.10.59.3"), Novedad(102, "Nesto") });

            List<NovedadUsuario> mostrar = await Comprobar();

            CollectionAssert.AreEquivalent(new[] { 40, 101, 102 }, mostrar.Select(n => n.Id).ToArray());
            Assert.AreEqual(101, mostrar.Last().Id, "La de la versión más antigua va al final");
            Assert.AreEqual("102", guardados[Parametros.Claves.UltimaNovedadVista]);
        }

        [TestMethod]
        public async Task NovedadDeNestoDeUnaVersionQueAunNoHaLlegado_EsperaYLoDeDetrasTambien()
        {
            parametros[Parametros.Claves.UltimaVersionNovedades] = VERSION;
            parametros[Parametros.Claves.UltimaNovedadVista] = "100";
            A.CallTo(() => servicio.ObtenerNovedadesDesdeId(100)).Returns(new List<NovedadUsuario>
            {
                Novedad(101), Novedad(102, "Nesto", "1.10.61.0"), Novedad(103)
            });

            List<NovedadUsuario> mostrar = await Comprobar();

            CollectionAssert.AreEqual(new[] { 101 }, mostrar.Select(n => n.Id).ToArray());
            Assert.AreEqual("101", guardados[Parametros.Claves.UltimaNovedadVista]);
        }

        [TestMethod]
        public async Task SiLaLecturaPorIdFalla_NoSeGuardaNada()
        {
            parametros[Parametros.Claves.UltimaVersionNovedades] = VERSION;
            A.CallTo(() => servicio.ObtenerNovedadesDesdeId(A<int>._)).Returns(Task.FromResult<List<NovedadUsuario>>(null));

            List<NovedadUsuario> mostrar = await Comprobar();

            Assert.AreEqual(0, mostrar.Count);
            Assert.IsFalse(guardados.ContainsKey(Parametros.Claves.UltimaNovedadVista));
        }

        [TestMethod]
        public async Task LasSugerenciasSinVersion_NoSeAvisanPeroSeDanPorVistas()
        {
            parametros[Parametros.Claves.UltimaVersionNovedades] = VERSION;
            parametros[Parametros.Claves.UltimaNovedadVista] = "100";
            A.CallTo(() => servicio.ObtenerNovedadesDesdeId(100)).Returns(new List<NovedadUsuario> { Novedad(101, "Nesto", null) });

            List<NovedadUsuario> mostrar = await Comprobar();

            Assert.AreEqual(0, mostrar.Count);
            Assert.AreEqual("101", guardados[Parametros.Claves.UltimaNovedadVista]);
        }

        [TestMethod]
        public async Task SiGuardarFalla_NoLanzaYEnsenaIgual()
        {
            parametros[Parametros.Claves.UltimaVersionNovedades] = VERSION;
            parametros[Parametros.Claves.UltimaNovedadVista] = "100";
            A.CallTo(() => servicio.ObtenerNovedadesDesdeId(100)).Returns(new List<NovedadUsuario> { Novedad(101) });
            A.CallTo(() => configuracion.GuardarParametro(A<string>._, A<string>._, A<string>._)).Throws(new Exception("sin red"));

            List<NovedadUsuario> mostrar = await Comprobar();

            Assert.AreEqual(1, mostrar.Count);
        }

        [TestMethod]
        public void AunNoHaLlegado_SoloLasDeNestoDeUnaVersionPosteriorDeLaMismaNumeracion()
        {
            Assert.IsTrue(NovedadesHelper.AunNoHaLlegado(Novedad(1, "Nesto", "1.10.60.1"), VERSION));
            Assert.IsFalse(NovedadesHelper.AunNoHaLlegado(Novedad(1, "Nesto", "1.10.60.0"), VERSION));
            Assert.IsFalse(NovedadesHelper.AunNoHaLlegado(Novedad(1, "NestoAPI", "1.10.61.0"), VERSION));
            Assert.IsFalse(NovedadesHelper.AunNoHaLlegado(Novedad(1, "Nesto", "2.17.0"), VERSION), "Mal etiquetada (como la 753): no puede atascar el aviso");
            Assert.IsFalse(NovedadesHelper.AunNoHaLlegado(Novedad(1, "Nesto", "x"), VERSION));
        }

        [TestMethod]
        public void UltimaNovedadVistaDe_VacioONoNumero_EsNull()
        {
            Assert.IsNull(NovedadesHelper.UltimaNovedadVistaDe(""));
            Assert.IsNull(NovedadesHelper.UltimaNovedadVistaDe(null));
            Assert.IsNull(NovedadesHelper.UltimaNovedadVistaDe("1.10.60.0"));
            Assert.AreEqual(123, NovedadesHelper.UltimaNovedadVistaDe(" 123 "));
        }

        [TestMethod]
        public async Task ObtenerNovedadesDesdeId_PideLosAmbitosDeNestoYElId()
        {
            var handler = new HandlerFalso { Respuesta = "[{\"Id\":7,\"Version\":\"1.10.60.1\",\"Ambito\":\"NestoAPI\"}]" };
            var real = new NovedadesService(new FactoriaFalsa(handler));

            List<NovedadUsuario> lista = await real.ObtenerNovedadesDesdeId(123);

            Assert.AreEqual("/api/Novedades?ambitos=Nesto%2CNestoAPI&desdeId=123", handler.Url);
            Assert.AreEqual(7, lista.Single().Id);
        }

        [TestMethod]
        public async Task ObtenerNovedadesDesdeId_SiFalla_DevuelveNull()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.NotFound };
            var real = new NovedadesService(new FactoriaFalsa(handler));

            Assert.IsNull(await real.ObtenerNovedadesDesdeId(1));
        }

        private sealed class HandlerFalso : HttpMessageHandler
        {
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = "[]";
            public string Url;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Url = request.RequestUri.PathAndQuery;
                return Task.FromResult(new HttpResponseMessage(Codigo) { Content = new StringContent(Respuesta, Encoding.UTF8, "application/json") });
            }
        }

        private sealed class FactoriaFalsa : IClienteApiFactory
        {
            private readonly HttpMessageHandler _handler;
            public FactoriaFalsa(HttpMessageHandler handler) { _handler = handler; }
            public HttpClient Crear() => new HttpClient(_handler, false) { BaseAddress = new Uri("http://api.test/api/") };
        }
    }
}
