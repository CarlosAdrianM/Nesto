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

        #region colorEstado Tests

        // Issue #357: en la plantilla, 'estado' es el estado del PRODUCTO; estado=4 = a extinguir.
        // Debe mostrarse en púrpura, prevaleciendo sobre el color por histórico de ventas.
        [TestMethod]
        public void ColorEstado_ProductoAExtinguir_Estado4_DevuelvePurpura()
        {
            var linea = new LineaPlantillaVenta { estado = 4 };

            Assert.AreEqual(Brushes.Purple, linea.colorEstado);
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

        #region cantidadOferta / puedeEditarCantidadOferta (Nesto#401)

        // Nesto#401: el campo cantidad oferta se quedaba inactivo (IsEnabled bindeaba la ficha)
        // y no había forma de quitar una oferta ya puesta. Además, poner una oferta inicializaba
        // aplicarDescuentoFicha a False si aún era null (el setter público de aplicarDescuento
        // la inicializa con el primer valor que recibe), corrompiéndola al recargar borradores
        // (Json.NET asigna cantidadOferta ANTES que la ficha, por orden de declaración).

        [TestMethod]
        public void CantidadOferta_PonerOfertaConFichaNull_NoInicializaLaFicha()
        {
            var linea = new LineaPlantillaVenta();

            linea.cantidadOferta = 1;

            Assert.IsNull(linea.aplicarDescuentoFicha,
                "Poner una oferta no debe inicializar la ficha con un valor derivado");
            Assert.IsFalse(linea.aplicarDescuento, "Oferta y descuento no se suman");
        }

        [TestMethod]
        public void PuedeEditarCantidadOferta_ConOfertaPuesta_SiempreEditable()
        {
            // Un producto sin descuento de ficha PUEDE llevar una oferta autorizada (OfertasPermitidas):
            // quitar o reducir esa oferta debe estar siempre permitido (la validación del servidor manda).
            var linea = new LineaPlantillaVenta { aplicarDescuentoFicha = false, cantidadOferta = 1 };

            Assert.IsTrue(linea.puedeEditarCantidadOferta,
                "Con oferta puesta el control debe estar activo aunque la ficha no admita descuentos");
        }

        [TestMethod]
        public void PuedeEditarCantidadOferta_SinOferta_SegunLaFicha()
        {
            Assert.IsFalse(new LineaPlantillaVenta { aplicarDescuentoFicha = false }.puedeEditarCantidadOferta);
            Assert.IsFalse(new LineaPlantillaVenta().puedeEditarCantidadOferta, "Ficha null sin oferta: no editable");
            Assert.IsTrue(new LineaPlantillaVenta { aplicarDescuentoFicha = true }.puedeEditarCantidadOferta);
        }

        [TestMethod]
        public void CantidadOferta_QuitarLaOferta_RestauraElDescuentoDeLaFicha()
        {
            var linea = new LineaPlantillaVenta { aplicarDescuento = true }; // inicializa ficha = true
            linea.cantidadOferta = 2;
            Assert.IsFalse(linea.aplicarDescuento);

            linea.cantidadOferta = 0;

            Assert.IsTrue(linea.aplicarDescuento, "Al quitar la oferta vuelve el descuento de ficha");
            Assert.IsTrue(linea.aplicarDescuentoFicha.Value, "La ficha no se toca en todo el ciclo");
        }

        [TestMethod]
        public void CantidadOferta_DeserializarBorradorConOfertaAntesDeLaFicha_LaFichaDelJsonPrevalece()
        {
            // Orden REAL de las propiedades en el JSON del borrador (caso 27593, cliente 9471):
            // cantidadOferta llega ANTES que aplicarDescuento/aplicarDescuentoFicha. Antes del fix,
            // el setter de cantidadOferta inicializaba la ficha a False vía el setter público de
            // aplicarDescuento.
            string json = "{\"producto\":\"27593\",\"cantidad\":0,\"cantidadOferta\":1," +
                "\"aplicarDescuento\":false,\"aplicarDescuentoFicha\":true}";

            var linea = Newtonsoft.Json.JsonConvert.DeserializeObject<LineaPlantillaVenta>(json);

            Assert.IsTrue(linea.aplicarDescuentoFicha.Value, "La ficha del JSON debe prevalecer");
            Assert.IsTrue(linea.puedeEditarCantidadOferta);
        }

        [TestMethod]
        public void CantidadOferta_DeserializarBorradorViejoConFichaCorrupta_SePuedeQuitarLaOferta()
        {
            // Borradores guardados ANTES del fix llevan la ficha ya corrompida a false. El control
            // debe quedar editable igualmente (cantidadOferta > 0) para poder quitar la oferta.
            string json = "{\"producto\":\"27593\",\"cantidad\":0,\"cantidadOferta\":1," +
                "\"aplicarDescuento\":false,\"aplicarDescuentoFicha\":false}";

            var linea = Newtonsoft.Json.JsonConvert.DeserializeObject<LineaPlantillaVenta>(json);

            Assert.IsTrue(linea.puedeEditarCantidadOferta,
                "Con oferta puesta siempre se puede editar, aunque el borrador viejo traiga la ficha corrupta");
        }

        #endregion
    }
}
