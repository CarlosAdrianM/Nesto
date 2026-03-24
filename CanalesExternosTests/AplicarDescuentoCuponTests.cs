using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Models;
using Nesto.Models.Nesto.Models;
using Nesto.Modulos.CanalesExternos;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CanalesExternosTests
{
    [TestClass]
    public class AplicarDescuentoCuponTests
    {
        private static List<LineaPedidoVentaDTO> CrearLineasProducto(params (decimal precio, short cantidad)[] productos)
        {
            var lineas = new List<LineaPedidoVentaDTO>();
            foreach (var (precio, cantidad) in productos)
            {
                lineas.Add(new LineaPedidoVentaDTO
                {
                    tipoLinea = 1,
                    PrecioUnitario = precio,
                    Cantidad = cantidad,
                    almacen = "ALG",
                    delegacion = "ALG",
                    formaVenta = "WEB",
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = "G21",
                    Usuario = "test"
                });
            }
            return lineas;
        }

        private static void AñadirLineaPortes(List<LineaPedidoVentaDTO> lineas, decimal importe = 3M)
        {
            lineas.Add(new LineaPedidoVentaDTO
            {
                tipoLinea = 2,
                PrecioUnitario = importe,
                Cantidad = 1,
                Producto = "62400003",
                texto = "GASTOS DE TRANSPORTE",
                almacen = "ALG",
                delegacion = "ALG",
                formaVenta = "WEB",
                estado = 1,
                fechaEntrega = DateTime.Today,
                iva = "G21",
                Usuario = "test"
            });
        }

        private static void Aplicar(
            List<LineaPedidoVentaDTO> lineas,
            decimal totalDescuentosSinIva,
            decimal totalProductosSinIva,
            decimal totalDescuentosConIva = 0,
            string iva = "G21")
        {
            if (totalDescuentosConIva == 0)
            {
                totalDescuentosConIva = Math.Round(totalDescuentosSinIva * 1.21M, 2, MidpointRounding.AwayFromZero);
            }
            CanalExternoPedidosPrestashopNuevaVision.AplicarDescuentoCupon(
                lineas, totalDescuentosSinIva, totalProductosSinIva, totalDescuentosConIva,
                formaVenta: "WEB", iva: iva, usuario: "test");
        }

        #region DetectarPorcentajeConocido

        [TestMethod]
        public void DetectarPorcentaje_15PorcientoDe321_Detecta15()
        {
            // 321 * 15% = 48.15
            decimal resultado = CanalExternoPedidosPrestashopNuevaVision.DetectarPorcentajeConocido(48.15M, 321M);
            Assert.AreEqual(15M, resultado);
        }

        [TestMethod]
        public void DetectarPorcentaje_15PorcientoDe77punto80_Detecta15()
        {
            // 77.80 * 15% = 11.67
            decimal resultado = CanalExternoPedidosPrestashopNuevaVision.DetectarPorcentajeConocido(11.67M, 77.80M);
            Assert.AreEqual(15M, resultado);
        }

        [TestMethod]
        public void DetectarPorcentaje_10PorcientoDe50_Detecta10()
        {
            decimal resultado = CanalExternoPedidosPrestashopNuevaVision.DetectarPorcentajeConocido(5M, 50M);
            Assert.AreEqual(10M, resultado);
        }

        [TestMethod]
        public void DetectarPorcentaje_ImporteFijoNoCoincide_Devuelve0()
        {
            // 7.50€ de descuento en un pedido de 83.20€ → no coincide con ningún porcentaje
            decimal resultado = CanalExternoPedidosPrestashopNuevaVision.DetectarPorcentajeConocido(7.50M, 83.20M);
            Assert.AreEqual(0M, resultado);
        }

        [TestMethod]
        public void DetectarPorcentaje_ProductosCero_Devuelve0()
        {
            decimal resultado = CanalExternoPedidosPrestashopNuevaVision.DetectarPorcentajeConocido(5M, 0M);
            Assert.AreEqual(0M, resultado);
        }

        #endregion

        #region Descuento porcentual

        [TestMethod]
        public void AplicarDescuentoCupon_Porcentual15_DistribuyeDescuentoEnLineas()
        {
            var lineas = CrearLineasProducto((100M, 1), (50M, 2));

            Aplicar(lineas, totalDescuentosSinIva: 30M, totalProductosSinIva: 200M);

            Assert.AreEqual(2, lineas.Count, "No debe añadir línea TICKET");
            Assert.AreEqual(0.15M, lineas[0].DescuentoLinea);
            Assert.AreEqual(0.15M, lineas[1].DescuentoLinea);
        }

        [TestMethod]
        public void AplicarDescuentoCupon_Porcentual_NoAñadeLineaTicket()
        {
            var lineas = CrearLineasProducto((80M, 1));

            Aplicar(lineas, totalDescuentosSinIva: 8M, totalProductosSinIva: 80M);

            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"));
        }

        [TestMethod]
        public void AplicarDescuentoCupon_Porcentual_NoAfectaPortes()
        {
            var lineas = CrearLineasProducto((100M, 1));
            AñadirLineaPortes(lineas, 5M);

            Aplicar(lineas, totalDescuentosSinIva: 10M, totalProductosSinIva: 100M);

            var lineaPortes = lineas.Single(l => l.tipoLinea == 2);
            Assert.AreEqual(0M, lineaPortes.DescuentoLinea, "Los portes no deben tener descuento");
            Assert.AreEqual(2, lineas.Count, "No debe añadir línea TICKET");
        }

        // Caso real pedido 19940: 30 x 10.70€ = 321€ base, descuento 48.15€ base
        [TestMethod]
        public void AplicarDescuentoCupon_CasoReal_Pedido19940_Detecta15Porciento()
        {
            var lineas = CrearLineasProducto((10.70M, 30));

            Aplicar(lineas, totalDescuentosSinIva: 48.15M, totalProductosSinIva: 321M,
                totalDescuentosConIva: 58.28M);

            Assert.AreEqual(1, lineas.Count, "No debe añadir línea TICKET");
            Assert.AreEqual(0.15M, lineas[0].DescuentoLinea);
        }

        // Caso real: productos con portes, descuento 15%
        [TestMethod]
        public void AplicarDescuentoCupon_CasoReal_ProductosConPortes_Detecta15Porciento()
        {
            var lineas = CrearLineasProducto((24.83M, 1), (20.74M, 1), (32.23M, 1));
            AñadirLineaPortes(lineas, 2.48M);

            // 77.80 * 15% = 11.67
            Aplicar(lineas, totalDescuentosSinIva: 11.67M, totalProductosSinIva: 77.80M,
                totalDescuentosConIva: 14.12M);

            var lineasProducto = lineas.Where(l => l.tipoLinea == 1).ToList();
            foreach (var linea in lineasProducto)
            {
                Assert.AreEqual(0.15M, linea.DescuentoLinea, "Debe detectar 15%");
            }

            Assert.AreEqual(0M, lineas.Single(l => l.tipoLinea == 2).DescuentoLinea, "Portes sin descuento");
            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"), "No debe haber TICKET");
        }

        [TestMethod]
        public void AplicarDescuentoCupon_Porcentual5Porciento_AplicaCorrectamente()
        {
            var lineas = CrearLineasProducto((200M, 1));

            Aplicar(lineas, totalDescuentosSinIva: 10M, totalProductosSinIva: 200M);

            Assert.AreEqual(0.05M, lineas[0].DescuentoLinea);
        }

        #endregion

        #region Descuento fijo (fallback a TICKET)

        [TestMethod]
        public void AplicarDescuentoCupon_Fijo_AñadeLineaTicket()
        {
            var lineas = CrearLineasProducto((100M, 1), (50M, 1));

            // 7.33€ no coincide con ningún porcentaje conocido de 150€
            Aplicar(lineas, totalDescuentosSinIva: 7.33M, totalProductosSinIva: 150M,
                totalDescuentosConIva: 8.87M);

            Assert.AreEqual(3, lineas.Count, "Debe añadir línea TICKET");
            var lineaTicket = lineas.Single(l => l.Producto == "TiCKET");
            Assert.AreEqual(-1, lineaTicket.Cantidad);
        }

        [TestMethod]
        public void AplicarDescuentoCupon_Fijo_NoModificaDescuentoDeLineas()
        {
            var lineas = CrearLineasProducto((100M, 1));

            Aplicar(lineas, totalDescuentosSinIva: 4.13M, totalProductosSinIva: 100M,
                totalDescuentosConIva: 5M);

            Assert.AreEqual(0M, lineas[0].DescuentoLinea);
        }

        [TestMethod]
        public void AplicarDescuentoCupon_Fijo_ConIva_DivideBaseImponible()
        {
            var lineas = CrearLineasProducto((100M, 1));

            // 8.33€ no coincide con ningún porcentaje conocido de 100€
            Aplicar(lineas, totalDescuentosSinIva: 8.33M, totalProductosSinIva: 100M,
                totalDescuentosConIva: 10.08M);

            var lineaTicket = lineas.Single(l => l.Producto == "TiCKET");
            Assert.AreEqual(10.08M / 1.21M, lineaTicket.PrecioUnitario);
            Assert.AreEqual(.21M, lineaTicket.PorcentajeIva);
        }

        [TestMethod]
        public void AplicarDescuentoCupon_Fijo_SinIva_NoDividePrecio()
        {
            var lineas = CrearLineasProducto((100M, 1));

            // 3.33€ no coincide con ningún porcentaje conocido de 100€
            CanalExternoPedidosPrestashopNuevaVision.AplicarDescuentoCupon(
                lineas, totalDescuentosSinIva: 3.33M, totalProductosSinIva: 100M,
                totalDescuentosConIva: 3.33M,
                formaVenta: "WEB", iva: null, usuario: "test");

            var lineaTicket = lineas.Single(l => l.Producto == "TiCKET");
            Assert.AreEqual(3.33M, lineaTicket.PrecioUnitario);
        }

        [TestMethod]
        public void AplicarDescuentoCupon_Fijo_ConPortes_PortesSinDescuento()
        {
            var lineas = CrearLineasProducto((100M, 1));
            AñadirLineaPortes(lineas, 5M);

            Aplicar(lineas, totalDescuentosSinIva: 8.26M, totalProductosSinIva: 100M,
                totalDescuentosConIva: 10M);

            var lineaPortes = lineas.Single(l => l.tipoLinea == 2);
            Assert.AreEqual(0M, lineaPortes.DescuentoLinea);
        }

        #endregion
    }
}
