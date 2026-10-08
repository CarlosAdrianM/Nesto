using FakeItEasy;
using FikaAmazonAPI.AmazonSpApiSDK.Models.Orders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.CanalesExternos;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CanalesExternosTests
{
    /// <summary>
    /// Novedades 548: en Amazon, cuando el cliente anula un artículo antes de que lo enviemos, la
    /// línea sigue llegando con QuantityOrdered = 0. Esas líneas no se pasan a Nesto (daban error al
    /// crear el pedido) y, si no queda ninguna, el pedido no se crea y se dice por qué.
    /// </summary>
    [TestClass]
    public class CanalExternoPedidosAmazonLineasTests
    {
        private CanalExternoPedidosAmazon CrearCanal()
        {
            var configuracion = A.Fake<IConfiguracion>();
            return new CanalExternoPedidosAmazon(configuracion, A.Fake<Nesto.Modulos.CanalesExternos.Interfaces.IClientesPorTelefonoService>());
        }

        private static OrderItem Linea(string sku, int cantidad, string precio, string portes = null)
        {
            return new OrderItem(
                aSIN: "B0" + sku,
                sellerSKU: sku,
                orderItemId: "OI" + sku,
                title: "Producto " + sku,
                quantityOrdered: cantidad,
                itemPrice: precio == null ? null : new Money { Amount = precio, CurrencyCode = "EUR" },
                shippingPrice: portes == null ? null : new Money { Amount = portes, CurrencyCode = "EUR" });
        }

        [TestMethod]
        public void TransformarLineas_UnaLineaAnuladaYOtraNormal_SoloPasaLaNormal()
        {
            var canal = CrearCanal();
            var lineasAmazon = new List<OrderItem>
            {
                Linea("17404", 0, "0.00"),
                Linea("12345", 2, "24.20", "3.63")
            };

            var lineas = canal.TransformarLineas(lineasAmazon, "ALG", "G21", "EUR");

            var productos = lineas.Where(l => l.tipoLinea == 1).ToList();
            Assert.AreEqual(1, productos.Count);
            Assert.AreEqual("12345", productos[0].Producto);
            Assert.AreEqual((short)2, productos[0].Cantidad);
            Assert.IsFalse(lineas.Any(l => l.Cantidad == 0), "No puede quedar ninguna línea con cantidad 0");
            Assert.AreEqual(1, lineas.Count(l => l.tipoLinea == 2), "Los portes de la línea normal se mantienen");
        }

        [TestMethod]
        public void TransformarLineas_PackConCantidadCero_NoPasa()
        {
            var canal = CrearCanal();
            var lineasAmazon = new List<OrderItem>
            {
                Linea("17404x3", 0, null),
                Linea("12345", 1, "12.10")
            };

            var lineas = canal.TransformarLineas(lineasAmazon, "ALG", "G21", "EUR");

            Assert.AreEqual(1, lineas.Count);
            Assert.AreEqual("12345", lineas[0].Producto);
        }

        [TestMethod]
        public void TransformarLineas_TodasAnuladas_NoDevuelveNingunaLineaYSeAvisa()
        {
            var canal = CrearCanal();
            var lineasAmazon = new List<OrderItem>
            {
                Linea("17404", 0, "0.00"),
                Linea("12345", 0, null)
            };

            var lineas = canal.TransformarLineas(lineasAmazon, "ALG", "G21", "EUR");

            Assert.AreEqual(0, lineas.Count);
            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => CanalExternoPedidosAmazon.ComprobarQueTieneLineas(lineas, "408-1234567-1234567"));
            Assert.AreEqual("El pedido de Amazon 408-1234567-1234567 no tiene ninguna línea con cantidad: lo anuló el cliente.", ex.Message);
        }

        [TestMethod]
        public void TransformarLineas_SinLineasAnuladas_IgualQueAntes()
        {
            var canal = CrearCanal();
            var lineasAmazon = new List<OrderItem>
            {
                Linea("17404", 1, "12.10"),
                Linea("12345", 2, "24.20", "3.63")
            };

            var lineas = canal.TransformarLineas(lineasAmazon, "ALG", "G21", "EUR");

            Assert.AreEqual(3, lineas.Count);
            Assert.AreEqual("17404", lineas[0].Producto);
            Assert.AreEqual((short)1, lineas[0].Cantidad);
            Assert.AreEqual(10M, lineas[0].PrecioUnitario);
            Assert.AreEqual("12345", lineas[1].Producto);
            Assert.AreEqual((short)2, lineas[1].Cantidad);
            Assert.AreEqual(10M, lineas[1].PrecioUnitario);
            Assert.AreEqual("62400003", lineas[2].Producto);
            Assert.AreEqual(3M, lineas[2].PrecioUnitario);
            CanalExternoPedidosAmazon.ComprobarQueTieneLineas(lineas, "408-1234567-1234567");
        }

        [TestMethod]
        public void ComprobarQueTieneLineas_SoloPortes_SeAvisa()
        {
            var lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { tipoLinea = 2, Producto = "62400003", Cantidad = 1, PrecioUnitario = 3 }
            };

            Assert.ThrowsException<InvalidOperationException>(
                () => CanalExternoPedidosAmazon.ComprobarQueTieneLineas(lineas, "FBA 408-1234567-1234567"));
        }
    }
}
