using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Nesto.Modulos.CanalesExternos.Models;
using Nesto.Modulos.CanalesExternos.ViewModels;
using System;
using System.Threading.Tasks;

namespace CanalesExternosTests
{
    [TestClass]
    public class CanalesExternosProductosViewModelTests
    {
        private ICanalesExternosProductosService _servicio;
        private IProductoService _servicioProducto;
        private IServicioDialogos _dialogService;

        [TestInitialize]
        public void Setup()
        {
            _servicio = A.Fake<ICanalesExternosProductosService>();
            _servicioProducto = A.Fake<IProductoService>();
            _dialogService = A.Fake<IServicioDialogos>();
        }

        private CanalesExternosProductosViewModel CrearVm()
        {
            return new CanalesExternosProductosViewModel(_servicio, _servicioProducto, _dialogService);
        }

        // Nesto#363: el bug original. La API devolvía el producto con ProductoId trimeado
        // ("TEST-XYZ") aunque el input venía con espacios ("TEST-XYZ "). El handler usaba
        // .First(p => p.ProductoId == ProductoBuscar) sobre la colección y lanzaba
        // InvalidOperationException, que en async void mataba la app WPF entera.
        [TestMethod]
        public async Task OnAnnadirProductoAsync_ProductoIdDevueltoNoCoincideConInput_NoCrashea()
        {
            var vm = CrearVm();
            vm.ProductoBuscar = "TEST-XYZ ";
            var productoCreado = new ProductoCanalExterno { ProductoId = "TEST-XYZ" }; // trimeado por la API
            A.CallTo(() => _servicio.AddProductoAsync(A<string>._)).Returns(Task.FromResult(productoCreado));

            await vm.OnAnnadirProductoAsync();

            Assert.AreSame(productoCreado, vm.ProductoSeleccionado,
                "ProductoSeleccionado debe ser exactamente el producto que devolvió la API, sin volver a buscarlo por ProductoId.");
            Assert.AreEqual(1, vm.ProductosSinVistoBueno.Count);
            Assert.AreSame(productoCreado, vm.ProductosSinVistoBueno[0]);
            A.CallTo(() => _dialogService.ShowError(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _dialogService.ShowNotification(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _dialogService.ShowNotification(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task OnAnnadirProductoAsync_TrasAnnadir_LimpiaProductoBuscar()
        {
            var vm = CrearVm();
            vm.ProductoBuscar = "ABC";
            A.CallTo(() => _servicio.AddProductoAsync(A<string>._))
                .Returns(Task.FromResult(new ProductoCanalExterno { ProductoId = "ABC" }));

            await vm.OnAnnadirProductoAsync();

            Assert.AreEqual(string.Empty, vm.ProductoBuscar,
                "Al terminar de añadir el campo debe quedar vacío para encadenar otra referencia.");
        }

        [TestMethod]
        public async Task OnAnnadirProductoAsync_ServicioLanzaExcepcion_NoPropagaYMuestraError()
        {
            var vm = CrearVm();
            vm.ProductoBuscar = "FALLA";
            A.CallTo(() => _servicio.AddProductoAsync(A<string>._))
                .ThrowsAsync(new InvalidOperationException("API caída"));

            await vm.OnAnnadirProductoAsync(); // no debe propagar (en async void mataría la app)

            A.CallTo(() => _dialogService.ShowError("API caída")).MustHaveHappenedOnceExactly();
            Assert.IsNull(vm.ProductoSeleccionado);
        }

        // Nesto#647: con Intro, un código de barras de varios productos también pregunta cuál es.
        [TestMethod]
        public async Task BuscarProducto_CodigoDeVariosProductos_PreguntaYCargaElElegido()
        {
            const string ean = "8437017506362";
            var vm = CrearVm();
            vm.ProductoBuscar = ean;
            var elegido = new ProductoCanalExterno { ProductoId = "41001" };
            A.CallTo(() => _servicio.GetProductoAsync(ean)).Returns(Task.FromResult<ProductoCanalExterno>(null));
            A.CallTo(() => _servicio.GetProductoAsync("41001")).Returns(Task.FromResult(elegido));
            A.CallTo(() => _servicioProducto.BuscarProductosPorCodigoBarras(ean)).Returns(new System.Collections.Generic.List<ControlesUsuario.Models.ProductoCodigoBarrasDuplicado>
            {
                new() { Producto = "41000" }, new() { Producto = "41001" }
            });
            A.CallTo(() => _dialogService.ShowDialogAsync("SelectorProductoDuplicadoDialog", A<ParametrosDialogo>._))
                .Returns(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "producto", "41001" } }));

            await vm.BuscarProductoAsync();

            Assert.AreSame(elegido, vm.ProductoSeleccionado);
            Assert.AreEqual("41001", vm.ProductoBuscar);
        }

        [TestMethod]
        public async Task BuscarProducto_CodigoDeVariosProductos_SiCancelaNoCargaNada()
        {
            const string ean = "8437017506362";
            var vm = CrearVm();
            vm.ProductoBuscar = ean;
            A.CallTo(() => _servicio.GetProductoAsync(A<string>._)).Returns(Task.FromResult<ProductoCanalExterno>(null));
            A.CallTo(() => _servicioProducto.BuscarProductosPorCodigoBarras(ean)).Returns(new System.Collections.Generic.List<ControlesUsuario.Models.ProductoCodigoBarrasDuplicado>
            {
                new() { Producto = "41000" }, new() { Producto = "41001" }
            });
            A.CallTo(() => _dialogService.ShowDialogAsync("SelectorProductoDuplicadoDialog", A<ParametrosDialogo>._))
                .Returns(new ResultadoDialogo(ResultadoBoton.Cancel));

            await vm.BuscarProductoAsync();

            Assert.IsNull(vm.ProductoSeleccionado);
            A.CallTo(() => _servicio.GetProductoAsync(A<string>._)).MustHaveHappenedOnceExactly();
        }
    }
}
