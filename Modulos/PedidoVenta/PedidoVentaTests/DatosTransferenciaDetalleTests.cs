using ControlesUsuario.Models;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity;

namespace PedidoVentaTests
{
    /// <summary>
    /// Sugerencia 396 de Novedades (Paloma): en el detalle de un pedido prepago por transferencia, un botón
    /// copia el IBAN, el beneficiario, el concepto y el importe que da la API, y avisa con una notificación.
    /// </summary>
    [TestClass]
    public class DatosTransferenciaDetalleTests
    {
        private const string TEXTO = "IBAN: ES91 2100 0418 4502 0005 1332\r\nBeneficiario: NUEVA VISION, S.A.\r\nConcepto: Cliente 29606 - Pedido 927160\r\nImporte: 121,12 €";

        private class PortapapelesDePrueba : IPortapapelesTexto
        {
            public List<string> Copiados { get; } = new List<string>();
            public void CopiarTexto(string texto) => Copiados.Add(texto);
        }

        private static PedidoVentaDTO Pedido(string formaPago = "TRN", string plazosPago = "PRE", int numero = 927160) => new PedidoVentaDTO
        {
            empresa = "1",
            numero = numero,
            cliente = "29606",
            contacto = "0",
            formaPago = formaPago,
            plazosPago = plazosPago,
            Lineas = new List<LineaPedidoVentaDTO>()
        };

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IDialogService dialogService, PedidoVentaDTO pedido, PortapapelesDePrueba portapapeles = null)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IRegionManager>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogService, A.Fake<IUnityContainer>(), A.Fake<IServicioAutenticacion>());
            vm.Portapapeles = portapapeles ?? new PortapapelesDePrueba();
            vm.pedido = new PedidoVentaWrapper(pedido);
            return vm;
        }

        #region Cuándo se ofrece

        [TestMethod]
        public void EsPrepagoPorTransferencia_SoloConTrnYPre_ConRellenoYMayusculasDaIgual()
        {
            Assert.IsTrue(DatosTransferenciaPedido.EsPrepagoPorTransferencia("TRN", "PRE"));
            Assert.IsTrue(DatosTransferenciaPedido.EsPrepagoPorTransferencia("TRN ", "pre       "));
            Assert.IsFalse(DatosTransferenciaPedido.EsPrepagoPorTransferencia("TRN", "CONTADO"));
            Assert.IsFalse(DatosTransferenciaPedido.EsPrepagoPorTransferencia("TAR", "PRE"));
            Assert.IsFalse(DatosTransferenciaPedido.EsPrepagoPorTransferencia("RCB", "1/30"));
            Assert.IsFalse(DatosTransferenciaPedido.EsPrepagoPorTransferencia(null, null));
        }

        [TestMethod]
        public void Detalle_PrepagoPorTransferencia_BotonVisibleYHabilitado()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IDialogService>(), Pedido());

            Assert.IsTrue(vm.EsPrepagoPorTransferencia);
            Assert.IsTrue(vm.CopiarDatosTransferenciaCommand.CanExecute(null));
        }

        [TestMethod]
        public void Detalle_OtraFormaDePago_NoSeOfrece()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IDialogService>(), Pedido(formaPago: "RCB", plazosPago: "PRE"));

            Assert.IsFalse(vm.EsPrepagoPorTransferencia);
            Assert.IsFalse(vm.CopiarDatosTransferenciaCommand.CanExecute(null));
        }

        [TestMethod]
        public void Detalle_TransferenciaSinPrepago_NoSeOfrece()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IDialogService>(), Pedido(plazosPago: "CONTADO"));

            Assert.IsFalse(vm.EsPrepagoPorTransferencia);
            Assert.IsFalse(vm.CopiarDatosTransferenciaCommand.CanExecute(null));
        }

        [TestMethod]
        public void Detalle_AlCambiarAPrepagoPorTransferencia_ApareceElBoton()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IDialogService>(), Pedido(formaPago: "RCB", plazosPago: "1/30"));
            var cambiadas = new List<string>();
            vm.PropertyChanged += (s, e) => cambiadas.Add(e.PropertyName);
            bool puedeEjecutarCambio = false;
            vm.CopiarDatosTransferenciaCommand.CanExecuteChanged += (s, e) => puedeEjecutarCambio = true;

            vm.pedido.formaPago = "TRN";
            vm.pedido.plazosPago = "PRE";

            Assert.IsTrue(vm.EsPrepagoPorTransferencia);
            CollectionAssert.Contains(cambiadas, nameof(DetallePedidoViewModel.EsPrepagoPorTransferencia));
            Assert.IsTrue(puedeEjecutarCambio);
            Assert.IsTrue(vm.CopiarDatosTransferenciaCommand.CanExecute(null));
        }

        [TestMethod]
        public void Detalle_PedidoSinGuardar_VisiblePeroDeshabilitado()
        {
            // El concepto lleva el nº de pedido: hasta que no se guarda no hay nada que copiar
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IDialogService>(), Pedido(numero: 0));

            Assert.IsTrue(vm.EsPrepagoPorTransferencia);
            Assert.IsFalse(vm.CopiarDatosTransferenciaCommand.CanExecute(null));
        }

        #endregion

        #region Qué se copia

        [TestMethod]
        public async Task Copiar_CopiaElTextoDeLaApiYAvisaConUnaNotificacion()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.LeerDatosTransferencia("1", 927160)).Returns(new DatosTransferenciaPedidoModel
            {
                Pedido = 927160,
                Iban = "ES91 2100 0418 4502 0005 1332",
                Titular = "NUEVA VISION, S.A.",
                Concepto = "Cliente 29606 - Pedido 927160",
                Importe = 121.12M,
                Texto = TEXTO
            });
            IDialogService dialogService = A.Fake<IDialogService>();
            var portapapeles = new PortapapelesDePrueba();
            var vm = Vm(servicio, dialogService, Pedido(), portapapeles);

            string copiado = await vm.CopiarDatosTransferenciaAsync();

            Assert.AreEqual(TEXTO, copiado);
            CollectionAssert.AreEqual(new List<string> { TEXTO }, portapapeles.Copiados);
            A.CallTo(() => dialogService.ShowDialog("NotificationDialog",
                A<IDialogParameters>.That.Matches(p => p.GetValue<string>("title") == DatosTransferenciaPedido.TITULO &&
                                                       p.GetValue<string>("message").Contains(TEXTO)),
                A<Action<IDialogResult>>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Copiar_SiLaApiFalla_NoCopiaNadaYEnseñaElMotivo()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.LeerDatosTransferencia(A<string>._, A<int>._))
                .ThrowsAsync(new Exception("La empresa 1 no tiene ninguna cuenta bancaria para recibir transferencias."));
            IDialogService dialogService = A.Fake<IDialogService>();
            var portapapeles = new PortapapelesDePrueba();
            var vm = Vm(servicio, dialogService, Pedido(), portapapeles);

            string copiado = await vm.CopiarDatosTransferenciaAsync();

            Assert.IsNull(copiado);
            Assert.AreEqual(0, portapapeles.Copiados.Count);
            A.CallTo(() => dialogService.ShowDialog("NotificationDialog",
                A<IDialogParameters>.That.Matches(p => p.GetValue<string>("message").Contains("cuenta bancaria")),
                A<Action<IDialogResult>>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Copiar_SinPrepagoPorTransferencia_NoLlamaALaApi()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            var portapapeles = new PortapapelesDePrueba();
            var vm = Vm(servicio, A.Fake<IDialogService>(), Pedido(formaPago: "TAR"), portapapeles);

            Assert.IsNull(await vm.CopiarDatosTransferenciaAsync());
            A.CallTo(() => servicio.LeerDatosTransferencia(A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.AreEqual(0, portapapeles.Copiados.Count);
        }

        #endregion
    }
}
