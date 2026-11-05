using ControlesUsuario.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Nesto#625 (NestoAPI#726): el diálogo «Cambiar dirección de entrega» de un envío de CTT que ya viaja. Se abre con los datos
    /// del envío; teléfono y correo son obligatorios (CTT los exige) y sin ellos no se puede aceptar.
    /// </summary>
    [TestClass]
    public class DireccionEntregaEnvioDialogViewModelTests
    {
        private static DireccionEntregaEnvioDialogViewModel Abierto(string pais = null)
        {
            var vm = new DireccionEntregaEnvioDialogViewModel();
            vm.OnDialogOpened(new ParametrosDialogo
            {
                { DireccionEntregaEnvioDialogViewModel.PARAMETRO_ENVIO, 251300 },
                { DireccionEntregaEnvioDialogViewModel.PARAMETRO_PEDIDO, "929000" },
                { DireccionEntregaEnvioDialogViewModel.PARAMETRO_NOMBRE, "Peluquería Ejemplo  " },
                { DireccionEntregaEnvioDialogViewModel.PARAMETRO_DIRECCION, "Calle Vieja 1   " },
                { DireccionEntregaEnvioDialogViewModel.PARAMETRO_COD_POSTAL, "28001" },
                { DireccionEntregaEnvioDialogViewModel.PARAMETRO_POBLACION, "Madrid" },
                { DireccionEntregaEnvioDialogViewModel.PARAMETRO_PAIS, pais },
                { DireccionEntregaEnvioDialogViewModel.PARAMETRO_TELEFONO, "600111222" },
                { DireccionEntregaEnvioDialogViewModel.PARAMETRO_CORREO, "cliente@ejemplo.es" }
            });
            return vm;
        }

        [TestMethod]
        public void AlAbrir_LosDatosDelEnvioYEspanaPorDefecto()
        {
            DireccionEntregaEnvioDialogViewModel vm = Abierto();

            Assert.AreEqual("Pedido 929000 (envío 251300)", vm.Cabecera);
            Assert.AreEqual("Cambiar la dirección de entrega del envío 251300", vm.Title);
            Assert.AreEqual("Peluquería Ejemplo", vm.Destinatario);
            Assert.AreEqual("Calle Vieja 1", vm.Direccion);
            Assert.AreEqual("ES", vm.Pais);
            Assert.AreEqual("600111222", vm.Telefono);
            Assert.AreEqual("cliente@ejemplo.es", vm.Correo);
            Assert.AreEqual(string.Empty, vm.Aviso);
            Assert.IsTrue(vm.AceptarCommand.CanExecute(null));
        }

        [TestMethod]
        public void SinTelefono_NoSePuedeAceptarYDiceQueFalta()
        {
            DireccionEntregaEnvioDialogViewModel vm = Abierto();

            vm.Telefono = "  ";

            Assert.IsFalse(vm.AceptarCommand.CanExecute(null));
            StringAssert.Contains(vm.Aviso, "teléfono");
        }

        [TestMethod]
        public void TelefonoCorto_NoSePuedeAceptar()
        {
            DireccionEntregaEnvioDialogViewModel vm = Abierto();

            vm.Telefono = "6001";

            Assert.IsFalse(vm.AceptarCommand.CanExecute(null));
        }

        [TestMethod]
        public void SinCorreoOConElCorreoMalEscrito_NoSePuedeAceptar()
        {
            DireccionEntregaEnvioDialogViewModel vm = Abierto();

            vm.Correo = "";
            Assert.IsFalse(vm.AceptarCommand.CanExecute(null));
            StringAssert.Contains(vm.Aviso, "correo");

            vm.Correo = "cliente@ejemplo";
            Assert.IsFalse(vm.AceptarCommand.CanExecute(null));

            vm.Correo = "cliente@ejemplo.es";
            Assert.IsTrue(vm.AceptarCommand.CanExecute(null));
        }

        [TestMethod]
        public void SinDireccionOSinCodigoPostal_NoSePuedeAceptar()
        {
            DireccionEntregaEnvioDialogViewModel vm = Abierto();

            vm.Direccion = " ";
            Assert.IsFalse(vm.AceptarCommand.CanExecute(null));
            vm.Direccion = "Calle Nueva 7";
            vm.CodPostal = "";
            Assert.IsFalse(vm.AceptarCommand.CanExecute(null));
        }

        [TestMethod]
        public void Aceptar_DevuelveLoCorregidoLimpio()
        {
            DireccionEntregaEnvioDialogViewModel vm = Abierto("pt");
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;
            vm.Direccion = "  Rua Nova 7 ";
            vm.Correo = " otro@ejemplo.pt ";

            vm.AceptarCommand.Execute(null);

            Assert.IsNotNull(resultado);
            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
            Assert.AreEqual("Rua Nova 7", resultado.Parameters.GetValue<string>(DireccionEntregaEnvioDialogViewModel.PARAMETRO_DIRECCION));
            Assert.AreEqual("PT", resultado.Parameters.GetValue<string>(DireccionEntregaEnvioDialogViewModel.PARAMETRO_PAIS));
            Assert.AreEqual("otro@ejemplo.pt", resultado.Parameters.GetValue<string>(DireccionEntregaEnvioDialogViewModel.PARAMETRO_CORREO));
            Assert.AreEqual("600111222", resultado.Parameters.GetValue<string>(DireccionEntregaEnvioDialogViewModel.PARAMETRO_TELEFONO));
        }

        [TestMethod]
        public void Cancelar_DevuelveCancel()
        {
            DireccionEntregaEnvioDialogViewModel vm = Abierto();
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.CancelarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
        }
    }
}
