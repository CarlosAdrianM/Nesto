using ControlesUsuario.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// NestoAPI#699 (corte 2): el diálogo «Volver a enviar» de un envío devuelto. Trae la dirección y los teléfonos del envío
    /// para corregirlos y la casilla de portes desmarcada; «Volver a enviar» devuelve lo tecleado.
    /// </summary>
    [TestClass]
    public class ReenviarEnvioDialogViewModelTests
    {
        private static ReenviarEnvioDialogViewModel Abierto()
        {
            var vm = new ReenviarEnvioDialogViewModel();
            vm.OnDialogOpened(new ParametrosDialogo
            {
                { ReenviarEnvioDialogViewModel.PARAMETRO_ENVIO, 251300 },
                { ReenviarEnvioDialogViewModel.PARAMETRO_PEDIDO, "925137" },
                { ReenviarEnvioDialogViewModel.PARAMETRO_NOMBRE, "Peluquería Ejemplo  " },
                { ReenviarEnvioDialogViewModel.PARAMETRO_DIRECCION, "Calle Vieja 1   " },
                { ReenviarEnvioDialogViewModel.PARAMETRO_COD_POSTAL, "28001" },
                { ReenviarEnvioDialogViewModel.PARAMETRO_POBLACION, "Madrid" },
                { ReenviarEnvioDialogViewModel.PARAMETRO_TELEFONO, "910000000" }
            });
            return vm;
        }

        [TestMethod]
        public void AlAbrir_LosDatosDelEnvioYLosPortesDesmarcados()
        {
            ReenviarEnvioDialogViewModel vm = Abierto();

            Assert.AreEqual("Pedido 925137 (envío 251300): Peluquería Ejemplo", vm.Cabecera);
            Assert.AreEqual("Volver a enviar el envío 251300", vm.Title);
            Assert.AreEqual("Calle Vieja 1", vm.Direccion);
            Assert.AreEqual("28001", vm.CodPostal);
            Assert.AreEqual("910000000", vm.Telefono);
            Assert.AreEqual(string.Empty, vm.Movil);
            Assert.IsFalse(vm.IncluirPortes, "Los portes del reenvío nos los comemos por defecto");
            Assert.IsTrue(vm.AceptarCommand.CanExecute(null));
        }

        [TestMethod]
        public void SinDireccionOSinCodigoPostal_NoSePuedeAceptar()
        {
            ReenviarEnvioDialogViewModel vm = Abierto();

            vm.Direccion = "  ";
            Assert.IsFalse(vm.AceptarCommand.CanExecute(null));
            vm.Direccion = "Calle Nueva 7";
            vm.CodPostal = "";
            Assert.IsFalse(vm.AceptarCommand.CanExecute(null));
        }

        [TestMethod]
        public void Aceptar_DevuelveLoCorregidoYLaCasilla()
        {
            ReenviarEnvioDialogViewModel vm = Abierto();
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;
            vm.Direccion = " Calle Nueva 7 ";
            vm.IncluirPortes = true;

            vm.AceptarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
            Assert.AreEqual("Calle Nueva 7", resultado.Parameters.GetValue<string>(ReenviarEnvioDialogViewModel.PARAMETRO_DIRECCION));
            Assert.AreEqual("28001", resultado.Parameters.GetValue<string>(ReenviarEnvioDialogViewModel.PARAMETRO_COD_POSTAL));
            Assert.IsTrue(resultado.Parameters.GetValue<bool>(ReenviarEnvioDialogViewModel.PARAMETRO_INCLUIR_PORTES));
        }

        [TestMethod]
        public void Cancelar_DevuelveCancel()
        {
            ReenviarEnvioDialogViewModel vm = Abierto();
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.CancelarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
        }
    }
}
