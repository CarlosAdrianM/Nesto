using Nesto.Modules.Producto.Models;

namespace Producto.Tests
{
    [TestClass]
    public class ControlStockProductoWrapperTests
    {
        [TestMethod]
        public void ControlStockProductoWrapper_AlSubirElStockMaximoDeUnAlmacenQueNoEsLaCentral_BajaElStockDeLaCentral()
        {
            // Arrange
            ControlStockProductoModel model = new ControlStockProductoModel();
            model.ControlesStocksAlmacen.Add(new ControlStockAlmacenModel
            {
                Almacen = "ALG",
                StockMaximoActual = 10,
                StockMaximoCalculado = 10,
                StockMaximoInicial = 10
            });
            model.ControlesStocksAlmacen.Add(new ControlStockAlmacenModel
            {
                Almacen = "REI",
                StockMaximoActual = 1,
                StockMaximoCalculado = 1,
                StockMaximoInicial = 1
            });
            ControlStockProductoWrapper wrapper = new ControlStockProductoWrapper(model);
            ControlStockAlmacenWrapper stockTienda = wrapper.ControlesStocksAlmacen.Single(c => c.Model.Almacen == "REI");
            ControlStockAlmacenWrapper stockCentral = wrapper.ControlesStocksAlmacen.Single(c => c.Model.Almacen == "ALG");

            // Act
            stockTienda.StockMaximoActual = 2;

            // Assert
            Assert.AreEqual(9, stockCentral.StockMaximoActual);
            Assert.AreEqual(2, stockTienda.StockMaximoActual);
        }
        [TestMethod]
        public void ControlStockProductoWrapper_AlSubirElStockMaximoDeUnAlmacenQueNoEsLaCentral_LaCentralNoPuedeQuedarNegativa()
        {
            // Arrange
            ControlStockProductoModel model = new ControlStockProductoModel();
            model.ControlesStocksAlmacen.Add(new ControlStockAlmacenModel
            {
                Almacen = "ALG",
                StockMaximoActual = 0,
                StockMaximoCalculado = 0,
                StockMaximoInicial = 0
            });
            model.ControlesStocksAlmacen.Add(new ControlStockAlmacenModel
            {
                Almacen = "REI",
                StockMaximoActual = 1,
                StockMaximoCalculado = 1,
                StockMaximoInicial = 1
            });
            ControlStockProductoWrapper wrapper = new ControlStockProductoWrapper(model);
            ControlStockAlmacenWrapper stockTienda = wrapper.ControlesStocksAlmacen.Single(c => c.Model.Almacen == "REI");
            ControlStockAlmacenWrapper stockCentral = wrapper.ControlesStocksAlmacen.Single(c => c.Model.Almacen == "ALG");

            // Act
            stockTienda.StockMaximoActual = 2;

            // Assert
            Assert.AreEqual(0, stockCentral.StockMaximoActual);
            Assert.AreEqual(1, stockTienda.StockMaximoActual);
        }

        [TestMethod]
        public void ControlStockProductoWrapper_AlSubirElStockMaximoDeUnAlmacenQueNoEsLaCentral_SiElMaximoDeLaCentralQuedaPorDebajoDelMinimoNoActualizoNinguno()
        {
            // Arrange
            ControlStockProductoModel model = new ControlStockProductoModel
            {
                StockMinimoActual = 3,
                StockMinimoInicial = 3
            };
            model.ControlesStocksAlmacen.Add(new ControlStockAlmacenModel
            {
                Almacen = "ALG",
                StockMaximoActual = 3,
                StockMaximoCalculado = 3,
                StockMaximoInicial = 3
            });
            model.ControlesStocksAlmacen.Add(new ControlStockAlmacenModel
            {
                Almacen = "REI",
                StockMaximoActual = 0,
                StockMaximoCalculado = 0,
                StockMaximoInicial = 0
            });
            ControlStockProductoWrapper wrapper = new ControlStockProductoWrapper(model);
            ControlStockAlmacenWrapper stockTienda = wrapper.ControlesStocksAlmacen.Single(c => c.Model.Almacen == "REI");
            ControlStockAlmacenWrapper stockCentral = wrapper.ControlesStocksAlmacen.Single(c => c.Model.Almacen == "ALG");

            // Act
            stockTienda.StockMaximoActual = 1;

            // Assert
            Assert.AreEqual(3, stockCentral.StockMaximoActual);
            Assert.AreEqual(0, stockTienda.StockMaximoActual);
        }

        [TestMethod]
        public void ControlStockProductoWrapper_AlSubirElStockMaximoDeUnAlmacenQueNoEsLaCentral_SiElMaximoDeLaCentralQuedaPorDebajoDelMinimoNoActualizoNingunoAunqueDesbloquearControlesSeaTrue()
        {
            // Arrange
            ControlStockProductoModel model = new ControlStockProductoModel
            {
                StockMinimoActual = 3,
                StockMinimoInicial = 3
            };
            model.ControlesStocksAlmacen.Add(new ControlStockAlmacenModel
            {
                Almacen = "ALG",
                StockMaximoActual = 3,
                StockMaximoCalculado = 3,
                StockMaximoInicial = 3
            });
            model.ControlesStocksAlmacen.Add(new ControlStockAlmacenModel
            {
                Almacen = "REI",
                StockMaximoActual = 1,
                StockMaximoCalculado = 1,
                StockMaximoInicial = 1
            });
            ControlStockProductoWrapper wrapper = new ControlStockProductoWrapper(model);
            wrapper.DesbloquearControlesStock = true;
            ControlStockAlmacenWrapper stockTienda = wrapper.ControlesStocksAlmacen.Single(c => c.Model.Almacen == "REI");
            ControlStockAlmacenWrapper stockCentral = wrapper.ControlesStocksAlmacen.Single(c => c.Model.Almacen == "ALG");

            // Act
            stockTienda.StockMaximoActual = 2;

            // Assert
            Assert.AreEqual(3, stockCentral.StockMaximoActual);
            Assert.AreEqual(1, stockTienda.StockMaximoActual);
        }

        [TestMethod]
        public void ControlStockProductoWrapper_AlSubirElStockMinimo_ElStockMinimoNoPuedeSerMayorQueElMaximoDeLaCentral()
        {
            // Arrange
            ControlStockProductoModel model = new ControlStockProductoModel
            {
                StockMinimoActual = 0,
                StockMinimoInicial = 0
            };
            model.ControlesStocksAlmacen.Add(new ControlStockAlmacenModel
            {
                Almacen = "ALG",
                StockMaximoActual = 1,
                StockMaximoCalculado = 1,
                StockMaximoInicial = 1
            });
            model.ControlesStocksAlmacen.Add(new ControlStockAlmacenModel
            {
                Almacen = "REI",
                StockMaximoActual = 1,
                StockMaximoCalculado = 1,
                StockMaximoInicial = 1
            });
            ControlStockProductoWrapper wrapper = new ControlStockProductoWrapper(model);

            // Act
            wrapper.StockMinimoActual = 2;

            // Assert
            Assert.AreEqual(0, wrapper.StockMinimoActual);
        }
    }
}