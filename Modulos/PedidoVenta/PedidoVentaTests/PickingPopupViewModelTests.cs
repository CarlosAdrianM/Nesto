using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Modulos.PedidoVenta;
using Prism.Events;
using Prism.Services.Dialogs;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    // Nesto#340 (RDLC -> QuestPDF): los informes de picking/packing se descargan ya en PDF
    // del backend; el VM solo resuelve el número de picking y pide el PDF.
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
        public async Task ObtenerPdfPicking_SinNumero_PideUltimoPickingYLoUsa()
        {
            var servicioInformes = A.Fake<IInformesService>();
            A.CallTo(() => servicioInformes.LeerUltimoPicking()).Returns(Task.FromResult(98765));
            A.CallTo(() => servicioInformes.DescargarPickingPdf(A<int>.Ignored, A<string>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(new byte[] { 1, 2, 3 }));
            var vm = CrearViewModel(servicioInformes);

            await vm.ObtenerPdfPickingAsync();

            A.CallTo(() => servicioInformes.LeerUltimoPicking()).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicioInformes.DescargarPickingPdf(98765, "1", 1)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(98765, vm.numeroPicking);
        }

        [TestMethod]
        public async Task ObtenerPdfPicking_ConNumeroYaEstablecido_NoPideUltimoPicking()
        {
            var servicioInformes = A.Fake<IInformesService>();
            A.CallTo(() => servicioInformes.DescargarPickingPdf(A<int>.Ignored, A<string>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(new byte[] { 1 }));
            var vm = CrearViewModel(servicioInformes);
            vm.numeroPicking = 12345;

            await vm.ObtenerPdfPickingAsync();

            A.CallTo(() => servicioInformes.LeerUltimoPicking()).MustNotHaveHappened();
            A.CallTo(() => servicioInformes.DescargarPickingPdf(12345, "1", 1)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ObtenerPdfPicking_DevuelveElPdfDelServicio()
        {
            var servicioInformes = A.Fake<IInformesService>();
            A.CallTo(() => servicioInformes.LeerUltimoPicking()).Returns(Task.FromResult(1));
            A.CallTo(() => servicioInformes.DescargarPickingPdf(A<int>.Ignored, A<string>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(new byte[] { 9, 8, 7 }));
            var vm = CrearViewModel(servicioInformes);

            var resultado = await vm.ObtenerPdfPickingAsync();

            Assert.AreEqual(3, resultado.Length);
            Assert.AreEqual(9, resultado[0]);
        }

        [TestMethod]
        public async Task ObtenerPdfPacking_SinNumero_PideUltimoPickingYLoUsa()
        {
            var servicioInformes = A.Fake<IInformesService>();
            A.CallTo(() => servicioInformes.LeerUltimoPicking()).Returns(Task.FromResult(22222));
            A.CallTo(() => servicioInformes.DescargarPackingPdf(A<int>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(new byte[] { 1, 2 }));
            var vm = CrearViewModel(servicioInformes);

            await vm.ObtenerPdfPackingAsync();

            A.CallTo(() => servicioInformes.LeerUltimoPicking()).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicioInformes.DescargarPackingPdf(22222, 1)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(22222, vm.numeroPicking);
        }

        [TestMethod]
        public async Task ObtenerPdfPacking_ConNumeroYaEstablecido_NoPideUltimoPicking()
        {
            var servicioInformes = A.Fake<IInformesService>();
            A.CallTo(() => servicioInformes.DescargarPackingPdf(A<int>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(new byte[] { 1 }));
            var vm = CrearViewModel(servicioInformes);
            vm.numeroPicking = 33333;

            await vm.ObtenerPdfPackingAsync();

            A.CallTo(() => servicioInformes.LeerUltimoPicking()).MustNotHaveHappened();
            A.CallTo(() => servicioInformes.DescargarPackingPdf(33333, 1)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ObtenerPdfPacking_DevuelveElPdfDelServicio()
        {
            var servicioInformes = A.Fake<IInformesService>();
            A.CallTo(() => servicioInformes.LeerUltimoPicking()).Returns(Task.FromResult(1));
            A.CallTo(() => servicioInformes.DescargarPackingPdf(A<int>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(new byte[] { 5, 5, 5, 5 }));
            var vm = CrearViewModel(servicioInformes);

            var resultado = await vm.ObtenerPdfPackingAsync();

            Assert.AreEqual(4, resultado.Length);
            Assert.AreEqual(5, resultado[0]);
        }
    }
}
