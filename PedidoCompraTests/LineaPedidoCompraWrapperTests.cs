using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Models;
using System.Linq;

namespace PedidoCompraTests
{
    [TestClass]
    public class LineaPedidoCompraWrapperTests
    {
        [TestMethod]
        public void LineaPedidoCompraWrapper_AlInsertarLinea_LosModelosQuedanIguales()
        {
            var servicio = A.Fake<IPedidoCompraService>();
            var pedidoDTO = new PedidoCompraDTO();
            var pedido = new PedidoCompraWrapper(pedidoDTO, servicio);
            var lineaDTO = new LineaPedidoCompraDTO { Cantidad = 1, PrecioUnitario = 100 };
            var linea = new LineaPedidoCompraWrapper(lineaDTO, servicio);
            
            pedido.Lineas.Add(linea);

            Assert.AreEqual(1, pedido.Lineas.Count);
            Assert.AreEqual(1, pedido.Model.Lineas.Count);
            Assert.AreEqual(100, pedido.Lineas.First().BaseImponible);
            Assert.AreEqual(100, pedido.Model.Lineas.First().BaseImponible);
            Assert.AreEqual(100, pedido.Lineas.First().Model.BaseImponible);
        }

        [TestMethod]
        public void LineaPedidoCompraWrapper_AlCargarProducto_LosModelosQuedanIguales()
        {
            var servicio = A.Fake<IPedidoCompraService>();
            A.CallTo(() => servicio.LeerProducto(A<string>.Ignored, "Prod_A", A<string>.Ignored, A<string>.Ignored)).Returns(new LineaPedidoCompraDTO {
                Producto = "Prod_A",
                Cantidad = 2,
                PrecioUnitario = 50
            });
            var pedidoDTO = new PedidoCompraDTO();
            var pedido = new PedidoCompraWrapper(pedidoDTO, servicio);
            var lineaDTO = new LineaPedidoCompraDTO { Producto = "Prod_Viejo", Cantidad = 1, PrecioUnitario = 1 };
            var linea = new LineaPedidoCompraWrapper(lineaDTO, servicio);

            pedido.Lineas.Add(linea);
            linea.Producto = "Prod_A";

            Assert.AreEqual(1, pedido.Lineas.Count);
            Assert.AreEqual(1, pedido.Model.Lineas.Count);
            Assert.AreEqual("Prod_A", pedido.Lineas.First().Producto);
            Assert.AreEqual("Prod_A", pedido.Model.Lineas.First().Producto);
            Assert.AreEqual("Prod_A", pedido.Lineas.First().Model.Producto);
        }
    }
}
