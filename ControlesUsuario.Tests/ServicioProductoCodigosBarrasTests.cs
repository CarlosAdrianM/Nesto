using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// NestoAPI#605: la búsqueda de producto (plantilla, líneas de pedido...) casa por cualquiera de los
    /// códigos de barras del producto, no solo por el principal de la ficha.
    /// </summary>
    [TestClass]
    public class ServicioProductoCodigosBarrasTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public List<string> Urls { get; } = new();

            public HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.RequestUri.PathAndQuery);
                return Task.FromResult(_responder(request));
            }
        }

        private static ServicioProducto CrearServicio(HandlerFalso handler)
        {
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() =>
                new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.local/api/") });
            return new ServicioProducto(A.Fake<IConfiguracion>(), null, factoria);
        }

        private static HttpResponseMessage Json(string json, HttpStatusCode codigo = HttpStatusCode.OK)
        {
            return new HttpResponseMessage(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }

        private static string Producto(string numero, string nombre) =>
            $"{{\"producto\":\"{numero}\",\"nombre\":\"{nombre}\",\"precio\":1.5,\"aplicarDescuento\":true,\"iva\":\"G21\"}}";

        [TestMethod]
        public async Task NumeroDeProducto_NoPreguntaPorCodigoDeBarras()
        {
            var handler = new HandlerFalso(_ => Json(Producto("32565", "GUANTES NITRILO T/M")));
            var sut = CrearServicio(handler);

            var producto = await sut.BuscarProducto("1", "32565", null, null, 1);

            Assert.AreEqual("32565", producto.Producto);
            Assert.IsFalse(handler.Urls.Any(u => u.Contains("PorCodigoBarras")));
        }

        [TestMethod]
        public async Task CodigoAlternativo_LoEncuentraPorLaLista()
        {
            var handler = new HandlerFalso(r =>
            {
                string url = r.RequestUri.PathAndQuery;
                if (url.Contains("PorCodigoBarras"))
                {
                    return Json("[{\"Producto\":\"32565\",\"Nombre\":\"GUANTES NITRILO T/M\",\"Cantidad\":1,\"Principal\":false}]");
                }
                return url.Contains("id=32565")
                    ? Json(Producto("32565", "GUANTES NITRILO T/M"))
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            });
            var sut = CrearServicio(handler);

            var producto = await sut.BuscarProducto("1", "8437017506386", null, null, 1);

            Assert.AreEqual("32565", producto.Producto);
        }

        [TestMethod]
        public async Task CodigoCompartido_LanzaElSelectorConTodosLosProductos()
        {
            // El código es el principal de la talla P y alternativo de la M: la búsqueda de siempre
            // devolvía la P sin preguntar.
            var handler = new HandlerFalso(r => r.RequestUri.PathAndQuery.Contains("PorCodigoBarras")
                ? Json("[{\"Producto\":\"32564\",\"Nombre\":\"GUANTES NITRILO T/P\",\"Principal\":true},{\"Producto\":\"32565\",\"Nombre\":\"GUANTES NITRILO T/M\",\"Principal\":false}]")
                : Json(Producto("32564", "GUANTES NITRILO T/P")));
            var sut = CrearServicio(handler);

            var ex = await Assert.ThrowsExceptionAsync<CodigoBarrasDuplicadoException>(() => sut.BuscarProducto("1", "8437017506362", null, null, 1));

            CollectionAssert.AreEqual(new[] { "32564", "32565" }, ex.Candidatos.Select(c => c.Producto).ToArray());
        }

        [TestMethod]
        public async Task SinEndpointEnLaApi_SeQuedaLaBusquedaDeSiempre()
        {
            var handler = new HandlerFalso(r => r.RequestUri.PathAndQuery.Contains("PorCodigoBarras")
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : Json(Producto("32564", "GUANTES NITRILO T/P")));
            var sut = CrearServicio(handler);

            var producto = await sut.BuscarProducto("1", "8437017506362", null, null, 1);

            Assert.AreEqual("32564", producto.Producto);
        }

        [TestMethod]
        public async Task SinEndpointEnLaApi_ElDuplicadoDeSiempreSigueSaliendo()
        {
            var handler = new HandlerFalso(r => r.RequestUri.PathAndQuery.Contains("PorCodigoBarras")
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : Json("[{\"Producto\":\"1\",\"Nombre\":\"A\"},{\"Producto\":\"2\",\"Nombre\":\"B\"}]", HttpStatusCode.Conflict));
            var sut = CrearServicio(handler);

            var ex = await Assert.ThrowsExceptionAsync<CodigoBarrasDuplicadoException>(() => sut.BuscarProducto("1", "8411", null, null, 1));

            Assert.AreEqual(2, ex.Candidatos.Count);
        }
    }
}
