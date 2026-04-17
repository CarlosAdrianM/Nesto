using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.CanalesExternos;
using Nesto.Modulos.CanalesExternos.Models;
using Nesto.Modulos.CanalesExternos.Models.Cuadres;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CanalesExternosTests
{
    [TestClass]
    public class CuadreLiquidacionesAmazonTests
    {
        private static ApunteExtractoProveedorDto Apunte(string documentoProv, decimal importe, int id = 1)
            => new ApunteExtractoProveedorDto
            {
                Id = id,
                Fecha = new DateTime(2026, 2, 15),
                Documento = $"PAG-{id}",
                DocumentoProveedor = documentoProv,
                Concepto = "Pago Amazon",
                Importe = importe
            };

        private static PagoCanalExterno Liquidacion(string externalId, decimal importe)
            => new PagoCanalExterno
            {
                PagoExternalId = externalId,
                Importe = importe,
                FechaPago = new DateTime(2026, 2, 15)
            };

        [TestMethod]
        public void ConstruirCuadre_SoloFactura_NoCuentaComoLiquidacion()
        {
            // Los apuntes de factura (importe > 0 en extracto proveedor) son deuda nuestra
            // con el proveedor, no pagos hechos. Se ignoran en el cuadre de liquidaciones.
            var apuntes = new[]
            {
                Apunte(documentoProv: "AMZ-INV-001", importe: 100M)  // factura, positivo
            };
            var liquidaciones = new List<PagoCanalExterno>();

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadreLiquidaciones(apuntes, liquidaciones);

            Assert.AreEqual(0, resultado.TotalElementos,
                "Los apuntes de factura no deben aparecer en el cuadre de liquidaciones");
        }

        [TestMethod]
        public void ConstruirCuadre_LiquidacionCoincidenteYMismoImporte_Cuadrada()
        {
            // Pago Nesto negativo (DEBE), importe absoluto coincide con liquidación Amazon.
            var apuntes = new[] { Apunte(documentoProv: "FEG-001", importe: -500M) };
            var liquidaciones = new[] { Liquidacion("FEG-001", 500M) };

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadreLiquidaciones(apuntes, liquidaciones);

            Assert.AreEqual(1, resultado.Cuadrados.Count);
            Assert.AreEqual(0, resultado.ImportesDistintos.Count);
            Assert.AreEqual("FEG-001", resultado.Cuadrados[0].Clave);
            Assert.AreEqual(500M, resultado.Cuadrados[0].ImporteNesto);
            Assert.AreEqual(500M, resultado.Cuadrados[0].ImporteAmazon);
        }

        [TestMethod]
        public void ConstruirCuadre_LiquidacionAmazonSinContabilizar_VaASoloAmazon()
        {
            var apuntes = new ApunteExtractoProveedorDto[0];
            var liquidaciones = new[] { Liquidacion("FEG-NUEVA", 300M) };

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadreLiquidaciones(apuntes, liquidaciones);

            Assert.AreEqual(1, resultado.SoloEnAmazon.Count);
            Assert.AreEqual("FEG-NUEVA", resultado.SoloEnAmazon[0].Clave);
            Assert.AreEqual(300M, resultado.SoloEnAmazon[0].ImporteAmazon);
        }

        [TestMethod]
        public void ConstruirCuadre_PagoContabilizadoSinLiquidacionAmazon_VaASoloNesto()
        {
            var apuntes = new[] { Apunte(documentoProv: "PAGO-HUERFANO", importe: -250M) };
            var liquidaciones = new PagoCanalExterno[0];

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadreLiquidaciones(apuntes, liquidaciones);

            Assert.AreEqual(1, resultado.SoloEnNesto.Count);
            Assert.AreEqual("PAGO-HUERFANO", resultado.SoloEnNesto[0].Clave);
            Assert.AreEqual(250M, resultado.SoloEnNesto[0].ImporteNesto);
        }

        [TestMethod]
        public void ConstruirCuadre_LiquidacionConImportesDistintos_VaAImportesDistintos()
        {
            var apuntes = new[] { Apunte(documentoProv: "FEG-X", importe: -498M) };
            var liquidaciones = new[] { Liquidacion("FEG-X", 500M) };

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadreLiquidaciones(apuntes, liquidaciones);

            Assert.AreEqual(1, resultado.ImportesDistintos.Count);
            var elem = resultado.ImportesDistintos[0];
            Assert.AreEqual("FEG-X", elem.Clave);
            Assert.AreEqual(498M, elem.ImporteNesto);
            Assert.AreEqual(500M, elem.ImporteAmazon);
            Assert.AreEqual(-2M, elem.Diferencia, "Nesto menos que Amazon → diferencia negativa");
        }

        [TestMethod]
        public void ConstruirCuadre_ApuntesSinDocumentoProveedor_SeIgnoran()
        {
            // Sin DocumentoProveedor no hay clave para emparejar; ignorar evita que aparezcan
            // como "solo en Nesto" sin posibilidad de resolución.
            var apuntes = new[]
            {
                Apunte(documentoProv: null, importe: -100M),
                Apunte(documentoProv: "", importe: -200M),
                Apunte(documentoProv: "  ", importe: -300M)
            };
            var liquidaciones = new PagoCanalExterno[0];

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadreLiquidaciones(apuntes, liquidaciones);

            Assert.AreEqual(0, resultado.TotalElementos);
        }

        [TestMethod]
        public void ConstruirCuadre_MezclaCompleta_CadaElementoEnSuBloque()
        {
            var apuntes = new[]
            {
                Apunte(documentoProv: "CUADRADA", importe: -500M),
                Apunte(documentoProv: "SOLO-NESTO", importe: -100M),
                Apunte(documentoProv: "DIFF", importe: -200M),
                // Factura que no debe aparecer
                Apunte(documentoProv: "FACTURA", importe: 1000M)
            };
            var liquidaciones = new[]
            {
                Liquidacion("CUADRADA", 500M),
                Liquidacion("SOLO-AMAZON", 300M),
                Liquidacion("DIFF", 205M)
            };

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadreLiquidaciones(apuntes, liquidaciones);

            Assert.AreEqual(1, resultado.Cuadrados.Count);
            Assert.AreEqual("CUADRADA", resultado.Cuadrados[0].Clave);
            Assert.AreEqual(1, resultado.SoloEnNesto.Count);
            Assert.AreEqual("SOLO-NESTO", resultado.SoloEnNesto[0].Clave);
            Assert.AreEqual(1, resultado.SoloEnAmazon.Count);
            Assert.AreEqual("SOLO-AMAZON", resultado.SoloEnAmazon[0].Clave);
            Assert.AreEqual(1, resultado.ImportesDistintos.Count);
            Assert.AreEqual("DIFF", resultado.ImportesDistintos[0].Clave);
        }
    }
}
