using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ControlesUsuario.Dialogs;

namespace PedidoVentaTests
{
    /// <summary>
    /// NestoAPI#698: retener un pedido hasta nuevo aviso (modo de servicio 5 + tabla PedidosRetenidos) en vez de poner la
    /// fecha de entrega un año más tarde. Se pone y se quita solo con «Retener…» y «Liberar»; el selector de modo no deja
    /// elegir el 5 y, mientras está retenido, está deshabilitado.
    /// </summary>
    [TestClass]
    public class RetencionPedidoDetalleTests
    {
        private const int NUMERO = 929200;

        private static PedidoVentaDTO Pedido(short estado = 1, byte? modo = ModosServicio.TRAS_REPONER_DE_TIENDAS, RetencionPedidoDTO retencion = null) => new PedidoVentaDTO
        {
            empresa = "1",
            numero = NUMERO,
            cliente = "10000",
            contacto = "0",
            modoServicio = modo,
            retencion = retencion,
            Lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { id = 1, Producto = "38093", Cantidad = 2, almacen = "ALG", tipoLinea = 1, estado = estado }
            }
        };

        private static RetencionPedidoDTO Retencion(DateTime? hasta = null, string usuario = @"NUEVAVISION\Alfredo", byte? anterior = ModosServicio.TRAS_REPONER_DE_TIENDAS) => new RetencionPedidoDTO
        {
            Id = 7,
            Empresa = "1",
            Pedido = NUMERO,
            Motivo = "El cliente cierra por obras",
            HastaFecha = hasta,
            ModoServicioAnterior = anterior,
            Usuario = usuario,
            Fecha = new DateTime(2026, 10, 22, 10, 30, 0)
        };

