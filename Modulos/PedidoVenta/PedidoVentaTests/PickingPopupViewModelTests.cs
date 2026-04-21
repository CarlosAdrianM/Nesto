using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Informes;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Modulos.PedidoVenta;
using Prism.Events;
using Prism.Services.Dialogs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    [TestClass]
    public class PickingPopupViewModelTests
    {
        private IPedidoVentaService _servicioPedido;
        private IEventAggregator _eventAggregator;
        private IDialogService _dialogService;
        private IConfiguracion _configuracion;

        [TestInitialize]
        public void Initialize()
        {
            _servicioPedido = A.Fake<IPedidoVentaService>();
            _eventAggregator = A.Fake<IEventAggregator>();
            _dialogService = A.Fake<IDialogService>();
            _configuracion = A.Fake<IConfiguracion>();
        }

        private PickingPopupViewModel CrearViewModel(IInformesService servicioInformes)
        {
            return new PickingPopupViewModel(_servicioPedido, _eventAggregator, _dialogService, _configuracion, servicioInformes);
        }

        [TestMethod]
        public async Task ObtenerDatosPicking_SinNumero_PideUltimoPickingYLoUsa()
        {
            var servicioInformes = A.Fake<IInformesService>();
            A.CallTo(() => servicioInformes.LeerUltimoPicking()).Returns(Task.FromResult(98765));
            A.CallTo(() => servicioInformes.LeerPicking(A<int>.Ignored, A<string>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(new List<PickingModel>()));
            var vm = CrearViewModel(servicioInformes);

            await vm.ObtenerDatosPickingAsync();

            A.CallTo(() => servicioInformes.LeerUltimoPicking()).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicioInformes.LeerPicking(98765, "1", 1)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(98765, vm.numeroPicking);
        }

        [TestMethod]
        public async Task ObtenerDatosPicking_ConNumeroYaEstablecido_NoPideUltimoPicking()
        {
            var servicioInformes = A.Fake<IInformesService>();
            A.CallTo(() => servicioInformes.LeerPicking(A<int>.Ignored, A<string>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(new List<PickingModel>()));
            var vm = CrearViewModel(servicioInformes);
            vm.numeroPicking = 12345;

            await vm.ObtenerDatosPickingAsync();

            A.CallTo(() => servicioInformes.LeerUltimoPicking()).MustNotHaveHappened();
            A.CallTo(() => servicioInformes.LeerPicking(12345, "1", 1)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ObtenerDatosPicking_DevuelveLaListaDelServicio()
        {
            var servicioInformes = A.Fake<IInformesService>();
            var datos = new List<PickingModel> { new PickingModel { Producto = "12345", Cantidad = 3 } };
            A.CallTo(() => servicioInformes.LeerUltimoPicking()).Returns(Task.FromResult(1));
            A.CallTo(() => servicioInformes.LeerPicking(A<int>.Ignored, A<string>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(datos));
            var vm = CrearViewModel(servicioInformes);

            var resultado = await vm.ObtenerDatosPickingAsync();

            Assert.AreEqual(1, resultado.Count);
            Assert.AreEqual("12345", resultado[0].Producto);
        }

        [TestMethod]
        public async Task ObtenerDatosPacking_SinNumero_PideUltimoPickingYLoUsa()
        {
            var servicioInformes = A.Fake<IInformesService>();
            A.CallTo(() => servicioInformes.LeerUltimoPicking()).Returns(Task.FromResult(22222));
            A.CallTo(() => servicioInformes.LeerPacking(A<int>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(new List<PackingModel>()));
            var vm = CrearViewModel(servicioInformes);

            await vm.ObtenerDatosPackingAsync();

            A.CallTo(() => servicioInformes.LeerUltimoPicking()).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicioInformes.LeerPacking(22222, 1)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(22222, vm.numeroPicking);
        }

        [TestMethod]
        public async Task ObtenerDatosPacking_ConNumeroYaEstablecido_NoPideUltimoPicking()
        {
            var servicioInformes = A.Fake<IInformesService>();
            A.CallTo(() => servicioInformes.LeerPacking(A<int>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(new List<PackingModel>()));
            var vm = CrearViewModel(servicioInformes);
            vm.numeroPicking = 33333;

            await vm.ObtenerDatosPackingAsync();

            A.CallTo(() => servicioInformes.LeerUltimoPicking()).MustNotHaveHappened();
            A.CallTo(() => servicioInformes.LeerPacking(33333, 1)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ObtenerDatosPacking_DevuelveLaListaDelServicio()
        {
            var servicioInformes = A.Fake<IInformesService>();
            var datos = new List<PackingModel> { new PackingModel { Número = 555555 } };
            A.CallTo(() => servicioInformes.LeerUltimoPicking()).Returns(Task.FromResult(1));
            A.CallTo(() => servicioInformes.LeerPacking(A<int>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(datos));
            var vm = CrearViewModel(servicioInformes);

            var resultado = await vm.ObtenerDatosPackingAsync();

            Assert.AreEqual(1, resultado.Count);
            Assert.AreEqual(555555, resultado[0].Número);
        }
    }
}
