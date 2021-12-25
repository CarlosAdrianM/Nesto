using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PedidoCompraTests
{
    [TestClass]
    public class LineaPedidoCompraDTOTests
    {
        [TestMethod]
        public void LineaPedidoCompraDTO_AlPonerCantidad0_LeeBienLosPreciosYDescuentos()
        {
            // Arrange
            var lineaDTO = new LineaPedidoCompraDTO {
                Cantidad = 5,
                PrecioUnitario = 1
            };
            var descuentos = new List<DescuentoCantidadCompra> {
                new DescuentoCantidadCompra
                {
                    CantidadMinima = 0,
                    Precio = 10
                },
                new DescuentoCantidadCompra
                {
                    CantidadMinima = 5,
                    Precio = 1
                }
            };
            lineaDTO.Descuentos = descuentos;

            // Act
            lineaDTO.Cantidad = 0;

            // Assert
            Assert.AreEqual(10, lineaDTO.PrecioUnitario);
        }

        [TestMethod]
        public void LineaPedidoCompraDTO_SiLaCantidadEsMenorALaOferta_NoSeRegalaNada()
        {
            // Arrange
            var lineaDTO = new LineaPedidoCompraDTO
            {
                Cantidad = 6
            };
            var ofertas = new List<OfertaCompra> {
                new OfertaCompra
                {
                    CantidadCobrada = 6,
                    CantidadRegalo = 1
                }
            };
            lineaDTO.Ofertas = ofertas;

            // Act

            // Assert
            Assert.IsNull(lineaDTO.CantidadCobrada);
            Assert.IsNull(lineaDTO.CantidadRegalo);
        }

        [TestMethod]
        public void LineaPedidoCompraDTO_SiLaCantidadEsIgualALaOferta_LaOfertaActualCoincideConLoDevuelto()
        {
            // Arrange
            LineaPedidoCompraDTO lineaDTO = new();
            var ofertas = new List<OfertaCompra> {
                new OfertaCompra
                {
                    CantidadCobrada = 6,
                    CantidadRegalo = 1
                }
            };
            lineaDTO.Ofertas = ofertas;

            // Act
            lineaDTO.Cantidad = 7;

            // Assert
            Assert.AreEqual(6, lineaDTO.CantidadCobrada);
            Assert.AreEqual(1, lineaDTO.CantidadRegalo);
        }

        [TestMethod]
        public void LineaPedidoCompraDTO_SiLaCantidadEsMayorALaOferta_ElRestoVaEnCantidadCobrada()
        {
            // Arrange
            LineaPedidoCompraDTO lineaDTO = new();
            var ofertas = new List<OfertaCompra> {
                new OfertaCompra
                {
                    CantidadCobrada = 6,
                    CantidadRegalo = 1
                }
            };
            lineaDTO.Ofertas = ofertas;

            // Act
            lineaDTO.Cantidad = 8;

            // Assert
            Assert.AreEqual(7, lineaDTO.CantidadCobrada);
            Assert.AreEqual(1, lineaDTO.CantidadRegalo);
        }

        [TestMethod]
        public void LineaPedidoCompraDTO_SiLaCantidadEsMultiploDLaOferta_SeRegalaTambienMultiplo()
        {
            // Arrange
            LineaPedidoCompraDTO lineaDTO = new();
            var ofertas = new List<OfertaCompra> {
                new OfertaCompra
                {
                    CantidadCobrada = 6,
                    CantidadRegalo = 1
                }
            };
            lineaDTO.Ofertas = ofertas;

            // Act
            lineaDTO.Cantidad = 14;

            // Assert
            Assert.AreEqual(12, lineaDTO.CantidadCobrada);
            Assert.AreEqual(2, lineaDTO.CantidadRegalo);
        }

        [TestMethod]
        public void LineaPedidoCompraDTO_SiLaCantidadEsMayorAlMultiploDeLaOferta_SeRegalaTambienMultiploYSeSumaElRestoALoCobrado()
        {
            // Arrange
            LineaPedidoCompraDTO lineaDTO = new();
            var ofertas = new List<OfertaCompra> {
                new OfertaCompra
                {
                    CantidadCobrada = 6,
                    CantidadRegalo = 1
                }
            };
            lineaDTO.Ofertas = ofertas;

            // Act
            lineaDTO.Cantidad = 15;

            // Assert
            Assert.AreEqual(13, lineaDTO.CantidadCobrada);
            Assert.AreEqual(2, lineaDTO.CantidadRegalo);
        }
    }
}
