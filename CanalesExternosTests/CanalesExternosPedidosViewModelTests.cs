using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Nesto.Modulos.CanalesExternos.ViewModels;
using Nesto.Modulos.PedidoVenta;

namespace CanalesExternosTests
{
    /// <summary>
    /// Nesto#374: al volver de un pedido abierto con doble clic, la vista se re-engancha a la
    /// región y el Loaded reasignaba el canal, lo que reseteaba a Miravia y relanzaba la descarga
    /// completa (lentísima en Amazon). El setter de CanalSeleccionado solo debe avisar cuando el
    /// canal cambia de verdad.
    /// </summary>
    [TestClass]
    public class CanalesExternosPedidosViewModelTests
    {
        private static CanalesExternosPedidosViewModel CrearViewModel(IAbridorPedidos abridorPedidos = null)
        {
            return new CanalesExternosPedidosViewModel(
                A.Fake<IConfiguracion>(),
                A.Fake<IServicioDialogos>(),
                A.Fake<IPedidoVentaService>(),
                abridorPedidos ?? A.Fake<IAbridorPedidos>(),
                A.Fake<IFacturasAmazonService>(),
                A.Fake<IClientesPorTelefonoService>());
        }

        [TestMethod]
        public void CanalSeleccionado_CambiaDeCanal_DisparaLaRecarga()
        {
            var vm = CrearViewModel();
            int avisos = 0;
            vm.CanalSeleccionadoHaCambiado += (s, e) => avisos++;
            var canal = A.Fake<ICanalExternoPedidos>();

            vm.CanalSeleccionado = canal;

            Assert.AreEqual(1, avisos);
            A.CallTo(() => canal.GetAllPedidosAsync(A<System.DateTime>._, A<int>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void CanalSeleccionado_MismoCanal_NoRelanzaLaDescarga()
        {
            // Reasignar el mismo canal (Loaded al volver de un pedido, re-binding del combo...)
            // no debe volver a descargar los pedidos del canal.
            var vm = CrearViewModel();
            var canal = A.Fake<ICanalExternoPedidos>();
            vm.CanalSeleccionado = canal;
            int avisos = 0;
            vm.CanalSeleccionadoHaCambiado += (s, e) => avisos++;

            vm.CanalSeleccionado = canal;

            Assert.AreEqual(0, avisos, "Mismo canal: no debe avisar");
            A.CallTo(() => canal.GetAllPedidosAsync(A<System.DateTime>._, A<int>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Factory_ElPrimerCanalEsMiravia_QueEsElQueSeCargaAlAbrir()
        {
            // El Loaded de la vista inicializa con Factory.First(): documentamos que es Miravia
            // (canal rápido); si algún día se reordena, que sea a conciencia.
            var vm = CrearViewModel();

            Assert.AreEqual("Miravia", System.Linq.Enumerable.First(vm.Factory).Key);
        }

        [TestMethod]
        public void AbrirPedidoNesto_AbreElPedidoAsignadoConElAbridorDePedidos()
        {
            // Nesto#490 (4C.5, tramo 3a): antes, PedidoVentaViewModel.CargarPedido con el IUnityContainer
            var abridor = A.Fake<IAbridorPedidos>();
            var vm = CrearViewModel(abridor);
            var pedidoExterno = new PedidoCanalExterno
            {
                PedidoNestoId = 987654,
                Pedido = new Nesto.Models.PedidoVentaDTO { empresa = "3" }
            };

            Assert.IsTrue(vm.AbrirPedidoNestoCommand.CanExecute(pedidoExterno));
            vm.AbrirPedidoNestoCommand.Execute(pedidoExterno);

            A.CallTo(() => abridor.Abrir("3", 987654)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Factory_TieneLaTiendaDeEvaVisnu()
        {
            // Nesto#520: el usuario la elige en el mismo desplegable que la de Nueva Visión
            var vm = CrearViewModel();

            Assert.IsInstanceOfType(vm.Factory["PrestashopEV"], typeof(CanalExternoPedidosPrestashopEvaVisnu));
            Assert.IsInstanceOfType(vm.Factory["PrestashopNV"], typeof(CanalExternoPedidosPrestashopNuevaVision));
        }
    }
}
