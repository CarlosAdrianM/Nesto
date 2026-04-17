using FakeItEasy;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using Prism.Regions;
using Prism.Services.Dialogs;

namespace Producto.Tests
{
    [TestClass]
    public class CorreccionVideoProductoViewModelTests
    {
        private IProductoService _productoService = null!;
        private IRegionManager _regionManager = null!;
        private CorreccionVideoProductoViewModel _sut = null!;

        [TestInitialize]
        public void Setup()
        {
            _productoService = A.Fake<IProductoService>();
            _regionManager = A.Fake<IRegionManager>();
            _sut = new CorreccionVideoProductoViewModel(_productoService, _regionManager);
        }

        private void AnadirEditable(string? referencia, string? nombreVideo = "Alta Frecuencia")
        {
            var productoVideo = new ProductoVideoModel
            {
                Id = 1,
                Referencia = referencia,
                NombreProducto = nombreVideo
            };
            _sut.ProductosEditables.Add(new ProductoEditable(productoVideo));
        }

        [TestMethod]
        public async Task CargarNombresProductoAsociadoAsync_ConReferenciaValida_RellenaNombreAsociado()
        {
            // Issue #347: el usuario necesita ver el nombre del producto al que apunta
            // la Referencia para detectar si la referencia es errónea (p. ej. vídeo de
            // "Alta Frecuencia" con referencia de una crema anticelulítica).
            AnadirEditable("AB01");
            A.CallTo(() => _productoService.LeerProducto("AB01"))
                .Returns(Task.FromResult(new ProductoModel { Producto = "AB01", Nombre = "Alta Frecuencia Sapphire" }));

            await _sut.CargarNombresProductoAsociadoAsync();

            Assert.AreEqual("Alta Frecuencia Sapphire", _sut.ProductosEditables[0].NombreProductoAsociado);
        }

        [TestMethod]
        public async Task CargarNombresProductoAsociadoAsync_NoSobreescribeNombreProductoDelVideo()
        {
            // El nombre del VideoProducto (lo que el vídeo describe) debe mantenerse intacto
            // aunque la Referencia resuelva a un producto con otro nombre.
            AnadirEditable("AB01", nombreVideo: "Alta Frecuencia");
            A.CallTo(() => _productoService.LeerProducto("AB01"))
                .Returns(Task.FromResult(new ProductoModel { Producto = "AB01", Nombre = "Crema Anticelulítica 500ml" }));

            await _sut.CargarNombresProductoAsociadoAsync();

            Assert.AreEqual("Alta Frecuencia", _sut.ProductosEditables[0].NombreProducto);
            Assert.AreEqual("Crema Anticelulítica 500ml", _sut.ProductosEditables[0].NombreProductoAsociado);
        }

        [TestMethod]
        public async Task CargarNombresProductoAsociadoAsync_ConReferenciaVacia_NoLlamaAlServicio()
        {
            AnadirEditable("");

            await _sut.CargarNombresProductoAsociadoAsync();

            A.CallTo(() => _productoService.LeerProducto(A<string>._)).MustNotHaveHappened();
            Assert.IsNull(_sut.ProductosEditables[0].NombreProductoAsociado);
        }

        [TestMethod]
        public async Task CargarNombresProductoAsociadoAsync_ConReferenciaNull_NoLlamaAlServicio()
        {
            AnadirEditable(null);

            await _sut.CargarNombresProductoAsociadoAsync();

            A.CallTo(() => _productoService.LeerProducto(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task CargarNombresProductoAsociadoAsync_ServicioDevuelveNull_NombreAsociadoQuedaNull()
        {
            // Si la referencia no existe, LeerProducto puede devolver null.
            // No queremos que eso haga crash al dialogo ni que muestre texto incorrecto.
            AnadirEditable("NOEXISTE");
            A.CallTo(() => _productoService.LeerProducto("NOEXISTE"))
                .Returns(Task.FromResult<ProductoModel>(null!));

            await _sut.CargarNombresProductoAsociadoAsync();

            Assert.IsNull(_sut.ProductosEditables[0].NombreProductoAsociado);
        }

        [TestMethod]
        public async Task CargarNombresProductoAsociadoAsync_ServicioLanzaExcepcion_NoTumbaElDialogoYSiguePopulandoOtros()
        {
            // Si la API falla para un producto concreto, no debe romper la carga
            // del resto de editables. Detectar errores por timing/red no debe bloquear
            // al usuario que está revisando 10 productos.
            AnadirEditable("FALLA");
            AnadirEditable("OK");

            A.CallTo(() => _productoService.LeerProducto("FALLA"))
                .ThrowsAsync(new InvalidOperationException("API down"));
            A.CallTo(() => _productoService.LeerProducto("OK"))
                .Returns(Task.FromResult(new ProductoModel { Producto = "OK", Nombre = "Producto OK" }));

            await _sut.CargarNombresProductoAsociadoAsync();

            Assert.IsNull(_sut.ProductosEditables[0].NombreProductoAsociado);
            Assert.AreEqual("Producto OK", _sut.ProductosEditables[1].NombreProductoAsociado);
        }

        [TestMethod]
        public void AbrirProductosConBusqueda_ConNombreValido_CanExecuteEsTrue()
        {
            Assert.IsTrue(_sut.AbrirProductosConBusquedaCommand.CanExecute("Alta Frecuencia"));
        }

        [TestMethod]
        public void AbrirProductosConBusqueda_ConNombreNull_CanExecuteEsFalse()
        {
            Assert.IsFalse(_sut.AbrirProductosConBusquedaCommand.CanExecute(null));
        }

        [TestMethod]
        public void AbrirProductosConBusqueda_ConNombreSoloEspacios_CanExecuteEsFalse()
        {
            Assert.IsFalse(_sut.AbrirProductosConBusquedaCommand.CanExecute("   "));
        }

        [TestMethod]
        public void AbrirProductosConBusqueda_ConNombreValido_CierraElDialogo()
        {
            // Issue #343: al lanzar la búsqueda contextual cerramos el diálogo modal
            // para que la navegación a ProductoView no quede bajo un popup.
            IDialogResult resultadoCierre = null;
            ((IDialogAware)_sut).RequestClose += r => resultadoCierre = r;

            _sut.AbrirProductosConBusquedaCommand.Execute("Alta Frecuencia");

            Assert.IsNotNull(resultadoCierre, "El diálogo debería haberse cerrado");
            Assert.AreEqual(ButtonResult.Cancel, resultadoCierre.Result);
        }

        [TestMethod]
        public void AbrirProductosConBusqueda_ConNombreVacio_NoCierraElDialogo()
        {
            IDialogResult resultadoCierre = null;
            ((IDialogAware)_sut).RequestClose += r => resultadoCierre = r;

            _sut.AbrirProductosConBusquedaCommand.Execute("");

            Assert.IsNull(resultadoCierre);
        }
    }
}
