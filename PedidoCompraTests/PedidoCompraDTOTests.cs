using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PedidoCompra.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PedidoCompraTests
{
    [TestClass]
    public class PedidoCompraDTOTests
    {
        [TestMethod]
        public void PedidoCompraDTO_SiAlCargarElPedidoHayUnaOfertaYaCreada_LaSegundaLineaVaAPrecioCero()
        {
            // Arrange
            var pedido = new PedidoCompraDTO();
            var lineaCobrada = new LineaPedidoCompraDTO
            {
                Cantidad = 3,
                PrecioUnitario = 10
            };
            var lineaRegalo = new LineaPedidoCompraDTO
            {
                Cantidad = 1,
                PrecioUnitario = 0
            };
            var descuentos = new List<DescuentoCantidadCompra> {
                new DescuentoCantidadCompra
                {
                    CantidadMinima = 0,
                    Descuento = .5M,
                    Precio = 5
                }
            };
            var ofertas = new List<OfertaCompra> {
                new OfertaCompra
                {
                    CantidadCobrada = 3,
                    CantidadRegalo = 1
                }
            };
            lineaCobrada.Descuentos = descuentos;
            lineaRegalo.Descuentos = descuentos;
            lineaCobrada.Ofertas = ofertas;
            lineaRegalo.Ofertas = ofertas;

            // Act
            lineaCobrada.Cantidad = 12;

            // Assert
            Assert.AreEqual(0, lineaRegalo.BaseImponible);
        }
    }
}
