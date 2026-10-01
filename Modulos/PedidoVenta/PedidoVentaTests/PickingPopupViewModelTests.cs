using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Modulos.PedidoVenta;
using CommunityToolkit.Mvvm.Messaging;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    // Nesto#340 (RDLC -> QuestPDF): los informes de picking/packing se descargan ya en PDF
    // del backend; el VM solo resuelve el número de picking y pide el PDF.
    [TestClass]
    public class PickingPopupViewModelTests
    {
        private IPedidoVentaService _servicioPedido;
        private IMessenger _messenger;
        private IServicioDialogos _dialogService;
        private IConfiguracion _configuracion;

        [TestInitialize]
        public void Initialize()
        {
            _servicioPedido = A.Fake<IPedidoVentaService>();
            _messenger = new WeakReferenceMessenger();
            _dialogService = A.Fake<IServicioDialogos>();
            _configuracion = A.Fake<IConfiguracion>();
        }

        private PickingPopupViewModel CrearViewModel(IInformesService servicioInformes)
        {
            return new PickingPopupViewModel(_servicioPedido, _messenger, _dialogService, _configuracion, servicioInformes);
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

        // ===== NestoAPI#405: no se puede pedir un picking mientras hay otro en marcha =====

        [TestMethod]
        public void SacarPicking_MientrasSeEstaSacandoOtro_ElBotonSeDeshabilita()
        {
            // El 25/08/2026 el picking tardo 2 min 47 s (lo normal son 5-7 s), el usuario penso
            // que no habia pasado nada y pulso otra vez 3 segundos despues. Las dos ejecuciones se
            // solaparon, cada una reservo sus ubicaciones y el packing salio con el DOBLE.
            // El BusyIndicator solo TAPABA la ventana: el comando seguia habilitado.
            A.CallTo(() => _configuracion.UsuarioEnGrupo(A<string>.Ignored)).Returns(true);
            var vm = CrearViewModel(A.Fake<IInformesService>());

            Assert.IsTrue(vm.cmdSacarPicking.CanExecute(null), "En reposo el boton tiene que estar activo");

            vm.estaSacandoPicking = true;

            Assert.IsFalse(vm.cmdSacarPicking.CanExecute(null), "Con un picking en marcha NO se puede pedir otro");
        }

        [TestMethod]
        public void SacarPicking_AlTerminar_ElBotonVuelveAHabilitarse()
        {
            A.CallTo(() => _configuracion.UsuarioEnGrupo(A<string>.Ignored)).Returns(true);
            var vm = CrearViewModel(A.Fake<IInformesService>());
            vm.estaSacandoPicking = true;

            vm.estaSacandoPicking = false;

            Assert.IsTrue(vm.cmdSacarPicking.CanExecute(null), "Terminado el picking se puede volver a sacar");
        }

        [TestMethod]
        public void SacarPicking_UsuarioSinPermiso_SigueDeshabilitadoAunqueNoHayaPickingEnMarcha()
        {
            // La guarda nueva se suma a la de siempre, no la sustituye.
            A.CallTo(() => _configuracion.UsuarioEnGrupo(A<string>.Ignored)).Returns(false);
            var vm = CrearViewModel(A.Fake<IInformesService>());

            Assert.IsFalse(vm.cmdSacarPicking.CanExecute(null));
        }

        // 01/10/26 (Alfredo, cliente 5057 «LOS LUNES CIERRA»): el picking de un pedido no sale porque el cliente
        // cierra el día de la entrega. Antes decía «No hay stock suficiente…»; ahora lo dice y, en el picking de UN
        // pedido, pregunta si se le asigna igualmente.

        private PickingPopupViewModel PickingDeUnPedido(int pedido)
        {
            A.CallTo(() => _configuracion.UsuarioEnGrupo(A<string>.Ignored)).Returns(true);
            var vm = CrearViewModel(A.Fake<IInformesService>());
            vm.esPickingPedido = true;
            vm.numeroPedidoPicking = pedido;
            return vm;
        }

        [TestMethod]
        public async Task SacarPicking_PedidoConElClienteCerrado_PreguntaYSiConfirmaLoSacaIgualmente()
        {
            var vm = PickingDeUnPedido(927586);
            A.CallTo(() => _servicioPedido.sacarPickingPedido("1", 927586, false))
                .Throws(new PickingClienteCerradoException("El pedido 927586 no sale: el cliente 5057 cierra el lunes 05/10/2026."));
            A.CallTo(() => _dialogService.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).Returns(true);

            await vm.SacarPickingAsync(null);

            A.CallTo(() => _dialogService.ShowConfirmationAnswer(A<string>.Ignored,
                A<string>.That.Contains("¿Aún así quieres asignarle picking?"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicioPedido.sacarPickingPedido("1", 927586, true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _dialogService.ShowNotification("Picking", A<string>.That.Contains("927586"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SacarPicking_PedidoConElClienteCerrado_SiNoConfirma_NoLoSaca()
        {
            var vm = PickingDeUnPedido(927586);
            A.CallTo(() => _servicioPedido.sacarPickingPedido("1", 927586, false))
                .Throws(new PickingClienteCerradoException("El cliente cierra ese día."));
            A.CallTo(() => _dialogService.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).Returns(false);

            await vm.SacarPickingAsync(null);

            A.CallTo(() => _servicioPedido.sacarPickingPedido("1", 927586, true)).MustNotHaveHappened();
            A.CallTo(() => _dialogService.ShowNotification("Picking", A<string>.Ignored)).MustNotHaveHappened();
            Assert.IsFalse(vm.estaSacandoPicking);
        }

        [TestMethod]
        public async Task SacarPicking_DeUnCliente_ConElClienteCerrado_SoloAvisaSinPreguntar()
        {
            // La pregunta es solo para el picking de UN pedido.
            A.CallTo(() => _configuracion.UsuarioEnGrupo(A<string>.Ignored)).Returns(true);
            var vm = CrearViewModel(A.Fake<IInformesService>());
            vm.esPickingPedido = false;
            vm.esPickingCliente = true;
            vm.numeroClientePicking = "5057";
            A.CallTo(() => _servicioPedido.sacarPickingPedido("5057"))
                .Throws(new PickingClienteCerradoException("El cliente 5057 cierra el lunes."));

            await vm.SacarPickingAsync(null);

            A.CallTo(() => _dialogService.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => _dialogService.ShowNotification(A<string>.Ignored, A<string>.That.Contains("cierra el lunes"))).MustHaveHappenedOnceExactly();
        }
    }
}
