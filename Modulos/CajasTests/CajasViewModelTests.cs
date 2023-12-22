using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas;
using Nesto.Modulos.Cajas.Models;
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
            var servicio = A.Fake<IContabilidadService>();
            var configuracion = A.Fake<IConfiguracion>();
            var dialogService = A.Fake<IDialogService>();
            var sut = new CajasViewModel(servicio, configuracion, dialogService);
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
            var servicio = A.Fake<IContabilidadService>();
            var configuracion = A.Fake<IConfiguracion>();
            var dialogService = A.Fake<IDialogService>();
            var sut = new CajasViewModel(servicio, configuracion, dialogService);
            CuentaContableDTO cuentaOrigen = new CuentaContableDTO { Cuenta = "570" };
            CuentaContableDTO cuentaDestino = new CuentaContableDTO { Cuenta = "571" };
            sut.CuentaOrigen = cuentaOrigen;
            sut.CuentaDestino = cuentaDestino;
            sut.Concepto = "Traspaso entre cajas";

            // Act
            sut.ContabilizarTraspasoCommand.Execute(null);

            // Assert
            // Assert
            A.CallTo(() => servicio.Contabilizar(A<PreContabilidadDTO>._))
                .MustHaveHappenedOnceExactly();
        }
    }
}