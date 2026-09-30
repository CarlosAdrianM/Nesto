using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PlantillaVenta;
using System;
using System.Collections.Generic;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#505 (sugerencia de Paloma, 30/09/26): al mandar el cobro por tarjeta se avisa de lo que el
    /// cliente tiene a su favor, pero no se descuenta solo: lo tiene que confirmar el usuario.
    /// </summary>
    [TestClass]
    public class SaldoAFavorClienteTests
    {
        private static SaldoAFavorCliente Saldo(decimal total, string cliente = "15191")
        {
            return new SaldoAFavorCliente { Total = total, Cliente = cliente };
        }

        [TestMethod]
        public void SaldoADescontar_SinMarcarLaCasilla_NoSeDescuentaNada()
        {
            // Lo decidido por Carlos: sin confirmación expresa, el enlace sale por el total, como siempre
            Assert.AreEqual(0m, SaldoAFavorCliente.SaldoADescontar(Saldo(42.50m), false, "15191"));
        }

        [TestMethod]
        public void SaldoADescontar_MarcadaYDelMismoCliente_ElSaldoEntero()
        {
            Assert.AreEqual(42.50m, SaldoAFavorCliente.SaldoADescontar(Saldo(42.50m, "15191 "), true, " 15191"));
        }

        [TestMethod]
        public void SaldoADescontar_ElSaldoEsDeOtroCliente_NoSeDescuenta()
        {
            // Se marcó la casilla y luego se cambió de cliente en la plantilla
            Assert.AreEqual(0m, SaldoAFavorCliente.SaldoADescontar(Saldo(42.50m, "15191"), true, "20000"));
        }

        [TestMethod]
        public void SaldoADescontar_SinSaldo_Cero()
        {
            Assert.AreEqual(0m, SaldoAFavorCliente.SaldoADescontar(null, true, "15191"));
            Assert.AreEqual(0m, SaldoAFavorCliente.SaldoADescontar(Saldo(0m), true, "15191"));
        }

        [TestMethod]
        public void ImporteDelEnlace_DescuentaElSaldoYNuncaBajaDeCero()
        {
            Assert.AreEqual(57.50m, SaldoAFavorCliente.ImporteDelEnlace(100m, 42.50m));
            Assert.AreEqual(100m, SaldoAFavorCliente.ImporteDelEnlace(100m, 0m));
            Assert.AreEqual(0m, SaldoAFavorCliente.ImporteDelEnlace(30m, 42.50m), "El saldo cubre el pedido: no se manda enlace");
            Assert.AreEqual(100m, SaldoAFavorCliente.ImporteDelEnlace(100m, -5m), "Un saldo negativo no sube el importe");
        }

        [TestMethod]
        public void Textos_DicenElImporteYDeQueEsCadaMovimiento()
        {
            var saldo = new SaldoAFavorCliente
            {
                Total = 42.50m,
                PendienteDePago = 80m,
                Movimientos = new List<MovimientoAFavor>
                {
                    new MovimientoAFavor { Fecha = new DateTime(2026, 9, 12), Concepto = "Entrega a cuenta ", Documento = "926100", Importe = 30m },
                    new MovimientoAFavor { Fecha = new DateTime(2026, 8, 1), Concepto = " ", Documento = "RV2600046", Importe = 12.50m }
                }
            };

            Assert.IsTrue(saldo.HayAlgoAFavor);
            StringAssert.Contains(saldo.Resumen, "42,50");
            StringAssert.Contains(saldo.TextoDescontar, "42,50");
            Assert.IsTrue(saldo.TieneAvisoPendienteDePago);
            StringAssert.Contains(saldo.AvisoPendienteDePago, "80,00");
            StringAssert.StartsWith(saldo.Movimientos[0].Texto, "12/09/26 · Entrega a cuenta · 30,00");
            StringAssert.StartsWith(saldo.Movimientos[1].Texto, "01/08/26 · RV2600046 · 12,50", "Sin concepto, el documento");
        }

        [TestMethod]
        public void SinNadaAFavor_NoHayAvisos()
        {
            var saldo = new SaldoAFavorCliente { Total = 0m, PendienteDePago = 0m };

            Assert.IsFalse(saldo.HayAlgoAFavor);
            Assert.IsFalse(saldo.TieneAvisoPendienteDePago);
            Assert.AreEqual(string.Empty, saldo.AvisoPendienteDePago);
        }
    }
}
