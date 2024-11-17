using FakeItEasy;
using Microsoft.Identity.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.Cajas;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.Models.ReglasContabilizacion;
using Prism.Services.Dialogs;

namespace CajasTests
{
    [TestClass]
    public class ReglaFinanciacionLineaRiesgoTests
    {
        [TestMethod]
        public void ReglaFinanciacionLineaRiesgo_ConSoloUnApunteDeBancoYUnoDeContabilidad_NoSePuedeContabilizar()
        {
            // Arrange
            var _dialogService = A.Fake<IDialogService>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var regla = new ReglaFinanciacionLineaRiesgo(_dialogService, _recursosHumanosService);
            var apunteBancario = new ApunteBancarioDTO
            {
                ConceptoComun = "02",
                ConceptoPropio = "036",
                RegistrosConcepto = new List<RegistroComplementarioConcepto>
                {
                    new RegistroComplementarioConcepto
                    {
                        Concepto2 = "R2F 0627302000063"
                    }
                }
            };
            var apuntesBancarios = new List<ApunteBancarioDTO> {
                apunteBancario
            };
            var apuntesContabilidad = new List<ContabilidadDTO> {
                new ContabilidadDTO()
            };


            // Act
            var resultado = regla.EsContabilizable(apuntesBancarios, apuntesContabilidad);

            // Assert
            Assert.IsFalse(resultado);
        }

        [TestMethod]
        public void ReglaFinanciacionLineaRiesgo_ConDosApuntesDeBancoYUnoDeContabilidad_SePuedeContabilizar()
        {
            // Arrange
            var _dialogService = A.Fake<IDialogService>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var regla = new ReglaFinanciacionLineaRiesgo(_dialogService, _recursosHumanosService);
            var apunteBancarioCargo = new ApunteBancarioDTO
            {
                ConceptoComun = "00",
                ConceptoPropio = "000",
                RegistrosConcepto = new List<RegistroComplementarioConcepto>
                {
                    new RegistroComplementarioConcepto
                    {
                        Concepto2 = "Cargo por domiciliación"
                    }
                },
                ImporteMovimiento = -2
            };
            var apunteBancarioAbono = new ApunteBancarioDTO
            {
                ConceptoComun = "02",
                ConceptoPropio = "036",
                RegistrosConcepto = new List<RegistroComplementarioConcepto>
                {
                    new RegistroComplementarioConcepto
                    {
                        Concepto2 = "R2F 0627302000063"
                    }
                },
                ImporteMovimiento = 1.5M
            };
            var apuntesBancarios = new List<ApunteBancarioDTO> {
                apunteBancarioCargo,
                apunteBancarioAbono
            };
            var apuntesContabilidad = new List<ContabilidadDTO> {
                new ContabilidadDTO() { Haber = 2 }
            };


            // Act
            var resultado = regla.EsContabilizable(apuntesBancarios, apuntesContabilidad);

            // Assert
            Assert.IsTrue(resultado);
        }

        [TestMethod]
        public void ReglaFinanciacionLineaRiesgo_ConCuatroApuntesDeBancoYDosDeContabilidad_SePuedeContabilizar()
        {
            // Arrange
            var _dialogService = A.Fake<IDialogService>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var regla = new ReglaFinanciacionLineaRiesgo(_dialogService, _recursosHumanosService);
            var apunteBancarioCargo = new ApunteBancarioDTO
            {
                ConceptoComun = "00",
                ConceptoPropio = "000",
                RegistrosConcepto = new List<RegistroComplementarioConcepto>
                {
                    new RegistroComplementarioConcepto
                    {
                        Concepto2 = "Cargo por domiciliación"
                    }
                },
                ImporteMovimiento = -2
            };
            var apunteBancarioAbono = new ApunteBancarioDTO
            {
                ConceptoComun = "02",
                ConceptoPropio = "036",
                RegistrosConcepto = new List<RegistroComplementarioConcepto>
                {
                    new RegistroComplementarioConcepto
                    {
                        Concepto2 = "R2F 0627302000063"
                    }
                },
                ImporteMovimiento = 1.5M
            };
            var apuntesBancarios = new List<ApunteBancarioDTO> {
                apunteBancarioCargo,
                apunteBancarioCargo,
                apunteBancarioAbono,
                apunteBancarioAbono,
            };
            var apuntesContabilidad = new List<ContabilidadDTO> {
                new ContabilidadDTO() { Haber = 3 },
                new ContabilidadDTO() { Haber = 1 },
            };


            // Act
            var resultado = regla.EsContabilizable(apuntesBancarios, apuntesContabilidad);

            // Assert
            Assert.IsTrue(resultado);
        }
    }    
}
