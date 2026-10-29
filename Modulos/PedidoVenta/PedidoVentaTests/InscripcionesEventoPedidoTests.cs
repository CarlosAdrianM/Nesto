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
    /// Nesto#567 (NestoAPI#732): si el PUT del pedido le quita los productos que justifican una inscripción gratuita por
    /// compra, la API contesta 409 PEDIDO_JUSTIFICA_INSCRIPCIONES_EVENTO con la pregunta. El detalle la hace: Sí → repite
    /// con ConfirmarAnularInscripcionesEventos = True; No → no graba y se vuelve a editar, sin más mensajes.
    /// </summary>
    [TestClass]
    public class InscripcionesEventoPedidoTests
    {
        private const string PREGUNTA = "El pedido 930001 justifica la inscripción gratuita por compra de: 15191/0 (LUNA) en «Formación por compra Lifting Coreano» del 25/11/2026. Si quita del pedido 930001 esa línea, se anulará la inscripción y se avisará a la tienda del evento. ¿Quiere seguir?";

        private const string RESPUESTA_409 = "{\"error\":{\"code\":\"PEDIDO_JUSTIFICA_INSCRIPCIONES_EVENTO\",\"message\":\"" + PREGUNTA + "\",\"details\":{\"empresa\":\"1\",\"pedido\":930001,"
            + "\"inscripciones\":[{\"Id\":7,\"EventoId\":4,\"Evento\":\"Formación por compra Lifting Coreano\",\"FechaEvento\":\"2026-11-25T00:00:00\",\"Almacen\":\"ALC\",\"Empresa\":\"1\",\"Cliente\":\"15191\",\"Contacto\":\"0\",\"Nombre\":\"LUNA\"}]}}}";

        private static PedidoVentaDTO Pedido() => new PedidoVentaDTO
        {
            empresa = "1",
            numero = 930001,
            cliente = "15191",
            contacto = "0",
            Lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { id = 1, Producto = "38093", Cantidad = 2, almacen = "ALG", tipoLinea = 1, fechaEntrega = DateTime.Today }
            }
        };

        private static IServicioDialogos DialogoQueResponde(bool acepta)
        {
            IServicioDialogos dialogService = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogService.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(acepta));
            return dialogService;
        }

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IServicioDialogos dialogService)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogService, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.pedido = new PedidoVentaWrapper(Pedido());
            vm.MarcarComoGuardado();
            return vm;
        }

        private static List<bool?> ModificarQueAvisaSinConfirmar(IPedidoVentaService servicio, List<AvisoPedidoModel> avisos = null)
        {
            var flags = new List<bool?>();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ReturnsLazily((PedidoVentaDTO p) =>
            {
                flags.Add(p.ConfirmarAnularInscripcionesEventos);
                if (p.ConfirmarAnularInscripcionesEventos != true)
                {
                    throw PedidoVentaService.InterpretarRespuestaError(RESPUESTA_409);
                }
                return Task.FromResult(avisos ?? new List<AvisoPedidoModel>());
            });
            return flags;
        }

        [TestMethod]
        public void InterpretarRespuestaError_ReconoceElAvisoConLaPreguntaDeLaApi()
        {
            Exception ex = PedidoVentaService.InterpretarRespuestaError(RESPUESTA_409);

            Assert.IsInstanceOfType(ex, typeof(InscripcionesEventoAnularException));
            Assert.AreEqual(PREGUNTA, ex.Message, "La pregunta ya nombra la inscripción: no se repite");
        }

        [TestMethod]
        public void DesdeRespuesta_SinMensaje_ConstruyeLaPreguntaConLasInscripciones()
        {
            JObject json = JObject.Parse(RESPUESTA_409);
            json["error"]["message"] = null;

            InscripcionesEventoAnularException ex = InscripcionesEventoAnularException.DesdeRespuesta(json, "x");

            StringAssert.Contains(ex.Message, "¿Quiere seguir y anularlas?");
            StringAssert.Contains(ex.Message, "- 15191/0 (LUNA) en «Formación por compra Lifting Coreano» del 25/11/2026");
        }

        [TestMethod]
        public void DesdeRespuesta_OtroCodigo_Nothing()
        {
            Assert.IsNull(InscripcionesEventoAnularException.DesdeRespuesta(JObject.Parse("{\"error\":{\"code\":\"FECHA_ENTREGA_LEJANA\",\"message\":\"x\"}}"), "x"));
        }

        [TestMethod]
        public async Task Detalle_Modificar_SiDiceQueSi_ReenviaConfirmandoYAvisaDeLasAnuladas()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            List<bool?> flags = ModificarQueAvisaSinConfirmar(servicio, new List<AvisoPedidoModel>
            {
                new AvisoPedidoModel { Tipo = AvisoPedidoModel.TIPO_INSCRIPCIONES_EVENTO_ANULADAS, Mensaje = "Se ha anulado la inscripción de 15191/0." }
            });
            IServicioDialogos dialogService = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(servicio, dialogService);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogService.ShowConfirmationAsync(InscripcionesEventoAnularException.TITULO, PREGUNTA)).MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new List<bool?> { false, true }, flags, "Primero sin confirmar; tras el Sí, confirmando");
            A.CallTo(() => dialogService.ShowError(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => dialogService.ShowNotification("Inscripciones anuladas", "Se ha anulado la inscripción de 15191/0.")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Detalle_Modificar_SiDiceQueNo_NoGrabaNiEnsenaErrores()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            List<bool?> flags = ModificarQueAvisaSinConfirmar(servicio);
            IServicioDialogos dialogService = DialogoQueResponde(false);
            DetallePedidoViewModel vm = Vm(servicio, dialogService);

            await vm.ModificarPedidoAsync();

            CollectionAssert.AreEqual(new List<bool?> { false }, flags, "Tras el No no se reenvía");
            A.CallTo(() => dialogService.ShowError(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => dialogService.ShowNotification("Pedido Modificado", A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task GuardarConAvisos_CadaGuardadoVuelveAPreguntar()
        {
            PedidoVentaDTO pedido = Pedido();
            pedido.ConfirmarAnularInscripcionesEventos = true; // de un guardado anterior
            bool? enviado = null;

            await GuardadoPedidoConAvisos.GuardarPreguntando(pedido, () =>
            {
                enviado = pedido.ConfirmarAnularInscripcionesEventos;
                return Task.FromResult(1);
            }, (t, m) => throw new AssertFailedException("No hay nada que preguntar"));

            Assert.AreEqual(false, enviado);
        }

        [TestMethod]
        public async Task GuardarConAvisos_LosDosAvisos_PreguntaLosDosYConfirmaLosDos()
        {
            PedidoVentaDTO pedido = Pedido();
            var preguntas = new List<string>();
            int llamadas = 0;

            await GuardadoPedidoConAvisos.GuardarPreguntando(pedido, () =>
            {
                llamadas++;
                if (pedido.ConfirmarFechaEntregaLejana != true)
                {
                    throw new FechaEntregaLejanaException("¿Es correcta la fecha?");
                }
                if (pedido.ConfirmarAnularInscripcionesEventos != true)
                {
                    throw new InscripcionesEventoAnularException(PREGUNTA);
                }
                return Task.FromResult(1);
            }, (t, m) => { preguntas.Add(t); return Task.FromResult(true); });

            CollectionAssert.AreEqual(new List<string> { FechaEntregaLejanaException.TITULO, InscripcionesEventoAnularException.TITULO }, preguntas);
            Assert.AreEqual(3, llamadas);
        }
    }
}
