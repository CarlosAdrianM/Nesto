using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.CanalesExternos.Cuadres;
using Nesto.Modulos.CanalesExternos.Models.Cuadres;
using System.Collections.Generic;
using System.Linq;

namespace CanalesExternosTests
{
    [TestClass]
    public class MotorCuadreTests
    {
        // Ejemplos minimalistas de "filas" de cada lado. En los cuadres reales serían
        // FacturaCanalExterno / FacturaContabilizadaProveedorDTO, pero el motor es genérico.
        private record FilaNesto(string Clave, decimal Importe);
        private record FilaAmazon(string Clave, decimal Importe);

        private static ResultadoCuadre<string> Conciliar(
            IEnumerable<FilaNesto> nesto,
            IEnumerable<FilaAmazon> amazon)
        {
            return MotorCuadre.Conciliar(
                nesto, amazon,
                n => n.Clave, a => a.Clave,
                n => n.Importe, a => a.Importe);
        }

        [TestMethod]
        public void Conciliar_AmbosLadosVacios_ResultadoVacio()
        {
            var resultado = Conciliar(new FilaNesto[0], new FilaAmazon[0]);

            Assert.AreEqual(0, resultado.TotalElementos);
            Assert.IsTrue(resultado.EstaCuadrado);
            Assert.AreEqual(0M, resultado.TotalImporteNesto);
            Assert.AreEqual(0M, resultado.TotalImporteAmazon);
        }

        [TestMethod]
        public void Conciliar_SoloNesto_TodoVaASoloEnNesto()
        {
            var nesto = new[] { new FilaNesto("A", 10M), new FilaNesto("B", 20M) };
            var amazon = new FilaAmazon[0];

            var resultado = Conciliar(nesto, amazon);

            Assert.AreEqual(2, resultado.SoloEnNesto.Count);
            Assert.AreEqual(0, resultado.SoloEnAmazon.Count);
            Assert.AreEqual(0, resultado.Cuadrados.Count);
            Assert.AreEqual(30M, resultado.TotalImporteNesto);
            Assert.AreEqual(0M, resultado.TotalImporteAmazon);
            Assert.IsFalse(resultado.EstaCuadrado);
        }

        [TestMethod]
        public void Conciliar_SoloAmazon_TodoVaASoloEnAmazon()
        {
            var nesto = new FilaNesto[0];
            var amazon = new[] { new FilaAmazon("X", 50M) };

            var resultado = Conciliar(nesto, amazon);

            Assert.AreEqual(1, resultado.SoloEnAmazon.Count);
            Assert.AreEqual("X", resultado.SoloEnAmazon[0].Clave);
            Assert.AreEqual(50M, resultado.SoloEnAmazon[0].ImporteAmazon);
            Assert.IsNull(resultado.SoloEnAmazon[0].ImporteNesto);
        }

        [TestMethod]
        public void Conciliar_CoincidentesConMismoImporte_VanACuadrados()
        {
            var nesto = new[] { new FilaNesto("A", 10M), new FilaNesto("B", 20M) };
            var amazon = new[] { new FilaAmazon("A", 10M), new FilaAmazon("B", 20M) };

            var resultado = Conciliar(nesto, amazon);

            Assert.AreEqual(2, resultado.Cuadrados.Count);
            Assert.AreEqual(0, resultado.SoloEnNesto.Count);
            Assert.AreEqual(0, resultado.SoloEnAmazon.Count);
            Assert.AreEqual(0, resultado.ImportesDistintos.Count);
            Assert.IsTrue(resultado.EstaCuadrado);
            foreach (var e in resultado.Cuadrados)
            {
                Assert.IsTrue(e.Cuadrado);
                Assert.AreEqual(0M, e.Diferencia);
            }
        }

        [TestMethod]
        public void Conciliar_CoincidentesConImporteDistinto_VanAImportesDistintos()
        {
            var nesto = new[] { new FilaNesto("A", 10M) };
            var amazon = new[] { new FilaAmazon("A", 12M) };

            var resultado = Conciliar(nesto, amazon);

            Assert.AreEqual(1, resultado.ImportesDistintos.Count);
            Assert.AreEqual(0, resultado.Cuadrados.Count);
            var e = resultado.ImportesDistintos[0];
            Assert.AreEqual("A", e.Clave);
            Assert.AreEqual(10M, e.ImporteNesto);
            Assert.AreEqual(12M, e.ImporteAmazon);
            Assert.IsFalse(e.Cuadrado);
            Assert.AreEqual(-2M, e.Diferencia);
        }

