using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PlantillaVenta;

namespace PlantillaVentaTests
{
    /// <summary>
    /// NestoAPI#211 / Nesto#365: la base de portes con servir junto debe contar todas las líneas
    /// (una única entrega); sin servir junto se excluyen las líneas sobre pedido (estado != 0 sin
    /// stock en el almacén). Réplica cliente de GestorPortes en NestoAPI.
    /// </summary>
    [TestClass]
    public class EsSobrePedidoParaPortesTests
    {
        private static LineaPlantillaVenta Linea(int estado, bool stockActualizado, int cantidadDisponible,
            int cantidad = 1, int cantidadOferta = 0)
        {
            return new LineaPlantillaVenta
            {
                estado = estado,
                stockActualizado = stockActualizado,
                cantidadDisponible = cantidadDisponible,
                cantidad = cantidad,
                cantidadOferta = cantidadOferta
            };
        }

        [TestMethod]
        public void ServirJunto_NuncaEsSobrePedido()
        {
            // Una única entrega: la línea estado != 0 sin stock cuenta igualmente.
            var linea = Linea(estado: 4, stockActualizado: true, cantidadDisponible: 0, cantidad: 2);
            Assert.IsFalse(PlantillaVentaViewModel.EsSobrePedidoParaPortes(linea, servirJunto: true));
        }

        [TestMethod]
        public void SinServirJunto_Estado0_NoEsSobrePedido()
        {
            var linea = Linea(estado: 0, stockActualizado: true, cantidadDisponible: 0, cantidad: 5);
            Assert.IsFalse(PlantillaVentaViewModel.EsSobrePedidoParaPortes(linea, servirJunto: false));
        }

        [TestMethod]
        public void SinServirJunto_EstadoDistinto0_SinStockAlmacen_EsSobrePedido()
        {
            var linea = Linea(estado: 4, stockActualizado: true, cantidadDisponible: 1, cantidad: 2);
            Assert.IsTrue(PlantillaVentaViewModel.EsSobrePedidoParaPortes(linea, servirJunto: false));
        }

        [TestMethod]
        public void SinServirJunto_EstadoDistinto0_ConStockAlmacen_NoEsSobrePedido()
        {
            var linea = Linea(estado: 4, stockActualizado: true, cantidadDisponible: 5, cantidad: 2);
            Assert.IsFalse(PlantillaVentaViewModel.EsSobrePedidoParaPortes(linea, servirJunto: false));
        }

        [TestMethod]
        public void SinServirJunto_StockNoActualizado_AsumeSobrePedido()
        {
            var linea = Linea(estado: 4, stockActualizado: false, cantidadDisponible: 0, cantidad: 2);
            Assert.IsTrue(PlantillaVentaViewModel.EsSobrePedidoParaPortes(linea, servirJunto: false));
        }
    }
}
