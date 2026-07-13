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

        // ----- Nesto#392: múltiplos a nivel producto, persistiendo en el almacén central -----

        private static ControlStockProductoModel ModelConDosAlmacenes(int multiplosCentral = 1, int multiplosTienda = 1)
        {
            ControlStockProductoModel model = new ControlStockProductoModel();
            model.ControlesStocksAlmacen.Add(new ControlStockAlmacenModel
            {
                Almacen = "ALG",
                Multiplos = multiplosCentral,
                StockMaximoActual = 10,
                StockMaximoInicial = 10,
                YaExiste = true
            });
            model.ControlesStocksAlmacen.Add(new ControlStockAlmacenModel
            {
                Almacen = "REI",
                Multiplos = multiplosTienda,
                StockMaximoActual = 1,
                StockMaximoInicial = 1,
                YaExiste = true
            });
            return model;
        }

        [TestMethod]
        public void ControlStockProductoWrapper_AlCambiarMultiplos_SeEscribenEnElAlmacenCentralYNoEnElResto()
        {
            ControlStockProductoModel model = ModelConDosAlmacenes(multiplosCentral: 1, multiplosTienda: 3);
            ControlStockProductoWrapper wrapper = new ControlStockProductoWrapper(model);

            wrapper.MultiplosActual = 6;

            Assert.AreEqual(6, wrapper.MultiplosActual);
            Assert.AreEqual(6, model.ControlesStocksAlmacen.Single(c => c.Almacen == "ALG").Multiplos);
            Assert.AreEqual(3, model.ControlesStocksAlmacen.Single(c => c.Almacen == "REI").Multiplos,
                "Los múltiplos de los demás almacenes no se tocan");
        }

        [TestMethod]
        public void ControlStockProductoWrapper_MultiplosMenoresQueUno_NoSeAdmiten()
        {
            ControlStockProductoModel model = ModelConDosAlmacenes(multiplosCentral: 6);
            ControlStockProductoWrapper wrapper = new ControlStockProductoWrapper(model);

            wrapper.MultiplosActual = 0;

            Assert.AreEqual(6, wrapper.MultiplosActual, "Un múltiplo menor que 1 no debe aceptarse");
        }

        [TestMethod]
        public void ControlStockProductoWrapper_CambiarSoloMultiplos_ToListModificadosIncluyeLaCentral()
        {
            // Bug que motiva la issue: un cambio SOLO de múltiplos no se detectaba y no se guardaba.
            ControlStockProductoModel model = ModelConDosAlmacenes(multiplosCentral: 1);
            ControlStockProductoWrapper wrapper = new ControlStockProductoWrapper(model);

            wrapper.MultiplosActual = 12;
            var modificados = wrapper.ToListModificados;

            Assert.AreEqual(1, modificados.Count, "Solo debe guardarse el almacén central");
            Assert.AreEqual("ALG", modificados.Single().Almacén);
            Assert.AreEqual(12, modificados.Single().Múltiplos);
        }

        [TestMethod]
        public void ControlStockProductoWrapper_SinCambios_ToListModificadosVacio()
        {
            ControlStockProductoModel model = ModelConDosAlmacenes(multiplosCentral: 6, multiplosTienda: 3);
            ControlStockProductoWrapper wrapper = new ControlStockProductoWrapper(model);

            Assert.AreEqual(0, wrapper.ToListModificados.Count,
                "Recién cargado, sin tocar nada, no debe haber cambios que guardar");
        }

        [TestMethod]
        public void ControlStockProductoWrapper_CambiarMultiplos_DisparaStockChanged()
        {
            // El VM se apoya en StockChanged para reevaluar el CanExecute del botón Guardar.
            ControlStockProductoModel model = ModelConDosAlmacenes(multiplosCentral: 1);
            ControlStockProductoWrapper wrapper = new ControlStockProductoWrapper(model);
            bool disparado = false;
            wrapper.StockChanged += (s, e) => disparado = true;

            wrapper.MultiplosActual = 6;

            Assert.IsTrue(disparado, "Cambiar los múltiplos debe disparar StockChanged");
        }
    }
}