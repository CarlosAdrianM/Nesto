using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.CanalesExternos.Models;
using Nesto.Modulos.CanalesExternos.ViewModels;
using System.Collections.ObjectModel;
using System.Linq;

namespace CanalesExternosTests
{
    [TestClass]
    public class CanalesExternosPagosViewModelTests
    {
        private PagoCanalExterno CrearPagoEUR(decimal importe = 100M)
        {
            return new PagoCanalExterno
            {
                PagoExternalId = "TEST1234567890123456",
                MonedaOriginal = "EUR",
                Importe = importe,
                ImporteRecibidoBanco = 0,
                FechaPago = new System.DateTime(2026, 3, 15),
                DetallesPago = new ObservableCollection<DetallePagoCanalExterno>
                {
                    new DetallePagoCanalExterno
                    {
                        ExternalId = "ORDER-001",
                        CuentaContablePago = "55500047",
                        CuentaContableComisiones = "55500062",
                        Importe = 120M,
                        Comisiones = -20M
                    }
                },
                Comision = 0,
                Publicidad = 0,
                AjusteRetencion = 0,
                RestoAjustes = 0
            };
        }

        private PagoCanalExterno CrearPagoSEK()
        {
            return new PagoCanalExterno
            {
                PagoExternalId = "TEST_SEK_67890123456",
                MonedaOriginal = "SEK",
                Importe = 87M, // 1000 SEK * 0.087
                ImporteOriginal = 1000M,
                CambioDivisas = 0.087M,
                ImporteRecibidoBanco = 87M,
                FechaPago = new System.DateTime(2026, 3, 15),
                DetallesPago = new ObservableCollection<DetallePagoCanalExterno>
                {
                    new DetallePagoCanalExterno
                    {
                        ExternalId = "ORDER-SE-001",
                        CuentaContablePago = "55500072",
                        CuentaContableComisiones = "55500073",
                        Importe = 100M,
                        Comisiones = -13M
                    }
                },
                Comision = 0,
                Publicidad = 0,
                AjusteRetencion = 0,
                RestoAjustes = 0
            };
        }

        // === Tests del modelo PagoCanalExterno ===

        [TestMethod]
        public void PagoCanalExterno_DiferenciaCambioDivisas_SiImporteRecibidoBancoEsCero_DevuelveCero()
        {
            var pago = CrearPagoEUR();
            pago.ImporteRecibidoBanco = 0;

            Assert.AreEqual(0, pago.DiferenciaCambioDivisas);
        }

        [TestMethod]
        public void PagoCanalExterno_DiferenciaCambioDivisas_SiImporteRecibidoIgualImporte_DevuelveCero()
        {
            var pago = CrearPagoEUR(100M);
            pago.ImporteRecibidoBanco = 100M;

            Assert.AreEqual(0, pago.DiferenciaCambioDivisas);
        }

        [TestMethod]
        public void PagoCanalExterno_DiferenciaCambioDivisas_SiBancoRecibesMas_DevuelvePositivo()
        {
            var pago = CrearPagoSEK();
            pago.Importe = 87M;
            pago.ImporteRecibidoBanco = 89M;

            Assert.AreEqual(2M, pago.DiferenciaCambioDivisas);
        }

        [TestMethod]
        public void PagoCanalExterno_DiferenciaCambioDivisas_SiBancoRecibesMenos_DevuelveNegativo()
        {
            var pago = CrearPagoSEK();
            pago.Importe = 87M;
            pago.ImporteRecibidoBanco = 85M;

            Assert.AreEqual(-2M, pago.DiferenciaCambioDivisas);
        }

        // === Tests de GenerarApuntesContables: Asiento 1 básico ===

        [TestMethod]
        public void GenerarApuntes_PagoEUR_Asiento1_ProveedorHaberIgualImporte()
        {
            var pago = CrearPagoEUR(100M);

            var apuntes = CanalesExternosPagosViewModel.GenerarApuntesContables(pago);

            var proveedor = apuntes.First(a => a.Asiento == 1 && a.TipoCuenta == "3"); // PROVEEDOR
            Assert.AreEqual(100M, proveedor.Haber);
        }

