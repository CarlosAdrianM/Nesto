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
    }
}
