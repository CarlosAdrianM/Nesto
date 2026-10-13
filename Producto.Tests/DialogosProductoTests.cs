using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using Prism.Services.Dialogs;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 2): «Actualizar controles de stock por proveedor» y «Correcciones de
    /// productos del vídeo» ya son IDialogoNesto (DialogoNestoBase) y tienen que abrirse igual con las dos
    /// ventanas: la de Prism (usuarios sin el piloto VentanaDialogosPropia), que los ve como IDialogAware, y la
    /// propia. El botón Cancelar de sus vistas cierra con CerrarDialogo(Cancel).
    /// </summary>
    [TestClass]
    public class DialogosProductoTests
    {
        private static ActualizarControlesStockPopupViewModel CrearControlesStock(string almacen = "REI")
        {
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta))
                .Returns(Task.FromResult(almacen));
            return new ActualizarControlesStockPopupViewModel(A.Fake<IProductoService>(), configuracion);
        }

        [TestMethod]
        public void ControlesStock_ConLaVentanaDePrism_LeeElAlmacenYCancelarCierraConCancel()
        {
            var vm = CrearControlesStock();
            IDialogAware prism = vm;
            IDialogResult? resultado = null;
            prism.RequestClose += r => resultado = r;

            prism.OnDialogOpened(new DialogParameters());
            vm.CerrarDialogo(ResultadoBoton.Cancel);

            Assert.AreEqual("Actualizar Controles de Stock por Proveedor", prism.Title);
            Assert.AreEqual("REI", vm.AlmacenSeleccionado);
            Assert.AreEqual(ButtonResult.Cancel, resultado!.Result);
        }

        [TestMethod]
        public void ControlesStock_ConLaVentanaPropia_LeeElAlmacenYCancelarCierraConCancel()
        {
            var vm = CrearControlesStock();
            ResultadoDialogo? resultado = null;
            ((IDialogoNesto)vm).RequestClose += r => resultado = r;

            vm.OnDialogOpened(new ParametrosDialogo());
            vm.CerrarDialogo(ResultadoBoton.Cancel);

            Assert.AreEqual("Actualizar Controles de Stock por Proveedor", vm.Title);
            Assert.AreEqual("REI", vm.AlmacenSeleccionado);
            Assert.AreEqual(ResultadoBoton.Cancel, resultado!.Result);
        }

        [TestMethod]
        public void ControlesStock_MientrasActualiza_NoSePuedeCerrarConNingunaVentana()
        {
            var vm = CrearControlesStock();

            vm.EstaActualizando = true;

            Assert.IsFalse(((IDialogAware)vm).CanCloseDialog());
            Assert.IsFalse(((IDialogoNesto)vm).CanCloseDialog());
        }

        private static VideoModel VideoConUnProducto() => new()
        {
            UrlVideo = "https://youtu.be/x",
            Productos = new List<ProductoVideoModel> { new() { Id = 7, Referencia = "AB01", NombreProducto = "Alta Frecuencia" } }
        };

        [TestMethod]
        public void CorreccionVideo_ConLaVentanaDePrism_RecibeElVideoYCancelarCierraConCancel()
        {
            var vm = new CorreccionVideoProductoViewModel(A.Fake<IProductoService>(), A.Fake<IServicioNavegacion>());
            IDialogAware prism = vm;
            IDialogResult? resultado = null;
            prism.RequestClose += r => resultado = r;
            var video = VideoConUnProducto();

            prism.OnDialogOpened(new DialogParameters { { "producto", video } });
            vm.CerrarDialogo(ResultadoBoton.Cancel);

            Assert.AreEqual("Correcciones de productos del vídeo", prism.Title);
            Assert.AreSame(video, vm.Video);
            Assert.AreEqual(1, vm.ProductosEditables.Count);
            Assert.AreEqual(ButtonResult.Cancel, resultado!.Result);
        }

        [TestMethod]
        public async Task CorreccionVideo_ConLaVentanaDePrism_GuardarDevuelveCuantosSeHanModificado()
        {
            var vm = new CorreccionVideoProductoViewModel(A.Fake<IProductoService>(), A.Fake<IServicioNavegacion>());
            IDialogResult? resultado = null;
            ((IDialogAware)vm).RequestClose += r => resultado = r;
            ((IDialogAware)vm).OnDialogOpened(new DialogParameters { { "producto", VideoConUnProducto() } });
            vm.ProductosEditables[0].NombreProducto = "Alta Frecuencia Sapphire";

            vm.GuardarCommand.Execute(null);
            await Task.Delay(50);

            Assert.AreEqual(ButtonResult.OK, resultado!.Result);
            Assert.AreEqual(1, resultado.Parameters.GetValue<int>("productosModificados"));
        }

        [TestMethod]
        public async Task CorreccionVideo_ConLaVentanaPropia_GuardarDevuelveCuantosSeHanModificado()
        {
            var vm = new CorreccionVideoProductoViewModel(A.Fake<IProductoService>(), A.Fake<IServicioNavegacion>());
            ResultadoDialogo? resultado = null;
            ((IDialogoNesto)vm).RequestClose += r => resultado = r;
            vm.OnDialogOpened(new ParametrosDialogo { { "producto", VideoConUnProducto() } });
            vm.ProductosEditables[0].NombreProducto = "Alta Frecuencia Sapphire";

            vm.GuardarCommand.Execute(null);
            await Task.Delay(50);

            Assert.AreEqual("Correcciones de productos del vídeo", vm.Title);
            Assert.AreEqual(ResultadoBoton.OK, resultado!.Result);
            Assert.AreEqual(1, resultado.Parameters.GetValue<int>("productosModificados"));
        }

        [TestMethod]
        public void CorreccionVideo_ConLaVentanaPropia_GuardarSinCambiosCierraConCancel()
        {
            var vm = new CorreccionVideoProductoViewModel(A.Fake<IProductoService>(), A.Fake<IServicioNavegacion>());
            ResultadoDialogo? resultado = null;
            ((IDialogoNesto)vm).RequestClose += r => resultado = r;
            vm.OnDialogOpened(new ParametrosDialogo { { "producto", VideoConUnProducto() } });

            // Guardar solo se habilita con cambios, pero el cierre sin cambios sigue siendo Cancel.
            vm.ProductosEditables[0].NombreProducto = "otro";
            vm.ProductosEditables[0].NombreProducto = "Alta Frecuencia";
            Assert.IsFalse(vm.GuardarCommand.CanExecute(null));
            vm.CerrarDialogo(ResultadoBoton.Cancel);

            Assert.AreEqual(ResultadoBoton.Cancel, resultado!.Result);
        }
    }
}
