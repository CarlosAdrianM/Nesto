using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.Models.ReglasContabilizacion;

namespace CajasTests
{
    /// <summary>
    /// Regla de Aplázame del cuadre de bancos: comisión del 1,5 % + IVA (o Aplázame Zero, entre el 2 % y el 20 %).
    /// Carlos 13/11/26: una venta puede llegar en VARIOS ingresos (caso real: 841,13 € el 21/10/26 y 0,10 € el 24/10/26, por
    /// una operación de 856,68 € cancelada y rehecha por 856,78 €); la regla suma los ingresos seleccionados.
    /// </summary>
    [TestClass]
    public class ReglaAplazameTests
    {
        private ReglaAplazame _regla;
        private BancoDTO _banco;

        [TestInitialize]
        public void Setup()
        {
            _regla = new ReglaAplazame();
            _banco = new BancoDTO { CuentaContable = "57200013" };
        }

        private static ApunteBancarioDTO Ingreso(decimal importe, string ordenante = "WIZINK BANK S.A.", string conceptoPropio = "002") => new()
        {
            ConceptoComun = "02",
            ConceptoPropio = conceptoPropio,
            RegistrosConcepto = [new RegistroComplementarioConcepto { Concepto = ordenante }],
            ImporteMovimiento = importe,
            Referencia2 = "ORIGEN: 01824017",
            FechaOperacion = new DateTime(2026, 10, 21)
        };

        private static ContabilidadDTO Venta(decimal importe) => new() { Debe = importe };

        [TestMethod]
        public void UnSoloIngreso_ComisionEstandar_EsContabilizable()
        {
            // 300,77 € - 5,46 € (1,5 % + IVA) = 295,31 € (caso real del 06/10/26)
            Assert.IsTrue(_regla.EsContabilizable(new[] { Ingreso(295.31m) }, new[] { Venta(300.77m) }));
        }

        [TestMethod]
        public void DosIngresosQueSumanLaVentaMenosLaComision_EsContabilizable()
        {
            // 856,78 € - 15,55 € = 841,23 €, que llegó como 841,13 € + 0,10 €
            Assert.IsTrue(_regla.EsContabilizable(new[] { Ingreso(841.13m), Ingreso(0.10m) }, new[] { Venta(856.78m) }));
        }

        [TestMethod]
        public void SoloElPrimeroDeLosDosIngresos_NoEsContabilizable()
        {
            // Con 841,13 € solo, la «comisión» sería 15,65 €: no es el 1,5 % + IVA ni llega al 2 % de Zero
            Assert.IsFalse(_regla.EsContabilizable(new[] { Ingreso(841.13m) }, new[] { Venta(856.78m) }));
        }

        [TestMethod]
        public void DosIngresos_UnoNoEsDeAplazame_NoEsContabilizable()
        {
            Assert.IsFalse(_regla.EsContabilizable(new[] { Ingreso(841.13m), Ingreso(0.10m, ordenante: "OTRO ORDENANTE") }, new[] { Venta(856.78m) }));
            Assert.IsFalse(_regla.EsContabilizable(new[] { Ingreso(841.13m), Ingreso(0.10m, conceptoPropio: "040") }, new[] { Venta(856.78m) }));
        }

        [TestMethod]
        public void SinBancoOSinContabilidad_NoEsContabilizable()
        {
            Assert.IsFalse(_regla.EsContabilizable(Array.Empty<ApunteBancarioDTO>(), new[] { Venta(856.78m) }));
            Assert.IsFalse(_regla.EsContabilizable(new[] { Ingreso(841.23m) }, Array.Empty<ContabilidadDTO>()));
            Assert.IsFalse(_regla.EsContabilizable(null, null));
        }

        [TestMethod]
        public void DosIngresos_ContabilizaUnaSolaLineaConLaComisionDelTotal()
        {
            ReglaContabilizacionResponse respuesta = _regla.ApuntesContabilizar(
                new[] { Ingreso(841.13m), Ingreso(0.10m) }, new[] { Venta(856.78m) }, _banco);

            Assert.AreEqual(1, respuesta.Lineas.Count);
            PreContabilidadDTO linea = respuesta.Lineas.Single();
            Assert.AreEqual(15.55m, linea.Debe);
            Assert.AreEqual("1070", linea.Cuenta);
            Assert.AreEqual("57200013", linea.Contrapartida);
        }
    }
}
