using ControlesUsuario.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>Nesto#571 (NestoAPI#739): el diálogo de la prueba de entrega del reparto propio.</summary>
    [TestClass]
    public class PruebaEntregaDialogViewModelTests
    {
        private static PruebaEntregaDialogViewModel Abrir(string cobro, decimal? importe, bool tieneFirma, byte[] firma)
        {
            var vm = new PruebaEntregaDialogViewModel();
            vm.OnDialogOpened(new ParametrosDialogo
            {
                { PruebaEntregaDialogViewModel.PARAMETRO_ENVIO, 251500 },
                { PruebaEntregaDialogViewModel.PARAMETRO_PEDIDO, "930001" },
                { PruebaEntregaDialogViewModel.PARAMETRO_FECHA_HORA, new DateTime(2026, 10, 29, 12, 34, 0) },
                { PruebaEntregaDialogViewModel.PARAMETRO_RECEPTOR, " Ana López " },
                { PruebaEntregaDialogViewModel.PARAMETRO_DNI, null },
                { PruebaEntregaDialogViewModel.PARAMETRO_COBRO_REEMBOLSO, cobro },
                { PruebaEntregaDialogViewModel.PARAMETRO_IMPORTE_COBRADO, importe },
                { PruebaEntregaDialogViewModel.PARAMETRO_USUARIO, "Pedro" },
                { PruebaEntregaDialogViewModel.PARAMETRO_TIENE_FIRMA, tieneFirma },
                { PruebaEntregaDialogViewModel.PARAMETRO_FIRMA, firma }
            });
            return vm;
        }

        [TestMethod]
        public void Abrir_PintaLosDatos()
        {
            var vm = Abrir("Efectivo", 123.45M, tieneFirma: true, firma: new byte[] { 1, 2, 3 });

            Assert.AreEqual("Envío 251500 (pedido 930001)", vm.Cabecera);
            Assert.AreEqual("Prueba de entrega del envío 251500", vm.Title);
            Assert.AreEqual("Ana López", vm.Receptor);
            Assert.AreEqual("-", vm.Dni, "Sin DNI (entregó con firma), un guion");
            Assert.AreEqual("29/10/2026 a las 12:34", vm.FechaHora);
            Assert.AreEqual("Pedro", vm.Usuario);
            Assert.IsTrue(vm.HayFirma);
        }

        [TestMethod]
        public void TextoCobro_CadaFormaDeCobro()
        {
            StringAssert.StartsWith(PruebaEntregaDialogViewModel.TextoCobro("Efectivo", 123.45M), "Cobrado en efectivo: 123,45");
            StringAssert.StartsWith(PruebaEntregaDialogViewModel.TextoCobro("Tarjeta", 80M), "Cobrado con tarjeta: 80,00");
            Assert.AreEqual("No se cobró el reembolso", PruebaEntregaDialogViewModel.TextoCobro("NoCobrado", 0M));
            Assert.AreEqual("Sin reembolso", PruebaEntregaDialogViewModel.TextoCobro(null, null));
            Assert.AreEqual("Sin reembolso", PruebaEntregaDialogViewModel.TextoCobro("", null));
        }

        [TestMethod]
        public void SinFirma_DiceQueSeEntregoConDni()
        {
            var vm = Abrir(null, null, tieneFirma: false, firma: null);

            Assert.IsFalse(vm.HayFirma);
            Assert.AreEqual("Sin firma (se entregó con DNI).", vm.TextoSinFirma);
            Assert.AreEqual("Sin reembolso", vm.Cobro);
        }

        [TestMethod]
        public void FirmaQueNoSePudoLeer_LoDiceDeUsted()
        {
            var vm = Abrir(null, null, tieneFirma: true, firma: null);

            Assert.IsFalse(vm.HayFirma);
            StringAssert.Contains(vm.TextoSinFirma, "Inténtelo de nuevo");
        }

        [TestMethod]
        public void Cerrar_CierraConOk()
        {
            var vm = Abrir(null, null, tieneFirma: false, firma: null);
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.CerrarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
        }
    }
}
