using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modules.Producto;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Nesto.Modulos.CanalesExternos.Models;
using Nesto.Modulos.CanalesExternos.ViewModels;
using Prism.Services.Dialogs;
using System;
using System.Threading.Tasks;

namespace CanalesExternosTests
{
    [TestClass]
    public class CanalesExternosProductosViewModelTests
    {
        private ICanalesExternosProductosService _servicio;
        private IProductoService _servicioProducto;
        private IDialogService _dialogService;

        [TestInitialize]
        public void Setup()
        {
            _servicio = A.Fake<ICanalesExternosProductosService>();
            _servicioProducto = A.Fake<IProductoService>();
            _dialogService = A.Fake<IDialogService>();
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
            A.CallTo(() => _dialogService.ShowDialog(A<string>._, A<IDialogParameters>._, A<Action<IDialogResult>>._))
                .MustNotHaveHappened();
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

            // ShowError es extension method que internamente llama a ShowDialog("NotificationDialog", ...)
            // con title="¡Error!" y message=ex.Message. Verificamos el método real, no la extensión.
            A.CallTo(() => _dialogService.ShowDialog(
                "NotificationDialog",
                A<IDialogParameters>.That.Matches(p => p.GetValue<string>("message") == "API caída"),
                A<Action<IDialogResult>>._))
                .MustHaveHappenedOnceExactly();
            Assert.IsNull(vm.ProductoSeleccionado);
        }
    }
}
