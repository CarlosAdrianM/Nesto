using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PlantillaVenta;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#430 (slice 1): al cambiar el almacén con el pedido empezado, se refrescan los
    /// stocks conservando las cantidades y se avisa de las líneas del pedido que se quedan
    /// sin stock suficiente en el nuevo almacén. Este helper decide cuáles son.
    /// </summary>
    [TestClass]
    public class CambioAlmacenTests
    {
        private static LineaPlantillaVenta Linea(short cantidad, int disponible, short cantidadOferta = 0,
            bool esPortes = false, string producto = "12345")
            => new LineaPlantillaVenta
            {
                producto = producto,
                cantidad = cantidad,
                cantidadOferta = cantidadOferta,
                cantidadDisponible = disponible,
                esLineaPortes = esPortes
            };

        [TestMethod]
        public void LineasConFaltaDeStock_SinLineas_DevuelveVacia()
        {
            Assert.AreEqual(0, PlantillaVentaViewModel.LineasConFaltaDeStock(null).Count);
            Assert.AreEqual(0, PlantillaVentaViewModel.LineasConFaltaDeStock(new List<LineaPlantillaVenta>()).Count);
        }

        [TestMethod]
        public void LineasConFaltaDeStock_LineaSinCantidad_NoAparece()
        {
            // Productos del catálogo que no están en el pedido: no son problema del cambio.
            var lineas = new List<LineaPlantillaVenta> { Linea(cantidad: 0, disponible: 0) };

            Assert.AreEqual(0, PlantillaVentaViewModel.LineasConFaltaDeStock(lineas).Count);
        }

        [TestMethod]
        public void LineasConFaltaDeStock_CantidadMayorQueDisponible_Aparece()
        {
            var lineas = new List<LineaPlantillaVenta> { Linea(cantidad: 5, disponible: 3) };

            var falta = PlantillaVentaViewModel.LineasConFaltaDeStock(lineas);

            Assert.AreEqual(1, falta.Count);
        }

        [TestMethod]
        public void LineasConFaltaDeStock_StockJusto_NoAparece()
        {
            var lineas = new List<LineaPlantillaVenta> { Linea(cantidad: 2, disponible: 2) };

            Assert.AreEqual(0, PlantillaVentaViewModel.LineasConFaltaDeStock(lineas).Count);
        }

        [TestMethod]
        public void LineasConFaltaDeStock_LaCantidadDeOfertaTambienCuenta()
        {
            // 1 vendida + 2 de oferta = 3 unidades a servir; con 2 disponibles falta stock.
            var lineas = new List<LineaPlantillaVenta> { Linea(cantidad: 1, disponible: 2, cantidadOferta: 2) };

            Assert.AreEqual(1, PlantillaVentaViewModel.LineasConFaltaDeStock(lineas).Count);
        }

        [TestMethod]
        public void LineasConFaltaDeStock_LaLineaDePortesSeIgnora()
        {
            var lineas = new List<LineaPlantillaVenta> { Linea(cantidad: 1, disponible: 0, esPortes: true) };

            Assert.AreEqual(0, PlantillaVentaViewModel.LineasConFaltaDeStock(lineas).Count);
        }
    }
}
