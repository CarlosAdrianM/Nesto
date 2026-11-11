using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Models;
using Nesto.Modulos.PlantillaVenta;
using System.Collections.Generic;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Sugerencia 1192: con un cliente de público final (estado 8) la plantilla enseña junto al precio el precio con
    /// IVA («con IVA: 83,49 €»). Solo informativo.
    /// </summary>
    [TestClass]
    public class PrecioConIvaPublicoFinalTests
    {
        private static readonly List<ParametrosIvaBase> PARAMETROS_G21 = new List<ParametrosIvaBase>
        {
            new ParametrosIvaBase { CodigoIvaProducto = "G21", PorcentajeIvaProducto = 0.21M },
            new ParametrosIvaBase { CodigoIvaProducto = "R10", PorcentajeIvaProducto = 0.10M }
        };

        [TestMethod]
        public void PrecioConIva_LaLamparaDe69_Son83Con49()
        {
            // El caso real de la sugerencia: 69,00 sin IVA en la plantilla y 83,49 con IVA en la web
            Assert.AreEqual(83.49M, PrecioConIvaPublicoFinal.PrecioConIva(69M, 0.21M));
            Assert.AreEqual("con IVA: 83,49 €", PrecioConIvaPublicoFinal.Texto(69M, 0.21M));
        }

        [TestMethod]
        public void PrecioConIva_RedondeaAlCentimoHaciaArribaEnElMedio()
        {
            // 0,05 * 1,10 = 0,055 -> 0,06 (AwayFromZero, como RoundingHelper de la API)
            Assert.AreEqual(0.06M, PrecioConIvaPublicoFinal.PrecioConIva(0.05M, 0.10M));
            Assert.AreEqual("con IVA: 1.210,00 €", PrecioConIvaPublicoFinal.Texto(1000M, 0.21M));
        }

        [TestMethod]
        public void SoloSeEnsennaConClienteDePublicoFinalConIva()
        {
            Assert.IsTrue(PrecioConIvaPublicoFinal.EsPublicoFinal(8, "G21", "12345"));
            Assert.IsFalse(PrecioConIvaPublicoFinal.EsPublicoFinal(0, "G21", "12345"));
            Assert.IsFalse(PrecioConIvaPublicoFinal.EsPublicoFinal(9, "G21", "12345"));
            Assert.IsFalse(PrecioConIvaPublicoFinal.EsPublicoFinal(8, null, "12345"));
            Assert.IsFalse(PrecioConIvaPublicoFinal.EsPublicoFinal(8, "  ", "12345"));
        }

        [TestMethod]
        public void VentaTienda10458EnEstado3_SiEsPublicoFinal()
        {
            // El caso real de la sugerencia: el cliente «venta tienda» está en estado 3, no en 8
            Assert.IsTrue(PrecioConIvaPublicoFinal.EsPublicoFinal(3, "G21", "10458"));
            Assert.IsTrue(PrecioConIvaPublicoFinal.EsPublicoFinal(3, "G21", "10458     "));
            Assert.IsFalse(PrecioConIvaPublicoFinal.EsPublicoFinal(3, null, "10458"));
            Assert.IsFalse(PrecioConIvaPublicoFinal.EsPublicoFinal(3, "G21", "10459"));
        }

        [TestMethod]
        public void PorcentajeDe_UsaElIvaDelProductoYSiNoLoEncuentraNoEnsennaNada()
        {
            Assert.AreEqual(0.21M, PrecioConIvaPublicoFinal.PorcentajeDe("G21", PARAMETROS_G21));
            Assert.AreEqual(0.10M, PrecioConIvaPublicoFinal.PorcentajeDe("r10 ", PARAMETROS_G21));
            Assert.IsNull(PrecioConIvaPublicoFinal.PorcentajeDe("EX", PARAMETROS_G21));
            Assert.IsNull(PrecioConIvaPublicoFinal.PorcentajeDe(null, PARAMETROS_G21));
            Assert.IsNull(PrecioConIvaPublicoFinal.PorcentajeDe("G21", null));
            Assert.IsNull(PrecioConIvaPublicoFinal.Texto(69M, null));
        }

        [TestMethod]
        public void Linea_SinPorcentajeNoTieneTextoYConPorcentajeSigueElPrecio()
        {
            var linea = new LineaPlantillaVenta { producto = "1", precio = 69M };
            Assert.IsNull(linea.TextoPrecioConIva);

            var avisados = new List<string>();
            linea.PropertyChanged += (s, e) => avisados.Add(e.PropertyName);
            linea.PorcentajeIvaPublicoFinal = 0.21M;
            Assert.AreEqual("con IVA: 83,49 €", linea.TextoPrecioConIva);
            CollectionAssert.Contains(avisados, nameof(LineaPlantillaVenta.TextoPrecioConIva));

            avisados.Clear();
            linea.precio = 100M;
            Assert.AreEqual("con IVA: 121,00 €", linea.TextoPrecioConIva);
            CollectionAssert.Contains(avisados, nameof(LineaPlantillaVenta.TextoPrecioConIva));
        }

        [TestMethod]
        public void Linea_ElPorcentajeNoViajaEnElJson()
        {
            var linea = new LineaPlantillaVenta { producto = "1", precio = 69M, PorcentajeIvaPublicoFinal = 0.21M };
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(linea);
            StringAssert.DoesNotMatch(json, new System.Text.RegularExpressions.Regex("PorcentajeIvaPublicoFinal|TextoPrecioConIva"));
        }
    }
}
