using FakeItEasy;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.Models.ReglasContabilizacion;
using Prism.Services.Dialogs;

namespace CajasTests
{
    /// <summary>
    /// Nesto#502: las reglas que salen solo del movimiento bancario (Adelantos nómina, Asociación de
    /// Esteticistas, Ayuntamiento de Madrid, Comisión de remesa de recibos) exigían algún apunte de
    /// contabilidad seleccionado sin usarlo. Si no había ninguno (movimiento 15398, 28/09/26), no generaban
    /// nada: «La regla no ha generado ningún apunte con lo seleccionado».
    /// </summary>
    [TestClass]
    public class ReglasSinApuntesContabilidadTests
    {
        private readonly BancoDTO _banco = new() { Empresa = "1", Codigo = "5", CuentaContable = "57200013" };

        [TestMethod]
        public void AdelantosNomina_SinApuntesDeContabilidad_GeneraLaLineaDel460()
        {
            var adelanto = new ApunteBancarioDTO
            {
                Id = 15398,
                ConceptoComun = "04",
                ConceptoPropio = "002",
                ImporteMovimiento = -275M,
                FechaOperacion = new DateTime(2026, 9, 21),
                Referencia2 = "0000000000123456789",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "TRANSFERENCIA" },
                    new RegistroComplementarioConcepto { Concepto = "ADELANTO NOMINA NUEVA VISION" },
                    new RegistroComplementarioConcepto { Concepto = "SEPTIEMBRE" }
                ]
            };
            var regla = new ReglaAdelantosNomina();

            Assert.IsTrue(regla.EsContabilizable([adelanto], []));
            ReglaContabilizacionResponse respuesta = regla.ApuntesContabilizar([adelanto], [], _banco);

            Assert.AreEqual(1, respuesta.Lineas.Count);
            Assert.AreEqual("46000000", respuesta.Lineas[0].Cuenta);
            Assert.AreEqual(275M, respuesta.Lineas[0].Debe);
            Assert.AreEqual("57200013", respuesta.Lineas[0].Contrapartida);
            Assert.AreEqual(1, regla.ApuntesContabilizar([adelanto], null!, _banco).Lineas.Count, "Tampoco con null");
        }

        [TestMethod]
        public void AyuntamientoMadrid_SinApuntesDeContabilidad_GeneraLaLinea()
        {
            var ibi = new ApunteBancarioDTO
            {
                ConceptoComun = "17",
                ConceptoPropio = "016",
                ImporteMovimiento = -120M,
                FechaOperacion = new DateTime(2026, 9, 21),
                Referencia2 = "0000000000123456789",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "COREAYUNTAMIENTO DE MADRID", Concepto2 = " IBI 2026" }
                ]
            };

            ReglaContabilizacionResponse respuesta = new ReglaAyuntamientoMadrid(A.Fake<IDialogService>()).ApuntesContabilizar([ibi], [], _banco);

            Assert.AreEqual(1, respuesta.Lineas.Count);
            Assert.AreEqual("63100000", respuesta.Lineas[0].Cuenta);
        }

        [TestMethod]
        public void AsociacionEsteticistas_SinApuntesDeContabilidad_GeneraLaLinea()
        {
            var curso = new ApunteBancarioDTO
            {
                ConceptoComun = "03",
                ConceptoPropio = "049",
                ImporteMovimiento = -80M,
                FechaOperacion = new DateTime(2026, 9, 21),
                Referencia2 = "0000000000123456789",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto = "COREASOCIACION DE ESTETICISTAS DE LA C" },
                    new RegistroComplementarioConcepto { Concepto = "" },
                    new RegistroComplementarioConcepto { Concepto = "CUOTA" },
                    new RegistroComplementarioConcepto { Concepto = "0123456789012345678901234PROFESORA NOELIA", Concepto2 = "NOELIA" }
                ]
            };

            ReglaContabilizacionResponse respuesta = new ReglaAsociacionEsteticistas().ApuntesContabilizar([curso], [], _banco);

            Assert.AreEqual(1, respuesta.Lineas.Count);
            Assert.AreEqual("62920000", respuesta.Lineas[0].Cuenta);
            Assert.AreEqual(80M, respuesta.Lineas[0].Debe);
        }

        [TestMethod]
        public void ComisionRemesaRecibos_SinApuntesDeContabilidad_GeneraLasFacturas()
        {
            IBancosService servicio = A.Fake<IBancosService>();
            var comision = new ApunteBancarioDTO
            {
                Id = 14547,
                ConceptoComun = "17",
                ConceptoPropio = "036",
                ImporteMovimiento = -5.98M,
                FechaOperacion = new DateTime(2026, 7, 27),
                Referencia2 = "A7836825510903RC",
                RegistrosConcepto =
                [
                    new RegistroComplementarioConcepto { Concepto2 = "PR.FA189642364" },
                    new RegistroComplementarioConcepto { Concepto2 = "PREC.FATUR.DOMICIL" }
                ]
            };
            A.CallTo(() => servicio.LeerApuntesBanco("1", "5", new DateTime(2026, 7, 27), new DateTime(2026, 7, 27)))
                .Returns(Task.FromResult(new List<ApunteBancarioDTO> { comision }));
            A.CallTo(() => servicio.NumeroRecibosRemesa("10903")).Returns(Task.FromResult(38));

            ReglaContabilizacionResponse respuesta = new ReglaComisionRemesaRecibos(servicio).ApuntesContabilizar([comision], [], _banco);

            Assert.AreEqual(1, respuesta.Lineas.Count);
            Assert.IsTrue(respuesta.CrearFacturas);
        }
    }
}
