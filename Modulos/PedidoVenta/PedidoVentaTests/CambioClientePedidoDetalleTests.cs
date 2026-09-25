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
using System.ComponentModel.DataAnnotations;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Unity;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#496 / NestoAPI#519: cambiar el cliente de un pedido que todavía no ha salido. El botón solo se ofrece
    /// cuando tiene sentido, se pide confirmación, se llama a la API y se recarga el pedido.
    /// </summary>
    [TestClass]
    public class CambioClientePedidoDetalleTests
    {
        private static PedidoVentaDTO Pedido(int picking = 0, short estado = 1) => new PedidoVentaDTO
        {
            empresa = "1",
            numero = 926000,
            cliente = "10000",
            contacto = "0",
            Lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { id = 1, Producto = "38093", Cantidad = 2, almacen = "ALG", tipoLinea = 1, estado = estado, picking = picking }
            }
        };

        private static IDialogService DialogoQueResponde(ButtonResult respuesta)
        {
            IDialogService dialogService = A.Fake<IDialogService>();
            A.CallTo(() => dialogService.ShowDialog("ConfirmationDialog", A<IDialogParameters>._, A<Action<IDialogResult>>._))
                .Invokes(call => call.GetArgument<Action<IDialogResult>>(2)(new DialogResult(respuesta)));
            return dialogService;
        }

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IDialogService dialogService, PedidoVentaDTO pedido = null)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IRegionManager>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogService, A.Fake<IUnityContainer>(), A.Fake<IServicioAutenticacion>());
            vm.pedido = new PedidoVentaWrapper(pedido ?? Pedido());
            return vm;
        }

        #region Cuándo se ofrece

        [TestMethod]
        public void PuedeCambiarse_PedidoGuardadoSinPicking_Si()
        {
            Assert.IsTrue(CambioClientePedido.PuedeCambiarse(Pedido()));
        }

        [TestMethod]
        public void PuedeCambiarse_ConPicking_No()
        {
            Assert.IsFalse(CambioClientePedido.PuedeCambiarse(Pedido(picking: 55)));
        }

        [TestMethod]
        public void PuedeCambiarse_ConAlbaran_No()
        {
            Assert.IsFalse(CambioClientePedido.PuedeCambiarse(Pedido(estado: 2)));
        }

        [TestMethod]
        public void PuedeCambiarse_PedidoNuevoONotaDeEntrega_No()
        {
            PedidoVentaDTO nuevo = Pedido();
            nuevo.numero = 0;
            PedidoVentaDTO nota = Pedido();
            nota.notaEntrega = true;

            Assert.IsFalse(CambioClientePedido.PuedeCambiarse(nuevo));
            Assert.IsFalse(CambioClientePedido.PuedeCambiarse(nota));
        }

        [TestMethod]
        public void PuedeCambiarse_ConPrepagoSinFacturar_No()
        {
            PedidoVentaDTO pedido = Pedido();
            pedido.Prepagos = new ObservableCollection<PrepagoDTO> { new PrepagoDTO { Importe = 10 } };

            Assert.IsFalse(CambioClientePedido.PuedeCambiarse(pedido));
        }

        [TestMethod]
        public void PuedeCambiarCliente_EnElDetalle_SigueAlPedido()
        {
            Assert.IsTrue(Vm(A.Fake<IPedidoVentaService>(), A.Fake<IDialogService>()).PuedeCambiarCliente);
            Assert.IsFalse(Vm(A.Fake<IPedidoVentaService>(), A.Fake<IDialogService>(), Pedido(picking: 3)).PuedeCambiarCliente);
        }

        #endregion

        #region Aplicar

        [TestMethod]
        public async Task AplicarCambioCliente_ConfirmaYLlamaALaApiYRecarga()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.CambiarCliente("1", 926000, "20000", "0", false))
                .Returns(new CambiarClientePedidoRespuestaModel { Numero = 926000, ClienteAnterior = "10000", ContactoAnterior = "0", Cliente = "20000", Contacto = "0" });
            DetallePedidoViewModel vm = Vm(servicio, DialogoQueResponde(ButtonResult.OK));
            vm.AbrirCambioClienteCommand.Execute(null);
            vm.ClienteNuevo = "20000";
            vm.ContactoNuevo = "0";

            await vm.AplicarCambioClienteAsync();

            A.CallTo(() => servicio.CambiarCliente("1", 926000, "20000", "0", false)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.cargarPedido("1", 926000)).MustHaveHappened();
            Assert.IsFalse(vm.MostrarCambioCliente);
        }

        [TestMethod]
        public async Task AplicarCambioCliente_SiNoConfirma_NoLlamaALaApi()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            DetallePedidoViewModel vm = Vm(servicio, DialogoQueResponde(ButtonResult.Cancel));
            vm.AbrirCambioClienteCommand.Execute(null);
            vm.ClienteNuevo = "20000";

            await vm.AplicarCambioClienteAsync();

            A.CallTo(() => servicio.CambiarCliente(A<string>._, A<int>._, A<string>._, A<string>._, A<bool>._)).MustNotHaveHappened();
            Assert.IsTrue(vm.MostrarCambioCliente);
        }

        [TestMethod]
        public async Task AplicarCambioCliente_NoPasaLaValidacionYNoPuedeForzar_NoReintentaNiRecarga()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.CambiarCliente(A<string>._, A<int>._, A<string>._, A<string>._, false))
                .ThrowsAsync(new ValidationException("Oferta no permitida para el cliente 20000"));
            DetallePedidoViewModel vm = Vm(servicio, DialogoQueResponde(ButtonResult.OK));
            vm.ClienteNuevo = "20000";

            await vm.AplicarCambioClienteAsync();

            A.CallTo(() => servicio.CambiarCliente(A<string>._, A<int>._, A<string>._, A<string>._, true)).MustNotHaveHappened();
            A.CallTo(() => servicio.cargarPedido(A<string>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void TextoConfirmacion_ConCambiosSinGuardar_AvisaDeQueSePierden()
        {
            string texto = CambioClientePedido.TextoConfirmacion(926000, "10000", "0", "20000", null, "PELUQUERÍA NUEVA", true);

            StringAssert.Contains(texto, "10000/0");
            StringAssert.Contains(texto, "20000/principal (PELUQUERÍA NUEVA)");
            StringAssert.Contains(texto, "se van a perder");
        }

        [TestMethod]
        public void TextoResultado_EnumeraLosCambios()
        {
            string texto = CambioClientePedido.TextoResultado(new CambiarClientePedidoRespuestaModel
            {
                Numero = 926000, ClienteAnterior = "10000", ContactoAnterior = "0", Cliente = "20000", Contacto = "0",
                Cambios = new List<string> { "Forma de pago: EFC → RCB" }
            });

            StringAssert.Contains(texto, "• Forma de pago: EFC → RCB");
        }

        #endregion
    }
}
