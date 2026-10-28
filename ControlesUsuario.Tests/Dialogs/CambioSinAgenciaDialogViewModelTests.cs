using ControlesUsuario.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>Nesto#564 (NestoAPI#725): el aviso en rojo del cambio que no llega a la agencia.</summary>
    [TestClass]
    public class CambioSinAgenciaDialogViewModelTests
    {
        private static CambioSinAgenciaDialogViewModel Abrir(bool pedirTexto)
        {
            var vm = new CambioSinAgenciaDialogViewModel();
            vm.OnDialogOpened(new ParametrosDialogo
            {
                { CambioSinAgenciaDialogViewModel.PARAMETRO_MENSAJE, "aviso" },
                { CambioSinAgenciaDialogViewModel.PARAMETRO_PEDIR_TEXTO, pedirTexto }
            });
            return vm;
        }

        [TestMethod]
        public void PrimeraPregunta_SePuedeAceptarSinTexto()
        {
            var vm = Abrir(pedirTexto: false);

            Assert.IsTrue(vm.AceptarCommand.CanExecute(null));
            Assert.AreEqual("aviso", vm.Message);
        }

        [TestMethod]
        public void SegundaPregunta_SinTextoOCorto_NoSePuedeAceptar_YConTextoDevuelveElTexto()
        {
            var vm = Abrir(pedirTexto: true);
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            Assert.IsFalse(vm.AceptarCommand.CanExecute(null));
            vm.Texto = " CTT ";
            Assert.IsFalse(vm.AceptarCommand.CanExecute(null), "al menos 5 caracteres sin contar espacios");
            vm.Texto = " Marta de CTT, 28/10 ";
            Assert.IsTrue(vm.AceptarCommand.CanExecute(null));

            vm.AceptarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
            Assert.AreEqual("Marta de CTT, 28/10", resultado.Parameters.GetValue<string>(CambioSinAgenciaDialogViewModel.PARAMETRO_TEXTO));
        }

        [TestMethod]
        public void Cancelar_DevuelveCancel()
        {
            var vm = Abrir(pedirTexto: true);
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.CancelarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
        }

        [TestMethod]
        public void Mensaje_DeUstedConLosCamposYElOtroBoton()
        {
            string mensaje = PeticionCambioSinAgencia.Mensaje(new[] { "Retorno", "Reembolso" });

            StringAssert.Contains(mensaje, "campos: Retorno, Reembolso");
            StringAssert.Contains(mensaje, "NUESTRO sistema pero NO en el de la agencia");
            StringAssert.Contains(mensaje, "Solo debe hacerse si ha hablado con la agencia");
            StringAssert.Contains(mensaje, "«Modificar datos y reenviar a la agencia»");
        }
    }
}
