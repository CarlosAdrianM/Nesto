using ControlesUsuario.Models;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#582 (sugerencia 908, Reina): la casilla de referencia de la ficha acepta también un código de barras.
    /// Primero la referencia exacta, como siempre; si no existe, GET api/Productos/PorCodigoBarras.
    /// </summary>
    [TestClass]
    public class ProductoViewModelBuscarCodigoBarrasTests
    {
        private const string GUANTES_M = "32565";
        private const string GUANTES_P = "32564";
        private const string EAN = "8437017506379";

        private static ProductoViewModel CrearViewModel(out IProductoService servicio, out IServicioDialogos dialogos)
        {
            var fake = A.Fake<IProductoService>();
            servicio = fake;
            A.CallTo(() => fake.LeerProducto(A<string>._)).Throws(new Exception("El producto no se ha podido cargar correctamente"));
            A.CallTo(() => fake.LeerProducto(GUANTES_M)).Returns(new ProductoModel { Producto = GUANTES_M, Nombre = "GUANTES NITRILO T/M" });
            A.CallTo(() => fake.LeerProducto(GUANTES_P)).Returns(new ProductoModel { Producto = GUANTES_P, Nombre = "GUANTES NITRILO T/P" });
            A.CallTo(() => fake.LeerVariantes(A<string>._)).Returns(new List<VarianteModel>());
            A.CallTo(() => fake.LeerCodigosBarras(A<string>._)).Returns(new List<CodigoBarrasProductoModel>());
            dialogos = A.Fake<IServicioDialogos>();
            return new ProductoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio,
                new WeakReferenceMessenger(), dialogos, A.Fake<IServicioAutenticacion>());
        }

        private static List<ProductoCodigoBarrasDuplicado> Candidatos(params string[] productos)
        {
            return productos.Select(p => new ProductoCodigoBarrasDuplicado { Producto = p, Nombre = "GUANTES " + p }).ToList();
        }

        [TestMethod]
        public void UnaReferenciaNormal_SeCargaComoSiempreSinMirarCodigosDeBarras()
        {
            var sut = CrearViewModel(out var servicio, out var dialogos);

            sut.ReferenciaBuscar = GUANTES_M;

            Assert.AreEqual(GUANTES_M, sut.ProductoActual.Producto);
            Assert.AreEqual(GUANTES_M, sut.ReferenciaBuscar);
            A.CallTo(() => servicio.BuscarProductosPorCodigoBarras(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void UnCodigoDeBarrasDeUnProducto_AbreEseProductoYLaCasillaMuestraSuReferencia()
        {
            var sut = CrearViewModel(out var servicio, out var dialogos);
            A.CallTo(() => servicio.BuscarProductosPorCodigoBarras(EAN)).Returns(Candidatos(GUANTES_M));

            sut.ReferenciaBuscar = EAN;

            Assert.AreEqual(GUANTES_M, sut.ProductoActual.Producto);
            Assert.AreEqual(GUANTES_M, sut.ReferenciaBuscar);
            Assert.AreEqual("Producto " + GUANTES_M, sut.Titulo);
            A.CallTo(() => servicio.LeerKitsContienePertenece(GUANTES_M)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void UnCodigoDeBarrasDeVariosProductos_DejaElegirYAbreElElegido()
        {
            var sut = CrearViewModel(out var servicio, out var dialogos);
            A.CallTo(() => servicio.BuscarProductosPorCodigoBarras(EAN)).Returns(Candidatos(GUANTES_M, GUANTES_P));
            A.CallTo(() => dialogos.ShowDialogAsync("SelectorProductoDuplicadoDialog", A<ParametrosDialogo>._))
                .Returns(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "producto", GUANTES_P } }));

            sut.ReferenciaBuscar = EAN;

            Assert.AreEqual(GUANTES_P, sut.ProductoActual.Producto);
            Assert.AreEqual(GUANTES_P, sut.ReferenciaBuscar);
            A.CallTo(() => dialogos.ShowDialogAsync("SelectorProductoDuplicadoDialog",
                A<ParametrosDialogo>.That.Matches(p => p.GetValue<IEnumerable<ProductoCodigoBarrasDuplicado>>("candidatos").Count() == 2)))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void SiSeCancelaElSelector_NoCambiaElProductoNiSaleError()
        {
            var sut = CrearViewModel(out var servicio, out var dialogos);
            sut.ReferenciaBuscar = GUANTES_M;
            A.CallTo(() => servicio.BuscarProductosPorCodigoBarras(EAN)).Returns(Candidatos(GUANTES_M, GUANTES_P));
            A.CallTo(() => dialogos.ShowDialogAsync("SelectorProductoDuplicadoDialog", A<ParametrosDialogo>._))
                .Returns(new ResultadoDialogo(ResultadoBoton.Cancel));

            sut.ReferenciaBuscar = EAN;

            Assert.AreEqual(GUANTES_M, sut.ProductoActual.Producto);
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void UnCodigoDeBarrasQueNoEsDeNadie_DiceQueNoHayNingunProducto()
        {
            var sut = CrearViewModel(out var servicio, out var dialogos);
            A.CallTo(() => servicio.BuscarProductosPorCodigoBarras(EAN)).Returns(new List<ProductoCodigoBarrasDuplicado>());

            sut.ReferenciaBuscar = EAN;

            A.CallTo(() => dialogos.ShowError($"No hay ningún producto con el código de barras {EAN}.")).MustHaveHappenedOnceExactly();
            Assert.IsNull(sut.ProductoActual);
        }

        [TestMethod]
        public void UnaReferenciaQueNoExisteYNoPareceCodigo_SigueDandoElErrorDeSiempre()
        {
            var sut = CrearViewModel(out var servicio, out var dialogos);
            A.CallTo(() => servicio.BuscarProductosPorCodigoBarras("ABC")).Returns(new List<ProductoCodigoBarrasDuplicado>());

            sut.ReferenciaBuscar = "ABC";

            A.CallTo(() => dialogos.ShowError("El producto no se ha podido cargar correctamente")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void SiLaApiNoPuedeMirarElCodigo_SigueDandoElErrorDeSiempre()
        {
            var sut = CrearViewModel(out var servicio, out var dialogos);
            A.CallTo(() => servicio.BuscarProductosPorCodigoBarras(EAN)).Returns(Task.FromResult<List<ProductoCodigoBarrasDuplicado>>(null));

            sut.ReferenciaBuscar = EAN;

            A.CallTo(() => dialogos.ShowError("El producto no se ha podido cargar correctamente")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void PareceCodigoBarras_SoloCifrasYAlMenosOcho()
        {
            Assert.IsTrue(ProductoViewModel.PareceCodigoBarras("8437017506379"));
            Assert.IsTrue(ProductoViewModel.PareceCodigoBarras("12345670"));
            Assert.IsFalse(ProductoViewModel.PareceCodigoBarras("32565"));
            Assert.IsFalse(ProductoViewModel.PareceCodigoBarras("843701750637A"));
            Assert.IsFalse(ProductoViewModel.PareceCodigoBarras(null));
        }
    }

    /// <summary>Nesto#582: la llamada a GET api/Productos/PorCodigoBarras desde la ficha.</summary>
    [TestClass]
    public class ProductoServiceBuscarPorCodigoBarrasTests
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

        private static ProductoService CrearServicio(HandlerFalso handler)
        {
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() =>
                new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.local/api/") });
            return new ProductoService(A.Fake<IConfiguracion>(), A.Fake<IServicioAutenticacion>(), factoria);
        }

        [TestMethod]
        public async Task LlamaAlEndpointYDejaUnaFilaPorProducto()
        {
            const string json = "[{\"Producto\":\"32565  \",\"Nombre\":\"GUANTES M \",\"Cantidad\":1,\"Principal\":true}," +
                "{\"Producto\":\"32565\",\"Nombre\":\"GUANTES M\",\"Cantidad\":100,\"Principal\":false}," +
                "{\"Producto\":\"32564\",\"Nombre\":\"GUANTES P\",\"Cantidad\":1,\"Principal\":false}]";
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

            var resultado = await CrearServicio(handler).BuscarProductosPorCodigoBarras(" 8437017506379 ");

            CollectionAssert.AreEqual(new[] { "32565", "32564" }, resultado.Select(p => p.Producto).ToArray());
            Assert.AreEqual("GUANTES M", resultado[0].Nombre);
            Assert.AreEqual("/api/Productos/PorCodigoBarras?codigo=8437017506379&empresa=1", handler.Urls.Single());
        }

        [TestMethod]
        public async Task SiLaApiNoContestaBien_DevuelveNull()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

            Assert.IsNull(await CrearServicio(handler).BuscarProductosPorCodigoBarras("8437017506379"));
        }
    }
}
