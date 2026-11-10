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

        #region Issue #328: Casos reales con comisión contrarreembolso y muestras

        private static void AñadirLineaComisionReembolso(List<LineaPedidoVentaDTO> lineas, decimal importe = 3M)
        {
            lineas.Add(new LineaPedidoVentaDTO
            {
                tipoLinea = 2, // Cuenta contable (EsCuentaContable lo detecta en GetLineas)
                PrecioUnitario = importe,
                Cantidad = 1,
                Producto = "62400003",
                texto = "COMISIÓN POR CONTRARREEMBOLSO",
                AplicarDescuento = false,
                almacen = "ALG",
                delegacion = "ALG",
                formaVenta = "WEB",
                estado = 1,
                fechaEntrega = DateTime.Today,
                iva = "G21",
                Usuario = "test"
            });
        }

        // Caso real: 80,73€ en productos + 3€ comisión reembolso + descuento 12,11€ (15% de 80,73)
        // Prestashop reporta total_products = 83,73 (incluye comisión)
        // 15% de 83,73 = 12,56 ≠ 12,11 → DetectarPorcentajeConocido falla
        [TestMethod]
        public void AplicarDescuentoCupon_ConComisionReembolso_Detecta15Porciento()
        {
            var lineas = CrearLineasProducto(
                (27.55M, 1), (6.21M, 1), (3.95M, 1),
                (5.60M, 1), (2.42M, 1), (35.00M, 1));
            AñadirLineaComisionReembolso(lineas, 3.00M);

            // total_products de Prestashop = 83,73 (incluye comisión)
            // pero el descuento real es 15% de 80,73 = 12,11
            Aplicar(lineas, totalDescuentosSinIva: 12.11M, totalProductosSinIva: 83.73M,
                totalDescuentosConIva: 14.65M);

            var lineasProducto = lineas.Where(l => l.tipoLinea == 1 && l.Producto != "62400003").ToList();
            foreach (var linea in lineasProducto)
            {
                Assert.AreEqual(0.15M, linea.DescuentoLinea,
                    $"Producto {linea.Producto} debería tener 15% de descuento");
            }

            // La comisión no debe tener descuento
            var lineaComision = lineas.Single(l => l.Producto == "62400003");
            Assert.AreEqual(0M, lineaComision.DescuentoLinea,
                "La comisión por contrarreembolso no debe tener descuento");

            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"),
                "No debe crear línea TICKET cuando es un porcentaje conocido");
        }

        // Caso real: 222,30€ con muestras (precio 0) + descuento 33,35€ (15%)
        [TestMethod]
        public void AplicarDescuentoCupon_ConMuestrasPrecioCero_Detecta15Porciento()
        {
            var lineas = CrearLineasProducto(
                (4.50M, 1), (9.90M, 1), (42.15M, 1), (18.65M, 1),
                (32.95M, 1), (72.35M, 1), (37.30M, 1), (4.50M, 1));
            // Muestras con precio 0
            lineas.AddRange(CrearLineasProducto((0M, 1), (0M, 1), (0M, 1)));

            // 15% de 222,30 = 33,345 → redondeado = 33,35
            Aplicar(lineas, totalDescuentosSinIva: 33.35M, totalProductosSinIva: 222.30M,
                totalDescuentosConIva: 40.35M);

            var lineasConPrecio = lineas.Where(l => l.tipoLinea == 1 && l.PrecioUnitario > 0).ToList();
            foreach (var linea in lineasConPrecio)
            {
                Assert.AreEqual(0.15M, linea.DescuentoLinea,
                    $"Producto con precio {linea.PrecioUnitario} debería tener 15% de descuento");
            }

            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"),
                "No debe crear línea TICKET");
        }

        // Caso real: 47,52€ producto único + descuento 7,13€ (15%)
        [TestMethod]
        public void AplicarDescuentoCupon_ProductoUnico_Detecta15Porciento()
        {
            var lineas = CrearLineasProducto((47.52M, 1));

            // 15% de 47,52 = 7,128 → redondeado = 7,13
            Aplicar(lineas, totalDescuentosSinIva: 7.13M, totalProductosSinIva: 47.52M,
                totalDescuentosConIva: 8.63M);

            Assert.AreEqual(0.15M, lineas[0].DescuentoLinea);
            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"));
        }

        // La detección debe funcionar calculando sobre líneas descontables,
        // no sobre total_products de Prestashop
        [TestMethod]
        public void DetectarPorcentaje_ConComisionInflandoTotal_DebeDetectar15()
        {
            // total_products de Prestashop = 83,73 (incluye 3€ de comisión)
            // Descuento real = 12,11 = 15% de 80,73 (sin comisión)
            // Con el total inflado: 15% de 83,73 = 12,56 ≠ 12,11
            // El método debe ser capaz de detectar que es 15% de los productos reales

            // Este test verifica que necesitamos calcular sobre las líneas, no sobre total_products
            decimal resultado = CanalExternoPedidosPrestashopNuevaVision.DetectarPorcentajeConocido(12.11M, 80.73M);
            Assert.AreEqual(15M, resultado, "15% de 80,73 = 12,11 debe detectarse");

            // Y que con el total inflado NO detecta (situación actual, documentamos el bug)
            decimal resultadoInflado = CanalExternoPedidosPrestashopNuevaVision.DetectarPorcentajeConocido(12.11M, 83.73M);
            Assert.AreEqual(0M, resultadoInflado, "Con total inflado no detecta el porcentaje");
        }

        #endregion

        #region Issue #350: cupón = importe exacto de una línea (producto regalo)

        // Caso real pedido 915207: el cupón coincide exactamente con el precio de un producto
        // (PACK PROTOCOLO 846,43 €), que va gratis junto con el MULTITERAPIA (1.645 €).
        [TestMethod]
        public void AplicarDescuentoCupon_CasoReal_Pedido915207_RegaloEnLineaCoincidente()
        {
            var lineas = CrearLineasProducto((1645M, 1), (846.43M, 1));

            // 846,43 sobre 2.491,43 ≈ 33,97% → no es porcentaje conocido → detección por línea
            Aplicar(lineas, totalDescuentosSinIva: 846.43M, totalProductosSinIva: 2491.43M,
                totalDescuentosConIva: 1024.18M);

            Assert.AreEqual(2, lineas.Count, "No debe añadir línea TICKET");
            Assert.AreEqual(0M, lineas[0].DescuentoLinea, "El producto comprado no lleva descuento");
            Assert.AreEqual(1M, lineas[1].DescuentoLinea, "El producto regalo lleva 100% de descuento");
            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"));
        }

        [TestMethod]
        public void AplicarDescuentoCupon_CuponCoincideConVariasLineas_DejaTicket()
        {
            // Dos líneas con el mismo importe que el cupón: ambiguo (no sabemos cuál es el regalo) →
            // se mantiene como TICKET, sin tocar descuentos.
            var lineas = CrearLineasProducto((50M, 1), (50M, 1));

            Aplicar(lineas, totalDescuentosSinIva: 50M, totalProductosSinIva: 100M,
                totalDescuentosConIva: 60.50M);

            Assert.IsTrue(lineas.Any(l => l.Producto == "TiCKET"), "Caso ambiguo → línea TICKET");
            Assert.IsFalse(lineas.Where(l => l.tipoLinea == 1 && l.Producto != "TiCKET").Any(l => l.DescuentoLinea == 1M),
                "Ninguna línea debe quedar con 100% en el caso ambiguo");
        }

        [TestMethod]
        public void AplicarDescuentoCupon_PedidoEnteroGratis_Detecta100Porciento()
        {
            // 100 ya está en PORCENTAJES_CONOCIDOS: un cupón igual al total del pedido → todo gratis.
            var lineas = CrearLineasProducto((100M, 1), (40M, 2));

            Aplicar(lineas, totalDescuentosSinIva: 180M, totalProductosSinIva: 180M,
                totalDescuentosConIva: 217.80M);

            Assert.IsTrue(lineas.All(l => l.DescuentoLinea == 1M), "Todas las líneas al 100%");
            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"));
        }

        [TestMethod]
        public void AplicarRegaloLineaCompleta_UnaLineaCoincide_Aplica100YDevuelveTrue()
        {
            var lineas = CrearLineasProducto((1645M, 1), (846.43M, 1));

            bool aplicado = CanalExternoPedidosPrestashopNuevaVision.AplicarRegaloLineaCompleta(lineas, 846.43M);

            Assert.IsTrue(aplicado);
            Assert.AreEqual(1M, lineas[1].DescuentoLinea);
        }

        [TestMethod]
        public void AplicarRegaloLineaCompleta_CuponCero_DevuelveFalse()
        {
            var lineas = CrearLineasProducto((100M, 1));
            Assert.IsFalse(CanalExternoPedidosPrestashopNuevaVision.AplicarRegaloLineaCompleta(lineas, 0M));
        }

        [TestMethod]
        public void AplicarRegaloLineaCompleta_NingunaCoincide_DevuelveFalse()
        {
            var lineas = CrearLineasProducto((100M, 1), (50M, 1));
            Assert.IsFalse(CanalExternoPedidosPrestashopNuevaVision.AplicarRegaloLineaCompleta(lineas, 33.33M));
        }

        #endregion

        #region Feria online 11/26: N+M de la tienda como regalo (forma de la plantilla)

        private static List<LineaPedidoVentaDTO> Lineas(params (string producto, decimal precio, int cantidad)[] productos)
        {
            var lineas = new List<LineaPedidoVentaDTO>();
            foreach (var (producto, precio, cantidad) in productos)
            {
                lineas.Add(new LineaPedidoVentaDTO
                {
                    tipoLinea = 1,
                    Producto = producto,
                    texto = "PRODUCTO " + producto,
                    PrecioUnitario = precio,
                    Cantidad = cantidad,
                    PorcentajeIva = .21M,
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

        private static string AplicarCon(List<LineaPedidoVentaDTO> lineas, decimal descuentoSinIva, decimal? descuentoConIva = null, string iva = "G21")
        {
            decimal totalProductos = lineas.Where(l => l.tipoLinea == 1).Sum(l => l.PrecioUnitario * l.Cantidad);
            return CanalExternoPedidosPrestashopNuevaVision.AplicarDescuentoCupon(
                lineas, descuentoSinIva, totalProductos, descuentoConIva ?? Math.Round(descuentoSinIva * 1.21M, 2, MidpointRounding.AwayFromZero),
                formaVenta: "WEB", iva: iva, usuario: "test");
        }

        private static void AssertRegalo(List<LineaPedidoVentaDTO> lineas, string producto, int pagadas, int regalo, decimal precio)
        {
            var delProducto = lineas.Where(l => l.Producto == producto).ToList();
            Assert.AreEqual(2, delProducto.Count, $"{producto}: línea de pago y línea de regalo");
            var pago = delProducto.Single(l => l.PrecioUnitario != 0);
            var gratis = delProducto.Single(l => l.PrecioUnitario == 0);
            Assert.AreEqual(pagadas, pago.Cantidad);
            Assert.AreEqual(precio, pago.PrecioUnitario);
            Assert.AreEqual(0M, pago.DescuentoLinea);
            Assert.AreEqual(regalo, gratis.Cantidad);
            Assert.AreEqual(0M, gratis.DescuentoLinea);
            Assert.AreEqual(0M, gratis.DescuentoProducto);
            Assert.IsFalse(gratis.AplicarDescuento);
            Assert.AreEqual("WEB", gratis.formaVenta);
            Assert.AreEqual(pago.texto, gratis.texto);
            Assert.IsNotNull(pago.oferta);
            Assert.AreEqual(pago.oferta, gratis.oferta, "Las dos líneas del N+M comparten número de oferta, como en la plantilla");
            Assert.AreEqual(lineas.IndexOf(pago) + 1, lineas.IndexOf(gratis), "La de regalo va justo detrás de la de pago");
        }

        [TestMethod]
        public void RegaloNM_SeisMasUnoEnLineaDeSiete_EntraComoRegalo()
        {
            var lineas = Lineas(("P1", 10M, 7));

            string aviso = AplicarCon(lineas, 10M);

            Assert.IsNull(aviso);
            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"));
            AssertRegalo(lineas, "P1", 6, 1, 10M);
        }

        [TestMethod]
        public void RegaloNM_OchoMasDosEnLineaDeDiez_EntraComoRegaloYNoComoVeintePorCiento()
        {
            var lineas = Lineas(("P1", 10M, 10));

            AplicarCon(lineas, 20M); // 20 % del pedido: parece porcentaje conocido, pero es un 8+2

            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"));
            AssertRegalo(lineas, "P1", 8, 2, 10M);
        }

        [TestMethod]
        public void RegaloNM_Multiplo_CatorceUnidadesDosDeRegalo()
        {
            var lineas = Lineas(("P1", 10M, 14));

            AplicarCon(lineas, 20M);

            AssertRegalo(lineas, "P1", 12, 2, 10M);
        }

        [TestMethod]
        public void RegaloNM_AceitesMezclados_RegaloDeLaUnicaLineaQueCuadra()
        {
            var lineas = Lineas(("ACEITE_A", 12M, 2), ("ACEITE_B", 9M, 2));

            AplicarCon(lineas, 9M);

            Assert.AreEqual(2, lineas.Count(l => l.Producto == "ACEITE_A" || l.Producto == "ACEITE_B") - 1);
            Assert.AreEqual(1, lineas.Count(l => l.Producto == "ACEITE_A"), "El A no se toca");
            Assert.IsNull(lineas.Single(l => l.Producto == "ACEITE_A").oferta);
            AssertRegalo(lineas, "ACEITE_B", 1, 1, 9M);
        }

        [TestMethod]
        public void RegaloNM_VariasLineasDelMismoPrecio_EligeLaPrimera()
        {
            var lineas = Lineas(("ACEITE_A", 10M, 2), ("ACEITE_B", 10M, 3));

            AplicarCon(lineas, 10M);

            AssertRegalo(lineas, "ACEITE_A", 1, 1, 10M);
            Assert.AreEqual(1, lineas.Count(l => l.Producto == "ACEITE_B"));
            Assert.AreEqual(3, lineas.Single(l => l.Producto == "ACEITE_B").Cantidad);
        }

        [TestMethod]
        public void RegaloNM_DosOfertasDistintas_CadaUnaConSuNumeroDeOferta()
        {
            var lineas = Lineas(("P1", 10M, 7), ("P2", 15M, 7));

            AplicarCon(lineas, 25M);

            AssertRegalo(lineas, "P1", 6, 1, 10M);
            AssertRegalo(lineas, "P2", 6, 1, 15M);
            Assert.AreNotEqual(lineas.First(l => l.Producto == "P1").oferta, lineas.First(l => l.Producto == "P2").oferta);
            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"));
        }

        [TestMethod]
        public void RegaloNM_Ambiguo_DejaTicketYAvisa()
        {
            // 30 € = 1 de 10 + 1 de 20, o 2 de 15: dos soluciones igual de buenas
            var lineas = Lineas(("P1", 10M, 4), ("P2", 20M, 3), ("P3", 15M, 3));

            string aviso = AplicarCon(lineas, 30M, 36.30M);

            Assert.IsTrue(lineas.Any(l => l.Producto == "TiCKET"), "Respaldo: TiCKET como hoy");
            Assert.IsFalse(lineas.Any(l => l.oferta != null));
            Assert.AreEqual(3, lineas.Count(l => l.Producto != "TiCKET"));
            Assert.IsNotNull(aviso);
            StringAssert.Contains(aviso, "revise el regalo");
        }

        [TestMethod]
        public void RegaloNM_NoCuadra_DejaTicketSinAviso()
        {
            var lineas = Lineas(("P1", 10M, 7));

            string aviso = AplicarCon(lineas, 7.33M, 8.87M);

            Assert.IsTrue(lineas.Any(l => l.Producto == "TiCKET"));
            Assert.AreEqual(7, lineas.Single(l => l.Producto == "P1").Cantidad);
            Assert.IsNull(aviso);
        }

        [TestMethod]
        public void RegaloNM_ConCentimosDeRedondeoDePhp_EntraComoRegalo()
        {
            // 9,95 € IVA incl. → 8,2231 sin IVA; la tienda descuenta 8,22
            var lineas = Lineas(("P1", 8.2231M, 7));

            AplicarCon(lineas, 8.22M, 9.95M);

            AssertRegalo(lineas, "P1", 6, 1, 8.2231M);
        }

        [TestMethod]
        public void RegaloNM_PreciosConIva_CuadraConElDescuentoConIva()
        {
            // Sin IVA en el pedido (pedidoSalida.iva == null): las líneas van con IVA y no llevan PorcentajeIva
            var lineas = Lineas(("P1", 12.10M, 7));
            lineas[0].PorcentajeIva = 0;

            AplicarCon(lineas, 10M, 12.10M, iva: null);

            AssertRegalo(lineas, "P1", 6, 1, 12.10M);
        }

        [TestMethod]
        public void RegaloNM_VoucherQuincePorCiento_SigueSiendoPorcentaje()
        {
            // 15 % de 20 × 10 € = 30 € = 3 unidades, pero el 15 % es un voucher de la tienda
            var lineas = Lineas(("P1", 10M, 20));

            AplicarCon(lineas, 30M);

            Assert.AreEqual(1, lineas.Count);
            Assert.AreEqual(.15M, lineas[0].DescuentoLinea);
            Assert.IsNull(lineas[0].oferta);
        }

        [TestMethod]
        public void RegaloNM_LineaEnteraSigueSiendoNesto350()
        {
            var lineas = Lineas(("P1", 20M, 3), ("P2", 10M, 1));

            AplicarCon(lineas, 10M);

            Assert.AreEqual(2, lineas.Count, "Nesto#350: la línea entera de 10 € va al 100 %, sin partir nada");
            Assert.AreEqual(1M, lineas[1].DescuentoLinea);
            Assert.AreEqual(0M, lineas[0].DescuentoLinea);
        }

        [TestMethod]
        public void RegaloNM_PortesNoEntranEnElRegalo()
        {
            var lineas = Lineas(("P1", 10M, 7));
            AñadirLineaPortes(lineas, 10M);

            AplicarCon(lineas, 10M);

            AssertRegalo(lineas, "P1", 6, 1, 10M);
            Assert.AreEqual(1, lineas.Count(l => l.tipoLinea == 2));
        }

        #endregion
    }
}

