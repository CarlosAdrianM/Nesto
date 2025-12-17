using FakeItEasy;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.Models.ReglasContabilizacion;
using Prism.Services.Dialogs;

namespace CajasTests
{
    [TestClass]
    public class ReglaAyuntamientoMadridTests
    {
        private IDialogService _dialogService = null!;
        private ReglaAyuntamientoMadrid _regla = null!;
        private BancoDTO _banco = null!;

        [TestInitialize]
        public void Setup()
        {
            _dialogService = A.Fake<IDialogService>();
            _regla = new ReglaAyuntamientoMadrid(_dialogService);
            _banco = new BancoDTO
            {
                CuentaContable = "57200001"
            };
        }

        #region EsContabilizable Tests

        [TestMethod]
        public void EsContabilizable_ConCOREAYUNTAMIENTODEMADRID_EnPrimerRegistro_DevuelveTrue()
        {
            // Arrange
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "17",
                ConceptoPropio = "016",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "COREAYUNTAMIENTO DE MADRID", Concepto2 = " IBI" }
                ]
            };

            // Act
            var resultado = _regla.EsContabilizable([apunteBanco], [new ContabilidadDTO()]);

            // Assert
            Assert.IsTrue(resultado);
        }

        [TestMethod]
        public void EsContabilizable_ConCOREAYUNTAMIENTODEMADRID_EnSegundoRegistro_DevuelveTrue()
        {
            // Arrange
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "17",
                ConceptoPropio = "016",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "OTRO CONCEPTO" },
                    new RegistroComplementarioConcepto { Concepto = "COREAYUNTAMIENTO DE MADRID", Concepto2 = " IBI" }
                ]
            };

            // Act
            var resultado = _regla.EsContabilizable([apunteBanco], [new ContabilidadDTO()]);

            // Assert
            Assert.IsTrue(resultado);
        }

        [TestMethod]
        public void EsContabilizable_ConPAGOAYTOMADRID_DevuelveTrue()
        {
            // Arrange
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "17",
                ConceptoPropio = "016",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "PAGO AYTO. MADRID", Concepto2 = " IAE" }
                ]
            };

            // Act
            var resultado = _regla.EsContabilizable([apunteBanco], [new ContabilidadDTO()]);

            // Assert
            Assert.IsTrue(resultado);
        }

        [TestMethod]
        public void EsContabilizable_SinConceptoMadrid_DevuelveFalse()
        {
            // Arrange
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "17",
                ConceptoPropio = "016",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "OTRO CONCEPTO" }
                ]
            };

            // Act
            var resultado = _regla.EsContabilizable([apunteBanco], [new ContabilidadDTO()]);

            // Assert
            Assert.IsFalse(resultado);
        }

        [TestMethod]
        public void EsContabilizable_ConConceptoComunIncorrecto_DevuelveFalse()
        {
            // Arrange
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "02", // Incorrecto, deberia ser "17"
                ConceptoPropio = "016",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "COREAYUNTAMIENTO DE MADRID", Concepto2 = " IBI" }
                ]
            };

            // Act
            var resultado = _regla.EsContabilizable([apunteBanco], [new ContabilidadDTO()]);

            // Assert
            Assert.IsFalse(resultado);
        }

        #endregion

        #region ApuntesContabilizar Tests - Cuenta

        [TestMethod]
        public void ApuntesContabilizar_ConIBI_AsignaCuenta63100000()
        {
            // Arrange
            var apunteBanco = CrearApunteBancarioMadridCORE("COREAYUNTAMIENTO DE MADRID", " IBI 2024");

            // Act
            var resultado = _regla.ApuntesContabilizar([apunteBanco], [new ContabilidadDTO()], _banco);

            // Assert
            Assert.IsNotNull(resultado);
            Assert.AreEqual(1, resultado.Lineas.Count);
            Assert.AreEqual("63100000", resultado.Lineas[0].Cuenta);
        }

        [TestMethod]
        public void ApuntesContabilizar_ConIAE_AsignaCuenta63100001()
        {
            // Arrange
            var apunteBanco = CrearApunteBancarioMadridCORE("COREAYUNTAMIENTO DE MADRID", " IAE 2024");

            // Act
            var resultado = _regla.ApuntesContabilizar([apunteBanco], [new ContabilidadDTO()], _banco);

            // Assert
            Assert.IsNotNull(resultado);
            Assert.AreEqual(1, resultado.Lineas.Count);
            Assert.AreEqual("63100001", resultado.Lineas[0].Cuenta);
        }

        [TestMethod]
        public void ApuntesContabilizar_SinIBIniIAE_AsignaCuenta63100003()
        {
            // Arrange
            var apunteBanco = CrearApunteBancarioMadridCORE("COREAYUNTAMIENTO DE MADRID", " TASA RESIDUOS 2024");

            // Act
            var resultado = _regla.ApuntesContabilizar([apunteBanco], [new ContabilidadDTO()], _banco);

            // Assert
            Assert.IsNotNull(resultado);
            Assert.AreEqual(1, resultado.Lineas.Count);
            Assert.AreEqual("63100003", resultado.Lineas[0].Cuenta);
        }

        #endregion

        #region ApuntesContabilizar Tests - Delegacion

        [TestMethod]
        public void ApuntesContabilizar_AsignaDelegacionREI()
        {
            // Arrange
            var apunteBanco = CrearApunteBancarioMadridCORE("COREAYUNTAMIENTO DE MADRID", " IBI 2024");

            // Act
            var resultado = _regla.ApuntesContabilizar([apunteBanco], [new ContabilidadDTO()], _banco);

            // Assert
            Assert.AreEqual("REI", resultado!.Lineas[0].Delegacion);
        }

        #endregion

        #region ApuntesContabilizar Tests - Concepto Adicional

        [TestMethod]
        public void ApuntesContabilizar_ConPAGOAYTOMADRID_PideConceptoAdicionalYLoIncluye()
        {
            // Arrange
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "17",
                ConceptoPropio = "016",
                ImporteMovimiento = -100m,
                FechaOperacion = DateTime.Today,
                Referencia2 = "1234567890",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto
                    {
                        Concepto = "PAGO AYTO. MADRID",
                        Concepto2 = " IAE 2024"
                    }
                ]
            };

            // Configurar mock para devolver texto
            A.CallTo(() => _dialogService.ShowDialog(
                A<string>.That.IsEqualTo("InputTextDialog"),
                A<IDialogParameters>._,
                A<Action<IDialogResult>>._))
                .Invokes((string name, IDialogParameters p, Action<IDialogResult> callback) =>
                {
                    var resultParams = new DialogParameters { { "text", "IAE 2024" } };
                    callback(new DialogResult(ButtonResult.OK, resultParams));
                });

            // Act
            var resultado = _regla.ApuntesContabilizar([apunteBanco], [new ContabilidadDTO()], _banco);

            // Assert
            Assert.IsNotNull(resultado);
            Assert.AreEqual("Ayto. Madrid IAE 2024", resultado.Lineas[0].Concepto);
        }

        [TestMethod]
        public void ApuntesContabilizar_ConPAGOAYTOMADRID_UsuarioCancela_DevuelveNull()
        {
            // Arrange
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "17",
                ConceptoPropio = "016",
                ImporteMovimiento = -100m,
                FechaOperacion = DateTime.Today,
                Referencia2 = "1234567890",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto
                    {
                        Concepto = "PAGO AYTO. MADRID"
                    }
                ]
            };

            // Configurar mock para simular cancelacion (devuelve null)
            A.CallTo(() => _dialogService.ShowDialog(
                A<string>.That.IsEqualTo("InputTextDialog"),
                A<IDialogParameters>._,
                A<Action<IDialogResult>>._))
                .Invokes((string name, IDialogParameters p, Action<IDialogResult> callback) =>
                {
                    callback(new DialogResult(ButtonResult.Cancel));
                });

            // Act
            var resultado = _regla.ApuntesContabilizar([apunteBanco], [new ContabilidadDTO()], _banco);

            // Assert
            Assert.IsNull(resultado);
        }

        #endregion

        #region ApuntesContabilizar Tests - Concepto desde registro correcto

        [TestMethod]
        public void ApuntesContabilizar_UsaConceptoDelRegistroQueContieneIBIoIAE()
        {
            // Arrange - El concepto de Madrid esta en el tercer registro (indice 2)
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "17",
                ConceptoPropio = "016",
                ImporteMovimiento = -100m,
                FechaOperacion = DateTime.Today,
                Referencia2 = "1234567890",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "OTRO" },
                    new RegistroComplementarioConcepto { Concepto = "OTRO2" },
                    new RegistroComplementarioConcepto { Concepto = "COREAYUNTAMIENTO DE MADRID", Concepto2 = " IBI 2024" }
                ]
            };

            // Act
            var resultado = _regla.ApuntesContabilizar([apunteBanco], [new ContabilidadDTO()], _banco);

            // Assert
            Assert.IsNotNull(resultado);
            // El concepto debe venir del registro[2] que contiene IBI, no del registro[0]
            // FormatearConcepto convierte a TitleCase, por eso buscamos con mayusculas iniciales
            Assert.IsTrue(resultado.Lineas[0].Concepto!.Contains("Ayuntamiento De Madrid Ibi"));
        }

        #endregion

        #region Helper Methods

        private static ApunteBancarioDTO CrearApunteBancarioMadridCORE(string concepto, string concepto2)
        {
            return new ApunteBancarioDTO
            {
                ConceptoComun = "17",
                ConceptoPropio = "016",
                ImporteMovimiento = -100m,
                FechaOperacion = DateTime.Today,
                Referencia2 = "1234567890",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto
                    {
                        Concepto = concepto,
                        Concepto2 = concepto2
                    }
                ]
            };
        }

        #endregion
    }
}