        [TestMethod]
        public void GenerarApuntes_PagoEUR_Asiento1_BancoDebeIgualImporte()
        {
            var pago = CrearPagoEUR(100M);

            var apuntes = CanalesExternosPagosViewModel.GenerarApuntesContables(pago);

            var banco = apuntes.First(a => a.Asiento == 1 && a.Nº_Cuenta == "57200013");
            Assert.AreEqual(100M, banco.Debe);
        }

        [TestMethod]
        public void GenerarApuntes_PagoEUR_SinDiferenciaCambio_NoGeneraApunte668Ni768()
        {
            var pago = CrearPagoEUR(100M);

            var apuntes = CanalesExternosPagosViewModel.GenerarApuntesContables(pago);

            Assert.IsFalse(apuntes.Any(a => a.Nº_Cuenta == "66800001" || a.Nº_Cuenta == "76800000"));
        }

        [TestMethod]
        public void GenerarApuntes_ImporteCero_NoGeneraAsiento1()
        {
            var pago = CrearPagoEUR(0M);

            var apuntes = CanalesExternosPagosViewModel.GenerarApuntesContables(pago);

            Assert.IsFalse(apuntes.Any(a => a.Asiento == 1));
        }

        // === Tests de diferencia de cambio ===

        [TestMethod]
        public void GenerarApuntes_DiferenciaPositiva_BancoUsaImporteRecibido_Y_Genera768()
        {
            var pago = CrearPagoSEK();
            pago.Importe = 87M;
            pago.ImporteRecibidoBanco = 89M;

            var apuntes = CanalesExternosPagosViewModel.GenerarApuntesContables(pago);

            var banco = apuntes.First(a => a.Asiento == 1 && a.Nº_Cuenta == "57200013");
            Assert.AreEqual(89M, banco.Debe, "El banco debe reflejar el importe realmente recibido");

            var dif = apuntes.First(a => a.Nº_Cuenta == "76800000");
            Assert.AreEqual(2M, dif.Haber, "Diferencia positiva va al Haber de 768");
            Assert.AreEqual(0M, dif.Debe);
        }

        [TestMethod]
        public void GenerarApuntes_DiferenciaNegativa_BancoUsaImporteRecibido_Y_Genera668()
        {
            var pago = CrearPagoSEK();
            pago.Importe = 87M;
            pago.ImporteRecibidoBanco = 85M;

            var apuntes = CanalesExternosPagosViewModel.GenerarApuntesContables(pago);

            var banco = apuntes.First(a => a.Asiento == 1 && a.Nº_Cuenta == "57200013");
            Assert.AreEqual(85M, banco.Debe, "El banco debe reflejar el importe realmente recibido");

            var dif = apuntes.First(a => a.Nº_Cuenta == "66800001");
            Assert.AreEqual(2M, dif.Debe, "Diferencia negativa va al Debe de 668");
            Assert.AreEqual(0M, dif.Haber);
        }

        [TestMethod]
        public void GenerarApuntes_DiferenciaCambio_Asiento1Cuadra()
        {
            var pago = CrearPagoSEK();
            pago.Importe = 87M;
            pago.ImporteRecibidoBanco = 89M;

            var apuntes = CanalesExternosPagosViewModel.GenerarApuntesContables(pago);

            var asiento1 = apuntes.Where(a => a.Asiento == 1).ToList();
            decimal totalDebe = asiento1.Sum(a => a.Debe);
            decimal totalHaber = asiento1.Sum(a => a.Haber);
            Assert.AreEqual(totalDebe, totalHaber, "El asiento 1 debe cuadrar (Debe = Haber)");
        }

        [TestMethod]
        public void GenerarApuntes_DiferenciaNegativa_Asiento1Cuadra()
        {
            var pago = CrearPagoSEK();
            pago.Importe = 87M;
            pago.ImporteRecibidoBanco = 85M;

            var apuntes = CanalesExternosPagosViewModel.GenerarApuntesContables(pago);

            var asiento1 = apuntes.Where(a => a.Asiento == 1).ToList();
            decimal totalDebe = asiento1.Sum(a => a.Debe);
            decimal totalHaber = asiento1.Sum(a => a.Haber);
            Assert.AreEqual(totalDebe, totalHaber, "El asiento 1 debe cuadrar (Debe = Haber)");
        }

