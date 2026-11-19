using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Sugerencia 1356: en la pestaña «Entrega» del detalle del pedido, los envíos devueltos o incidentados que esperan decisión, con
    /// las mismas acciones que Agencias › Devueltos.
    /// </summary>
    [TestClass]
    public class DevueltosDetalleTests
    {
        private static (DetallePedidoViewModel vm, IServicioEnviosDevueltos devueltos, IServicioDialogos dialogos, IPedidoVentaService servicio) Vm()
        {
            var servicio = A.Fake<IPedidoVentaService>();
            var dialogos = A.Fake<IServicioDialogos>();
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogos, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            var devueltos = A.Fake<IServicioEnviosDevueltos>();
            vm.ServicioDeDevueltos = devueltos;
            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", numero = 925137, cliente = "15191", contacto = "0" });
            return (vm, devueltos, dialogos, servicio);
        }

        private static List<PedidoVentaModel.EnvioAgenciaDTO> Envios(params short[] estados)
            => estados.Select((e, i) => new PedidoVentaModel.EnvioAgenciaDTO { Numero = 251300 + i, Estado = e }).ToList();

        private static EnvioDevueltoModel Devuelto(int numero, int? pedido, string decision = EnvioDevueltoModel.SIN_DECIDIR)
            => new EnvioDevueltoModel { Numero = numero, Pedido = pedido, Empresa = "1", Decision = decision, Estado = EnvioDevueltoModel.ESTADO_DEVUELTO };

        [TestMethod]
        public async Task SinEnviosDevueltos_NiPreguntaNiSeVe()
        {
            var (vm, devueltos, _, _) = Vm();
            vm.ListaEnlacesSeguimiento = Envios(1, 2);

            await vm.CargarDevueltosDelPedidoAsync();

            A.CallTo(() => devueltos.Cargar(A<string>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.HayDevueltosPendientes);
        }

        [TestMethod]
        public async Task ConUnDevuelto_EnsenaSoloLosDeEstePedidoSinDecidir()
        {
            var (vm, devueltos, _, _) = Vm();
            A.CallTo(() => devueltos.Cargar("1")).Returns(Task.FromResult(new List<EnvioDevueltoModel>
            {
                Devuelto(251300, 925137),
                Devuelto(251301, 925137, EnvioDevueltoModel.ABONAR),
                Devuelto(251400, 999999)
            }));

            vm.ListaEnlacesSeguimiento = Envios(2, 4);
            await vm.CargarDevueltosDelPedidoAsync();

            Assert.IsTrue(vm.HayDevueltosPendientes);
            CollectionAssert.AreEqual(new[] { 251300 }, vm.DevueltosPendientes.Select(d => d.Numero).ToArray());
        }

        [TestMethod]
        public async Task SiLaApiFalla_NoSeVeNadaYNoLanza()
        {
            var (vm, devueltos, dialogos, _) = Vm();
            A.CallTo(() => devueltos.Cargar(A<string>._)).ThrowsAsync(new System.UnauthorizedAccessException(ServicioEnviosDevueltos.MENSAJE_SIN_PERMISO));

            vm.ListaEnlacesSeguimiento = Envios(3);
            await vm.CargarDevueltosDelPedidoAsync();

            Assert.IsFalse(vm.HayDevueltosPendientes);
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Abonar_Confirmado_LoApuntaYRecargaLosEnvios()
        {
            var (vm, devueltos, dialogos, servicio) = Vm();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
            A.CallTo(() => servicio.CargarEnlacesSeguimiento("1", 925137)).Returns(Task.FromResult(Envios(4)));

            await vm.AbonarDevueltoAsync(Devuelto(251300, 925137));

            A.CallTo(() => devueltos.Abonar(251300)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.CargarEnlacesSeguimiento("1", 925137)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.estaBloqueado);
        }

        [TestMethod]
        public async Task Reenviar_AbreElDialogoDeDevueltos()
        {
            var (vm, devueltos, dialogos, _) = Vm();
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._))
                .Returns(Task.FromResult(new ResultadoDialogo(ResultadoBoton.Cancel)));

            await vm.ReenviarDevueltoAsync(Devuelto(251300, 925137));

            A.CallTo(() => dialogos.ShowDialogAsync(ReenviarEnvioDialogViewModel.NOMBRE, A<ParametrosDialogo>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => devueltos.Reenviar(A<int>._, A<ReenvioEnvioPeticion>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void LosComandosExistenParaLaVista()
        {
            var (vm, _, _, _) = Vm();

            Assert.IsNotNull(vm.ReenviarDevueltoCommand);
            Assert.IsNotNull(vm.AbonarDevueltoCommand);
        }
    }
}