        private static PedidoVentaDTO PedidoRetenido(DateTime? hasta = null) =>
            Pedido(modo: ModosServicio.RETENIDO, retencion: Retencion(hasta));

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IServicioDialogos dialogos, PedidoVentaDTO pedido)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogos, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.pedido = new PedidoVentaWrapper(pedido);
            return vm;
        }

        private static IServicioDialogos DialogoRetenerQueContesta(string motivo, DateTime? hasta)
        {
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowDialogAsync(RetenerPedidoDialogViewModel.NOMBRE, A<ParametrosDialogo>._))
                .Returns(Task.FromResult(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo
                {
                    { RetenerPedidoDialogViewModel.PARAMETRO_MOTIVO, motivo },
                    { RetenerPedidoDialogViewModel.PARAMETRO_HASTA_FECHA, hasta }
                })));
            return dialogos;
        }

        #region ModosServicio

        [TestMethod]
        public void ModosServicio_Efectivo_RetenidoMandaSobreServirJunto()
        {
            Assert.AreEqual(ModosServicio.RETENIDO, ModosServicio.Efectivo(ModosServicio.RETENIDO, true));
            Assert.AreEqual(ModosServicio.RETENIDO, ModosServicio.Efectivo(ModosServicio.RETENIDO, false));
        }

        [TestMethod]
        public void ModosServicio_Nombre5_EsRetenido()
        {
            Assert.AreEqual("Retenido", ModosServicio.Nombre(ModosServicio.RETENIDO));
        }

        [TestMethod]
        public void ModosServicio_RetenidoNoEsElegible()
        {
            Assert.IsFalse(ModosServicio.EsValido(ModosServicio.RETENIDO));
            Assert.IsTrue(ModosServicio.EsRetenido(ModosServicio.RETENIDO));
            Assert.IsFalse(ModosServicio.EsRetenido(ModosServicio.TODO_JUNTO));
            Assert.IsFalse(ModosServicio.Lista.Any(m => m.Codigo == ModosServicio.RETENIDO), "el selector no debe ofrecer el 5");
        }

        #endregion

        #region Texto del aviso

        [TestMethod]
        public void TextoRetenido_ConFecha_QuitaElDominio()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), PedidoRetenido(new DateTime(2026, 11, 15)));

            Assert.IsTrue(vm.EstaRetenido);
            Assert.AreEqual("Retenido por Alfredo desde 22/10/2026 hasta 15/11/2026: El cliente cierra por obras", vm.TextoRetenido);
        }

        [TestMethod]
        public void TextoRetenido_SinFecha_HastaNuevoAviso()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), PedidoRetenido());

            Assert.AreEqual("Retenido por Alfredo desde 22/10/2026 hasta nuevo aviso: El cliente cierra por obras", vm.TextoRetenido);
        }

        [TestMethod]
        public void TextoRetenido_UsuarioSinDominio_SeEnsenaTalCual()
        {
            StringAssert.StartsWith(RetencionPedido.Texto(Retencion(usuario: "Carlos")), "Retenido por Carlos desde");
        }

        [TestMethod]
        public void TextoRetenido_PedidoNoRetenido_Vacio()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), Pedido());

            Assert.IsFalse(vm.EstaRetenido);
            Assert.AreEqual(string.Empty, vm.TextoRetenido);
        }

        #endregion

        #region Cuándo se ofrecen los botones

        [TestMethod]
        public void CanExecute_PedidoPendienteSinRetener_RetenerSiLiberarNo()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), Pedido());

            Assert.IsTrue(vm.CmdRetenerPedido.CanExecute(null));
            Assert.IsFalse(vm.CmdLiberarPedido.CanExecute(null));
        }

        [TestMethod]
        public void CanExecute_PedidoRetenido_LiberarSiRetenerNo()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), PedidoRetenido());

            Assert.IsFalse(vm.CmdRetenerPedido.CanExecute(null));
            Assert.IsTrue(vm.CmdLiberarPedido.CanExecute(null));
        }

        [TestMethod]
        public void CanExecute_SinNadaPendiente_NoSeRetiene()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), Pedido(estado: 4));

            Assert.IsFalse(vm.CmdRetenerPedido.CanExecute(null));
        }

        [TestMethod]
        public void CanExecute_PedidoNuevoPresupuestoONota_NoSeRetiene()
        {
            PedidoVentaDTO nuevo = Pedido();
            nuevo.numero = 0;
            PedidoVentaDTO presupuesto = Pedido(estado: -3);
            presupuesto.EsPresupuesto = true;
            PedidoVentaDTO nota = Pedido();
            nota.notaEntrega = true;

            Assert.IsFalse(RetencionPedido.PuedeRetenerse(nuevo));
            Assert.IsFalse(RetencionPedido.PuedeRetenerse(presupuesto));
            Assert.IsFalse(RetencionPedido.PuedeRetenerse(nota));
        }

        #endregion

        #region Selector de modo

        [TestMethod]
        public void SelectorModo_Retenido_DeshabilitadoYSoloEnsenaRetenido()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), PedidoRetenido());

            Assert.IsFalse(vm.PuedeElegirModoServicio);
            Assert.AreEqual(ModosServicio.RETENIDO, vm.pedido.ModoServicio);
            Assert.AreEqual(1, vm.OpcionesModoServicio.Count);
            Assert.AreEqual("Retenido", vm.OpcionesModoServicio[0].Nombre);
            Assert.IsFalse(vm.OpcionesModoServicio[0].Habilitado);
        }

        [TestMethod]
        public void SelectorModo_NoRetenido_HabilitadoSinElCinco()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), Pedido());

            Assert.IsTrue(vm.PuedeElegirModoServicio);
            Assert.IsFalse(vm.OpcionesModoServicio.Any(o => o.Codigo == ModosServicio.RETENIDO));
        }

        [TestMethod]
        public void SelectorModo_Retenido_LaSugerenciaDeLaApiNoLoCambia()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), PedidoRetenido());

            vm.CambiarAModoValido(ModosServicio.SEGUN_VAYA_ENTRANDO, null, avisar: true);

            Assert.AreEqual(ModosServicio.RETENIDO, vm.pedido.ModoServicio);
        }

        #endregion

        #region Retener y liberar

        [TestMethod]
        public async Task Retener_LlamaALaApiConMotivoYFechaYQuedaRetenido()
        {
            var hasta = new DateTime(2026, 11, 15);
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.RetenerPedido("1", NUMERO, "El cliente cierra por obras", hasta))
                .Returns(Task.FromResult(Retencion(hasta)));
            var vm = Vm(servicio, DialogoRetenerQueContesta("  El cliente cierra por obras ", hasta), Pedido());
            var cambios = new List<string>();
            vm.PropertyChanged += (s, e) => cambios.Add(e.PropertyName);

            await vm.RetenerPedidoAsync();

            A.CallTo(() => servicio.RetenerPedido("1", NUMERO, "El cliente cierra por obras", hasta)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.EstaRetenido);
            Assert.AreEqual((byte?)ModosServicio.RETENIDO, vm.pedido.Model.modoServicio);
            Assert.AreEqual(7, vm.pedido.Model.retencion.Id);
            Assert.IsFalse(vm.PuedeElegirModoServicio);
            Assert.IsFalse(vm.CmdRetenerPedido.CanExecute(null));
            Assert.IsTrue(vm.CmdLiberarPedido.CanExecute(null));
            CollectionAssert.Contains(cambios, nameof(DetallePedidoViewModel.TextoRetenido));
            CollectionAssert.Contains(cambios, nameof(DetallePedidoViewModel.OpcionesModoServicio));
        }

        [TestMethod]
        public async Task Retener_SinFecha_MandaNull()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.RetenerPedido(A<string>._, A<int>._, A<string>._, A<DateTime?>._))
                .Returns(Task.FromResult(Retencion()));
            var vm = Vm(servicio, DialogoRetenerQueContesta("Esperar a que pague", null), Pedido());

            await vm.RetenerPedidoAsync();

            A.CallTo(() => servicio.RetenerPedido("1", NUMERO, "Esperar a que pague", null)).MustHaveHappenedOnceExactly();
            StringAssert.Contains(vm.TextoRetenido, "hasta nuevo aviso");
        }

        [TestMethod]
        public async Task Retener_SiCancelaElDialogo_NoLlamaALaApi()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._))
                .Returns(Task.FromResult(new ResultadoDialogo(ResultadoBoton.Cancel)));
            var vm = Vm(servicio, dialogos, Pedido());

            await vm.RetenerPedidoAsync();

            A.CallTo(() => servicio.RetenerPedido(A<string>._, A<int>._, A<string>._, A<DateTime?>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.EstaRetenido);
        }

        [TestMethod]
        public async Task Retener_SiLaApiLoRechaza_EnsenaElMotivoYNoCambiaNada()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.RetenerPedido(A<string>._, A<int>._, A<string>._, A<DateTime?>._))
                .ThrowsAsync(new Exception("El pedido no tiene nada pendiente de servir"));
            IServicioDialogos dialogos = DialogoRetenerQueContesta("Obras", null);
            var vm = Vm(servicio, dialogos, Pedido());

            await vm.RetenerPedidoAsync();

            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("nada pendiente"))).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.EstaRetenido);
            Assert.AreEqual(ModosServicio.TRAS_REPONER_DE_TIENDAS, vm.pedido.ModoServicio);
        }

        [TestMethod]
        public async Task Liberar_ConfirmaLlamaALaApiYVuelveAlModoAnterior()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            RetencionPedidoDTO liberada = Retencion(anterior: ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ);
            liberada.Liberado = new DateTime(2026, 10, 27);
            A.CallTo(() => servicio.LiberarPedido("1", NUMERO)).Returns(Task.FromResult(liberada));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(true));
            var vm = Vm(servicio, dialogos, PedidoRetenido());

            await vm.LiberarPedidoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(RetencionPedido.TITULO_LIBERAR,
                $"¿Liberar el pedido {NUMERO}? Volverá a su modo de servicio anterior y entrará en el picking.")).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.LiberarPedido("1", NUMERO)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.EstaRetenido);
            Assert.IsNull(vm.pedido.Model.retencion);
            Assert.AreEqual(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, vm.pedido.ModoServicio);
            Assert.IsTrue(vm.PuedeElegirModoServicio);
            Assert.IsTrue(vm.CmdRetenerPedido.CanExecute(null));
            Assert.IsFalse(vm.CmdLiberarPedido.CanExecute(null));
            Assert.AreEqual(string.Empty, vm.TextoRetenido);
        }

        [TestMethod]
        public async Task Liberar_SiNoConfirma_NoLlamaALaApi()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(false));
            var vm = Vm(servicio, dialogos, PedidoRetenido());

            await vm.LiberarPedidoAsync();

            A.CallTo(() => servicio.LiberarPedido(A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.IsTrue(vm.EstaRetenido);
        }

        #endregion
    }
}
