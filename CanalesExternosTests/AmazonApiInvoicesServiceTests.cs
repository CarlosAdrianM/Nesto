using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.CanalesExternos.ApisExternas;
using Nesto.Modulos.CanalesExternos.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CanalesExternosTests
{
    [TestClass]
    public class AmazonApiInvoicesServiceTests
    {
        private static AmazonApiInvoicesService.Fee F(decimal importe, string tipo = "Shipment:ItemFee:Commission",
            string mp = "Amazon.es", DateTime? fecha = null, string moneda = "EUR")
        {
            return new AmazonApiInvoicesService.Fee
            {
                Importe = importe,
                Tipo = tipo,
                MarketplaceName = mp,
                PostedDate = fecha ?? new DateTime(2026, 2, 15),
                Moneda = moneda
            };
        }

        [TestMethod]
        public void AgruparEnFacturas_SinFees_DevuelveColeccionVacia()
        {
            var resultado = AmazonApiInvoicesService.AgruparEnFacturas(new List<AmazonApiInvoicesService.Fee>());
            Assert.AreEqual(0, resultado.Count);
        }

        [TestMethod]
        public void AgruparEnFacturas_FeesNegativosYPositivos_GeneraDosFacturas()
        {
            // Fee negativo Amazon = cargo = "operaciones" (positivo tras invertir signo)
            // Fee positivo Amazon = abono  = "tarifas reembolsadas" (negativo tras invertir signo)
            var fees = new[] { F(-100M), F(5M, "Refund:ItemFeeAdj:Commission") };
            var resultado = AmazonApiInvoicesService.AgruparEnFacturas(fees);

            Assert.AreEqual(2, resultado.Count);
            Assert.IsTrue(resultado.Any(f => f.Concepto == "operaciones" && f.BaseImponible == 100M));
            Assert.IsTrue(resultado.Any(f => f.Concepto == "tarifas reembolsadas" && f.BaseImponible == -5M));
        }

        [TestMethod]
        public void AgruparEnFacturas_UnaFactura_IvaCalculadoAutomaticamente()
        {
            var fees = new[] { F(-100M, mp: "Amazon.es") };
            var f = AmazonApiInvoicesService.AgruparEnFacturas(fees).Single();

            Assert.AreEqual(100M, f.BaseImponible);
            Assert.AreEqual("G21", f.CodigoIva);
            Assert.AreEqual(21M, f.ImporteIva);
            Assert.AreEqual(121M, f.Total);
        }

        [TestMethod]
        public void AgruparEnFacturas_UK_CodigoIvaEX()
        {
            var fees = new[] { F(-100M, mp: "Amazon.co.uk", moneda: "GBP") };
            var f = AmazonApiInvoicesService.AgruparEnFacturas(fees).Single();

            Assert.AreEqual("EX", f.CodigoIva);
            Assert.AreEqual(0M, f.ImporteIva);
            Assert.AreEqual(f.BaseImponible, f.Total);
        }

        [TestMethod]
        public void AgruparEnFacturas_FechaFactura_EsUltimoDiaDelMes()
        {
            var fees = new[] { F(-10M, fecha: new DateTime(2026, 2, 5)) };
            var resultado = AmazonApiInvoicesService.AgruparEnFacturas(fees);
            Assert.AreEqual(new DateTime(2026, 2, 28), resultado.First().FechaFactura);
        }

        [TestMethod]
        public void FacturaCanalExterno_EditarBase_RecalculaIvaYTotal()
        {
            var factura = new FacturaCanalExterno { BaseImponible = 100M, CodigoIva = "G21" };
            Assert.AreEqual(21M, factura.ImporteIva);
            Assert.AreEqual(121M, factura.Total);

            factura.BaseImponible = 200M;

            Assert.AreEqual(42M, factura.ImporteIva);
            Assert.AreEqual(242M, factura.Total);
        }

        [TestMethod]
        public void FacturaCanalExterno_CambiarCodIva_RecalculaIvaYTotal()
        {
            var factura = new FacturaCanalExterno { BaseImponible = 100M, CodigoIva = "G21" };
            factura.CodigoIva = "EX";

            Assert.AreEqual(0M, factura.PorcentajeIva);
            Assert.AreEqual(0M, factura.ImporteIva);
            Assert.AreEqual(100M, factura.Total);
        }

        [TestMethod]
        public void FacturaCanalExterno_PropertyChanged_SeDisparaAlEditar()
        {
            var factura = new FacturaCanalExterno { BaseImponible = 100M, CodigoIva = "G21" };
            var cambios = new List<string>();
            factura.PropertyChanged += (s, e) => cambios.Add(e.PropertyName);

            factura.BaseImponible = 200M;

            CollectionAssert.Contains(cambios, nameof(FacturaCanalExterno.BaseImponible));
            CollectionAssert.Contains(cambios, nameof(FacturaCanalExterno.ImporteIva));
            CollectionAssert.Contains(cambios, nameof(FacturaCanalExterno.Total));
        }

        [TestMethod]
        public void ConvertirAEur_EUR_DevuelveMismoImporte()
        {
            Assert.AreEqual(100M, AmazonApiInvoicesService.ConvertirAEur(100M, "EUR"));
        }

        [TestMethod]
        public void ConvertirAEur_SEK_AplicaTasaAprox()
        {
            // SEK tasa aprox 0.09
            Assert.AreEqual(9M, AmazonApiInvoicesService.ConvertirAEur(100M, "SEK"));
        }

        [TestMethod]
        public void DeterminarCodigoIvaPorDefecto_UK_DevuelveEX()
        {
            Assert.AreEqual("EX", AmazonApiInvoicesService.DeterminarCodigoIvaPorDefecto("A1F83G8C2ARO7P"));
        }

        [TestMethod]
        public void DeterminarCodigoIvaPorDefecto_ES_DevuelveG21()
        {
            Assert.AreEqual("G21", AmazonApiInvoicesService.DeterminarCodigoIvaPorDefecto("A1RKKUPIHCS9HS"));
        }

        [DataTestMethod]
        [DataRow("Amazon.es", "España")]
        [DataRow("Amazon.fr", "Francia")]
        [DataRow("Amazon.it", "Italia")]
        [DataRow("Amazon.de", "Alemania")]
        [DataRow("Amazon.co.uk", "UK")]
        public void DeducirPais_MapeaCorrectamenteDesdeNombreMarket(string nombreMarket, string paisEsperado)
        {
            Assert.AreEqual(paisEsperado, AmazonApiInvoicesService.DeducirPais(nombreMarket, null));
        }
    }
}