        [TestMethod]
        public void GenerarApuntes_ImporteRecibidoBancoCero_UsaImporteComoFallback()
        {
            var pago = CrearPagoSEK();
            pago.Importe = 87M;
            pago.ImporteRecibidoBanco = 0;

            var apuntes = CanalesExternosPagosViewModel.GenerarApuntesContables(pago);

            var banco = apuntes.First(a => a.Asiento == 1 && a.Nº_Cuenta == "57200013");
            Assert.AreEqual(87M, banco.Debe, "Si ImporteRecibidoBanco es 0, usa Importe como fallback");
            Assert.IsFalse(apuntes.Any(a => a.Nº_Cuenta == "66800001" || a.Nº_Cuenta == "76800000"),
                "No debe generar apunte de diferencia de cambio");
        }

        // === Tests de Asiento 2 (liquidación) y Asiento 3 (gastos) ===

        [TestMethod]
        public void GenerarApuntes_ConDetalles_GeneraAsiento2LiquidacionPagos()
        {
            var pago = CrearPagoEUR(100M);

            var apuntes = CanalesExternosPagosViewModel.GenerarApuntesContables(pago);

            var liqProveedor = apuntes.First(a => a.Asiento == 2 && a.TipoCuenta == "3");
            Assert.AreEqual(120M, liqProveedor.Debe, "Debe del proveedor = TotalDetallePagos");

            var liqCuenta = apuntes.First(a => a.Asiento == 2 && a.Nº_Cuenta == "55500047");
            Assert.AreEqual(120M, liqCuenta.Haber, "Haber de cuenta 555 = importe del detalle");
        }

        [TestMethod]
        public void GenerarApuntes_ConComisionesDetalle_GeneraAsiento3Gastos()
        {
            var pago = CrearPagoEUR(100M);

            var apuntes = CanalesExternosPagosViewModel.GenerarApuntesContables(pago);

            var gastos = apuntes.First(a => a.Asiento == 3 && a.TipoCuenta == "3");
            Assert.IsTrue(gastos.Haber > 0, "Debe generar apunte de gastos al proveedor");

            var comisiones = apuntes.First(a => a.Asiento == 3 && a.Nº_Cuenta == "55500062");
            Assert.AreEqual(20M, comisiones.Debe, "Las comisiones del detalle van al Debe");
        }

        // === Tests de documento y concepto ===

        [TestMethod]
        public void GenerarApuntes_DocumentoFormato_AMZddMMyy()
        {
            var pago = CrearPagoEUR(100M);
            pago.FechaPago = new System.DateTime(2026, 3, 15);

            var apuntes = CanalesExternosPagosViewModel.GenerarApuntesContables(pago);

            Assert.IsTrue(apuntes.All(a => a.Nº_Documento == "AMZ150326"));
        }

        [TestMethod]
        public void GenerarApuntes_ConceptoPago_IncluyeNombreMarket()
        {
            var pago = CrearPagoEUR(100M);

            var apuntes = CanalesExternosPagosViewModel.GenerarApuntesContables(pago);

            var proveedor = apuntes.First(a => a.Asiento == 1 && a.TipoCuenta == "3");
            Assert.IsTrue(proveedor.Concepto.Contains("Amazon.es"));
        }

        [TestMethod]
        public void GenerarApuntes_SinDetalles_ConceptoIndicaSinDetalle()
        {
            var pago = CrearPagoEUR(100M);
            pago.DetallesPago = new ObservableCollection<DetallePagoCanalExterno>();

            var apuntes = CanalesExternosPagosViewModel.GenerarApuntesContables(pago);

            var proveedor = apuntes.First(a => a.Asiento == 1 && a.TipoCuenta == "3");
            Assert.IsTrue(proveedor.Concepto.Contains("sin detalle"));
        }
    }
}