        [TestMethod]
        public void Conciliar_MezclaCompleta_CadaElementoEnSuBloqueCorrecto()
        {
            // Nesto: A (cuadrado), B (solo Nesto), C (importe distinto)
            // Amazon: A (cuadrado), D (solo Amazon), C (importe distinto)
            var nesto = new[]
            {
                new FilaNesto("A", 10M),
                new FilaNesto("B", 20M),
                new FilaNesto("C", 30M)
            };
            var amazon = new[]
            {
                new FilaAmazon("A", 10M),
                new FilaAmazon("D", 40M),
                new FilaAmazon("C", 35M)
            };

            var resultado = Conciliar(nesto, amazon);

            Assert.AreEqual(1, resultado.Cuadrados.Count);
            Assert.AreEqual("A", resultado.Cuadrados[0].Clave);

            Assert.AreEqual(1, resultado.SoloEnNesto.Count);
            Assert.AreEqual("B", resultado.SoloEnNesto[0].Clave);

            Assert.AreEqual(1, resultado.SoloEnAmazon.Count);
            Assert.AreEqual("D", resultado.SoloEnAmazon[0].Clave);

            Assert.AreEqual(1, resultado.ImportesDistintos.Count);
            Assert.AreEqual("C", resultado.ImportesDistintos[0].Clave);

            Assert.AreEqual(4, resultado.TotalElementos);
            Assert.IsFalse(resultado.EstaCuadrado);
        }

        [TestMethod]
        public void Conciliar_TotalesSumanTodosLosElementosQueAparecenEnCadaLado()
        {
            var nesto = new[] { new FilaNesto("A", 10M), new FilaNesto("B", 20M) };
            var amazon = new[] { new FilaAmazon("A", 10M), new FilaAmazon("C", 5M) };

            var resultado = Conciliar(nesto, amazon);

            // Total Nesto = 10 (cuadrado) + 20 (solo Nesto)
            Assert.AreEqual(30M, resultado.TotalImporteNesto);
            // Total Amazon = 10 (cuadrado) + 5 (solo Amazon)
            Assert.AreEqual(15M, resultado.TotalImporteAmazon);
        }

        [TestMethod]
        public void Conciliar_DosElementosConMismaClaveEnUnLado_AgrupaImportes()
        {
            // Si Amazon envía dos eventos con la misma clave (p. ej. dos comisiones del mismo InvoiceId),
            // el motor los suma para comparar contra un único importe en Nesto.
            var nesto = new[] { new FilaNesto("A", 10M) };
            var amazon = new[] { new FilaAmazon("A", 6M), new FilaAmazon("A", 4M) };

            var resultado = Conciliar(nesto, amazon);

            Assert.AreEqual(1, resultado.Cuadrados.Count);
            Assert.AreEqual(10M, resultado.Cuadrados[0].ImporteAmazon);
        }

        [TestMethod]
        public void Conciliar_ConDescripcion_SePropagaAlElemento()
        {
            var nesto = new[] { new FilaNesto("A", 10M) };
            var amazon = new FilaAmazon[0];

            var resultado = MotorCuadre.Conciliar(
                nesto, amazon,
                n => n.Clave, a => a.Clave,
                n => n.Importe, a => a.Importe,
                clave => $"Factura {clave}");

            Assert.AreEqual("Factura A", resultado.SoloEnNesto[0].Descripcion);
        }

        // --- Modo "solo presencia": cuando uno de los lados no expone importe (p. ej. el
        // endpoint de Nesto solo devuelve InvoiceId → NumFactura). El motor solo clasifica
        // por existencia en un lado o en ambos; nunca cae en ImportesDistintos.

        [TestMethod]
        public void ConciliarPorPresencia_CoincidentesEnAmbos_VanACuadrados()
        {
            var nesto = new[] { "A", "B" };
            var amazon = new[] { "A", "B", "C" };

            var resultado = MotorCuadre.ConciliarPorPresencia<string, string, string>(
                nesto, amazon, n => n, a => a);

            Assert.AreEqual(2, resultado.Cuadrados.Count);
            Assert.AreEqual(0, resultado.SoloEnNesto.Count);
            Assert.AreEqual(1, resultado.SoloEnAmazon.Count);
            Assert.AreEqual(0, resultado.ImportesDistintos.Count);
            Assert.AreEqual("C", resultado.SoloEnAmazon[0].Clave);
        }

        [TestMethod]
        public void ConciliarPorPresencia_SinImportes_CuadradosTienenImporteNull()
        {
            // En modo presencia, los elementos cuadrados tienen importes nulos:
            // existen en ambos lados pero no sabemos comparar cantidades.
            var nesto = new[] { "A" };
            var amazon = new[] { "A" };

            var resultado = MotorCuadre.ConciliarPorPresencia<string, string, string>(
                nesto, amazon, n => n, a => a);

            var e = resultado.Cuadrados.Single();
            Assert.IsTrue(e.ExisteEnNesto);
            Assert.IsTrue(e.ExisteEnAmazon);
            Assert.IsNull(e.ImporteNesto);
            Assert.IsNull(e.ImporteAmazon);
        }

        [TestMethod]
        public void ConciliarPorPresencia_SoloUnLado_SeClasificaEnElBloqueCorrecto()
        {
            var nesto = new[] { "A" };
            var amazon = new[] { "B" };

            var resultado = MotorCuadre.ConciliarPorPresencia<string, string, string>(
                nesto, amazon, n => n, a => a);

            Assert.AreEqual(1, resultado.SoloEnNesto.Count);
            Assert.AreEqual("A", resultado.SoloEnNesto[0].Clave);
            Assert.AreEqual(1, resultado.SoloEnAmazon.Count);
            Assert.AreEqual("B", resultado.SoloEnAmazon[0].Clave);
            Assert.IsFalse(resultado.EstaCuadrado);
        }
    }
}
