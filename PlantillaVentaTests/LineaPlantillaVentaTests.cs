using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PlantillaVenta;
using System.Windows.Media;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Tests para LineaPlantillaVenta
    /// Issue #286: Verificar que stockActualizado notifica cambios correctamente
    /// </summary>
    [TestClass]
    public class LineaPlantillaVentaTests
    {
        #region colorStock Tests

        [TestMethod]
        public void ColorStock_StockNoActualizado_DevuelveGris()
        {
            // Arrange
            var linea = new LineaPlantillaVenta
            {
                stockActualizado = false,
                cantidad = 1,
                cantidadDisponible = 100
            };

            // Act
            var color = linea.colorStock;

            // Assert
            Assert.AreEqual(Brushes.Gray, color);
        }

        [TestMethod]
        public void ColorStock_SuficienteStockEnAlmacen_DevuelveVerde()
        {
            // Arrange
            var linea = new LineaPlantillaVenta
            {
                stockActualizado = true,
                cantidad = 5,
                cantidadOferta = 0,
                cantidadDisponible = 10,
                StockDisponibleTodosLosAlmacenes = 20
            };

            // Act
            var color = linea.colorStock;

            // Assert
            Assert.AreEqual(Brushes.Green, color);
        }

        [TestMethod]
        public void ColorStock_SuficienteStockEnOtrosAlmacenes_DevuelveDeepPink()
        {
            // Arrange
            var linea = new LineaPlantillaVenta
            {
                stockActualizado = true,
                cantidad = 5,
                cantidadOferta = 0,
                cantidadDisponible = 2, // No suficiente en este almacen
                StockDisponibleTodosLosAlmacenes = 10 // Pero si en total
            };

            // Act
            var color = linea.colorStock;

            // Assert
            Assert.AreEqual(Brushes.DeepPink, color);
        }

        [TestMethod]
        public void ColorStock_NoHayStockEnNingunAlmacen_DevuelveRojo()
        {
            // Arrange
            var linea = new LineaPlantillaVenta
            {
                stockActualizado = true,
                cantidad = 10,
                cantidadOferta = 0,
                cantidadDisponible = 2,
                StockDisponibleTodosLosAlmacenes = 5 // No suficiente ni en total
            };

            // Act
            var color = linea.colorStock;

            // Assert
            Assert.AreEqual(Brushes.Red, color);
        }

        [TestMethod]
        public void ColorStock_NoSuficienteEnTodosAlmacenes_PeroSuficienteConPendiente_DevuelveRojo()
        {
            // Arrange
            // NOTA: El codigo actual evalua StockDisponibleTodosLosAlmacenes primero,
            // por lo que Blue (pendiente) nunca se alcanza si StockDisponibleTodosLosAlmacenes < cantidad
            var linea = new LineaPlantillaVenta
            {
                stockActualizado = true,
                cantidad = 10,
                cantidadOferta = 0,
                cantidadDisponible = 5,
                cantidadPendienteRecibir = 10, // Disponible + Pendiente = 15 >= 10
                StockDisponibleTodosLosAlmacenes = 5 // Pero esto es < 10, asi que devuelve Rojo
            };

            // Act
            var color = linea.colorStock;

            // Assert - La logica actual devuelve Rojo porque StockDisponibleTodosLosAlmacenes < cantidad
            Assert.AreEqual(Brushes.Red, color);
        }

        [TestMethod]
        public void ColorStock_NoSuficienteEnTodosAlmacenes_DevuelveRojo()
        {
            // Arrange
            var linea = new LineaPlantillaVenta
            {
                stockActualizado = true,
                cantidad = 10,
                cantidadOferta = 0,
                cantidadDisponible = 2,
                cantidadPendienteRecibir = 0,
                stock = 15, // Stock fisico suficiente pero...
                StockDisponibleTodosLosAlmacenes = 5 // No suficiente en todos los almacenes
            };

            // Act
            var color = linea.colorStock;

            // Assert - La logica actual devuelve Rojo porque StockDisponibleTodosLosAlmacenes < cantidad
            Assert.AreEqual(Brushes.Red, color);
        }

        [TestMethod]
        public void ColorStock_IncluyeCantidadOferta_EnCalculo()
        {
            // Arrange
            var linea = new LineaPlantillaVenta
            {
                stockActualizado = true,
                cantidad = 5,
                cantidadOferta = 3, // Total necesario = 8
                cantidadDisponible = 6, // No suficiente para 8
                StockDisponibleTodosLosAlmacenes = 10 // Suficiente en total
            };

            // Act
            var color = linea.colorStock;

            // Assert
            Assert.AreEqual(Brushes.DeepPink, color); // No verde porque 6 < 8
        }

        #endregion

        #region stockActualizado PropertyChanged Tests

        [TestMethod]
        public void StockActualizado_AlCambiar_NotificaColorStock()
        {
            // Arrange
            var linea = new LineaPlantillaVenta
            {
                cantidad = 1,
                cantidadDisponible = 10,
                StockDisponibleTodosLosAlmacenes = 10
            };

            bool colorStockNotificado = false;
            linea.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(LineaPlantillaVenta.colorStock))
                {
                    colorStockNotificado = true;
                }
            };

            // Act
            linea.stockActualizado = true;

            // Assert
            Assert.IsTrue(colorStockNotificado, "Deberia notificar cambio en colorStock");
        }

        [TestMethod]
        public void Cantidad_AlCambiar_NotificaColorStock()
        {
            // Arrange
            var linea = new LineaPlantillaVenta
            {
                stockActualizado = true,
                cantidadDisponible = 10
            };

            bool colorStockNotificado = false;
            linea.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(LineaPlantillaVenta.colorStock))
                {
                    colorStockNotificado = true;
                }
            };

            // Act
            linea.cantidad = 5;

            // Assert
            Assert.IsTrue(colorStockNotificado, "Deberia notificar cambio en colorStock");
        }

        [TestMethod]
        public void CantidadOferta_AlCambiar_NotificaColorStock()
        {
            // Arrange
            var linea = new LineaPlantillaVenta
            {
                stockActualizado = true,
                cantidadDisponible = 10
            };

            bool colorStockNotificado = false;
            linea.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(LineaPlantillaVenta.colorStock))
                {
                    colorStockNotificado = true;
                }
            };

            // Act
            linea.cantidadOferta = 2;

            // Assert
            Assert.IsTrue(colorStockNotificado, "Deberia notificar cambio en colorStock");
        }

        [TestMethod]
        public void CantidadDisponible_AlCambiar_NotificaColorStock()
        {
            // Arrange
            var linea = new LineaPlantillaVenta
            {
                stockActualizado = true,
                cantidad = 5
            };

            bool colorStockNotificado = false;
            linea.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(LineaPlantillaVenta.colorStock))
                {
                    colorStockNotificado = true;
                }
            };

            // Act
            linea.cantidadDisponible = 10;

            // Assert
            Assert.IsTrue(colorStockNotificado, "Deberia notificar cambio en colorStock");
        }

        #endregion

        #region baseImponible Tests

        [TestMethod]
        public void BaseImponible_SinDescuento_CalculaCorrectamente()
        {
            // Arrange
            var linea = new LineaPlantillaVenta
            {
                cantidad = 3,
                precio = 10.00M,
                descuento = 0M
            };

            // Act
            decimal baseImponible = linea.baseImponible;

            // Assert
            Assert.AreEqual(30.00M, baseImponible);
        }

        [TestMethod]
        public void BaseImponible_ConDescuento_CalculaCorrectamente()
        {
            // Arrange - Ejemplo de Issue #242/#243
            var linea = new LineaPlantillaVenta
            {
                cantidad = 1,
                precio = 4.50M,
                descuento = 0.35M
            };

            // Act
            decimal baseImponible = linea.baseImponible;

            // Assert
            // Bruto = 1 * 4.50 = 4.50
            // ImporteDto = ROUND(4.50 * 0.35, 2) = ROUND(1.575, 2) = 1.58
            // BaseImponible = ROUND(4.50, 2) - 1.58 = 4.50 - 1.58 = 2.92
            Assert.AreEqual(2.92M, baseImponible);
        }

        [TestMethod]
        public void BaseImponible_Precio_AlCambiar_NotificaBaseImponible()
        {
            // Arrange
            var linea = new LineaPlantillaVenta { cantidad = 1 };

            bool baseImponibleNotificado = false;
            linea.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(LineaPlantillaVenta.baseImponible))
                {
                    baseImponibleNotificado = true;
                }
            };

            // Act
            linea.precio = 10.00M;

            // Assert
            Assert.IsTrue(baseImponibleNotificado);
        }

        [TestMethod]
        public void BaseImponible_Descuento_AlCambiar_NotificaBaseImponible()
        {
            // Arrange
            var linea = new LineaPlantillaVenta { cantidad = 1, precio = 10M };

            bool baseImponibleNotificado = false;
            linea.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(LineaPlantillaVenta.baseImponible))
                {
                    baseImponibleNotificado = true;
                }
            };

            // Act
            linea.descuento = 0.10M;

            // Assert
            Assert.IsTrue(baseImponibleNotificado);
        }

        #endregion
    }
}
