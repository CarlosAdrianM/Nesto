using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.Models.ReglasContabilizacion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CajasTests
{
    /// <summary>
    /// NestoAPI#384: con las remesas por vencimientos (NestoAPI#345) el banco carga las
    /// comisiones en N apuntes (uno por abono/fecha de cargo), cada uno autocontenido (su nº
    /// de factura en Concepto2, su tipo FR/RC en Referencia2 y su importe). La regla genera
    /// una factura POR APUNTE (1:1 con el punteo del banco) expandiendo la selección a los
    /// hermanos de la misma remesa, y cuadra los recibos deducidos contra los de la remesa.
    /// Datos reales de la remesa 10903 (27/07/26): 5,98 + 0,16 + 4,88 + 2,04 = 83 recibos.
    /// </summary>
    [TestClass]
    public class ReglaComisionRemesaRecibosTests
    {
        private IBancosService _servicio = null!;
        private ReglaComisionRemesaRecibos _regla = null!;
        private BancoDTO _banco = null!;

        [TestInitialize]
        public void Initialize()
        {
            _servicio = A.Fake<IBancosService>();
            _regla = new ReglaComisionRemesaRecibos(_servicio);
            _banco = new BancoDTO { Empresa = "1", Codigo = "5" };
        }

        private static ApunteBancarioDTO Comision(int id, decimal importe, string tipo, string factura)
        {
            return new ApunteBancarioDTO
            {
                Id = id,
                ConceptoComun = "17",
                ConceptoPropio = "036",
                ImporteMovimiento = -importe,
                FechaOperacion = new DateTime(2026, 7, 27),
                Referencia2 = $"A7836825510903{tipo}",
                RegistrosConcepto = new List<RegistroComplementarioConcepto>
                {
                    new RegistroComplementarioConcepto { Concepto2 = $"PR.FA{factura}" },
                    new RegistroComplementarioConcepto { Concepto2 = "PREC.FATUR.DOMICIL" }
                }
            };
        }

        // Abono TIR de la remesa (no es comisión): la expansión debe ignorarlo.
        private static ApunteBancarioDTO AbonoRemesa()
        {
            return new ApunteBancarioDTO
            {
                Id = 14542,
                ConceptoComun = "17",
                ConceptoPropio = "036",
                ImporteMovimiento = 8574.65M,
                FechaOperacion = new DateTime(2026, 7, 27),
                Referencia2 = "A7836825510903RC",
                RegistrosConcepto = new List<RegistroComplementarioConcepto>
                {
                    new RegistroComplementarioConcepto { Concepto2 = "FATIR189642364" },
                    new RegistroComplementarioConcepto { Concepto2 = "TRANSF. IMPORTE REM." }
                }
            };
        }

        private void ConApuntesDelDia(params ApunteBancarioDTO[] apuntes)
        {
            A.CallTo(() => _servicio.LeerApuntesBanco("1", "5", new DateTime(2026, 7, 27), new DateTime(2026, 7, 27)))
                .Returns(Task.FromResult(apuntes.ToList()));
        }

        private static List<ContabilidadDTO> ApuntesContabilidad()
        {
            return new List<ContabilidadDTO> { new ContabilidadDTO { Debe = 13.06M } };
        }

        [TestMethod]
        public void ApuntesContabilizar_SeleccionandoUnSoloApunte_GeneraUnaFacturaPorCadaApunteDeLaRemesa()
        {
            var seleccionado = Comision(14547, 5.98M, "RC", "189642364");
            ConApuntesDelDia(
                AbonoRemesa(),
                seleccionado,
                Comision(14548, 0.16M, "FR", "189642365"),
                Comision(14549, 4.88M, "RC", "189642366"),
                Comision(14550, 2.04M, "RC", "189642367"));
            A.CallTo(() => _servicio.NumeroRecibosRemesa("10903")).Returns(Task.FromResult(83));

            var respuesta = _regla.ApuntesContabilizar(new List<ApunteBancarioDTO> { seleccionado }, ApuntesContabilidad(), _banco);

            Assert.AreEqual(4, respuesta.Lineas.Count, "Una factura por apunte del banco (1:1 con el punteo)");
            CollectionAssert.AreEqual(new List<string> { "189642364", "189642365", "189642366", "189642367" },
                respuesta.Lineas.Select(l => l.Documento).ToList(), "Cada línea lleva el nº de factura de SU apunte");
            // 38, 1, 31 y 13 recibos a 0,13 €: la base de cada factura casa con su apunte
            CollectionAssert.AreEqual(new List<decimal> { 4.94M, 0.13M, 4.03M, 1.69M },
                respuesta.Lineas.Select(l => l.Debe).ToList());
            StringAssert.Contains(respuesta.Lineas[1].Concepto, "FRST", "El apunte FR se etiqueta como FRST");
            StringAssert.Contains(respuesta.Lineas[0].Concepto, "RCUR");
            Assert.IsTrue(respuesta.CrearFacturas);
            Assert.AreEqual("10903", respuesta.Documento);
        }

        [TestMethod]
        public void ApuntesContabilizar_LosRecibosNoCuadranConLaRemesa_LanzaConMensajeClaro()
        {
            // Falta un apunte (p. ej. comisión cargada en otra fecha): 38+1+31 = 70 ≠ 83.
            var seleccionado = Comision(14547, 5.98M, "RC", "189642364");
            ConApuntesDelDia(
                seleccionado,
                Comision(14548, 0.16M, "FR", "189642365"),
                Comision(14549, 4.88M, "RC", "189642366"));
            A.CallTo(() => _servicio.NumeroRecibosRemesa("10903")).Returns(Task.FromResult(83));

            var ex = Assert.ThrowsException<Exception>(() =>
                _regla.ApuntesContabilizar(new List<ApunteBancarioDTO> { seleccionado }, ApuntesContabilidad(), _banco));

            StringAssert.Contains(ex.Message, "70");
            StringAssert.Contains(ex.Message, "83");
            StringAssert.Contains(ex.Message, "10903");
        }

        [TestMethod]
        public void ApuntesContabilizar_ApuntesDeOtraRemesa_NoSeCuelan()
        {
            var seleccionado = Comision(14547, 5.98M, "RC", "189642364");
            var otraRemesa = Comision(14560, 0.16M, "FR", "189699999");
            otraRemesa.Referencia2 = "A7836825510904FR"; // remesa 10904
            ConApuntesDelDia(seleccionado, otraRemesa);
            A.CallTo(() => _servicio.NumeroRecibosRemesa("10903")).Returns(Task.FromResult(38));

            var respuesta = _regla.ApuntesContabilizar(new List<ApunteBancarioDTO> { seleccionado }, ApuntesContabilidad(), _banco);

            Assert.AreEqual(1, respuesta.Lineas.Count);
            Assert.AreEqual("189642364", respuesta.Lineas.Single().Documento);
        }

        [TestMethod]
        public void ApuntesContabilizar_SiFallaLaCargaDeHermanos_ContabilizaLosSeleccionadosSiCuadran()
        {
            // Best-effort: sin poder cargar el día completo, valen los seleccionados (y el
            // cuadre de recibos protege si faltara alguno).
            var seleccionados = new List<ApunteBancarioDTO>
            {
                Comision(14547, 5.98M, "RC", "189642364"),
                Comision(14548, 0.16M, "FR", "189642365"),
                Comision(14549, 4.88M, "RC", "189642366"),
                Comision(14550, 2.04M, "RC", "189642367")
            };
            A.CallTo(() => _servicio.LeerApuntesBanco(A<string>.Ignored, A<string>.Ignored, A<DateTime>.Ignored, A<DateTime>.Ignored))
                .Throws(new Exception("API caída"));
            A.CallTo(() => _servicio.NumeroRecibosRemesa("10903")).Returns(Task.FromResult(83));

            var respuesta = _regla.ApuntesContabilizar(seleccionados, ApuntesContabilidad(), _banco);

            Assert.AreEqual(4, respuesta.Lineas.Count);
        }
    }
}
