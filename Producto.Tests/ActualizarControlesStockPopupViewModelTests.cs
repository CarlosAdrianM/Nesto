using ControlesUsuario.Models;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using Prism.Services.Dialogs;
using static ControlesUsuario.Models.SelectorProveedorModel;

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

        [TestMethod]
        public void ActualizarMuestras_PorDefecto_EsFalse()
        {
            // Assert
            Assert.IsFalse(_sut.ActualizarMuestras);
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

        #region Filtrado de Muestras Tests

        [TestMethod]
        public async Task BuscarProductos_ActualizarMuestrasFalse_ExcluyeProductosConSubGrupoMuestras()
        {
            // Arrange
            var productosDelServicio = new List<ProductoControlStockModel>
            {
                new ProductoControlStockModel { ProductoId = "NORMAL1", SubGrupo = "ABC" },
                new ProductoControlStockModel { ProductoId = "MUESTRA1", SubGrupo = Constantes.Productos.Grupos.MUESTRAS },
                new ProductoControlStockModel { ProductoId = "NORMAL2", SubGrupo = "XYZ" }
            };

            A.CallTo(() => _productoService.LeerProductosProveedorControlStock(A<string>._, A<string>._))
                .Returns(Task.FromResult(productosDelServicio));

            _sut.AlmacenSeleccionado = Constantes.Almacenes.ALMACEN_CENTRAL;
            _sut.ProveedorSeleccionado = new ProveedorDTO { Proveedor = "PROV1" };
            _sut.ActualizarMuestras = false;

            // Act
            _sut.BuscarProductosCommand.Execute();
            await Task.Delay(100); // Esperar a que termine la tarea async

            // Assert
            Assert.AreEqual(2, _sut.Productos.Count);
            Assert.IsTrue(_sut.Productos.Any(p => p.ProductoId == "NORMAL1"));
            Assert.IsTrue(_sut.Productos.Any(p => p.ProductoId == "NORMAL2"));
            Assert.IsFalse(_sut.Productos.Any(p => p.ProductoId == "MUESTRA1"));
        }

        [TestMethod]
        public async Task BuscarProductos_ActualizarMuestrasTrue_IncluyeProductosConSubGrupoMuestras()
        {
            // Arrange
            var productosDelServicio = new List<ProductoControlStockModel>
            {
                new ProductoControlStockModel { ProductoId = "NORMAL1", SubGrupo = "ABC" },
                new ProductoControlStockModel { ProductoId = "MUESTRA1", SubGrupo = Constantes.Productos.Grupos.MUESTRAS },
                new ProductoControlStockModel { ProductoId = "NORMAL2", SubGrupo = "XYZ" }
            };

            A.CallTo(() => _productoService.LeerProductosProveedorControlStock(A<string>._, A<string>._))
                .Returns(Task.FromResult(productosDelServicio));

            _sut.AlmacenSeleccionado = Constantes.Almacenes.ALMACEN_CENTRAL;
            _sut.ProveedorSeleccionado = new ProveedorDTO { Proveedor = "PROV1" };
            _sut.ActualizarMuestras = true;

            // Act
            _sut.BuscarProductosCommand.Execute();
            await Task.Delay(100);

            // Assert
            Assert.AreEqual(3, _sut.Productos.Count);
            Assert.IsTrue(_sut.Productos.Any(p => p.ProductoId == "MUESTRA1"));
        }

        [TestMethod]
        public async Task BuscarProductos_ActualizarMuestrasFalse_ProductosNoMuestrasSiempreIncluidos()
        {
            // Arrange
            var productosDelServicio = new List<ProductoControlStockModel>
            {
                new ProductoControlStockModel { ProductoId = "PROD1", SubGrupo = "GRP1" },
                new ProductoControlStockModel { ProductoId = "PROD2", SubGrupo = "GRP2" },
                new ProductoControlStockModel { ProductoId = "PROD3", SubGrupo = null },
                new ProductoControlStockModel { ProductoId = "PROD4", SubGrupo = "" }
            };

            A.CallTo(() => _productoService.LeerProductosProveedorControlStock(A<string>._, A<string>._))
                .Returns(Task.FromResult(productosDelServicio));

            _sut.AlmacenSeleccionado = Constantes.Almacenes.ALMACEN_CENTRAL;
            _sut.ProveedorSeleccionado = new ProveedorDTO { Proveedor = "PROV1" };
            _sut.ActualizarMuestras = false;

            // Act
            _sut.BuscarProductosCommand.Execute();
            await Task.Delay(100);

            // Assert
            Assert.AreEqual(4, _sut.Productos.Count);
        }

        [TestMethod]
        public async Task BuscarProductos_ActualizarMuestrasFalse_SubGrupoConEspacios_TambienExcluye()
        {
            // Arrange - SubGrupo con espacios al final (legacy DB padding)
            var productosDelServicio = new List<ProductoControlStockModel>
            {
                new ProductoControlStockModel { ProductoId = "NORMAL1", SubGrupo = "ABC" },
                new ProductoControlStockModel { ProductoId = "MUESTRA_ESPACIOS", SubGrupo = Constantes.Productos.Grupos.MUESTRAS + "   " }
            };

            A.CallTo(() => _productoService.LeerProductosProveedorControlStock(A<string>._, A<string>._))
                .Returns(Task.FromResult(productosDelServicio));

            _sut.AlmacenSeleccionado = Constantes.Almacenes.ALMACEN_CENTRAL;
            _sut.ProveedorSeleccionado = new ProveedorDTO { Proveedor = "PROV1" };
            _sut.ActualizarMuestras = false;

            // Act
            _sut.BuscarProductosCommand.Execute();
            await Task.Delay(100);

            // Assert - Debe excluir "MMP   " gracias al Trim()
            Assert.AreEqual(1, _sut.Productos.Count);
            Assert.AreEqual("NORMAL1", _sut.Productos[0].ProductoId);
        }

        #endregion
    }
}
