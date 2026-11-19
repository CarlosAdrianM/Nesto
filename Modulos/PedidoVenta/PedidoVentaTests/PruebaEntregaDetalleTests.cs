using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Prueba de entrega por agencia (10/11/26): en el detalle del pedido, cada envío entregado de la lista de envíos tiene su «Ver
    /// prueba», con el mismo paso que Agencias › Tramitados (<see cref="IMostradorPruebaEntrega"/>).
    /// </summary>
    [TestClass]
    public class PruebaEntregaDetalleTests
    {
        private static (DetallePedidoViewModel vm, IMostradorPruebaEntrega mostrador) Vm()
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(), new WeakReferenceMessenger(),
                A.Fake<IServicioDialogos>(), A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            var mostrador = A.Fake<IMostradorPruebaEntrega>();
            vm.MostradorPruebaEntrega = mostrador;
            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", numero = 930001, cliente = "15191", contacto = "0" });
            return (vm, mostrador);
        }

        [TestMethod]
        public void NestoAPI880_EntregaIncompleta_SeVeEnLaPestanaEntrega()
        {
            var envios = new System.Collections.Generic.List<PedidoVentaModel.EnvioAgenciaDTO>
            {
                new PedidoVentaModel.EnvioAgenciaDTO { Numero = 251000, Estado = 2 },
                new PedidoVentaModel.EnvioAgenciaDTO { Numero = 251099, Estado = 2, EntregaIncompleta = true,
                    DetalleEntregaIncompleta = "Entregado (reparto propio): 4 de 5 bultos. Falta RP251099-5" }
            };

            Assert.AreEqual("Envío 251099: entregado con 4 de 5 bultos. Falta RP251099-5", DetallePedidoViewModel.TextoEntregaIncompleta(envios));
            Assert.IsNull(DetallePedidoViewModel.TextoEntregaIncompleta(envios.GetRange(0, 1)));
            Assert.IsNull(DetallePedidoViewModel.TextoEntregaIncompleta(null));
        }

        [TestMethod]
        public async Task EnvioEntregado_EnsenaSuPruebaConElPedido()
        {
            var (vm, mostrador) = Vm();

            await vm.VerPruebaEntregaAsync(new PedidoVentaModel.EnvioAgenciaDTO { Numero = 251500, AgenciaId = 13, Estado = 2 });

            A.CallTo(() => mostrador.Mostrar(251500, "930001")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task EnvioSinEntregar_NoPreguntaNada()
        {
            var (vm, mostrador) = Vm();

            await vm.VerPruebaEntregaAsync(new PedidoVentaModel.EnvioAgenciaDTO { Numero = 251500, AgenciaId = 1, Estado = 1 });
            await vm.VerPruebaEntregaAsync(null);

            A.CallTo(() => mostrador.Mostrar(A<int>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void ElComandoExisteParaLaLista()
        {
            var (vm, _) = Vm();

            Assert.IsNotNull(vm.VerPruebaEntregaCommand);
        }
    }
}
