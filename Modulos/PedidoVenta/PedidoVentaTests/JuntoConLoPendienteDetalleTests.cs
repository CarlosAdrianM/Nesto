using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services.ServirJunto;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Sugerencia 1340 (Paloma): el modo de servicio 6, «Junto con lo pendiente del cliente», en el combo «Servir» del
    /// detalle de pedido. Solo aparece cuando la API lo ofrece (o el pedido ya lo tiene), con su explicación debajo, y un
    /// pedido en 6 se guarda con 6 (antes Efectivo(6, True) daba 1).
    /// </summary>
    [TestClass]
    public class JuntoConLoPendienteDetalleTests
    {
        private const string EXPLICACION = "Saldrá junto con la nota de entrega 929701.";

        private static ModoServicioSugeridoDTO Sugerencia(bool ofreceElSeis, byte modo = ModosServicio.TRAS_REPONER_DE_TIENDAS, string explicacion = EXPLICACION)
        {
            var permitidos = new List<byte> { 1, 2, 3, 4 };
            var modos = permitidos.Select(m => new ModoServicioPermitidoDTO { Modo = m, Permitido = true }).ToList();
            if (ofreceElSeis)
            {
                permitidos.Add(ModosServicio.JUNTO_CON_LO_PENDIENTE);
                modos.Add(new ModoServicioPermitidoDTO
                {
                    Modo = ModosServicio.JUNTO_CON_LO_PENDIENTE,
                    Permitido = true,
                    Explicacion = explicacion,
                    PedidosConLosQueSeJunta = new List<int> { 929701 }
                });
            }
            return new ModoServicioSugeridoDTO { Modo = modo, ModosPermitidos = permitidos, Modos = modos };
        }

        private static PedidoVentaDTO Pedido(int numero, byte modo) => new PedidoVentaDTO
        {
            empresa = "1",
            numero = numero,
            cliente = "15191",
            contacto = "0",
            modoServicio = modo,
            servirJunto = ModosServicio.EsTodoJunto(modo),
            Lineas = new List<LineaPedidoVentaDTO> { new LineaPedidoVentaDTO { id = 1, Producto = "38093", Cantidad = 2, almacen = "ALG", tipoLinea = 1 } }
        };

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IServicioDialogos dialogService = null) => new DetallePedidoViewModel(
            A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
            dialogService ?? A.Fake<IServicioDialogos>(), A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());

        private static OpcionModo Seis(DetallePedidoViewModel vm) =>
            vm.OpcionesModoServicio.Single(o => o.Codigo == ModosServicio.JUNTO_CON_LO_PENDIENTE);

        [TestMethod]
        public async Task Detalle_SinNadaPendienteDelCliente_ElSeisNoAparece()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.OpcionesModos(A<PedidoVentaDTO>._)).Returns(new OpcionesModosDTO { Servicio = Sugerencia(ofreceElSeis: false) });
            DetallePedidoViewModel vm = Vm(servicio);
            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosServicio.TRAS_REPONER_DE_TIENDAS));

            await vm.RefrescarOpcionesModos();

            Assert.IsFalse(Seis(vm).Visible);
            Assert.IsTrue(vm.OpcionesModoServicio.Where(o => o.Codigo != ModosServicio.JUNTO_CON_LO_PENDIENTE).All(o => o.Visible));
            Assert.IsFalse(vm.HayExplicacionModoServicio);
        }

        [TestMethod]
        public async Task Detalle_LaApiLoOfrece_AparecePeroLaExplicacionSoloConElSeisElegido()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.OpcionesModos(A<PedidoVentaDTO>._)).Returns(new OpcionesModosDTO { Servicio = Sugerencia(ofreceElSeis: true) });
            DetallePedidoViewModel vm = Vm(servicio);
            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosServicio.TRAS_REPONER_DE_TIENDAS));

            await vm.RefrescarOpcionesModos();

            Assert.IsTrue(Seis(vm).Visible);
            Assert.IsTrue(Seis(vm).Habilitado);
            Assert.IsFalse(vm.HayExplicacionModoServicio, "Con otro modo elegido no se explica el 6");

            vm.pedido.ModoServicio = ModosServicio.JUNTO_CON_LO_PENDIENTE; // elección del usuario

            Assert.IsTrue(vm.pedido.servirJunto);
            Assert.AreEqual(EXPLICACION, vm.ExplicacionModoServicio);
            Assert.IsTrue(vm.HayExplicacionModoServicio);
        }

        [TestMethod]
        public void Detalle_PedidoYaEnSeis_SeVeAunqueLaApiNoHayaContestado()
        {
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>());

            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosServicio.JUNTO_CON_LO_PENDIENTE));

            Assert.AreEqual(ModosServicio.JUNTO_CON_LO_PENDIENTE, vm.pedido.ModoServicio, "Efectivo(6, True) ya no lo convierte en 1");
            Assert.IsTrue(Seis(vm).Visible);
        }

        [TestMethod]
        public async Task Detalle_PedidoEnSeis_SeGuardaConSeisYServirJunto()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            byte? modoMandado = null;
            bool? servirJuntoMandado = null;
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._))
                .Invokes((PedidoVentaDTO p) => { modoMandado = p.modoServicio; servirJuntoMandado = p.servirJunto; })
                .Returns(new List<AvisoPedidoModel>());
            DetallePedidoViewModel vm = Vm(servicio);
            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosServicio.JUNTO_CON_LO_PENDIENTE));

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).MustHaveHappened();
            Assert.AreEqual((byte?)ModosServicio.JUNTO_CON_LO_PENDIENTE, modoMandado);
            Assert.AreEqual(true, servirJuntoMandado);
        }

        [TestMethod]
        public async Task Detalle_ElegidoElSeisYLaApiDejaDeOfrecerlo_PasaAlSugeridoYAvisa()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            // Sugiere «Todo junto»: así el paso 6 → 1 no sale de «todo junto» y no hay validación de por medio.
            A.CallTo(() => servicio.OpcionesModos(A<PedidoVentaDTO>._)).Returns(new OpcionesModosDTO { Servicio = Sugerencia(ofreceElSeis: false, modo: ModosServicio.TODO_JUNTO) });
            DetallePedidoViewModel vm = Vm(servicio);
            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosServicio.TRAS_REPONER_DE_TIENDAS));

            vm.pedido.ModoServicio = ModosServicio.JUNTO_CON_LO_PENDIENTE; // elección del usuario
            await vm.RefrescarOpcionesModos();

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.pedido.ModoServicio);
            Assert.IsTrue(vm.HayAvisoModoServicio);
            Assert.IsFalse(Seis(vm).Visible);
            Assert.IsFalse(vm.HayExplicacionModoServicio);
        }

        [TestMethod]
        public async Task Detalle_DeSeisAUnModoParcialQueElServidorNoDeja_VuelveAlSeisYNoAlUno()
        {
            IServirJuntoService servirJunto = A.Fake<IServirJuntoService>();
            var denegada = new ValidarServirJuntoResponse { PuedeDesmarcar = false, Mensaje = "No se puede." };
            A.CallTo(() => servirJunto.Validar(A<string>._, A<List<ProductoBonificadoConCantidadRequest>>._, A<List<ProductoBonificadoConCantidadRequest>>._,
                    A<string>._, A<string>._, A<string>._, A<string>._, A<bool?>._, A<List<LineaPortesServirJuntoDTO>>._, A<int?>._, A<byte?>._))
                .Returns(Task.FromResult(denegada));
            IServicioDialogos dialogService = A.Fake<IServicioDialogos>();
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>(), dialogService);
            vm.ServicioServirJunto = servirJunto;
            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosServicio.JUNTO_CON_LO_PENDIENTE));

            vm.pedido.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO; // elección del usuario
            await Task.Yield();

            Assert.AreEqual(ModosServicio.JUNTO_CON_LO_PENDIENTE, vm.pedido.ModoServicio);
            Assert.IsTrue(vm.pedido.servirJunto);
            A.CallTo(() => dialogService.ShowError(A<string>._)).MustHaveHappened();
        }
    }
}
