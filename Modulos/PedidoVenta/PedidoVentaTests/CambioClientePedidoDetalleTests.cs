using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ControlesUsuario.Dialogs;

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

        private static IServicioDialogos DialogoQueResponde(bool respuesta)
        {
            IServicioDialogos dialogService = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogService.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(respuesta));
            return dialogService;
        }

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IServicioDialogos dialogService, PedidoVentaDTO pedido = null)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogService, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
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

        // Carlos, 01/10/26: el grupo Administración también crea albaranes y facturas de venta desde el detalle
        // (hasta ahora solo Almacén y Tiendas; la API no filtra por grupo).
        [DataTestMethod]
        [DataRow("Almacén", true)]
        [DataRow("Tiendas", true)]
        [DataRow("Administración", true)]
        [DataRow("Compras", false)]
        public void EsGrupoQuePuedeFacturar_SegunElGrupoDelUsuario(string grupo, bool puede)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(A<string>._)).ReturnsLazily((string g) => g == grupo);

            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPedidoVentaService>(), new WeakReferenceMessenger(),
                A.Fake<IServicioDialogos>(), A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());

            Assert.AreEqual(puede, vm.EsGrupoQuePuedeFacturar);
        }

        [TestMethod]
        public void PuedeCambiarse_PedidoNuevo_No()
        {
            PedidoVentaDTO nuevo = Pedido();
            nuevo.numero = 0;

            Assert.IsFalse(CambioClientePedido.PuedeCambiarse(nuevo));
        }

        // NestoAPI#582: la nota 927519 (renting) salió a nombre de Grenke y Alfredo no tenía el botón para pasarla
        // al cliente final. Una nota ya está facturada (yaFacturado): eso no bloquea; picking o albarán, sí.
        [TestMethod]
        public void PuedeCambiarse_NotaDeEntregaYaFacturadaSinPicking_Si()
        {
            PedidoVentaDTO nota = Pedido();
            nota.notaEntrega = true;
            nota.Lineas.Single().yaFacturado = true;

            Assert.IsTrue(CambioClientePedido.PuedeCambiarse(nota));
        }

        [TestMethod]
        public void PuedeCambiarse_NotaDeEntregaConPickingOAlbaran_No()
        {
            PedidoVentaDTO conPicking = Pedido(picking: 99700);
            conPicking.notaEntrega = true;
            conPicking.Lineas.Single().yaFacturado = true;
            PedidoVentaDTO conAlbaran = Pedido(estado: 2);
            conAlbaran.notaEntrega = true;

            Assert.IsFalse(CambioClientePedido.PuedeCambiarse(conPicking));
            Assert.IsFalse(CambioClientePedido.PuedeCambiarse(conAlbaran));
        }

        [TestMethod]
        public void PuedeCambiarse_ConPrepagoSinFacturar_Si()
        {
            // Incidencia 1360 (19/11/26): el prepago es del pedido y se va con él al cliente nuevo
            PedidoVentaDTO pedido = Pedido();
            pedido.Prepagos = new ObservableCollection<PrepagoDTO> { new PrepagoDTO { Importe = 10 } };

            Assert.IsTrue(CambioClientePedido.PuedeCambiarse(pedido));
        }

        [TestMethod]
        public void PuedeCambiarCliente_EnElDetalle_SigueAlPedido()
        {
            Assert.IsTrue(Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>()).PuedeCambiarCliente);
            Assert.IsFalse(Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), Pedido(picking: 3)).PuedeCambiarCliente);
        }

        #endregion

        #region Aplicar

        [TestMethod]
        public async Task AplicarCambioCliente_ConfirmaYLlamaALaApiYRecarga()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.CambiarCliente("1", 926000, "20000", "0", false))
                .Returns(new CambiarClientePedidoRespuestaModel { Numero = 926000, ClienteAnterior = "10000", ContactoAnterior = "0", Cliente = "20000", Contacto = "0" });
            DetallePedidoViewModel vm = Vm(servicio, DialogoQueResponde(true));
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
            DetallePedidoViewModel vm = Vm(servicio, DialogoQueResponde(false));
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
            DetallePedidoViewModel vm = Vm(servicio, DialogoQueResponde(true));
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

        #region Por qué no se puede (Carlos 26/11/26: el botón ya no desaparece)

        [TestMethod]
        public void MotivoNoSePuedeCambiar_SiSePuede_Nothing()
        {
            Assert.IsNull(CambioClientePedido.MotivoNoSePuedeCambiar(Pedido()));
        }

        [TestMethod]
        public void MotivoNoSePuedeCambiar_ConPicking_DiceElPickingYQueLoQuite()
        {
            Assert.AreEqual("No se puede cambiar el cliente: el pedido tiene el picking 100198. Quite el picking antes.",
                CambioClientePedido.MotivoNoSePuedeCambiar(Pedido(picking: 100198)));
        }

        [TestMethod]
        public void MotivoNoSePuedeCambiar_VariosPickings_LosEnumera()
        {
            PedidoVentaDTO pedido = Pedido(picking: 100200);
            pedido.Lineas.Add(new LineaPedidoVentaDTO { id = 2, Producto = "1", Cantidad = 1, estado = 1, picking = 100198 });
            pedido.Lineas.Add(new LineaPedidoVentaDTO { id = 3, Producto = "2", Cantidad = 1, estado = 1, picking = 100200 });

            Assert.AreEqual("No se puede cambiar el cliente: el pedido tiene los pickings 100198 y 100200. Quite el picking antes.",
                CambioClientePedido.MotivoNoSePuedeCambiar(pedido));
        }

        [TestMethod]
        public void MotivoNoSePuedeCambiar_ConAlbaran_DiceElAlbaran()
        {
            PedidoVentaDTO pedido = Pedido(picking: 7, estado: 2);
            pedido.Lineas.Single().Albaran = 734001;

            Assert.AreEqual("No se puede cambiar el cliente: el pedido tiene el albarán 734001.", CambioClientePedido.MotivoNoSePuedeCambiar(pedido));
        }

        [TestMethod]
        public void MotivoNoSePuedeCambiar_ConFactura_DiceLaFactura()
        {
            PedidoVentaDTO pedido = Pedido(picking: 7, estado: 4);
            pedido.Lineas.Single().Albaran = 734001;
            pedido.Lineas.Single().Factura = "NV26012345";

            Assert.AreEqual("No se puede cambiar el cliente: el pedido tiene la factura NV26012345.", CambioClientePedido.MotivoNoSePuedeCambiar(pedido));
        }

        [TestMethod]
        public void MotivoNoSePuedeCambiar_PedidoSinGuardar_PideGuardar()
        {
            PedidoVentaDTO nuevo = Pedido();
            nuevo.numero = 0;

            Assert.AreEqual("No se puede cambiar el cliente: guarde antes el pedido.", CambioClientePedido.MotivoNoSePuedeCambiar(nuevo));
        }

        [TestMethod]
        public void MotivoNoSePuedeCambiar_NotaYaFacturadaSinPicking_Nothing()
        {
            PedidoVentaDTO nota = Pedido();
            nota.notaEntrega = true;
            nota.Lineas.Single().yaFacturado = true;

            Assert.IsNull(CambioClientePedido.MotivoNoSePuedeCambiar(nota));
        }

        [TestMethod]
        public void MotivoNoSePuedeCambiar_EfectosAMano_DiceLosEfectos()
        {
            PedidoVentaDTO pedido = Pedido();
            pedido.CrearEfectosManualmente = true;
            pedido.Efectos = new ListaEfectos { new Efecto { Importe = 10 } };

            StringAssert.Contains(CambioClientePedido.MotivoNoSePuedeCambiar(pedido), "efectos creados a mano");
        }

        [TestMethod]
        public void Detalle_ConPicking_BotonVisibleYDeshabilitadoConElMotivo()
        {
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), Pedido(picking: 100198));

            Assert.IsTrue(vm.HayPedidoParaCambiarCliente);
            Assert.IsFalse(vm.AbrirCambioClienteCommand.CanExecute(null));
            Assert.AreEqual("No se puede cambiar el cliente: el pedido tiene el picking 100198. Quite el picking antes.", vm.ToolTipCambiarCliente);
        }

        [TestMethod]
        public void Detalle_SePuede_ToolTipExplicaQueHace()
        {
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>());

            Assert.IsTrue(vm.AbrirCambioClienteCommand.CanExecute(null));
            Assert.AreEqual(DetallePedidoViewModel.TOOLTIP_CAMBIAR_CLIENTE, vm.ToolTipCambiarCliente);
        }

        #endregion
    }
}
