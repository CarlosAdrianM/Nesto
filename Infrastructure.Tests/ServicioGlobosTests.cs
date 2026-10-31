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
    /// <summary>Nesto#592 (NestoAPI#774): qué globos se enseñan en cada pantalla (sin WPF).</summary>
    [TestClass]
    public class ServicioGlobosTests
    {
        private IClienteGlobos cliente;
        private ServicioGlobos servicio;

        [TestInitialize]
        public void Preparar()
        {
            cliente = A.Fake<IClienteGlobos>();
            servicio = new ServicioGlobos(cliente);
        }

        private static GloboNesto Globo(int id, string ancla = null) => new GloboNesto { Id = id, Pantalla = "EnviarReposicion", Ancla = ancla, Texto = "Texto " + id };

        private void Devuelve(string pantalla, params GloboNesto[] globos)
            => A.CallTo(() => cliente.LeerPendientesAsync(pantalla)).Returns(globos.ToList());

        private async Task<List<int>> Ids(string pantalla, Func<string, bool> visible = null)
            => (await servicio.QueEnsenarAsync(pantalla, visible ?? (_ => true))).Select(g => g.Globo.Id).ToList();

        [TestMethod]
        public async Task UnaSolaLlamadaPorPantallaEnLaSesion()
        {
            Devuelve("EnviarReposicion", Globo(1));

            List<int> primera = await Ids("EnviarReposicion");
            servicio.Soltar(Globo(1));
            List<int> segunda = await Ids("EnviarReposicion");

            CollectionAssert.AreEqual(new[] { 1 }, primera);
            CollectionAssert.AreEqual(new[] { 1 }, segunda);
            A.CallTo(() => cliente.LeerPendientesAsync("EnviarReposicion")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task CadaPantallaTieneSuLlamada()
        {
            Devuelve("A", Globo(1));
            Devuelve("B", Globo(2));

            CollectionAssert.AreEqual(new[] { 1 }, await Ids("A"));
            servicio.Aplazar(Globo(1));
            CollectionAssert.AreEqual(new[] { 2 }, await Ids("B"));
        }

        [TestMethod]
        public async Task ComoMuchoDosSeguidos_YLosDemasLaProximaVez()
        {
            Devuelve("EnviarReposicion", Globo(1), Globo(2), Globo(3));

            List<int> primera = await Ids("EnviarReposicion");
            await servicio.EntendidoAsync(Globo(1));
            await servicio.EntendidoAsync(Globo(2));
            List<int> segunda = await Ids("EnviarReposicion");

            CollectionAssert.AreEqual(new[] { 1, 2 }, primera);
            CollectionAssert.AreEqual(new[] { 3 }, segunda);
            Assert.AreEqual(2, servicio.MaximoSeguidos);
        }

        [TestMethod]
        public async Task AnclaAusenteNoVisibleOSinAncla_Centrado()
        {
            Devuelve("P", Globo(1, "P.Visible"), Globo(2, "P.Oculta"));
            var otro = new ServicioGlobos(cliente, maximoSeguidos: 5);
            A.CallTo(() => cliente.LeerPendientesAsync("Q")).Returns(new List<GloboNesto> { Globo(3, null), Globo(4, "Q.QueRevienta") });

            IReadOnlyList<GloboAEnsenar> p = await servicio.QueEnsenarAsync("P", a => a == "P.Visible");
            IReadOnlyList<GloboAEnsenar> q = await otro.QueEnsenarAsync("Q", a => throw new InvalidOperationException());

            Assert.IsFalse(p[0].Centrado);
            Assert.IsTrue(p[1].Centrado);
            Assert.IsTrue(q.All(g => g.Centrado));
        }

        [TestMethod]
        public async Task Entendido_MarcaVistoEnElServidor_YNoVuelve()
        {
            Devuelve("P", Globo(1));

            _ = await Ids("P");
            await servicio.EntendidoAsync(Globo(1));

            A.CallTo(() => cliente.MarcarVistoAsync(1)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, (await Ids("P")).Count);
        }

        [TestMethod]
        public async Task Entendido_SiFallaElServidor_NoRompeYNoVuelveEnLaSesion()
        {
            Devuelve("P", Globo(1));
            A.CallTo(() => cliente.MarcarVistoAsync(1)).Throws(new HttpRequestException("sin red"));

            _ = await Ids("P");
            await servicio.EntendidoAsync(Globo(1));

            Assert.AreEqual(0, (await Ids("P")).Count);
        }

        [TestMethod]
        public async Task Esc_NoMarcaVisto_YNoVuelveEnLaSesion()
        {
            Devuelve("P", Globo(1));

            _ = await Ids("P");
            servicio.Aplazar(Globo(1));

            A.CallTo(() => cliente.MarcarVistoAsync(A<int>._)).MustNotHaveHappened();
            Assert.AreEqual(0, (await Ids("P")).Count);
        }

        [TestMethod]
        public async Task UnaTandaALaVez_HastaQueSeContestaOSeSuelta()
        {
            Devuelve("A", Globo(1));
            Devuelve("B", Globo(2));

            _ = await Ids("A");
            List<int> mientras = await Ids("B");
            servicio.Soltar(Globo(1));
            List<int> despues = await Ids("B");

            Assert.AreEqual(0, mientras.Count);
            CollectionAssert.AreEqual(new[] { 2 }, despues);
        }

        [TestMethod]
        public async Task SiFallaLaLectura_Ninguno_YSeVuelveAPedirLaProximaVez()
        {
            A.CallTo(() => cliente.LeerPendientesAsync("P")).Throws(new HttpRequestException("sin red")).Once()
                .Then.Returns(new List<GloboNesto> { Globo(1) });

            List<int> primera = await Ids("P");
            List<int> segunda = await Ids("P");

            Assert.AreEqual(0, primera.Count);
            CollectionAssert.AreEqual(new[] { 1 }, segunda);
        }

        [TestMethod]
        public async Task SinPantallaOSinTexto_Ninguno()
        {
            A.CallTo(() => cliente.LeerPendientesAsync("P")).Returns(new List<GloboNesto> { new GloboNesto { Id = 1, Texto = " " }, null });

            Assert.AreEqual(0, (await Ids(null)).Count);
            Assert.AreEqual(0, (await Ids("P")).Count);
            A.CallTo(() => cliente.LeerPendientesAsync(null)).MustNotHaveHappened();
        }

        // ---- El cliente HTTP ----

        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<(HttpMethod Metodo, string Url)> Peticiones = new List<(HttpMethod, string)>();
            public string Respuesta = "[]";

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Peticiones.Add((request.Method, request.RequestUri.PathAndQuery));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Respuesta, Encoding.UTF8, "application/json") });
            }
        }

        private sealed class FactoriaFalsa : IClienteApiFactory
        {
            private readonly HttpMessageHandler _handler;
            public FactoriaFalsa(HttpMessageHandler handler) { _handler = handler; }
            public HttpClient Crear() => new HttpClient(_handler, false) { BaseAddress = new Uri("http://api.test/api/") };
        }

        [TestMethod]
        public async Task Cliente_PideConAplicacionPantallaYVersion_YMarcaVisto()
        {
            var handler = new HandlerFalso { Respuesta = "[{\"Id\":7,\"Pantalla\":\"EnviarReposicion\",\"Ancla\":\"EnviarReposicion.EstadoRecogida\",\"Texto\":\"Hola\"}]" };
            var http = new ClienteGlobos(new FactoriaFalsa(handler), () => "1.10.54.0");

            List<GloboNesto> globos = await http.LeerPendientesAsync("EnviarReposicion");
            await http.MarcarVistoAsync(7);

            Assert.AreEqual("/api/Globos?aplicacion=Nesto&pantalla=EnviarReposicion&version=1.10.54.0", handler.Peticiones[0].Url);
            Assert.AreEqual("EnviarReposicion.EstadoRecogida", globos.Single().Ancla);
            Assert.AreEqual(HttpMethod.Post, handler.Peticiones[1].Metodo);
            Assert.AreEqual("/api/Globos/7/Visto", handler.Peticiones[1].Url);
        }
    }
}
