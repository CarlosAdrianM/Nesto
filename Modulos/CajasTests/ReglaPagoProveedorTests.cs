using FakeItEasy;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.Models.ReglasContabilizacion;

namespace CajasTests
{
    [TestClass]
    public class ReglaPagoProveedorTests
    {
        private IBancosService _bancosService;
        private ReglaPagoProveedor _regla;

        [TestInitialize]
        public void Setup()
        {
            _bancosService = A.Fake<IBancosService>();
            _regla = new ReglaPagoProveedor(_bancosService);
        }

        #region Tests para el bug de IndexOutOfRange con 3 registros

        [TestMethod]
        public void EsContabilizable_PagoNacionalCon3Registros_ProveedorNoEncontrado_DevuelveFalseSinExcepcion()
        {
            // Arrange - Simula el caso del bug: 3 registros, proveedor en registro 2 no encontrado
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "99",
                ConceptoPropio = "067",
                ImporteMovimiento = -100m,
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "Referencia" },
                    new RegistroComplementarioConcepto { Concepto = "Ordenante Pago" },
                    new RegistroComplementarioConcepto { Concepto = "Nombre Proveedor Inexistente" }
                    // Solo 3 registros (indices 0, 1, 2) - NO hay indice 3
                ]
            };

            // El servicio devuelve null/empty para cualquier proveedor (no lo encuentra)
            A.CallTo(() => _bancosService.LeerProveedorPorNombre(A<string>.Ignored))
                .Returns(Task.FromResult<string>(null));

            // Act - NO debe lanzar ArgumentOutOfRangeException
            bool resultado = _regla.EsContabilizable(new[] { apunteBanco }, Array.Empty<ContabilidadDTO>());

            // Assert
            Assert.IsFalse(resultado, "Debe devolver false cuando el proveedor no se encuentra");
        }

        [TestMethod]
        public void EsContabilizable_PagoNacionalCon3Registros_ProveedorEncontradoEnRegistro2_DevuelveTrue()
        {
            // Arrange - 3 registros, proveedor encontrado en registro 2
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "99",
                ConceptoPropio = "067",
                ImporteMovimiento = -100m,
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "Referencia" },
                    new RegistroComplementarioConcepto { Concepto = "Ordenante Pago" },
                    new RegistroComplementarioConcepto { Concepto = "PROVEEDOR CONOCIDO" }
                ]
            };

            A.CallTo(() => _bancosService.LeerProveedorPorNombre("PROVEEDOR CONOCIDO"))
                .Returns(Task.FromResult("400001"));
            A.CallTo(() => _bancosService.PagoPendienteUnico("400001", -100m))
                .Returns(Task.FromResult(new ExtractoProveedorDTO { Proveedor = "400001", Id = 1 }));

            // Act
            bool resultado = _regla.EsContabilizable(new[] { apunteBanco }, Array.Empty<ContabilidadDTO>());

            // Assert
            Assert.IsTrue(resultado);
        }

        [TestMethod]
        public void EsContabilizable_PagoNacionalCon4Registros_ProveedorEnRegistro3_DevuelveTrue()
        {
            // Arrange - 4 registros, proveedor NO en registro 2, SI en registro 3
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "99",
                ConceptoPropio = "067",
                ImporteMovimiento = -100m,
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "Referencia" },
                    new RegistroComplementarioConcepto { Concepto = "Ordenante Pago" },
                    new RegistroComplementarioConcepto { Concepto = "Texto sin proveedor" },
                    new RegistroComplementarioConcepto { Concepto = "PROVEEDOR CONOCIDO" }
                ]
            };

            // Registro 2 no encuentra proveedor, registro 3 sí
            A.CallTo(() => _bancosService.LeerProveedorPorNombre("Texto sin proveedor"))
                .Returns(Task.FromResult<string>(null));
            A.CallTo(() => _bancosService.LeerProveedorPorNombre("PROVEEDOR CONOCIDO"))
                .Returns(Task.FromResult("400001"));
            A.CallTo(() => _bancosService.PagoPendienteUnico("400001", -100m))
                .Returns(Task.FromResult(new ExtractoProveedorDTO { Proveedor = "400001", Id = 1 }));

            // Act
            bool resultado = _regla.EsContabilizable(new[] { apunteBanco }, Array.Empty<ContabilidadDTO>());

            // Assert
            Assert.IsTrue(resultado);
        }

        #endregion

        #region Tests para TransferenciaInternacional con 3 registros

        [TestMethod]
        public void EsContabilizable_TransferenciaInternacionalCon3Registros_ProveedorNoEncontrado_DevuelveFalseSinExcepcion()
        {
            // Arrange - Transferencia internacional con 3 registros
            // ConceptoCompleto es calculado: $"{Concepto}{Concepto2}".Trim()
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "04",
                ConceptoPropio = "002",
                ImporteMovimiento = -500m,
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "Invoice 12345" }, // ConceptoCompleto = "Invoice 12345"
                    new RegistroComplementarioConcepto { Concepto = "Referencia" },
                    new RegistroComplementarioConcepto { Concepto = "Proveedor Desconocido" }
                    // Solo 3 registros - NO hay indice 3
                ]
            };

            A.CallTo(() => _bancosService.LeerProveedorPorNombre(A<string>.Ignored))
                .Returns(Task.FromResult<string>(null));

            // Act - NO debe lanzar ArgumentOutOfRangeException
            bool resultado = _regla.EsContabilizable(new[] { apunteBanco }, Array.Empty<ContabilidadDTO>());

            // Assert
            Assert.IsFalse(resultado, "Debe devolver false cuando el proveedor no se encuentra");
        }

        #endregion

        #region Tests para ReciboDomiciliado

        [TestMethod]
        public void EsContabilizable_ReciboDomiciliadoConNifCorto_DevuelveFalseSinExcepcion()
        {
            // Arrange - Recibo domiciliado pero el concepto es demasiado corto para extraer NIF
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "03",
                ConceptoPropio = "038",
                ImporteMovimiento = -200m,
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "CORE" },
                    new RegistroComplementarioConcepto { Concepto = "123" } // Muy corto, menos de 16 caracteres
                ]
            };

            // Act - NO debe lanzar excepción por Substring
            bool resultado = _regla.EsContabilizable(new[] { apunteBanco }, Array.Empty<ContabilidadDTO>());

            // Assert
            Assert.IsFalse(resultado);
        }

        [TestMethod]
        public void EsContabilizable_ReciboDomiciliadoConNifValido_ProveedorEncontrado_DevuelveTrue()
        {
            // Arrange
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "03",
                ConceptoPropio = "038",
                ImporteMovimiento = -200m,
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "CORE" },
                    new RegistroComplementarioConcepto { Concepto = "1234567B12345678X9012345" } // >= 16 caracteres, NIF en posiciones 7-15
                ]
            };

            A.CallTo(() => _bancosService.LeerProveedorPorNif("B12345678"))
                .Returns(Task.FromResult("400002"));
            A.CallTo(() => _bancosService.PagoPendienteUnico("400002", -200m))
                .Returns(Task.FromResult(new ExtractoProveedorDTO { Proveedor = "400002", Id = 2 }));

            // Act
            bool resultado = _regla.EsContabilizable(new[] { apunteBanco }, Array.Empty<ContabilidadDTO>());

            // Assert
            Assert.IsTrue(resultado);
        }

        #endregion

        #region Tests generales

        [TestMethod]
        public void EsContabilizable_ApuntesBancariosNull_DevuelveFalse()
        {
            // Act
            bool resultado = _regla.EsContabilizable(null, Array.Empty<ContabilidadDTO>());

            // Assert
            Assert.IsFalse(resultado);
        }

        [TestMethod]
        public void EsContabilizable_ApuntesBancariosVacios_DevuelveFalse()
        {
            // Act
            bool resultado = _regla.EsContabilizable(Array.Empty<ApunteBancarioDTO>(), Array.Empty<ContabilidadDTO>());

            // Assert
            Assert.IsFalse(resultado);
        }

        [TestMethod]
        public void EsContabilizable_ConceptoNoReconocido_DevuelveFalse()
        {
            // Arrange - Concepto que no es pago nacional, ni recibo domiciliado, ni transferencia internacional
            var apunteBanco = new ApunteBancarioDTO
            {
                ConceptoComun = "01",
                ConceptoPropio = "001",
                ImporteMovimiento = -100m,
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "Algo" }
                ]
            };

            // Act
            bool resultado = _regla.EsContabilizable(new[] { apunteBanco }, Array.Empty<ContabilidadDTO>());

            // Assert
            Assert.IsFalse(resultado);
        }

        #endregion
    }
}
