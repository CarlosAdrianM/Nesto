using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;

namespace PedidoVentaTests
{
    /// <summary>
    /// Inmovilizados (Carlos 25/11/26): «I/1823» escrito o leído con el lector en «Producto» pasa la línea a tipo inmovilizado (3)
    /// con Producto = «1823». Lo demás, como siempre.
    /// </summary>
    [TestClass]
    public class LineaInmovilizadoPorCodigoTests
    {
        private static LineaPedidoVentaWrapper LineaDeProducto()
            => new LineaPedidoVentaWrapper(new LineaPedidoVentaDTO { tipoLinea = 1, Cantidad = 1, AplicarDescuento = true });

        [TestMethod]
        public void Producto_CodigoInmovilizado_PasaATipo3ConElNumero()
        {
            var linea = LineaDeProducto();

            linea.Producto = "I/1823";

            Assert.AreEqual((byte?)3, linea.tipoLinea);
            Assert.AreEqual("1823", linea.Producto);
            Assert.IsFalse(linea.AplicarDescuento, "Un inmovilizado no lleva descuentos (reglas del tipo 3).");
        }

        [TestMethod]
        public void Producto_CodigoEnMinusculasYConEspacios_TambienVale()
        {
            var linea = LineaDeProducto();

            linea.Producto = " i/1823 ";

            Assert.AreEqual((byte?)3, linea.tipoLinea);
            Assert.AreEqual("1823", linea.Producto);
        }

        [TestMethod]
        public void Producto_YaEraInmovilizado_SeQuedaElNumero()
        {
            var linea = new LineaPedidoVentaWrapper(new LineaPedidoVentaDTO { tipoLinea = 3, Producto = "1700" });

            linea.Producto = "I/1823";

            Assert.AreEqual((byte?)3, linea.tipoLinea);
            Assert.AreEqual("1823", linea.Producto);
        }

        [TestMethod]
        public void Producto_LineaNuevaDelDataGrid_PasaATipo3()
        {
            var linea = new LineaPedidoVentaWrapper();

            linea.Producto = "I/1823";

            Assert.AreEqual((byte?)3, linea.tipoLinea);
            Assert.AreEqual("1823", linea.Producto);
        }

        [DataTestMethod]
        [DataRow("17404")]
        [DataRow("I/18A")]
        [DataRow("I1823")]
        public void Producto_NoEsCodigoInmovilizado_SigueSiendoProducto(string texto)
        {
            var linea = LineaDeProducto();

            linea.Producto = texto;

            Assert.AreEqual((byte?)1, linea.tipoLinea);
            Assert.AreEqual(texto, linea.Producto);
        }

        [TestMethod]
        public void Producto_InmovilizadoConNumeroSinPrefijo_NoCambiaNada()
        {
            var linea = new LineaPedidoVentaWrapper(new LineaPedidoVentaDTO { tipoLinea = 3 });

            linea.Producto = "1823";

            Assert.AreEqual((byte?)3, linea.tipoLinea);
            Assert.AreEqual("1823", linea.Producto);
        }
    }
}
