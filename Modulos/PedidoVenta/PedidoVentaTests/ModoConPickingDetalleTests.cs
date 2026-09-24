using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Newtonsoft.Json.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Unity;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#489 / NestoAPI#533: con picking (o albarán de hoy) la API no deja cambiar el modo de entrega
    /// (MODO_CON_PICKING). El detalle enseña el motivo, ofrece «Pedir el cambio a almacén» y vuelve a poner el
    /// modo guardado: el pedido no se ha grabado con el modo nuevo (caso 926879).
    /// </summary>
    [TestClass]
    public class ModoConPickingDetalleTests
    {
        private const string MOTIVO = "Este pedido ya está en preparación (tiene picking) y el modo de entrega ya no se puede cambiar desde aquí. " +
            "Si hace falta, se lo podemos pedir a almacén: lo intentarán, pero puede que ya no llegue a tiempo.";
        private const string RESPUESTA_OK = "Se lo hemos pedido a almacén. Si todavía están a tiempo, lo cambiarán ellos.";

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

        private static IDialogService DialogoQueResponde(ButtonResult respuesta)
        {
            IDialogService dialogService = A.Fake<IDialogService>();
            A.CallTo(() => dialogService.ShowDialog("ConfirmationDialog", A<IDialogParameters>._, A<Action<IDialogResult>>._))
                .Invokes(call => call.GetArgument<Action<IDialogResult>>(2)(new DialogResult(respuesta)));
            return dialogService;
        }

        /// <summary>Pedido grabado «Según vaya entrando» que el usuario pasa a «Todo junto» y la API rechaza.</summary>
        private static DetallePedidoViewModel VmConCambioRechazado(IPedidoVentaService servicio, IDialogService dialogService)
        {
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ThrowsAsync(new ModoConPickingException(MOTIVO));
            var vm = new DetallePedidoViewModel(A.Fake<IRegionManager>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogService, A.Fake<IUnityContainer>(), A.Fake<IServicioAutenticacion>());
            vm.pedido = new PedidoVentaWrapper(Pedido(926879, ModosServicio.SEGUN_VAYA_ENTRANDO));
            vm.MarcarComoGuardado();
            vm.pedido.ModoServicio = ModosServicio.TODO_JUNTO; // el cambio del usuario
            return vm;
        }

        private static bool EsDialogo(IDialogParameters p, string contiene) =>
            p != null && p.ContainsKey("message") && p.GetValue<string>("message").Contains(contiene);

        // ---------- Lo que llega de la API ----------

        [TestMethod]
        public void InterpretarRespuestaError_ReconoceModoConPicking_ConElMensajeLimpio()
        {
            Exception ex = PedidoVentaService.InterpretarRespuestaError(
                "{\"error\":{\"code\":\"MODO_CON_PICKING\",\"message\":\"" + MOTIVO + "\",\"details\":{\"empresa\":\"1\",\"pedido\":926879}}}");

            Assert.IsInstanceOfType(ex, typeof(ModoConPickingException));
            Assert.AreEqual(MOTIVO, ex.Message, "Sin el «[MODO_CON_PICKING]» delante");
        }

        [TestMethod]
        public void DesdeRespuesta_OtroCodigo_Nothing()
        {
            Assert.IsNull(ModoConPickingException.DesdeRespuesta(JObject.Parse("{\"error\":{\"code\":\"MODO_SERVICIO_NO_PERMITIDO\",\"message\":\"x\"}}"), "x"));
        }

        // ---------- El detalle de pedido ----------

        [TestMethod]
        public async Task Detalle_ConPicking_EnsenaElMotivoYOfreceLaSolicitud()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IDialogService dialogService = DialogoQueResponde(ButtonResult.Cancel);
            DetallePedidoViewModel vm = VmConCambioRechazado(servicio, dialogService);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogService.ShowDialog("ConfirmationDialog",
                    A<IDialogParameters>.That.Matches(p => EsDialogo(p, "ya está en preparación") && EsDialogo(p, "«Todo junto»")
                        && p.GetValue<string>("title") == SolicitudCambioModoAlmacen.TITULO),
                    A<Action<IDialogResult>>._))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Detalle_ConPicking_SiAcepta_PideElCambioConElModoDeseadoYEnsenaLaRespuesta()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.SolicitarCambioModo(A<string>._, A<int>._, A<byte>._, A<string>._)).Returns(RESPUESTA_OK);
            IDialogService dialogService = DialogoQueResponde(ButtonResult.OK);
            DetallePedidoViewModel vm = VmConCambioRechazado(servicio, dialogService);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.SolicitarCambioModo("1", 926879, ModosServicio.TODO_JUNTO, A<string>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogService.ShowDialog("NotificationDialog",
                    A<IDialogParameters>.That.Matches(p => EsDialogo(p, "Se lo hemos pedido a almacén")), A<Action<IDialogResult>>._))
                .MustHaveHappened();
        }

        [TestMethod]
        public async Task Detalle_ConPicking_SiRechaza_NoLlamaAlEndpoint()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IDialogService dialogService = DialogoQueResponde(ButtonResult.Cancel);
            DetallePedidoViewModel vm = VmConCambioRechazado(servicio, dialogService);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.SolicitarCambioModo(A<string>._, A<int>._, A<byte>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Detalle_ConPicking_VuelveAlModoGuardado_AcepteONo()
        {
            foreach (ButtonResult respuesta in new[] { ButtonResult.OK, ButtonResult.Cancel })
            {
                IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
                A.CallTo(() => servicio.SolicitarCambioModo(A<string>._, A<int>._, A<byte>._, A<string>._)).Returns(RESPUESTA_OK);
                DetallePedidoViewModel vm = VmConCambioRechazado(servicio, DialogoQueResponde(respuesta));

                await vm.ModificarPedidoAsync();

                Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, vm.pedido.ModoServicio, $"El pedido no se ha grabado con el modo nuevo ({respuesta})");
                Assert.IsFalse(vm.pedido.servirJunto);
                Assert.IsFalse(vm.TieneCambiosSinGuardar, "El modo vuelve a estar como el guardado");
            }
        }

        [TestMethod]
        public async Task Detalle_ConPicking_SiAlmacenNoSePuedeAvisar_EnsenaElMensajeDeLaApi()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.SolicitarCambioModo(A<string>._, A<int>._, A<byte>._, A<string>._))
                .ThrowsAsync(new Exception("No se ha podido mandar el correo a almacén. Llámales o escríbeles directamente."));
            IDialogService dialogService = DialogoQueResponde(ButtonResult.OK);
            DetallePedidoViewModel vm = VmConCambioRechazado(servicio, dialogService);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogService.ShowDialog("NotificationDialog",
                    A<IDialogParameters>.That.Matches(p => EsDialogo(p, "Llámales o escríbeles")), A<Action<IDialogResult>>._))
                .MustHaveHappened();
            A.CallTo(() => dialogService.ShowDialog("NotificationDialog",
                    A<IDialogParameters>.That.Matches(p => EsDialogo(p, "Se lo hemos pedido")), A<Action<IDialogResult>>._))
                .MustNotHaveHappened();
        }

        // ---------- El servicio ----------

        private class RespuestaFija : HttpMessageHandler
        {
            private readonly HttpStatusCode _codigo;
            private readonly string _cuerpo;
            public HttpRequestMessage Peticion { get; private set; }
            public string CuerpoPeticion { get; private set; }

            public RespuestaFija(HttpStatusCode codigo, string cuerpo)
            {
                _codigo = codigo;
                _cuerpo = cuerpo;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Peticion = request;
                CuerpoPeticion = request.Content == null ? null : await request.Content.ReadAsStringAsync();
                return new HttpResponseMessage(_codigo) { Content = new StringContent(_cuerpo) };
            }
        }

        private static PedidoVentaService Servicio(RespuestaFija handler)
        {
            IClienteApiFactory factory = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factory.Crear()).ReturnsLazily(() => new HttpClient(handler, false) { BaseAddress = new Uri("https://api.nuevavision.es/api/") });
            IServicioAutenticacion autenticacion = A.Fake<IServicioAutenticacion>();
            A.CallTo(() => autenticacion.ConfigurarAutorizacion(A<HttpClient>._)).Returns(Task.FromResult(true));
            return new PedidoVentaService(A.Fake<IConfiguracion>(), autenticacion, factory);
        }

        [TestMethod]
        public async Task Servicio_SolicitarCambioModo_PostConElModoDeseado_YDevuelveElTextoDeLaApi()
        {
            var handler = new RespuestaFija(HttpStatusCode.OK, "\"" + RESPUESTA_OK + "\"");

            string texto = await Servicio(handler).SolicitarCambioModo("1", 926879, ModosServicio.TODO_JUNTO, null);

            Assert.AreEqual(RESPUESTA_OK, texto);
            Assert.AreEqual(HttpMethod.Post, handler.Peticion.Method);
            StringAssert.EndsWith(handler.Peticion.RequestUri.AbsolutePath, "/api/PedidosVenta/SolicitudCambioModo");
            JObject cuerpo = JObject.Parse(handler.CuerpoPeticion);
            Assert.AreEqual("1", (string)cuerpo["Empresa"]);
            Assert.AreEqual(926879, (int)cuerpo["Pedido"]);
            Assert.AreEqual(ModosServicio.TODO_JUNTO, (byte)cuerpo["ModoDeseado"]);
        }

        [TestMethod]
        public async Task Servicio_SolicitarCambioModo_BadRequest_LanzaConElMensajeDeLaApi()
        {
            var handler = new RespuestaFija(HttpStatusCode.BadRequest, "{\"Message\":\"No se ha podido mandar el correo a almacén. Llámales o escríbeles directamente.\"}");

            Exception ex = await Assert.ThrowsExceptionAsync<Exception>(() => Servicio(handler).SolicitarCambioModo("1", 926879, ModosServicio.TODO_JUNTO, null));

            Assert.AreEqual("No se ha podido mandar el correo a almacén. Llámales o escríbeles directamente.", ex.Message);
        }
    }
}
