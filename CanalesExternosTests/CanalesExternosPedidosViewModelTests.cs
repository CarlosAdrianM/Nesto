using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos;
using Nesto.Modulos.CanalesExternos.ViewModels;
using Nesto.Modulos.PedidoVenta;
using Prism.Regions;
using Prism.Services.Dialogs;
using Unity;

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
        private static CanalesExternosPedidosViewModel CrearViewModel()
        {
            return new CanalesExternosPedidosViewModel(
                A.Fake<IRegionManager>(),
                A.Fake<IConfiguracion>(),
                A.Fake<IDialogService>(),
                A.Fake<IPedidoVentaService>(),
                A.Fake<IUnityContainer>());
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
    }
}
