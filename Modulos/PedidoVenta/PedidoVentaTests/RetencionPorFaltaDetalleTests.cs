using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// NestoAPI#858 (punto 4): el pedido «todo junto» retenido en el almacén por una falta de pago. En el detalle sale una banda
    /// con «Servir lo que hay» y «Esperar a tenerlo completo» (con confirmación); decide la API y luego se recarga el pedido.
    /// </summary>
    [TestClass]
    public class RetencionPorFaltaDetalleTests
    {
        private const int NUMERO = 930500;
        private static readonly DateTime PLAZO = new DateTime(2026, 11, 12, 12, 30, 0);

        private static PedidoVentaDTO Pedido(int picking = 55001, short estado = 1) => new PedidoVentaDTO
        {
            empresa = "1",
            numero = NUMERO,
            cliente = "10000",
            contacto = "0",
            Lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { id = 1, Producto = "38093", Cantidad = 2, almacen = "ALG", tipoLinea = 1, estado = estado, picking = picking },
                new LineaPedidoVentaDTO { id = 2, Producto = "17404", Cantidad = 1, almacen = "ALG", tipoLinea = 1, estado = -1 }
            }
        };

        private static RetencionPorFaltaModel Retencion(int pedido = NUMERO, bool puedeDecidir = true, string estado = "Pendiente") => new RetencionPorFaltaModel
        {
            Id = 3,
            Empresa = "1",
            Pedido = pedido,
            Picking = 55001,
            Cliente = "10000",
            Contacto = "0",
            NombreCliente = "PELUQUERÍA PRUEBA",
            Productos = "17404",
            Fecha = PLAZO.AddHours(-2),
            Plazo = PLAZO,
            Estado = estado,
            PuedeDecidir = puedeDecidir
        };

        private static IPedidoVentaService Servicio(params RetencionPorFaltaModel[] retenciones)
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.LeerRetenidosPorFalta()).Returns(Task.FromResult(new List<RetencionPorFaltaModel>(retenciones)));
            A.CallTo(() => servicio.cargarPedido(A<string>._, A<int>._)).ReturnsLazily(() => Task.FromResult(Pedido()));
            return servicio;
        }

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IServicioDialogos dialogos, PedidoVentaDTO pedido = null)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogos, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.pedido = new PedidoVentaWrapper(pedido ?? Pedido());
            return vm;
        }

        private static IServicioDialogos DialogosQueConfirman(bool respuesta)
        {
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(respuesta));
            return dialogos;
        }

        #region Carga

        [TestMethod]
        public async Task Cargar_PedidoRetenido_SaleLaBandaConElTexto()
        {
            var vm = Vm(Servicio(Retencion(pedido: 111), Retencion()), A.Fake<IServicioDialogos>());

            await vm.CargarRetencionPorFaltaAsync();

            Assert.IsTrue(vm.EstaRetenidoPorFalta);
            Assert.AreEqual("Pedido retenido en el almacén: falta 17404. Decida antes de las 12:30 del 12/11 o se esperará a tenerlo completo.",
                vm.TextoRetenidoPorFalta);
            Assert.IsTrue(vm.PuedeDecidirRetencionPorFalta);
            Assert.IsTrue(vm.ServirLoQueHayCommand.CanExecute(null));
            Assert.IsTrue(vm.EsperarCompletoCommand.CanExecute(null));
            Assert.AreEqual(string.Empty, vm.TextoNoPuedeDecidirRetencionPorFalta);
        }

        [TestMethod]
        public async Task Cargar_PedidoNoEstaEnLaLista_SinBanda()
        {
            var vm = Vm(Servicio(Retencion(pedido: 111)), A.Fake<IServicioDialogos>());

            await vm.CargarRetencionPorFaltaAsync();

            Assert.IsFalse(vm.EstaRetenidoPorFalta);
            Assert.AreEqual(string.Empty, vm.TextoRetenidoPorFalta);
            Assert.IsFalse(vm.ServirLoQueHayCommand.CanExecute(null));
            Assert.IsFalse(vm.EsperarCompletoCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Cargar_RetencionYaDecidida_SinBanda()
        {
            var vm = Vm(Servicio(Retencion(estado: "Esperar")), A.Fake<IServicioDialogos>());

            await vm.CargarRetencionPorFaltaAsync();

            Assert.IsFalse(vm.EstaRetenidoPorFalta);
        }

        [TestMethod]
        public async Task Cargar_503OError_SinBandaYSinMolestar()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.LeerRetenidosPorFalta()).Throws(new Exception("Falta el script de NestoAPI#858"));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            var vm = Vm(servicio, dialogos);

            await vm.CargarRetencionPorFaltaAsync();

            Assert.IsFalse(vm.EstaRetenidoPorFalta);
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowNotification(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cargar_PedidoSinPicking_NoPreguntaALaApi()
        {
            IPedidoVentaService servicio = Servicio(Retencion());
            var vm = Vm(servicio, A.Fake<IServicioDialogos>(), Pedido(picking: 0));

            await vm.CargarRetencionPorFaltaAsync();

            Assert.IsFalse(vm.EstaRetenidoPorFalta);
            A.CallTo(() => servicio.LeerRetenidosPorFalta()).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cargar_PedidoYaAlbaraneado_NoPreguntaALaApi()
        {
            IPedidoVentaService servicio = Servicio(Retencion());
            var vm = Vm(servicio, A.Fake<IServicioDialogos>(), Pedido(estado: 2));

            await vm.CargarRetencionPorFaltaAsync();

            A.CallTo(() => servicio.LeerRetenidosPorFalta()).MustNotHaveHappened();
        }

        [TestMethod]
        public void Cargar_AlPonerElPedido_SePreguntaSola()
        {
            IPedidoVentaService servicio = Servicio(Retencion());

            var vm = Vm(servicio, A.Fake<IServicioDialogos>());

            A.CallTo(() => servicio.LeerRetenidosPorFalta()).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.EstaRetenidoPorFalta);
        }

        [TestMethod]
        public async Task PuedeDecidirFalse_BotonesDeshabilitadosYSeDiceQuienPuede()
        {
            var vm = Vm(Servicio(Retencion(puedeDecidir: false)), A.Fake<IServicioDialogos>());

            await vm.CargarRetencionPorFaltaAsync();

            Assert.IsTrue(vm.EstaRetenidoPorFalta);
            Assert.IsFalse(vm.PuedeDecidirRetencionPorFalta);
            Assert.IsFalse(vm.ServirLoQueHayCommand.CanExecute(null));
            Assert.IsFalse(vm.EsperarCompletoCommand.CanExecute(null));
            Assert.AreEqual(ReglasRetencionPorFalta.TEXTO_NO_PUEDE_DECIDIR, vm.TextoNoPuedeDecidirRetencionPorFalta);
        }

        [TestMethod]
        public async Task PuedeDecidirFalse_DecidirNoLlamaALaApi()
        {
            IPedidoVentaService servicio = Servicio(Retencion(puedeDecidir: false));
            IServicioDialogos dialogos = DialogosQueConfirman(true);
            var vm = Vm(servicio, dialogos);

            await vm.DecidirRetencionPorFaltaAsync(ReglasRetencionPorFalta.DECISION_ESPERAR);

            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => servicio.DecidirRetencionPorFalta(A<string>._, A<int>._, A<string>._)).MustNotHaveHappened();
        }

        #endregion

        #region Decidir

        [DataTestMethod]
        [DataRow(ReglasRetencionPorFalta.DECISION_SERVIR_LO_QUE_HAY, ReglasRetencionPorFalta.TITULO_SERVIR_LO_QUE_HAY)]
        [DataRow(ReglasRetencionPorFalta.DECISION_ESPERAR, ReglasRetencionPorFalta.TITULO_ESPERAR)]
        public async Task Decidir_Confirmado_LlamaALaApiQuitaLaBandaYRecarga(string decision, string titulo)
        {
            IPedidoVentaService servicio = Servicio(Retencion());
            IServicioDialogos dialogos = DialogosQueConfirman(true);
            var vm = Vm(servicio, dialogos);
            A.CallTo(() => servicio.LeerRetenidosPorFalta()).Returns(Task.FromResult(new List<RetencionPorFaltaModel>())); // ya decidido
            A.CallTo(() => servicio.DecidirRetencionPorFalta("1", NUMERO, decision))
                .Returns(Task.FromResult(new RetencionPorFaltaModel { Empresa = "1", Pedido = NUMERO, Estado = decision }));

            await vm.DecidirRetencionPorFaltaAsync(decision);

            A.CallTo(() => dialogos.ShowConfirmationAsync(titulo, A<string>.That.Contains(NUMERO.ToString()))).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.DecidirRetencionPorFalta("1", NUMERO, decision)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.cargarPedido("1", NUMERO)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.EstaRetenidoPorFalta);
        }

        [TestMethod]
        public async Task Decidir_ComandoServir_MandaServirLoQueHay()
        {
            IPedidoVentaService servicio = Servicio(Retencion());
            var vm = Vm(servicio, DialogosQueConfirman(true));

            await vm.ServirLoQueHayCommand.ExecuteAsync(null);

            A.CallTo(() => servicio.DecidirRetencionPorFalta("1", NUMERO, "ServirLoQueHay")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Decidir_ComandoEsperar_MandaEsperar()
        {
            IPedidoVentaService servicio = Servicio(Retencion());
            var vm = Vm(servicio, DialogosQueConfirman(true));

            await vm.EsperarCompletoCommand.ExecuteAsync(null);

            A.CallTo(() => servicio.DecidirRetencionPorFalta("1", NUMERO, "Esperar")).MustHaveHappenedOnceExactly();
        }

        [DataTestMethod]
        [DataRow(ReglasRetencionPorFalta.DECISION_SERVIR_LO_QUE_HAY)]
        [DataRow(ReglasRetencionPorFalta.DECISION_ESPERAR)]
        public async Task Decidir_ConfirmacionNo_NoLlamaNiRecarga(string decision)
        {
            IPedidoVentaService servicio = Servicio(Retencion());
            var vm = Vm(servicio, DialogosQueConfirman(false));

            await vm.DecidirRetencionPorFaltaAsync(decision);

            A.CallTo(() => servicio.DecidirRetencionPorFalta(A<string>._, A<int>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => servicio.cargarPedido(A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.IsTrue(vm.EstaRetenidoPorFalta);
        }

        [TestMethod]
        public async Task Decidir_409_EnsenaElMensajeTalCualYRecarga()
        {
            const string mensaje = "El pedido 930500 ya no está retenido: lo decidió Alfredo a las 11:02.";
            IPedidoVentaService servicio = Servicio(Retencion());
            IServicioDialogos dialogos = DialogosQueConfirman(true);
            var vm = Vm(servicio, dialogos);
            A.CallTo(() => servicio.DecidirRetencionPorFalta(A<string>._, A<int>._, A<string>._)).Throws(new Exception(mensaje));

            await vm.DecidirRetencionPorFaltaAsync(ReglasRetencionPorFalta.DECISION_ESPERAR);

            A.CallTo(() => dialogos.ShowError(mensaje)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.cargarPedido("1", NUMERO)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.PuedeDecidirRetencionPorFalta, "tras el error se vuelve a poder pulsar (la recarga relee la retención)");
        }

        [TestMethod]
        public async Task Decidir_ConMensajeDeLaApi_SeEnsena()
        {
            IPedidoVentaService servicio = Servicio(Retencion());
            IServicioDialogos dialogos = DialogosQueConfirman(true);
            var vm = Vm(servicio, dialogos);
            A.CallTo(() => servicio.DecidirRetencionPorFalta(A<string>._, A<int>._, A<string>._))
                .Returns(Task.FromResult(new RetencionPorFaltaModel { Pedido = NUMERO, Estado = "Esperar", Mensaje = "Lo cogido queda pendiente de ubicar." }));

            await vm.DecidirRetencionPorFaltaAsync(ReglasRetencionPorFalta.DECISION_ESPERAR);

            A.CallTo(() => dialogos.ShowNotification(ReglasRetencionPorFalta.TITULO_ESPERAR, "Lo cogido queda pendiente de ubicar.")).MustHaveHappenedOnceExactly();
        }

        #endregion

        #region Textos

        [TestMethod]
        public void TextosConfirmacion_ExplicanLaConsecuencia()
        {
            StringAssert.Contains(ReglasRetencionPorFalta.TextoConfirmacionServir(Retencion()), "sin 17404");
            StringAssert.Contains(ReglasRetencionPorFalta.TextoConfirmacionServir(Retencion()), "pendiente");
            StringAssert.Contains(ReglasRetencionPorFalta.TextoConfirmacionEsperar(Retencion()), "sale del picking");
            StringAssert.Contains(ReglasRetencionPorFalta.TextoConfirmacionEsperar(Retencion()), "cuando llegue 17404");
        }

        #endregion
    }
}
