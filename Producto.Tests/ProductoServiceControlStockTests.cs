using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#512: POST api/ControlesStock devuelve 409 (sin cuerpo) si el control de ese producto y almacén ya
    /// existe; la ficha lo enseñaba como «No se ha podido modificar el control de stock» sin motivo.
    /// </summary>
    [TestClass]
    public class ProductoServiceControlStockTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public List<HttpMethod> Metodos { get; } = new();

            public HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Metodos.Add(request.Method);
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

        private static ControlStock Control() => new ControlStock { Empresa = "1", Almacén = "ALG", Número = "12345", StockMáximo = 6 };

        [TestMethod]
        public async Task CrearControlStock_SiYaExiste409_LoModificaConElPut()
        {
            var handler = new HandlerFalso(r => new HttpResponseMessage(
                r.Method == HttpMethod.Post ? HttpStatusCode.Conflict : HttpStatusCode.NoContent));
            var sut = CrearServicio(handler);

            await sut.CrearControlStock(Control());

            CollectionAssert.AreEqual(new[] { HttpMethod.Post, HttpMethod.Put }, handler.Metodos);
        }

        [TestMethod]
        public async Task CrearControlStock_OtroError_EnsenaElMessageDeLaApi()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"Message\":\"El almacén no admite control de stock\"}", Encoding.UTF8, "application/json")
            });
            var sut = CrearServicio(handler);

            var ex = await Assert.ThrowsExceptionAsync<Exception>(() => sut.CrearControlStock(Control()));

            StringAssert.Contains(ex.Message, "El almacén no admite control de stock");
            CollectionAssert.AreEqual(new[] { HttpMethod.Post }, handler.Metodos);
        }

        [TestMethod]
        public async Task GuardarControlStock_Error500_EnsenaElExceptionMessage()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"Message\":\"An error has occurred.\",\"ExceptionMessage\":\"Timeout de la BD\"}", Encoding.UTF8, "application/json")
            });
            var sut = CrearServicio(handler);

            var ex = await Assert.ThrowsExceptionAsync<Exception>(() => sut.GuardarControlStock(Control()));

            StringAssert.Contains(ex.Message, "Timeout de la BD");
        }

        [TestMethod]
        public void ErrorControlStock_SinCuerpo_DiceAlMenosElCodigo()
        {
            string texto = ProductoService.ErrorControlStock("", HttpStatusCode.BadGateway);

            StringAssert.Contains(texto, "No se ha podido modificar el control de stock");
            StringAssert.Contains(texto, "502");
        }

        [TestMethod]
        public void ErrorControlStock_ModelState_AnadeElPrimerError()
        {
            string cuerpo = "{\"Message\":\"The request is invalid.\",\"ModelState\":{\"controlStock.StockMáximo\":[\"El máximo no puede ser negativo\"]}}";

            StringAssert.Contains(ProductoService.ErrorControlStock(cuerpo, HttpStatusCode.BadRequest), "El máximo no puede ser negativo");
        }

        [TestMethod]
        public void GuardarProducto_SiElServicioFalla_ElMotivoLlegaAlUsuario()
        {
            var servicio = A.Fake<IProductoService>();
            var dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => servicio.CrearControlStock(A<ControlStock>._))
                .ThrowsAsync(new Exception("No se ha podido modificar el control de stock\nEl almacén no admite control de stock"));
            var sut = new ProductoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio,
                new WeakReferenceMessenger(), dialogos, A.Fake<IServicioAutenticacion>());
            var modelo = new ControlStockProductoModel { ProductoId = "12345" };
            modelo.ControlesStocksAlmacen.Add(new ControlStockAlmacenModel { Almacen = "ALG", StockMaximoInicial = 0, StockMaximoActual = 0, Multiplos = 1 });
            sut.ControlStock = new ControlStockProductoWrapper(modelo);
            sut.ControlStock.ControlesStocksAlmacen.Single().Model.StockMaximoActual = 6;

            sut.GuardarProductoCommand.Execute(null);

            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("El almacén no admite control de stock"))).MustHaveHappenedOnceExactly();
        }
    }
}
