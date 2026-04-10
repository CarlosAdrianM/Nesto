using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using Prism.Events;
using Prism.Regions;
using Prism.Services.Dialogs;
using System.Windows.Controls;

namespace Producto.Tests
{
    [TestClass]
    public class ProductoViewModelTests
    {
        [TestMethod]
        public void ProductoViewModel_AlCambiarDeProducto_SiLaPestannaSeleccionadaEsKitsPeroElNuevoProductoNoEsKitSeleccionaOtraPestanna()
        {
            // Arrange
            var regionManager = A.Fake<IRegionManager>();
            var configuracion = A.Fake<IConfiguracion>();
            var servicio = A.Fake<IProductoService>();
            var eventAggregator = A.Fake<IEventAggregator>();
            var dialogService = A.Fake<IDialogService>();
            A.CallTo(() => servicio.LeerProducto("KIT")).Returns(new ProductoModel
            {
                Producto = "KIT",
                ProductosKit = new List<ProductoKit>()
                {
                    new ProductoKit
                    {
                        ProductoId = "OTRO_PROD",
                        Cantidad = 1
                    }
                }
            });
            A.CallTo(() => servicio.LeerProducto("NO_KIT")).Returns(new ProductoModel
            {
                Producto = "NO_KIT"
            });
            var sut = new ProductoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService);
            sut.ReferenciaBuscar = "KIT";
            sut.PestannaSeleccionada = Pestannas.Kits;

            // Act
            sut.ReferenciaBuscar = "NO_KIT";

            // Assert
            Assert.AreEqual(Pestannas.Filtros, sut.PestannaSeleccionada);
        }

        // Issue #341: la búsqueda contextual debe respetar los filtros de Familia
        // y Subgrupo activos en el panel de filtros (Contains case-insensitive).

        [TestMethod]
        public void OnBuscarContextual_ConFiltroFamilia_FiltraResultadosPorFamilia()
        {
            // Arrange
            var sut = CrearViewModelConContextualesMockeados(out _, out var servicio);
            ICollection<ProductoModel> resultadoServicio = new List<ProductoModel>
            {
                CrearProductoConStock("P1", "Eva Visnú", "Cremas"),
                CrearProductoConStock("P2", "Lisap", "Cremas"),
                CrearProductoConStock("P3", "Eva Visnú", "Otros")
            };
            A.CallTo(() => servicio.BuscarProductosContextual("crema", false))
                .Returns(Task.FromResult(resultadoServicio));

            sut.FiltroFamilia = "Eva Visnú";

            // Act
            sut.BuscarContextualCommand.Execute("crema");

            // Assert
            var lista = sut.ProductosResultadoBusqueda.Lista.Cast<ProductoModel>().ToList();
            Assert.AreEqual(2, lista.Count);
            CollectionAssert.AreEquivalent(new[] { "P1", "P3" }, lista.Select(p => p.Producto).ToList());
        }

        [TestMethod]
        public void OnBuscarContextual_ConFiltroSubgrupo_FiltraResultadosPorSubgrupo()
        {
            // Arrange
            var sut = CrearViewModelConContextualesMockeados(out _, out var servicio);
            ICollection<ProductoModel> resultadoServicio = new List<ProductoModel>
            {
                CrearProductoConStock("P1", "Eva Visnú", "Cremas"),
                CrearProductoConStock("P2", "Lisap", "Cremas"),
                CrearProductoConStock("P3", "Eva Visnú", "Otros")
            };
            A.CallTo(() => servicio.BuscarProductosContextual("crema", false))
                .Returns(Task.FromResult(resultadoServicio));

            sut.FiltroSubgrupo = "cremas"; // case insensitive

            // Act
            sut.BuscarContextualCommand.Execute("crema");

            // Assert
            var lista = sut.ProductosResultadoBusqueda.Lista.Cast<ProductoModel>().ToList();
            Assert.AreEqual(2, lista.Count);
            CollectionAssert.AreEquivalent(new[] { "P1", "P2" }, lista.Select(p => p.Producto).ToList());
        }

        [TestMethod]
        public void OnBuscarContextual_SinFiltros_DevuelveTodosLosResultados()
        {
            // Arrange
            var sut = CrearViewModelConContextualesMockeados(out _, out var servicio);
            ICollection<ProductoModel> resultadoServicio = new List<ProductoModel>
            {
                CrearProductoConStock("P1", "Eva Visnú", "Cremas"),
                CrearProductoConStock("P2", "Lisap", "Cremas")
            };
            A.CallTo(() => servicio.BuscarProductosContextual("crema", false))
                .Returns(Task.FromResult(resultadoServicio));

            // Act
            sut.BuscarContextualCommand.Execute("crema");

            // Assert
            Assert.AreEqual(2, sut.ProductosResultadoBusqueda.Lista.Count);
        }

        // ----- Helpers -----

        private static ProductoViewModel CrearViewModelConContextualesMockeados(
            out IConfiguracion configuracion,
            out IProductoService servicio)
        {
            var regionManager = A.Fake<IRegionManager>();
            configuracion = A.Fake<IConfiguracion>();
            servicio = A.Fake<IProductoService>();
            var eventAggregator = A.Fake<IEventAggregator>();
            var dialogService = A.Fake<IDialogService>();
            return new ProductoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService);
        }

        private static ProductoModel CrearProductoConStock(string id, string familia, string subgrupo)
        {
            return new ProductoModel
            {
                Producto = id,
                Nombre = $"Producto {id}",
                Familia = familia,
                Subgrupo = subgrupo,
                Stocks = new List<ProductoModel.StockProducto>
                {
                    new ProductoModel.StockProducto { Stock = 10, CantidadDisponible = 10 }
                }
            };
        }
    }
}
