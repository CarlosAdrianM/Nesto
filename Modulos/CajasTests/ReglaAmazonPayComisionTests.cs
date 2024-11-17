using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.Cajas.Models.ReglasContabilizacion;
using Nesto.Modulos.Cajas.Models;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CajasTests
{
    [TestClass]
    public class ReglaAmazonPayComisionTests
    {
        [TestMethod]
        public void ReglaAmazonPayComision_SiHayVariosMovimientosDeBancoMarcados_EsContabilizablePuedeDevolverTrue()
        {
            // Arrange
            var regla = new ReglaAmazonPayComision();
            var apunteBancario = new ApunteBancarioDTO
            {
                ConceptoComun = "02",
                ConceptoPropio = "032",
                RegistrosConcepto = new List<RegistroComplementarioConcepto>
                {
                    new RegistroComplementarioConcepto
                    {
                        Concepto = "amazon payments europe sca"
                    }
                },
                ImporteMovimiento = 43.61M
            };
            var apuntesBancarios = new List<ApunteBancarioDTO>
            {
                apunteBancario,
                apunteBancario,
            };
            var apuntesContabilidad = new List<ContabilidadDTO>
            {
                new ContabilidadDTO()
                {
                    Debe = 90M
                }
            };

            // Act
            var resultado = regla.EsContabilizable(apuntesBancarios, apuntesContabilidad);

            // Assert
            Assert.IsTrue(resultado);

        }
    }
}
