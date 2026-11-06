using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#629 (NestoAPI#722): aplazar la fecha de entrega de un pedido cuyo envío ya tiene la agencia. Tres respuestas de la
    /// API al guardar: 200 con aviso FechaCambiadaEnAgencia (se enseña, como AplazadoConPicking); 403/502 de la agencia que rechaza
    /// la fecha (se enseña el motivo y el pedido se queda sin guardar); 403 PEDIDO_YA_EN_AGENCIA (se enseña el motivo con el
    /// botón «Mandar correo a logística», que llama a AvisarLogisticaAplazamiento con el envío y la fecha).
    /// </summary>
    [TestClass]
    public class AplazarConAgenciaDetalleTests
    {
        private const int NUMERO = 928084;
        private const int ENVIO = 250600;
        private static readonly DateTime FECHA_NUEVA = new DateTime(2026, 11, 9);
        private const string MENSAJE_CAMBIADA = "La fecha de entrega se ha cambiado también en CTT (envío 250655): la entregará el 09/11/2026.";
        private const string MENSAJE_RECHAZA = "No se ha guardado el pedido: no se ha podido cambiar la fecha de entrega en CTT (envío 250655). Día sin reparto.";
        private const string MENSAJE_YA_EN_AGENCIA = "El pedido 928084 ya lo tiene la agencia (envío 250600): no llegamos a tiempo para retrasar la entrega. " +
            "La única opción es que el cliente lo rechace. Si quiere, mandamos un correo a logística para que intenten hablar con la agencia.";
        private const string RESPUESTA_LOGISTICA = "Se ha mandado el correo a logística con copia al vendedor. Intentarán hablar con la agencia, pero no es seguro que lleguen a tiempo.";

        private static string JsonError(string codigo, string mensaje, string details = "{\"empresa\":\"1\",\"pedido\":928084}") =>
            "{\"error\":{\"code\":\"" + codigo + "\",\"message\":\"" + mensaje + "\",\"details\":" + details + "}}";

        private static string DetailsYaEnAgencia(bool puede) =>
            "{\"empresa\":\"1\",\"pedido\":928084,\"usuario\":\"NUEVAVISION\\\\Carlos\",\"envio\":250600,\"fechaEntrega\":\"2026-11-09\",\"puedeAvisarLogistica\":" +
            (puede ? "true" : "false") + "}";

        private static PedidoVentaDTO Pedido() => new PedidoVentaDTO
        {
            empresa = "1",
            numero = NUMERO,
            cliente = "15191",
            contacto = "0",
            Lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { id = 1, Producto = "38093", Cantidad = 2, almacen = "ALG", tipoLinea = 1, fechaEntrega = new DateTime(2026, 11, 6) }
            }
        };

        /// <summary>Pedido grabado al que el usuario le cambia la fecha de entrega de la línea.</summary>
        private static DetallePedidoViewModel VmConFechaCambiada(IPedidoVentaService servicio, IServicioDialogos dialogService)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogService, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.pedido = new PedidoVentaWrapper(Pedido());
            vm.MarcarComoGuardado();
            vm.pedido.Model.Lineas.First().fechaEntrega = FECHA_NUEVA; // el cambio del usuario
            // La foto del guardado compara la cabecera (no las líneas): con el comentario se ve si el pedido sigue sin guardar.
            vm.pedido.Model.comentarios = "Aplazar al lunes";
            return vm;
        }

        private static bool Contiene(string texto, string parte) => texto != null && texto.Contains(parte);

        /// <summary>El diálogo de confirmación con botones contesta con <paramref name="boton"/>.</summary>
        private static void ConfirmacionContesta(IServicioDialogos dialogService, ResultadoBoton boton) =>
            A.CallTo(() => dialogService.ShowDialog(ServicioDialogosBase.ConfirmationDialog, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string nombre, ParametrosDialogo parametros, Action<ResultadoDialogo> callback) => callback?.Invoke(new ResultadoDialogo(boton)));

        private static bool EsLaPreguntaDeLogistica(ParametrosDialogo p) =>
            p != null && p.GetValue<string>(ConfirmationDialogViewModel.PARAMETRO_TEXTO_SI) == AvisoLogisticaAplazamiento.TEXTO_BOTON_CORREO &&
            Contiene(p.GetValue<string>("message"), "ya lo tiene la agencia");

        // ---------- Lo que llega de la API ----------

        [TestMethod]
        public void InterpretarRespuestaError_AgenciaRechaza_ElMensajeLimpio()
        {
            Exception ex = PedidoVentaService.InterpretarRespuestaError(JsonError("AGENCIA_RECHAZA_FECHA_ENTREGA", MENSAJE_RECHAZA));

            Assert.IsInstanceOfType(ex, typeof(FechaEntregaAgenciaRechazadaException));
            Assert.AreEqual(MENSAJE_RECHAZA, ex.Message, "Sin el código delante");
        }

        [TestMethod]
        public void InterpretarRespuestaError_AgenciaNoResponde_TambienEsRechazo()
        {
            Exception ex = PedidoVentaService.InterpretarRespuestaError(JsonError("AGENCIA_NO_RESPONDE", "CTT no responde."));

            Assert.IsInstanceOfType(ex, typeof(FechaEntregaAgenciaRechazadaException));
            Assert.AreEqual("CTT no responde.", ex.Message);
        }

        [TestMethod]
        public void InterpretarRespuestaError_PedidoYaEnAgencia_ConEnvioFechaYBoton()
        {
            var ex = PedidoVentaService.InterpretarRespuestaError(JsonError("PEDIDO_YA_EN_AGENCIA", MENSAJE_YA_EN_AGENCIA, DetailsYaEnAgencia(true))) as PedidoYaEnAgenciaException;

            Assert.IsNotNull(ex);
            Assert.AreEqual(MENSAJE_YA_EN_AGENCIA, ex.Message);
            Assert.AreEqual(ENVIO, ex.Envio);
            Assert.AreEqual(FECHA_NUEVA, ex.FechaEntrega);
            Assert.IsTrue(ex.PuedeAvisarLogistica);
        }

        [TestMethod]
        public void DesdeRespuesta_PedidoYaEnAgencia_SinPuedeAvisar_SinBoton()
        {
            var ex = PedidoYaEnAgenciaException.DesdeRespuesta(JObject.Parse(JsonError("PEDIDO_YA_EN_AGENCIA", "x", "{\"envio\":250600}")), "x");

            Assert.IsNotNull(ex);
            Assert.IsFalse(ex.PuedeAvisarLogistica);
        }

        [TestMethod]
        public void DesdeRespuesta_OtroCodigo_Nothing()
        {
            JObject otro = JObject.Parse(JsonError("MODO_CON_PICKING", "x"));
            Assert.IsNull(PedidoYaEnAgenciaException.DesdeRespuesta(otro, "x"));
            Assert.IsNull(FechaEntregaAgenciaRechazadaException.DesdeRespuesta(otro, "x"));
        }

        // ---------- 1. 200 con FechaCambiadaEnAgencia ----------

        [TestMethod]
        public async Task Detalle_FechaCambiadaEnAgencia_EnsenaElAvisoYQuedaGuardado()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).Returns(new List<AvisoPedidoModel>
            {
                new AvisoPedidoModel
                {
                    Tipo = "FechaCambiadaEnAgencia",
                    Mensaje = MENSAJE_CAMBIADA,
                    Datos = JObject.Parse("{\"Envio\":250655,\"Agencia\":\"CTT\",\"FechaNueva\":\"2026-11-09\"}")
                }
            });
            IServicioDialogos dialogService = A.Fake<IServicioDialogos>();
            DetallePedidoViewModel vm = VmConFechaCambiada(servicio, dialogService);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogService.ShowNotification(DetallePedidoViewModel.TITULO_FECHA_CAMBIADA_EN_AGENCIA, MENSAJE_CAMBIADA)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogService.ShowError(A<string>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.TieneCambiosSinGuardar, "El pedido se ha guardado");
        }

        // ---------- 2. La agencia rechaza la fecha (403) o no responde (502) ----------

        [TestMethod]
        public async Task Detalle_AgenciaRechaza_EnsenaElMotivoYElPedidoSigueSinGuardar()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ThrowsAsync(new FechaEntregaAgenciaRechazadaException(MENSAJE_RECHAZA));
            IServicioDialogos dialogService = A.Fake<IServicioDialogos>();
            DetallePedidoViewModel vm = VmConFechaCambiada(servicio, dialogService);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogService.ShowError(MENSAJE_RECHAZA)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogService.ShowNotification(A<string>._, A<string>.That.Matches(m => Contiene(m, "modificado correctamente")))).MustNotHaveHappened();
            Assert.AreEqual(FECHA_NUEVA, vm.pedido.Model.Lineas.First().fechaEntrega, "Se queda la fecha que ha puesto, para corregirla");
            Assert.IsTrue(vm.TieneCambiosSinGuardar, "El pedido no se ha guardado");
            A.CallTo(() => servicio.AvisarLogisticaAplazamiento(A<string>._, A<int>._, A<DateTime>._, A<int?>._, A<string>._)).MustNotHaveHappened();
        }

        // ---------- 3. PEDIDO_YA_EN_AGENCIA ----------

        private static IPedidoVentaService ServicioYaEnAgencia(bool puedeAvisar)
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._))
                .ThrowsAsync(new PedidoYaEnAgenciaException(MENSAJE_YA_EN_AGENCIA, ENVIO, FECHA_NUEVA, puedeAvisar));
            A.CallTo(() => servicio.AvisarLogisticaAplazamiento(A<string>._, A<int>._, A<DateTime>._, A<int?>._, A<string>._)).Returns(RESPUESTA_LOGISTICA);
            return servicio;
        }

        [TestMethod]
        public async Task Detalle_YaEnAgencia_PulsaElBoton_LlamaAlEndpointConEnvioYFecha_YEnsenaLaRespuesta()
        {
            IPedidoVentaService servicio = ServicioYaEnAgencia(true);
            IServicioDialogos dialogService = A.Fake<IServicioDialogos>();
            ConfirmacionContesta(dialogService, ResultadoBoton.OK);
            A.CallTo(() => dialogService.GetText(A<string>._, A<string>._)).Returns("El cliente no está el lunes");
            DetallePedidoViewModel vm = VmConFechaCambiada(servicio, dialogService);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogService.ShowDialog(ServicioDialogosBase.ConfirmationDialog,
                    A<ParametrosDialogo>.That.Matches(p => EsLaPreguntaDeLogistica(p)), A<Action<ResultadoDialogo>>._))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.AvisarLogisticaAplazamiento("1", NUMERO, FECHA_NUEVA, ENVIO, "El cliente no está el lunes")).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogService.ShowNotification(A<string>._, RESPUESTA_LOGISTICA)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.TieneCambiosSinGuardar, "El pedido no se ha guardado");
        }

        [TestMethod]
        public async Task Detalle_YaEnAgencia_Cierra_NoLlamaAlEndpoint()
        {
            IPedidoVentaService servicio = ServicioYaEnAgencia(true);
            IServicioDialogos dialogService = A.Fake<IServicioDialogos>();
            ConfirmacionContesta(dialogService, ResultadoBoton.Cancel);
            DetallePedidoViewModel vm = VmConFechaCambiada(servicio, dialogService);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.AvisarLogisticaAplazamiento(A<string>._, A<int>._, A<DateTime>._, A<int?>._, A<string>._)).MustNotHaveHappened();
            Assert.IsTrue(vm.TieneCambiosSinGuardar);
        }

        [TestMethod]
        public async Task Detalle_YaEnAgencia_SinPuedeAvisarLogistica_SoloElMensajeSinBoton()
        {
            IPedidoVentaService servicio = ServicioYaEnAgencia(false);
            IServicioDialogos dialogService = A.Fake<IServicioDialogos>();
            DetallePedidoViewModel vm = VmConFechaCambiada(servicio, dialogService);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogService.ShowError(MENSAJE_YA_EN_AGENCIA)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogService.ShowDialog(ServicioDialogosBase.ConfirmationDialog, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustNotHaveHappened();
            A.CallTo(() => servicio.AvisarLogisticaAplazamiento(A<string>._, A<int>._, A<DateTime>._, A<int?>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Detalle_YaEnAgencia_SiElCorreoFalla_EnsenaElError()
        {
            IPedidoVentaService servicio = ServicioYaEnAgencia(true);
            A.CallTo(() => servicio.AvisarLogisticaAplazamiento(A<string>._, A<int>._, A<DateTime>._, A<int?>._, A<string>._))
                .ThrowsAsync(new Exception("No se ha podido mandar el correo."));
            IServicioDialogos dialogService = A.Fake<IServicioDialogos>();
            ConfirmacionContesta(dialogService, ResultadoBoton.OK);
            A.CallTo(() => dialogService.GetText(A<string>._, A<string>._)).Returns(string.Empty);
            DetallePedidoViewModel vm = VmConFechaCambiada(servicio, dialogService);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogService.ShowError(A<string>.That.Matches(m => Contiene(m, "No se ha podido mandar el correo.")))).MustHaveHappenedOnceExactly();
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
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.usuario).Returns("NUEVAVISION\\Carlos");
            return new PedidoVentaService(configuracion, autenticacion, factory);
        }

        [TestMethod]
        public async Task Servicio_AvisarLogisticaAplazamiento_PostConFechaEnvioUsuarioYComentario()
        {
            var handler = new RespuestaFija(HttpStatusCode.OK, "{\"Mensaje\":\"" + RESPUESTA_LOGISTICA + "\",\"Destinatarios\":[\"logistica@nuevavision.es\"]}");

            string texto = await Servicio(handler).AvisarLogisticaAplazamiento("1  ", NUMERO, FECHA_NUEVA, ENVIO, " Urgente ");

            Assert.AreEqual(RESPUESTA_LOGISTICA, texto);
            Assert.AreEqual(HttpMethod.Post, handler.Peticion.Method);
            StringAssert.EndsWith(handler.Peticion.RequestUri.AbsolutePath, "/api/PedidosVenta/1/928084/AvisarLogisticaAplazamiento");
            JObject cuerpo = JObject.Parse(handler.CuerpoPeticion);
            Assert.AreEqual("2026-11-09", cuerpo["Fecha"].ToString());
            Assert.AreEqual(ENVIO, cuerpo["Envio"].ToObject<int>());
            Assert.AreEqual("NUEVAVISION\\Carlos", cuerpo["Usuario"].ToString());
            Assert.AreEqual("Urgente", cuerpo["Comentario"].ToString());
        }

        [TestMethod]
        public async Task Servicio_AvisarLogisticaAplazamiento_Error_LanzaElMensajeDeLaApi()
        {
            var handler = new RespuestaFija(HttpStatusCode.BadGateway, "{\"error\":{\"code\":\"CORREO_NO_ENVIADO\",\"message\":\"No ha salido el correo.\"}}");

            Exception ex = await Assert.ThrowsExceptionAsync<Exception>(() => Servicio(handler).AvisarLogisticaAplazamiento("1", NUMERO, FECHA_NUEVA, null, null));

            StringAssert.Contains(ex.Message, "No ha salido el correo.");
        }

        [TestMethod]
        public async Task Servicio_ModificarPedido_403YaEnAgencia_LlegaSinEnvolver()
        {
            var handler = new RespuestaFija(HttpStatusCode.Forbidden, JsonError("PEDIDO_YA_EN_AGENCIA", MENSAJE_YA_EN_AGENCIA, DetailsYaEnAgencia(true)));

            var ex = await Assert.ThrowsExceptionAsync<PedidoYaEnAgenciaException>(() => Servicio(handler).modificarPedido(Pedido()));

            Assert.AreEqual(ENVIO, ex.Envio);
        }

        [TestMethod]
        public async Task Servicio_ModificarPedido_403AgenciaRechaza_LlegaSinEnvolver()
        {
            var handler = new RespuestaFija(HttpStatusCode.Forbidden, JsonError("AGENCIA_RECHAZA_FECHA_ENTREGA", MENSAJE_RECHAZA));

            var ex = await Assert.ThrowsExceptionAsync<FechaEntregaAgenciaRechazadaException>(() => Servicio(handler).modificarPedido(Pedido()));

            Assert.AreEqual(MENSAJE_RECHAZA, ex.Message, "Sin «Error al modificar el pedido:» delante");
        }
    }
}
