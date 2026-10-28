using ControlesUsuario.Dialogs;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#563 (NestoAPI#724, sugerencia 831): «Enviar por correo…» junto a cada descarga (pedido, mandato SEPA y
    /// factura), con un único diálogo. Con el servicio falso: no sale ningún correo real.
    /// </summary>
    [TestClass]
    public class EnvioDocumentoPorCorreoTests
    {
        private IServicioEnvioCorreoDocumentos servicio;
        private IServicioDialogos dialogos;
        private EnvioDocumentoPorCorreo envio;
        private ParametrosDialogo abiertoCon;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<IServicioEnvioCorreoDocumentos>();
            dialogos = A.Fake<IServicioDialogos>();
            envio = new EnvioDocumentoPorCorreo(servicio, dialogos);
            A.CallTo(() => servicio.LeerCorreosContacto("1", "15191", "0"))
                .Returns(Task.FromResult(new List<string> { "ana@cliente.es", "luis@cliente.es" }));
            A.CallTo(() => servicio.EnviarPedido(A<string>._, A<int>._, A<PeticionEnvioCorreo>._))
                .ReturnsLazily((string e, int n, PeticionEnvioCorreo p) => Task.FromResult(new ResultadoEnvioCorreo { Destinatarios = p.Destinatarios, Adjunto = "Pedido_929498.pdf" }));
        }

        private void ElUsuarioPulsa(ResultadoBoton boton, string destinatarios = null, string asunto = null, string texto = null)
        {
            var parametros = new ParametrosDialogo
            {
                { EnviarPorCorreoDialogViewModel.PARAMETRO_DESTINATARIOS, destinatarios },
                { EnviarPorCorreoDialogViewModel.PARAMETRO_ASUNTO, asunto },
                { EnviarPorCorreoDialogViewModel.PARAMETRO_TEXTO, texto }
            };
            A.CallTo(() => dialogos.ShowDialogAsync(EnviarPorCorreoDialogViewModel.NOMBRE, A<ParametrosDialogo>._))
                .ReturnsLazily((string n, ParametrosDialogo p) =>
                {
                    abiertoCon = p;
                    return Task.FromResult(new ResultadoDialogo(boton, parametros));
                });
        }

        [TestMethod]
        public async Task EnviarPedido_ElDialogoSaleConLasPersonasDeContactoPropuestas()
        {
            ElUsuarioPulsa(ResultadoBoton.Cancel);

            _ = await envio.EnviarPedido("1", 929498, "15191", "0");

            Assert.AreEqual("ana@cliente.es; luis@cliente.es", abiertoCon.GetValue<string>(EnviarPorCorreoDialogViewModel.PARAMETRO_DESTINATARIOS));
            StringAssert.Contains(abiertoCon.GetValue<string>(EnviarPorCorreoDialogViewModel.PARAMETRO_TITULO), "929498");
        }

        [TestMethod]
        public async Task EnviarPedido_DialogoCancelado_NoLlamaALaApi()
        {
            ElUsuarioPulsa(ResultadoBoton.Cancel, "ana@cliente.es");

            bool enviado = await envio.EnviarPedido("1", 929498, "15191", "0");

            Assert.IsFalse(enviado);
            A.CallTo(() => servicio.EnviarPedido(A<string>._, A<int>._, A<PeticionEnvioCorreo>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowNotification(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task EnviarPedido_Aceptado_LlamaConLosDestinatariosEscritosYAvisa()
        {
            ElUsuarioPulsa(ResultadoBoton.OK, "ana@cliente.es; otra@cliente.es, ana@cliente.es", "Su pedido", "Hola");

            bool enviado = await envio.EnviarPedido("1", 929498, "15191", "0");

            Assert.IsTrue(enviado);
            A.CallTo(() => servicio.EnviarPedido("1", 929498, A<PeticionEnvioCorreo>.That.Matches(p =>
                p.Destinatarios.Count == 2 && p.Destinatarios[0] == "ana@cliente.es" && p.Destinatarios[1] == "otra@cliente.es"
                && p.Asunto == "Su pedido" && p.Texto == "Hola"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowNotification(EnvioDocumentoPorCorreo.TITULO_ENVIADO, "Enviado a ana@cliente.es, otra@cliente.es."))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task EnviarPedido_LaApiLoRechaza_DiceElMotivo()
        {
            ElUsuarioPulsa(ResultadoBoton.OK, "ana@cliente.es");
            A.CallTo(() => servicio.EnviarPedido(A<string>._, A<int>._, A<PeticionEnvioCorreo>._))
                .ThrowsAsync(new InvalidOperationException("El correo no se ha podido enviar."));

            bool enviado = await envio.EnviarPedido("1", 929498, "15191", "0");

            Assert.IsFalse(enviado);
            A.CallTo(() => dialogos.ShowError("No se ha enviado el correo: El correo no se ha podido enviar.")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task EnviarPedido_SinPropuestaPorqueFallaLaApi_AbreElDialogoVacio()
        {
            A.CallTo(() => servicio.LeerCorreosContacto(A<string>._, A<string>._, A<string>._)).ThrowsAsync(new HttpRequestException("sin red"));
            ElUsuarioPulsa(ResultadoBoton.Cancel);

            _ = await envio.EnviarPedido("1", 929498, "15191", "0");

            Assert.AreEqual(string.Empty, abiertoCon.GetValue<string>(EnviarPorCorreoDialogViewModel.PARAMETRO_DESTINATARIOS));
        }

        [TestMethod]
        public async Task EnviarMandato_Aceptado_LlamaConLaCuentaYElMotor()
        {
            ElUsuarioPulsa(ResultadoBoton.OK, "ana@cliente.es");

            _ = await envio.EnviarMandato("1", "15191", "0", "2", "QuestPDF");

            A.CallTo(() => servicio.EnviarMandato("1", "15191", "0", "2", "QuestPDF",
                A<PeticionEnvioCorreo>.That.Matches(p => p.Destinatarios.Count == 1 && p.Destinatarios[0] == "ana@cliente.es"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task EnviarFactura_ProponeElCorreoDeFacturasYNoMandaAsuntoNiTexto()
        {
            A.CallTo(() => servicio.LeerCorreosFacturas("1", "NV2616199")).Returns(Task.FromResult(new List<string> { "facturas@cliente.es" }));
            ElUsuarioPulsa(ResultadoBoton.OK, "facturas@cliente.es", "no vale", "tampoco");

            _ = await envio.EnviarFactura("1", "NV2616199", "15191", "0");

            Assert.AreEqual("facturas@cliente.es", abiertoCon.GetValue<string>(EnviarPorCorreoDialogViewModel.PARAMETRO_DESTINATARIOS));
            Assert.IsFalse(abiertoCon.GetValue<bool>(EnviarPorCorreoDialogViewModel.PARAMETRO_ADMITE_ASUNTO_Y_TEXTO));
            A.CallTo(() => servicio.EnviarFactura("1", "NV2616199",
                A<PeticionEnvioCorreo>.That.Matches(p => p.Asunto == null && p.Texto == null && p.Destinatarios[0] == "facturas@cliente.es")))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task EnviarFactura_SinCorreoDeFacturas_ProponeLasPersonasDeContacto()
        {
            A.CallTo(() => servicio.LeerCorreosFacturas(A<string>._, A<string>._)).Returns(Task.FromResult(new List<string>()));
            ElUsuarioPulsa(ResultadoBoton.Cancel);

            _ = await envio.EnviarFactura("1", "NV2616199", "15191", "0");

            Assert.AreEqual("ana@cliente.es; luis@cliente.es", abiertoCon.GetValue<string>(EnviarPorCorreoDialogViewModel.PARAMETRO_DESTINATARIOS));
        }

        #region Diálogo

        [TestMethod]
        public void Dialogo_SinDestinatarios_NoSePuedeEnviar()
        {
            var vm = new EnviarPorCorreoDialogViewModel();
            vm.OnDialogOpened(new ParametrosDialogo { { EnviarPorCorreoDialogViewModel.PARAMETRO_DESTINATARIOS, " ; " } });

            Assert.IsFalse(vm.EnviarCommand.CanExecute(null));

            vm.Destinatarios = "ana@cliente.es";
            Assert.IsTrue(vm.EnviarCommand.CanExecute(null));
        }

        [TestMethod]
        public void Dialogo_Enviar_DevuelveOKConLoEscrito()
        {
            var vm = new EnviarPorCorreoDialogViewModel();
            ResultadoDialogo cerrado = null;
            vm.RequestClose += r => cerrado = r;
            vm.OnDialogOpened(new ParametrosDialogo { { EnviarPorCorreoDialogViewModel.PARAMETRO_DESTINATARIOS, "ana@cliente.es;  luis@cliente.es " } });
            vm.Asunto = "  Su pedido ";
            vm.Texto = "Hola";

            vm.EnviarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, cerrado.Result);
            Assert.AreEqual("ana@cliente.es; luis@cliente.es", cerrado.Parameters.GetValue<string>(EnviarPorCorreoDialogViewModel.PARAMETRO_DESTINATARIOS));
            Assert.AreEqual("Su pedido", cerrado.Parameters.GetValue<string>(EnviarPorCorreoDialogViewModel.PARAMETRO_ASUNTO));
            Assert.AreEqual("Hola", cerrado.Parameters.GetValue<string>(EnviarPorCorreoDialogViewModel.PARAMETRO_TEXTO));
        }

        [TestMethod]
        public void Dialogo_Cancelar_DevuelveCancel()
        {
            var vm = new EnviarPorCorreoDialogViewModel();
            ResultadoDialogo cerrado = null;
            vm.RequestClose += r => cerrado = r;

            vm.CancelarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.Cancel, cerrado.Result);
        }

        #endregion

        #region Servicio (HTTP)

        private sealed class HandlerFalso : HttpMessageHandler
        {
            public HttpRequestMessage Peticion;
            public string Cuerpo;
            public HttpStatusCode Estado = HttpStatusCode.OK;
            public string Respuesta = "{}";

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Peticion = request;
                Cuerpo = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(Estado) { Content = new StringContent(Respuesta, Encoding.UTF8, "application/json") };
            }
        }

        private static (ServicioEnvioCorreoDocumentos, HandlerFalso) ServicioHttp()
        {
            var handler = new HandlerFalso();
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() =>
                new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.local/api/") });
            return (new ServicioEnvioCorreoDocumentos(factoria), handler);
        }

        [TestMethod]
        public async Task Servicio_EnviarPedido_PostAlEndpointConLosDestinatarios()
        {
            var (servicioHttp, handler) = ServicioHttp();
            handler.Respuesta = "{\"Destinatarios\":[\"ana@cliente.es\"],\"Adjunto\":\"Pedido_929498.pdf\"}";

            ResultadoEnvioCorreo resultado = await servicioHttp.EnviarPedido("1  ", 929498,
                new PeticionEnvioCorreo { Destinatarios = new List<string> { "ana@cliente.es" } });

            Assert.AreEqual(HttpMethod.Post, handler.Peticion.Method);
            Assert.AreEqual("http://api.local/api/PedidosVenta/1/929498/EnviarPorCorreo", handler.Peticion.RequestUri.ToString());
            Assert.AreEqual("ana@cliente.es", JObject.Parse(handler.Cuerpo)["Destinatarios"][0].ToString());
            Assert.AreEqual("Pedido_929498.pdf", resultado.Adjunto);
        }

        [TestMethod]
        public async Task Servicio_EnviarMandato_PostConCuentaYMotor()
        {
            var (servicioHttp, handler) = ServicioHttp();

            _ = await servicioHttp.EnviarMandato("1", "15191 ", "0", "2", "QuestPDF", new PeticionEnvioCorreo());

            Assert.AreEqual("http://api.local/api/Clientes/1/15191/0/Mandato/EnviarPorCorreo", handler.Peticion.RequestUri.ToString());
            JObject cuerpo = JObject.Parse(handler.Cuerpo);
            Assert.AreEqual("2", cuerpo["Ccc"].ToString());
            Assert.AreEqual("QuestPDF", cuerpo["Motor"].ToString());
        }

        [TestMethod]
        public async Task Servicio_ErrorDeLaApi_LanzaConElMotivo()
        {
            var (servicioHttp, handler) = ServicioHttp();
            handler.Estado = HttpStatusCode.BadRequest;
            handler.Respuesta = "{\"Message\":\"El cliente no tiene ninguna persona de contacto con correo.\"}";

            var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => servicioHttp.EnviarPedido("1", 929498, new PeticionEnvioCorreo()));

            StringAssert.Contains(ex.Message, "ninguna persona de contacto");
        }

        [TestMethod]
        public void Servicio_MensajeDeError_UnaCadenaJson_SinComillas()
        {
            Assert.AreEqual("Solo los empleados pueden enviar documentos por correo.",
                ServicioEnvioCorreoDocumentos.MensajeDeError("\"Solo los empleados pueden enviar documentos por correo.\""));
        }

        [TestMethod]
        public async Task Servicio_EnviarFactura_UsaElEnvioDeFacturasYSiNoSaleLanza()
        {
            var (servicioHttp, handler) = ServicioHttp();
            handler.Estado = HttpStatusCode.BadGateway;
            handler.Respuesta = "{\"Enviado\":false,\"Mensaje\":\"El correo no se ha podido enviar.\"}";

            var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => servicioHttp.EnviarFactura("1", "NV2616199", new PeticionEnvioCorreo { Destinatarios = new List<string> { "a@b.es" } }));

            Assert.AreEqual("http://api.local/api/Facturas/EnviarPorCorreo", handler.Peticion.RequestUri.ToString());
            Assert.AreEqual("El correo no se ha podido enviar.", ex.Message);
            Assert.AreEqual("NV2616199", JObject.Parse(handler.Cuerpo)["Facturas"][0].ToString());
        }

        #endregion
    }
}
