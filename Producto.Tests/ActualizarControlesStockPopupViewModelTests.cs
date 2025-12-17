using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using Prism.Services.Dialogs;

namespace Producto.Tests
{
    [TestClass]
    public class ActualizarControlesStockPopupViewModelTests
    {
        private IProductoService _productoService;
        private IConfiguracion _configuracion;
        private ActualizarControlesStockPopupViewModel _sut;

        [TestInitialize]
        public void Setup()
        {
            _productoService = A.Fake<IProductoService>();
            _configuracion = A.Fake<IConfiguracion>();
            _sut = new ActualizarControlesStockPopupViewModel(_productoService, _configuracion);
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_ConServicioValido_CreaInstancia()
        {
            // Act
            var vm = new ActualizarControlesStockPopupViewModel(_productoService, _configuracion);

            // Assert
            Assert.IsNotNull(vm);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_ConServicioNull_LanzaExcepcion()
        {
            // Act
            var vm = new ActualizarControlesStockPopupViewModel(null, _configuracion);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_ConConfiguracionNull_LanzaExcepcion()
        {
            // Act
            var vm = new ActualizarControlesStockPopupViewModel(_productoService, null);
        }

        [TestMethod]
        public void Constructor_InicializaProductosVacia()
        {
            // Assert
            Assert.IsNotNull(_sut.Productos);
            Assert.AreEqual(0, _sut.Productos.Count);
        }

        [TestMethod]
        public void Constructor_InicializaComandos()
        {
            // Assert
            Assert.IsNotNull(_sut.BuscarProductosCommand);
            Assert.IsNotNull(_sut.ActualizarCommand);
        }

        #endregion

        #region IDialogAware Tests

        [TestMethod]
        public void Title_DevuelveTituloCorrecto()
        {
            // Assert
            Assert.AreEqual("Actualizar Controles de Stock por Proveedor", _sut.Title);
        }

        [TestMethod]
        public void CanCloseDialog_CuandoNoEstaActualizando_DevuelveTrue()
        {
            // Assert
            Assert.IsTrue(_sut.CanCloseDialog());
        }

        #endregion

        #region Property Tests

        [TestMethod]
        public void HayProductos_CuandoListaVacia_DevuelveFalse()
        {
            // Assert
            Assert.IsFalse(_sut.HayProductos);
        }

        [TestMethod]
        public void HayProductos_CuandoListaTieneElementos_DevuelveTrue()
        {
            // Arrange
            _sut.Productos.Add(new ProductoControlStockModel { ProductoId = "TEST" });

            // Assert
            Assert.IsTrue(_sut.HayProductos);
        }

        [TestMethod]
        public void TotalProductos_DevuelveCantidadCorrecta()
        {
            // Arrange
            _sut.Productos.Add(new ProductoControlStockModel { ProductoId = "1" });
            _sut.Productos.Add(new ProductoControlStockModel { ProductoId = "2" });
            _sut.Productos.Add(new ProductoControlStockModel { ProductoId = "3" });

            // Assert
            Assert.AreEqual(3, _sut.TotalProductos);
        }

        [TestMethod]
        public void ProductosAActualizar_CuentaSoloLosQueYaExistenYTienenCambios()
        {
            // Arrange
            _sut.Productos.Add(new ProductoControlStockModel
            {
                ProductoId = "1",
                YaExiste = true,
                StockMinimoActual = 5,
                StockMinimoCalculado = 10,
                StockMaximoActual = 20,
                StockMaximoCalculado = 20
            });
            _sut.Productos.Add(new ProductoControlStockModel
            {
                ProductoId = "2",
                YaExiste = false,
                StockMinimoActual = 0,
                StockMinimoCalculado = 5,
                StockMaximoActual = 0,
                StockMaximoCalculado = 10
            });

            // Assert
            Assert.AreEqual(1, _sut.ProductosAActualizar);
        }

        [TestMethod]
        public void ProductosACrear_CuentaSoloLosQueNoExistenYRequierenActualizacion()
        {
            // Arrange
            _sut.Productos.Add(new ProductoControlStockModel
            {
                ProductoId = "1",
                YaExiste = true,
                StockMinimoActual = 5,
                StockMinimoCalculado = 10,
                StockMaximoActual = 20,
                StockMaximoCalculado = 20
            });
            _sut.Productos.Add(new ProductoControlStockModel
            {
                ProductoId = "2",
                YaExiste = false,
                StockMinimoActual = 0,
                StockMinimoCalculado = 5,
                StockMaximoActual = 0,
                StockMaximoCalculado = 10
            });
            _sut.Productos.Add(new ProductoControlStockModel
            {
                ProductoId = "3",
                YaExiste = false,
                StockMinimoActual = 0,
                StockMinimoCalculado = 0,
                StockMaximoActual = 0,
                StockMaximoCalculado = 0
            });

            // Assert
            Assert.AreEqual(1, _sut.ProductosACrear);
        }

        [TestMethod]
        public void PuedeInteractuar_CuandoNoEstaCargandoNiActualizando_DevuelveTrue()
        {
            // Assert
            Assert.IsTrue(_sut.PuedeInteractuar);
        }

        #endregion

        #region ProductoControlStockModel Tests

        [TestMethod]
        public void ProductoControlStockModel_RequiereActualizacion_CuandoStockMinimoDistintoDeCero()
        {
            // Arrange
            var model = new ProductoControlStockModel
            {
                StockMinimoCalculado = 5,
                StockMaximoCalculado = 0
            };

            // Assert
            Assert.IsTrue(model.RequiereActualizacion);
        }

        [TestMethod]
        public void ProductoControlStockModel_RequiereActualizacion_CuandoStockMaximoDistintoDeCero()
        {
            // Arrange
            var model = new ProductoControlStockModel
            {
                StockMinimoCalculado = 0,
                StockMaximoCalculado = 10
            };

            // Assert
            Assert.IsTrue(model.RequiereActualizacion);
        }

        [TestMethod]
        public void ProductoControlStockModel_NoRequiereActualizacion_CuandoAmbosStocksSonCero()
        {
            // Arrange
            var model = new ProductoControlStockModel
            {
                StockMinimoCalculado = 0,
                StockMaximoCalculado = 0
            };

            // Assert
            Assert.IsFalse(model.RequiereActualizacion);
        }

        [TestMethod]
        public void ProductoControlStockModel_TieneCambios_CuandoStockMinimoActualDifiereDelCalculado()
        {
            // Arrange
            var model = new ProductoControlStockModel
            {
                StockMinimoActual = 5,
                StockMinimoCalculado = 10,
                StockMaximoActual = 20,
                StockMaximoCalculado = 20
            };

            // Assert
            Assert.IsTrue(model.TieneCambios);
        }

        [TestMethod]
        public void ProductoControlStockModel_TieneCambios_CuandoStockMaximoActualDifiereDelCalculado()
        {
            // Arrange
            var model = new ProductoControlStockModel
            {
                StockMinimoActual = 5,
                StockMinimoCalculado = 5,
                StockMaximoActual = 10,
                StockMaximoCalculado = 20
            };

            // Assert
            Assert.IsTrue(model.TieneCambios);
        }

        [TestMethod]
        public void ProductoControlStockModel_NoTieneCambios_CuandoValoresActualesIgualesACalculados()
        {
            // Arrange
            var model = new ProductoControlStockModel
            {
                StockMinimoActual = 5,
                StockMinimoCalculado = 5,
                StockMaximoActual = 20,
                StockMaximoCalculado = 20
            };

            // Assert
            Assert.IsFalse(model.TieneCambios);
        }

        #endregion
    }
}
