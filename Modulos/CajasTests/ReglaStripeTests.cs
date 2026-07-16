using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.Models.ReglasContabilizacion;

namespace CajasTests
{
    [TestClass]
    public class ReglaStripeTests
    {
        private ReglaStripe _regla;
        private BancoDTO _banco;

        [TestInitialize]
        public void Setup()
        {
            _regla = new ReglaStripe();
            _banco = new BancoDTO
            {
                CuentaContable = "57200001"
            };
        }

        [TestMethod]
        public void ReglaStripe_CasoStandard_UnSoloPago_EsContabilizableDevuelveTrue()
        {
            // Arrange
            decimal importeOriginal = 39.95m;
            decimal comision = Math.Round((importeOriginal * 0.015m) + 0.25m, 2, MidpointRounding.AwayFromZero);
            decimal importeNeto = importeOriginal - comision;

            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "02",
                ConceptoPropio = "032",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "STRIPE" }
                ],
                ImporteMovimiento = importeNeto
            };

            var apunteContabilidad = new ContabilidadDTO
            {
                Debe = importeOriginal
            };

            // Act
            var esContabilizable = _regla.EsContabilizable(new[] { apunteBanco }, new[] { apunteContabilidad });

            // Assert
            Assert.IsTrue(esContabilizable);
        }

        // Nesto#406: tarifas oficiales UK (2,5 % + 0,25) e internacional (3,25 % + 0,25) que
        // faltaban en el validador. Cuadre EXACTO.
        [TestMethod]
        public void ReglaStripe_TarjetaUK_EsContabilizableDevuelveTrue()
        {
            decimal comision = Math.Round((39.95m * 0.025m) + 0.25m, 2, MidpointRounding.AwayFromZero); // 1,25
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "02",
                ConceptoPropio = "032",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "STRIPE" }
                ],
                ImporteMovimiento = 39.95m - comision
            };
            var apunteContabilidad = new ContabilidadDTO { Debe = 39.95m };

            Assert.IsTrue(_regla.EsContabilizable(new[] { apunteBanco }, new[] { apunteContabilidad }));
        }

        [TestMethod]
        public void ReglaStripe_TarjetaInternacional_EsContabilizableDevuelveTrue()
        {
            decimal comision = Math.Round((39.95m * 0.0325m) + 0.25m, 2, MidpointRounding.AwayFromZero); // 1,55
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "02",
                ConceptoPropio = "032",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "STRIPE" }
                ],
                ImporteMovimiento = 39.95m - comision
            };
            var apunteContabilidad = new ContabilidadDTO { Debe = 39.95m };

            Assert.IsTrue(_regla.EsContabilizable(new[] { apunteBanco }, new[] { apunteContabilidad }));
        }

        // Nesto#406: el caso real 16/07/26 (comisión 1,33 € sobre 39,95 € ≈ 2,7 % + 0,25) NO
        // cuadra con NINGUNA tarifa oficial de Stripe → el botón debe seguir desactivado hasta
        // ver en el dashboard qué caso es y añadirlo como tarifa exacta. Decisión de Carlos:
        // cuadre exacto o nada; nada de rangos que activen el botón "casi siempre".
        [TestMethod]
        public void ReglaStripe_ComisionQueNoCuadraConNingunaTarifaOficial_EsContabilizableDevuelveFalse()
        {
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "02",
                ConceptoPropio = "032",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "STRIPE" }
                ],
                ImporteMovimiento = 38.62m
            };
            var apunteContabilidad = new ContabilidadDTO { Debe = 39.95m };

            Assert.IsFalse(_regla.EsContabilizable(new[] { apunteBanco }, new[] { apunteContabilidad }));
        }

        [TestMethod]
        public void ReglaStripe_DescuadreFueraDeRango_EsContabilizableDevuelveFalse()
        {
            // 5 € de "comisión" sobre 39,95 € no es ninguna tarifa de Stripe: seguir bloqueando
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "02",
                ConceptoPropio = "032",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "STRIPE" }
                ],
                ImporteMovimiento = 34.95m
            };
            var apunteContabilidad = new ContabilidadDTO { Debe = 39.95m };

            Assert.IsFalse(_regla.EsContabilizable(new[] { apunteBanco }, new[] { apunteContabilidad }));
        }

        [TestMethod]
        public void ReglaStripe_ComisionNegativa_EsContabilizableDevuelveFalse()
        {
            // El banco ingresa MÁS de lo contabilizado: eso no es una comisión
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "02",
                ConceptoPropio = "032",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "STRIPE" }
                ],
                ImporteMovimiento = 41.00m
            };
            var apunteContabilidad = new ContabilidadDTO { Debe = 39.95m };

            Assert.IsFalse(_regla.EsContabilizable(new[] { apunteBanco }, new[] { apunteContabilidad }));
        }

        [TestMethod]
        public void ReglaStripe_CasoPremium_UnSoloPago_EsContabilizableDevuelveTrue()
        {
            // Arrange
            decimal importeOriginal = 39.95m;
            decimal comision = Math.Round((importeOriginal * 0.019m) + 0.25m, 2, MidpointRounding.AwayFromZero);
            decimal importeNeto = importeOriginal - comision;

            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "02",
                ConceptoPropio = "032",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "STRIPE" }
                ],
                ImporteMovimiento = importeNeto
            };

            var apunteContabilidad = new ContabilidadDTO
            {
                Debe = importeOriginal
            };

            // Act
            var esContabilizable = _regla.EsContabilizable(new[] { apunteBanco }, new[] { apunteContabilidad });

            // Assert
            Assert.IsTrue(esContabilizable);
        }


        [TestMethod]
        public void ReglaStripe_CasoMixto_StandardYPremium_FallaValidacion()
        {
            // Arrange
            // Dos cobros de 39,95 €
            decimal importeOriginal1 = 39.95m; // Standard
            decimal comision1 = 1.01m;         // (1.5% + 0.25€)
            decimal importeOriginal2 = 39.95m; // Premium
            decimal comision2 = 0.85m;         // (1.9% + 0.25€)

            // Totales esperados
            _ = comision1 + comision2; // 1.86 €
            decimal totalIngresado = 78.04m;               // lo que llega al banco

            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "02",
                ConceptoPropio = "032",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "STRIPE" }
                ],
                ImporteMovimiento = totalIngresado
            };

            var apuntesContabilidad = new List<ContabilidadDTO>
            {
                new() { Debe = importeOriginal1 },
                new() { Debe = importeOriginal2 }
            };

            // Act
            bool esContabilizable = _regla.EsContabilizable(new[] { apunteBanco }, apuntesContabilidad);

            // Assert
            Assert.IsTrue(esContabilizable, "La regla debería permitir mezclar pagos Standard y Premium.");
        }
    }
}
