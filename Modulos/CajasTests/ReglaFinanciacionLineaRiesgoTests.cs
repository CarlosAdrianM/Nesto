using FakeItEasy;
using Nesto.Modulos.Cajas.Interfaces;
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

        [TestMethod]
        public void ReglaFinanciacionLineaRiesgo_SiSeHaAplazadoElImporteParcialmente_SePuedeContabilizar()
        {
            // Por ejemplo: se han aplazado 15000 € de un pago de 20000€. Tendríamos un apunte de banco de -20000€ y otro de -5000 menos intereses y dos de contabilidad de -15000€ y -5000€

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
                ImporteMovimiento = -20000
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
                ImporteMovimiento = 14500 // 15000 - 500 de intereses
            };
            var apuntesBancarios = new List<ApunteBancarioDTO> {
                apunteBancarioCargo,
                apunteBancarioAbono,
            };
            var apuntesContabilidad = new List<ContabilidadDTO> {
                new ContabilidadDTO() { Haber = 15000 },
                new ContabilidadDTO() { Haber = 5000 },
            };

            // Act
            var resultado = regla.EsContabilizable(apuntesBancarios, apuntesContabilidad);

            // Assert
            Assert.IsTrue(resultado);
        }

        [TestMethod]
        public void ReglaFinanciacionLineaRiesgo_SiSeHaAplazadoElImporteTotalmente_ApuntesContabilizarEstaCuadradoElDebeYElHaber()
        {
            // Arrange            
            var _dialogService = A.Fake<IDialogService>();
            // Configuramos el fake para que siempre responda "Sí"
            A.CallTo(() => _dialogService.ShowDialog(
                A<string>.That.Matches(x => x == "ConfirmationDialog"),
                A<IDialogParameters>.Ignored,
                A<Action<IDialogResult>>.Ignored
            )).Invokes((string name, IDialogParameters parameters, Action<IDialogResult> callback) => {
                var result = new DialogResult(ButtonResult.OK);
                callback(result);
            });
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
                ImporteMovimiento = -15000
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
                ImporteMovimiento = 14500 // 15000 - 500 de intereses
            };
            var apuntesBancarios = new List<ApunteBancarioDTO> {
                apunteBancarioCargo,
                apunteBancarioAbono,
            };
            var apuntesContabilidad = new List<ContabilidadDTO> {
                new ContabilidadDTO() { Haber = 15000 }
            };
            var banco = new BancoDTO { CuentaContable = "57200000" };

            // Act
            var resultado = regla.ApuntesContabilizar(apuntesBancarios, apuntesContabilidad, banco);

            // Assert
            Assert.AreEqual(0, resultado.Lineas.Sum(c => c.Importe)); // Está cuadrado el debe y el haber
            Assert.AreEqual(500, resultado.Lineas.Where(c => c.Concepto.StartsWith("Interés")).Sum(c => c.Importe));
            // Comprobamos que las líneas del contabilizacion de la cuenta de banco que empiezan por "Financiacion" son 15000 €
            Assert.AreEqual(15000, resultado.Lineas.Where(c => c.Contrapartida == banco.CuentaContable && c.Concepto.StartsWith("Financiación")).Sum(c => c.Debe));
            // Comprobamos que las líneas del contabilizacion del banco que no empiezan por "Financiacion" son 14500 €
            Assert.AreEqual(14500, resultado.Lineas.Where(c => c.Cuenta == banco.CuentaContable && !c.Concepto.StartsWith("Financiacion")).Sum(c => c.Importe));
        }

        [TestMethod]
        public void ReglaFinanciacionLineaRiesgo_SiSeHaAplazadoElImporteParcialmente_ApuntesContabilizarEstaCuadradoElDebeYElHaber()
        {
            // Por ejemplo: se han aplazado 15000 € de un pago de 20000 €

            // Arrange            
            var _dialogService = A.Fake<IDialogService>();
            // Configuramos el fake para que siempre responda "Sí"
            A.CallTo(() => _dialogService.ShowDialog(
                A<string>.That.Matches(x => x == "ConfirmationDialog"),
                A<IDialogParameters>.Ignored,
                A<Action<IDialogResult>>.Ignored
            )).Invokes((string name, IDialogParameters parameters, Action<IDialogResult> callback) => {
                var result = new DialogResult(ButtonResult.OK);
                callback(result);
            });
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
                ImporteMovimiento = -20000
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
                ImporteMovimiento = 14500 // 15000 - 500 de intereses
            };
            var apuntesBancarios = new List<ApunteBancarioDTO> {
                apunteBancarioCargo,
                apunteBancarioAbono,
            };
            var apuntesContabilidad = new List<ContabilidadDTO> {
                new ContabilidadDTO() { Haber = 15000 },
                new ContabilidadDTO() { Haber = 5000 },
            };
            var banco = new BancoDTO { CuentaContable = "57200000"};
            
            // Act
            var resultado = regla.ApuntesContabilizar(apuntesBancarios, apuntesContabilidad, banco);

            // Assert
            Assert.AreEqual(0, resultado.Lineas.Sum(c => c.Importe)); // Está cuadrado el debe y el haber
            Assert.AreEqual(500, resultado.Lineas.Where(c => c.Concepto.StartsWith("Interés")).Sum(c => c.Importe));
            // Comprobamos que las líneas del contabilizacion de la cuenta de banco que empiezan por "Financiacion" son 15000 €
            Assert.AreEqual(15000, resultado.Lineas.Where(c => c.Contrapartida == banco.CuentaContable && c.Concepto.StartsWith("Financiación")).Sum(c => c.Debe));
            // Comprobamos que las líneas del contabilizacion del banco que no empiezan por "Financiacion" son 14500 €
            Assert.AreEqual(14500, resultado.Lineas.Where(c => c.Cuenta == banco.CuentaContable && !c.Concepto.StartsWith("Financiacion")).Sum(c => c.Importe));
        }
    }
}
