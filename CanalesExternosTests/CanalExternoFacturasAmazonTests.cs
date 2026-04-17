using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.CanalesExternos;
using Nesto.Modulos.CanalesExternos.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CanalesExternosTests
{
    [TestClass]
    public class CanalExternoFacturasAmazonTests
    {
        private CanalExternoFacturasAmazon CrearCanal() => new CanalExternoFacturasAmazon(null, null);

        private FacturaCanalExterno CrearFactura(string invoiceId = "INV-1", decimal baseImp = 100M,
            string codigoIva = "G21", string concepto = "operaciones", string pais = "España")
        {
            return new FacturaCanalExterno
            {
                InvoiceId = invoiceId,
                FechaFactura = new DateTime(2026, 2, 28),
                MarketplaceId = "A1RKKUPIHCS9HS",
                NombreMarket = "Amazon.es",
                Pais = pais,
                Moneda = "EUR",
                Concepto = concepto,
                BaseImponible = baseImp,
                CodigoIva = codigoIva
            };
        }

        private static JObject AJson(object o) => JObject.Parse(JsonConvert.SerializeObject(o));

        [TestMethod]
        public void ValidarImportes_SinBase_Lanza()
        {
            var factura = CrearFactura(baseImp: 0M);
            Assert.ThrowsException<InvalidOperationException>(() => CrearCanal().ValidarImportes(factura));
        }

        [TestMethod]
        public void ValidarImportes_ConBase_NoLanza()
        {
            var factura = CrearFactura(baseImp: 100M);
            CrearCanal().ValidarImportes(factura);
        }

        [TestMethod]
        public void ConstruirLinea_TextoConcepto_España()
        {
            var factura = CrearFactura(concepto: "operaciones", pais: "España");
            var linea = AJson(CrearCanal().ConstruirLinea(factura));
            Assert.AreEqual("operaciones España", (string)linea["Texto"]);
        }

        [TestMethod]
        public void ConstruirLinea_TextoTruncadoA50()
        {
            var factura = CrearFactura(concepto: new string('X', 60), pais: "España");
            var linea = AJson(CrearCanal().ConstruirLinea(factura));
            Assert.AreEqual(50, ((string)linea["Texto"]).Length);
        }

        [TestMethod]
        public void ConstruirLinea_PropagaBaseYCodIva()
        {
            var factura = CrearFactura(baseImp: 100M, codigoIva: "G21");
            var linea = AJson(CrearCanal().ConstruirLinea(factura));
            Assert.AreEqual(100M, (decimal)linea["PrecioUnitario"]);
            Assert.AreEqual("G21", (string)linea["CodigoIvaProducto"]);
            Assert.AreEqual("STK", (string)linea["FormaVenta"]);
            Assert.AreEqual("ALG", (string)linea["Delegacion"]);
            Assert.AreEqual(1, (int)linea["Cantidad"]);
        }

        [TestMethod]
        public void ConstruirPedido_FijaCabeceraConDatosAmazon()
        {
            var factura = CrearFactura(invoiceId: "INV-1");
            var pedido = AJson(CrearCanal().ConstruirPedido(factura, pathPdf: @"C:\pdfs\INV-1.pdf"));

            Assert.AreEqual("1", (string)pedido["Empresa"]);
            Assert.AreEqual("999", (string)pedido["Proveedor"]);
            Assert.AreEqual("TRN", (string)pedido["FormaPago"]);
            Assert.AreEqual("CONTADO", (string)pedido["PlazosPago"]);
            Assert.AreEqual("NRM", (string)pedido["PeriodoFacturacion"]);
            Assert.AreEqual("INV-1", (string)pedido["FacturaProveedor"]);
            Assert.AreEqual(@"C:\pdfs\INV-1.pdf", (string)pedido["PathPedido"]);
            Assert.AreEqual(1, ((JArray)pedido["Lineas"]).Count);
        }

        [TestMethod]
        public void ConstruirRequest_NoCreaPagoYPropagaDocumento()
        {
            var factura = CrearFactura(invoiceId: "INV-XYZ");
            var request = AJson(CrearCanal().ConstruirRequest(factura));
            Assert.AreEqual(false, (bool)request["CrearPago"]);
            Assert.AreEqual("INV-XYZ", (string)request["Documento"]);
        }

        [TestMethod]
        public void MarcarEstados_YaContabilizadas_SeMarcanComoTal()
        {
            var facturas = new ObservableCollection<FacturaCanalExterno>
            {
                new FacturaCanalExterno { InvoiceId = "A", FechaFactura = new DateTime(2026, 2, 10) },
                new FacturaCanalExterno { InvoiceId = "B", FechaFactura = new DateTime(2026, 2, 20) }
            };
            var resultado = CanalExternoFacturasAmazon.MarcarEstados(
                facturas,
                new Dictionary<string, int> { ["A"] = 1001 });

            Assert.AreEqual(EstadoFacturaCanalExterno.YaContabilizada, resultado.First(f => f.InvoiceId == "A").Estado);
            Assert.AreEqual(EstadoFacturaCanalExterno.PendienteContabilizar, resultado.First(f => f.InvoiceId == "B").Estado);
        }

        [TestMethod]
        public void MarcarEstados_FacturaAnteriorAUltimaContabilizada_EsHueco()
        {
            var facturas = new ObservableCollection<FacturaCanalExterno>
            {
                new FacturaCanalExterno { InvoiceId = "A", FechaFactura = new DateTime(2026, 2, 5) },
                new FacturaCanalExterno { InvoiceId = "B", FechaFactura = new DateTime(2026, 2, 20) },
                new FacturaCanalExterno { InvoiceId = "C", FechaFactura = new DateTime(2026, 2, 25) }
            };
            var resultado = CanalExternoFacturasAmazon.MarcarEstados(
                facturas,
                new Dictionary<string, int> { ["B"] = 1002 });

            Assert.AreEqual(EstadoFacturaCanalExterno.Hueco, resultado.First(f => f.InvoiceId == "A").Estado);
            Assert.AreEqual(EstadoFacturaCanalExterno.YaContabilizada, resultado.First(f => f.InvoiceId == "B").Estado);
            Assert.AreEqual(EstadoFacturaCanalExterno.PendienteContabilizar, resultado.First(f => f.InvoiceId == "C").Estado);
        }
    }
}
