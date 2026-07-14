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
        public void LineaPedidoCompraDTO_SiLaCantidadMinimaEsMayorQueCantidad_LeeBienLosPreciosYDescuentos()
        {
            // Arrange
            var lineaDTO = new LineaPedidoCompraDTO
            {
                Cantidad = 0,
                PrecioUnitario = 1
            };
            var descuentos = new List<DescuentoCantidadCompra> {
                new DescuentoCantidadCompra
                {
                    CantidadMinima = 1,
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
            lineaDTO.Cantidad = 1;

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

        // ----- Oferta manual (Nesto#403): oferta puntual del proveedor solo para este pedido -----

        [TestMethod]
        public void PonerRegaloManual_AjustaLasCobradasYMarcaLaOfertaComoManual()
        {
            var lineaDTO = new LineaPedidoCompraDTO { Cantidad = 24 };

            lineaDTO.PonerRegaloManual(4);

            Assert.IsTrue(lineaDTO.OfertaManual);
            Assert.AreEqual(20, lineaDTO.CantidadCobrada);
            Assert.AreEqual(4, lineaDTO.CantidadRegalo);
        }

        [TestMethod]
        public void PonerCobradasManual_AjustaElRegalo()
        {
            var lineaDTO = new LineaPedidoCompraDTO { Cantidad = 24 };

            lineaDTO.PonerCobradasManual(20);

            Assert.IsTrue(lineaDTO.OfertaManual);
            Assert.AreEqual(20, lineaDTO.CantidadCobrada);
            Assert.AreEqual(4, lineaDTO.CantidadRegalo);
        }

        [TestMethod]
        public void OfertaManual_CambiarLaCantidad_MantieneElRegaloPactadoYNoLaPisaLaTabla()
        {
            // Con oferta de tabla 6+1, la manual 20+4 debe sobrevivir a un cambio de cantidad:
            // el recálculo automático no puede pisarla.
            var lineaDTO = new LineaPedidoCompraDTO
            {
                Cantidad = 24,
                Ofertas = new List<OfertaCompra> { new OfertaCompra { CantidadCobrada = 6, CantidadRegalo = 1 } }
            };
            lineaDTO.PonerRegaloManual(4);

            lineaDTO.Cantidad = 30;

            Assert.IsTrue(lineaDTO.OfertaManual);
            Assert.AreEqual(26, lineaDTO.CantidadCobrada, "Mantiene el regalo pactado y ajusta las cobradas");
            Assert.AreEqual(4, lineaDTO.CantidadRegalo);
        }

        [TestMethod]
        public void PonerRegaloManual_Vaciarlo_VuelveAlAutomaticoDeLaTabla()
        {
            var lineaDTO = new LineaPedidoCompraDTO
            {
                Cantidad = 14,
                Ofertas = new List<OfertaCompra> { new OfertaCompra { CantidadCobrada = 6, CantidadRegalo = 1 } }
            };
            lineaDTO.PonerRegaloManual(5);

            lineaDTO.PonerRegaloManual(null);

            Assert.IsFalse(lineaDTO.OfertaManual);
            Assert.AreEqual(12, lineaDTO.CantidadCobrada, "Vuelve al 6+1 de OfertasProveedores (14 = 2x(6+1))");
            Assert.AreEqual(2, lineaDTO.CantidadRegalo);
        }

        [TestMethod]
        public void PonerRegaloManual_SinOfertasDeTabla_VaciarloDejaLasCantidadesLimpias()
        {
            var lineaDTO = new LineaPedidoCompraDTO { Cantidad = 20 };
            lineaDTO.PonerRegaloManual(4);

            lineaDTO.PonerRegaloManual(0);

            Assert.IsFalse(lineaDTO.OfertaManual);
            Assert.IsNull(lineaDTO.CantidadCobrada);
            Assert.IsNull(lineaDTO.CantidadRegalo);
        }

        [TestMethod]
        public void PonerRegaloManual_MayorQueLaCantidad_SeRecortaALaCantidad()
        {
            var lineaDTO = new LineaPedidoCompraDTO { Cantidad = 10 };

            lineaDTO.PonerRegaloManual(15);

            Assert.AreEqual(10, lineaDTO.CantidadRegalo);
            Assert.AreEqual(0, lineaDTO.CantidadCobrada);
        }

        [TestMethod]
        public void PonerCobradasManual_IgualOMayorQueLaCantidad_NoHayRegaloYVuelveAlAutomatico()
        {
            var lineaDTO = new LineaPedidoCompraDTO { Cantidad = 20 };
            lineaDTO.PonerRegaloManual(4);

            lineaDTO.PonerCobradasManual(20);

            Assert.IsFalse(lineaDTO.OfertaManual);
            Assert.IsNull(lineaDTO.CantidadRegalo);
        }
    }
}
