using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Nesto.Modulos.PedidoVenta.PedidoVentaModel;

namespace PedidoVentaTests
{
    /// <summary>
    /// Filtro de ListaPedidosVenta: cliente y vendedor se comparan por igualdad, así que los espacios
    /// de los extremos del filtro sobran («41692 » no encontraba los pedidos del cliente 41692).
    /// En las comparaciones por «contiene» (nombre, dirección) el espacio sí cuenta: «tinte lk ».
    /// </summary>
    [TestClass]
    public class ResumenPedidoFiltroTests
    {
        [TestMethod]
        public void Contains_ClienteConEspacioAlFinalDelFiltro_LoEncuentra()
        {
            var resumen = new ResumenPedido { numero = 927000, cliente = "41692     ", nombre = "Peluquería X" };

            Assert.IsTrue(resumen.Contains("41692 "));
        }

        [TestMethod]
        public void Contains_VendedorConEspacioAlFinalDelFiltro_LoEncuentra()
        {
            var resumen = new ResumenPedido { numero = 927000, vendedor = "JM ", nombre = "Peluquería X" };

            Assert.IsTrue(resumen.Contains("jm "));
        }

        [TestMethod]
        public void Contains_NombreConEspacioAlFinalDelFiltro_ElEspacioCuenta()
        {
            var resumen = new ResumenPedido { numero = 927000, cliente = "1", nombre = "Tinte LKX" };

            Assert.IsFalse(resumen.Contains("tinte lk "));
            Assert.IsTrue(resumen.Contains("tinte lk"));
        }
    }
}
