using ControlesUsuario.Models;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.ViewModels;
using Prism.Services.Dialogs;

namespace CajasTests
{
    [TestClass]
    public class CajasViewModelTests
    {
        [TestMethod]
        public void CajasViewModel_ContabilizarTraspaso_NoSePuedeEjecutarSiLaCuentaOrigenYLaCuentaDestinoSonIguales()
        {
            // Arrange
            var servicioContabilidad = A.Fake<IContabilidadService>();
            var configuracion = A.Fake<IConfiguracion>();
            var dialogService = A.Fake<IDialogService>();
            var servicioClientes = A.Fake<IClientesService>();
            var sut = new CajasViewModel(servicioContabilidad, configuracion, dialogService, servicioClientes);
            CuentaContableDTO cuenta = new CuentaContableDTO
            {
                Cuenta = "570"
            };
            sut.CuentaDestino = cuenta;
            sut.CuentaOrigen = cuenta;

            // Act
            sut.ContabilizarTraspasoCommand.Execute(null);

            // Assert
            Assert.IsFalse(sut.ContabilizarTraspasoCommand.CanExecute(null));
        }

        [TestMethod]
        public void CajasViewModel_ContabilizarTraspaso_LlamaAContabilizarEnIContabilidadService()
        {
            // Arrange
            var servicioContabilidad = A.Fake<IContabilidadService>();
            var configuracion = A.Fake<IConfiguracion>();
            var dialogService = A.Fake<IDialogService>();
            var servicioClientes = A.Fake<IClientesService>();
            var sut = new CajasViewModel(servicioContabilidad, configuracion, dialogService, servicioClientes);
            CuentaContableDTO cuentaOrigen = new CuentaContableDTO { Cuenta = "570" };
            CuentaContableDTO cuentaDestino = new CuentaContableDTO { Cuenta = "571" };
            sut.CuentaOrigen = cuentaOrigen;
            sut.CuentaDestino = cuentaDestino;
            sut.Importe = 90M;
            sut.Concepto = "Traspaso entre cajas";

            // Act
            sut.ContabilizarTraspasoCommand.Execute(null);

            // Assert
            // Assert
            A.CallTo(() => servicioContabilidad.Contabilizar(A<PreContabilidadDTO>._))
                .MustHaveHappenedOnceExactly();
        }
    }
}