using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.ViewModels;

namespace CajasTests
{
    /// <summary>
    /// Carlos (09/11/26): Administración anula ella misma un enlace de pago pendiente desde «Enlaces de pago»
    /// (POST api/Pagos/{id}/Anular). El 867 del 928286, ya cobrado por reembolso, hubo que anularlo con un script.
    /// </summary>
    [TestClass]
    public class AnularEnlacePagoViewModelTests
    {
        private const string MOTIVO = "Pedido ya cobrado por reembolso";

        private IAuditoriaEnlacesPagoService servicio = null!;
        private IServicioDialogos dialogos = null!;
        private AuditoriaEnlacesPagoViewModel vm = null!;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<IAuditoriaEnlacesPagoService>();
            dialogos = A.Fake<IServicioDialogos>();
            vm = new AuditoriaEnlacesPagoViewModel(servicio, dialogos);
        }

        private static EnlacePagoAuditoriaModel Enlace(int id = 867, string estado = "Pendiente", string tipo = "TPVVirtual") => new EnlacePagoAuditoriaModel
        {
            Id = id,
            NumeroOrden = "A867",
            Tipo = tipo,
            Estado = estado,
            Cliente = "38515",
            NombreCliente = "CLIENTA",
            Importe = 428.92m,
            Descripcion = "Pago pedido 928286 de Nueva Visión",
            FechaCreacion = new DateTime(2026, 10, 13, 10, 2, 0)
        };

        private async Task Cargar(params EnlacePagoAuditoriaModel[] enlaces)
        {
            A.CallTo(() => servicio.Buscar(A<FiltroAuditoriaEnlacesPago>._)).Returns(enlaces.ToList());
            await vm.BuscarAsync();
        }

        private void ContestarMotivoYConfirmar(string? motivo = MOTIVO, bool confirmar = true)
        {
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns(motivo!);
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._, A<string>._)).Returns(motivo!);
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).Returns(confirmar);
        }

        [TestMethod]
        public async Task Anular_SoloConUnEnlacePendienteDeTpvVirtualSeleccionado()
        {
            await Cargar(Enlace(1), Enlace(2, "Autorizado"), Enlace(3, "Enviado", "P2F"), Enlace(4, "Cancelado"), Enlace(5, "Pendiente", "CarritoAplazame"));

            Assert.IsFalse(vm.AnularEnlaceCommand.CanExecute(null), "sin seleccionar");
            vm.EnlaceSeleccionado = vm.Enlaces[0];
            Assert.IsTrue(vm.AnularEnlaceCommand.CanExecute(null));
            foreach (int i in new[] { 1, 2, 3, 4 })
            {
                vm.EnlaceSeleccionado = vm.Enlaces[i];
                Assert.IsFalse(vm.AnularEnlaceCommand.CanExecute(null), vm.Enlaces[i].Estado + " " + vm.Enlaces[i].Tipo);
            }
        }

        [TestMethod]
        public async Task Anular_PideMotivoYConfirmacion_LlamaALaApiYRefrescaLaFila()
        {
            await Cargar(Enlace());
            vm.EnlaceSeleccionado = vm.Enlaces[0];
            ContestarMotivoYConfirmar();
            var fecha = new DateTime(2026, 11, 9, 13, 0, 0);
            A.CallTo(() => servicio.Anular(867, MOTIVO)).Returns(new RespuestaAnularEnlacePagoModel
            {
                Id = 867, Estado = "Cancelado", FechaActualizacion = fecha, Mensaje = "Enlace 867 de 428,92 € anulado: el cliente ya no puede pagarlo."
            });

            await vm.AnularEnlaceAsync();

            A.CallTo(() => servicio.Anular(867, MOTIVO)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Cancelado", vm.Enlaces[0].Estado);
            Assert.AreEqual(fecha, vm.Enlaces[0].FechaActualizacion);
            Assert.AreSame(vm.Enlaces[0], vm.EnlaceSeleccionado, "sigue seleccionada");
            Assert.IsFalse(vm.AnularEnlaceCommand.CanExecute(null), "ya no se puede volver a anular");
            StringAssert.Contains(vm.DetalleSeleccionado, "Cancelado");
            A.CallTo(() => dialogos.ShowNotification(A<string>._, "Enlace 867 de 428,92 € anulado: el cliente ya no puede pagarlo.")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Anular_SiCancelaElMotivo_NoHaceNada()
        {
            await Cargar(Enlace());
            vm.EnlaceSeleccionado = vm.Enlaces[0];
            ContestarMotivoYConfirmar(motivo: null);

            await vm.AnularEnlaceAsync();

            A.CallTo(() => servicio.Anular(A<int>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Anular_MotivoEnBlanco_AvisaYNoLlama()
        {
            await Cargar(Enlace());
            vm.EnlaceSeleccionado = vm.Enlaces[0];
            ContestarMotivoYConfirmar(motivo: "   ");

            await vm.AnularEnlaceAsync();

            A.CallTo(() => servicio.Anular(A<int>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("motivo"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Anular_SiNoConfirma_NoLlama()
        {
            await Cargar(Enlace());
            vm.EnlaceSeleccionado = vm.Enlaces[0];
            ContestarMotivoYConfirmar(confirmar: false);

            await vm.AnularEnlaceAsync();

            A.CallTo(() => servicio.Anular(A<int>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>.That.Contains($"{428.92m:N2}"), true)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Anular_SiLaApiDiceQueNo_EnsenaSuMensajeYNoTocaLaFila()
        {
            await Cargar(Enlace());
            vm.EnlaceSeleccionado = vm.Enlaces[0];
            ContestarMotivoYConfirmar();
            A.CallTo(() => servicio.Anular(867, MOTIVO)).Throws(new Exception("El enlace 867 ya está pagado: no se puede anular."));

            await vm.AnularEnlaceAsync();

            A.CallTo(() => dialogos.ShowError("El enlace 867 ya está pagado: no se puede anular.")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Pendiente", vm.Enlaces[0].Estado);
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public void Estados_IncluyeCancelado()
        {
            CollectionAssert.Contains(vm.Estados, "Cancelado");
        }
    }
}
