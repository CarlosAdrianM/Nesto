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
