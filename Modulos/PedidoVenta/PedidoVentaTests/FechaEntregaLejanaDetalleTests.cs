using ControlesUsuario.Dialogs;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// NestoAPI#712 (pedido 929057: 27/10/2027 en vez de 2026): si alguna línea va a más de 60 días, la API contesta
    /// FECHA_ENTREGA_LEJANA con la pregunta. El detalle la hace: Sí → repite el guardado con ConfirmarFechaEntregaLejana;
    /// No → no graba y se vuelve a editar, sin más mensajes.
    /// </summary>
    [TestClass]
    public class FechaEntregaLejanaDetalleTests
    {
        private const string PREGUNTA = "La fecha de entrega de las líneas es el miércoles 27/10/2027 (dentro de 365 días). ¿Es correcta?";

        private static PedidoVentaDTO Pedido(int numero) => new PedidoVentaDTO
        {
            empresa = "1",
            numero = numero,
            cliente = "6316",
            contacto = "0",
            Lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { id = 1, Producto = "38093", Cantidad = 2, almacen = "ALG", tipoLinea = 1, fechaEntrega = new DateTime(2027, 10, 27) }
            }
        };

        private static IServicioDialogos DialogoQueResponde(bool acepta)
        {
            IServicioDialogos dialogService = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogService.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(acepta));
            return dialogService;
        }

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IServicioDialogos dialogService, int numero)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogService, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.pedido = new PedidoVentaWrapper(Pedido(numero));
            vm.MarcarComoGuardado();
            return vm;
        }

        /// <summary>La API avisa mientras el pedido no venga confirmado (como hace NestoAPI con el flag a false).</summary>
        private static List<bool?> ModificarQueAvisaSinConfirmar(IPedidoVentaService servicio)
        {
            var flags = new List<bool?>();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ReturnsLazily((PedidoVentaDTO p) =>
            {
                flags.Add(p.ConfirmarFechaEntregaLejana);
                if (p.ConfirmarFechaEntregaLejana != true)
                {
                    throw new FechaEntregaLejanaException(PREGUNTA);
                }
                return Task.FromResult(new List<AvisoPedidoModel>());
            });
            return flags;
        }

        // ---------- Lo que llega de la API ----------

        [TestMethod]
        public void InterpretarRespuestaError_ReconoceFechaEntregaLejana_ConElMensajeLimpio()
        {
            Exception ex = PedidoVentaService.InterpretarRespuestaError(
                "{\"error\":{\"code\":\"FECHA_ENTREGA_LEJANA\",\"message\":\"" + PREGUNTA + "\",\"details\":{\"empresa\":\"1\",\"pedido\":929057,\"fechaEntrega\":\"2027-10-27\",\"dias\":365,\"lineas\":3}}}");

            Assert.IsInstanceOfType(ex, typeof(FechaEntregaLejanaException));
            Assert.AreEqual(PREGUNTA, ex.Message, "Sin el «[FECHA_ENTREGA_LEJANA]» delante");
        }

        [TestMethod]
        public void DesdeRespuesta_OtroCodigo_Nothing()
        {
            Assert.IsNull(FechaEntregaLejanaException.DesdeRespuesta(JObject.Parse("{\"error\":{\"code\":\"MODO_CON_PICKING\",\"message\":\"x\"}}"), "x"));
        }

        // ---------- El núcleo común ----------

        [TestMethod]
        public async Task GuardarPreguntando_SinAviso_GrabaUnaVezConElAvisoActivado()
        {
            var pedido = Pedido(929057);
            bool? enviado = null;
            int llamadas = 0;

            int resultado = await FechaEntregaLejanaException.GuardarPreguntando(pedido, () =>
            {
                llamadas++;
                enviado = pedido.ConfirmarFechaEntregaLejana;
                return Task.FromResult(7);
            }, (t, m) => throw new AssertFailedException("No hay nada que preguntar"));

            Assert.AreEqual(7, resultado);
            Assert.AreEqual(1, llamadas);
            Assert.AreEqual(false, enviado, "Nesto siempre manda el flag a False: así la API sabe que puede preguntar");
        }

        // ---------- El detalle de pedido: modificar ----------

        [TestMethod]
        public async Task Detalle_Modificar_SiEsCorrecta_PreguntaYReenviaConfirmando()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            List<bool?> flags = ModificarQueAvisaSinConfirmar(servicio);
            IServicioDialogos dialogService = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(servicio, dialogService, 929057);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogService.ShowConfirmationAsync(FechaEntregaLejanaException.TITULO, PREGUNTA)).MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new List<bool?> { false, true }, flags, "Primero sin confirmar; tras el Sí, confirmando");
            A.CallTo(() => dialogService.ShowError(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => dialogService.ShowNotification("Pedido Modificado", A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Detalle_Modificar_SiNoEsCorrecta_NoGrabaNiEnsenaErrores()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            List<bool?> flags = ModificarQueAvisaSinConfirmar(servicio);
            IServicioDialogos dialogService = DialogoQueResponde(false);
            DetallePedidoViewModel vm = Vm(servicio, dialogService, 929057);

            await vm.ModificarPedidoAsync();

            CollectionAssert.AreEqual(new List<bool?> { false }, flags, "Tras el No no se reenvía");
            A.CallTo(() => dialogService.ShowError(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => dialogService.ShowNotification("Pedido Modificado", A<string>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.estaBloqueado, "Se vuelve a editar");
        }

        [TestMethod]
        public async Task Detalle_Modificar_SinAviso_NoPregunta()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).Returns(Task.FromResult(new List<AvisoPedidoModel>()));
            IServicioDialogos dialogService = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(servicio, dialogService, 929057);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogService.ShowConfirmationAsync(FechaEntregaLejanaException.TITULO, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Detalle_GuardarOtraVez_LaConfirmacionAnteriorNoVale()
        {
            // Tras confirmar un guardado, el siguiente vuelve a ir sin confirmar: si el usuario cambia la fecha a otra lejana,
            // se vuelve a preguntar (si no la cambia, la API no avisa: no es una línea nueva ni con la fecha cambiada).
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            List<bool?> flags = ModificarQueAvisaSinConfirmar(servicio);
            DetallePedidoViewModel vm = Vm(servicio, DialogoQueResponde(true), 929057);

            await vm.ModificarPedidoAsync();
            await vm.ModificarPedidoAsync();

            CollectionAssert.AreEqual(new List<bool?> { false, true, false, true }, flags);
        }

        // ---------- El detalle de pedido: crear ----------

        [TestMethod]
        public async Task Detalle_Crear_SiEsCorrecta_PreguntaYReenviaConfirmando()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            var flags = new List<bool?>();
            A.CallTo(() => servicio.CrearPedido(A<PedidoVentaDTO>._)).ReturnsLazily((PedidoVentaDTO p) =>
            {
                flags.Add(p.ConfirmarFechaEntregaLejana);
                if (p.ConfirmarFechaEntregaLejana != true)
                {
                    throw new FechaEntregaLejanaException(PREGUNTA);
                }
                return Task.FromResult(929057);
            });
            IServicioDialogos dialogService = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(servicio, dialogService, 0);

            await vm.ModificarPedidoAsync();

            CollectionAssert.AreEqual(new List<bool?> { false, true }, flags);
            Assert.AreEqual(929057, vm.pedido.numero);
        }

        [TestMethod]
        public async Task Detalle_Crear_SiNoEsCorrecta_NoCreaElPedido()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.CrearPedido(A<PedidoVentaDTO>._)).ThrowsAsync(new FechaEntregaLejanaException(PREGUNTA));
            IServicioDialogos dialogService = DialogoQueResponde(false);
            DetallePedidoViewModel vm = Vm(servicio, dialogService, 0);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.CrearPedido(A<PedidoVentaDTO>._)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, vm.pedido.numero, "Sigue sin crear, para corregir la fecha");
            A.CallTo(() => dialogService.ShowError(A<string>._)).MustNotHaveHappened();
        }
    }
}
