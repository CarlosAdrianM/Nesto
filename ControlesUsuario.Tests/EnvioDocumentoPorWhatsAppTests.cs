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
    /// Nesto#570 (NestoAPI#737): «Enviar por WhatsApp…»: pide el enlace a la API y abre WhatsApp Web con el móvil del cliente y el
    /// texto de usted (URL-encoded). Sin móvil, avisa; con varios, pregunta. El navegador y la API, falsos.
    /// </summary>
    [TestClass]
    public class EnvioDocumentoPorWhatsAppTests
    {
        private const string TEXTO = "Buenos días, Peluquería Ana:\n\nLe enviamos su pedido 929498, de 1.234,56 €. Puede descargarlo en PDF desde este enlace:\nhttps://api.nuevavision.es/d/abc\n\nUn saludo,\nNueva Visión";

        private IServicioEnlacesDescarga servicio;
        private IServicioDialogos dialogos;
        private List<string> abiertas;
        private EnvioDocumentoPorWhatsApp envio;

        [TestInitialize]
        public void Inicializar()
        {
            servicio = A.Fake<IServicioEnlacesDescarga>();
            dialogos = A.Fake<IServicioDialogos>();
            abiertas = new List<string>();
            envio = new EnvioDocumentoPorWhatsApp(servicio, dialogos, url => abiertas.Add(url));
        }

        private void LaApiDevuelve(params string[] moviles)
            => A.CallTo(() => servicio.CrearEnlace(A<string>._, A<string>._, A<string>._)).Returns(Task.FromResult(new EnlaceDescargaDocumento
            {
                Url = "https://api.nuevavision.es/d/abc",
                Texto = TEXTO,
                Moviles = new List<string>(moviles)
            }));

        [TestMethod]
        public async Task EnviarPedido_UnMovil_AbreWhatsAppWebConElMovilYElTextoCodificado()
        {
            LaApiDevuelve("34600112233");

            bool abierto = await envio.EnviarPedido("1  ", 929498);

            Assert.IsTrue(abierto);
            A.CallTo(() => servicio.CrearEnlace("Pedido", "1", "929498")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, abiertas.Count);
            Assert.AreEqual("https://web.whatsapp.com/send?phone=34600112233&text=" + Uri.EscapeDataString(TEXTO), abiertas[0]);
            StringAssert.Contains(abiertas[0], "Buenos%20d%C3%ADas", "tildes y espacios codificados");
            StringAssert.Contains(abiertas[0], "https%3A%2F%2Fapi.nuevavision.es%2Fd%2Fabc", "el enlace va dentro del texto, codificado");
            Assert.IsFalse(abiertas[0].Contains("\n"));
        }

        [TestMethod]
        public async Task EnviarFactura_PideElEnlaceDeLaFactura()
        {
            LaApiDevuelve("34600112233");

            _ = await envio.EnviarFactura("1", " NV2616199 ");

            A.CallTo(() => servicio.CrearEnlace("Factura", "1", "NV2616199")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SinMovil_AvisaYSiDiceQueNo_NoAbreNada()
        {
            LaApiDevuelve();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(false);

            bool abierto = await envio.EnviarPedido("1", 929498);

            Assert.IsFalse(abierto);
            Assert.AreEqual(0, abiertas.Count);
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>.That.Contains("no tiene ningún móvil"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SinMovil_SiQuiere_AbreWhatsAppSinNumeroParaElegirElContacto()
        {
            LaApiDevuelve();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);

            _ = await envio.EnviarPedido("1", 929498);

            Assert.AreEqual("https://web.whatsapp.com/send?text=" + Uri.EscapeDataString(TEXTO), abiertas[0]);
        }

        [TestMethod]
        public async Task VariosMoviles_PreguntaConElPrimeroPropuestoYUsaElElegido()
        {
            LaApiDevuelve("34600112233", "34655443322");
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._, "34600112233")).Returns("655 44 33 22");

            _ = await envio.EnviarPedido("1", 929498);

            A.CallTo(() => dialogos.GetText(A<string>._, A<string>.That.Contains("+34655443322"), "34600112233")).MustHaveHappenedOnceExactly();
            StringAssert.StartsWith(abiertas[0], "https://web.whatsapp.com/send?phone=34655443322&text=");
        }

        [TestMethod]
        public async Task VariosMoviles_Cancela_NoAbreNada()
        {
            LaApiDevuelve("34600112233", "34655443322");
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._, A<string>._)).Returns(null);

            bool abierto = await envio.EnviarPedido("1", 929498);

            Assert.IsFalse(abierto);
            Assert.AreEqual(0, abiertas.Count);
        }

        [TestMethod]
        public async Task LaApiLoRechaza_DiceElMotivoYNoAbreNada()
        {
            A.CallTo(() => servicio.CrearEnlace(A<string>._, A<string>._, A<string>._))
                .ThrowsAsync(new InvalidOperationException("No existe el pedido 929498."));

            bool abierto = await envio.EnviarPedido("1", 929498);

            Assert.IsFalse(abierto);
            Assert.AreEqual(0, abiertas.Count);
            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("No existe el pedido 929498."))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Internacional_LoQueEscribeElUsuario()
        {
            Assert.AreEqual("34600112233", EnvioDocumentoPorWhatsApp.Internacional("600 11 22 33"));
            Assert.AreEqual("34600112233", EnvioDocumentoPorWhatsApp.Internacional("+34 600-112-233"));
            Assert.AreEqual("351912345678", EnvioDocumentoPorWhatsApp.Internacional("00351 912 345 678"));
        }

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

        private static (ServicioEnlacesDescarga, HandlerFalso) ServicioHttp()
        {
            var handler = new HandlerFalso();
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() =>
                new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.local/api/") });
            return (new ServicioEnlacesDescarga(factoria), handler);
        }

        [TestMethod]
        public async Task Servicio_CrearEnlace_PostAlEndpointYLeeLaRespuesta()
        {
            var (servicioHttp, handler) = ServicioHttp();
            handler.Respuesta = "{\"Url\":\"https://api.nuevavision.es/d/abc\",\"Caduca\":\"2026-11-29T10:15:00\",\"Moviles\":[\"34600112233\"],\"Texto\":\"Hola\"}";

            EnlaceDescargaDocumento enlace = await servicioHttp.CrearEnlace("Pedido", "1  ", "929498");

            Assert.AreEqual(HttpMethod.Post, handler.Peticion.Method);
            Assert.AreEqual("http://api.local/api/Documentos/EnlaceDescarga", handler.Peticion.RequestUri.ToString());
            JObject cuerpo = JObject.Parse(handler.Cuerpo);
            Assert.AreEqual("Pedido", cuerpo["Tipo"].ToString());
            Assert.AreEqual("1", cuerpo["Empresa"].ToString());
            Assert.AreEqual("929498", cuerpo["Numero"].ToString());
            Assert.AreEqual("https://api.nuevavision.es/d/abc", enlace.Url);
            Assert.AreEqual("34600112233", enlace.Moviles[0]);
            Assert.AreEqual(new DateTime(2026, 11, 29, 10, 15, 0), enlace.Caduca);
        }

        [TestMethod]
        public async Task Servicio_ErrorDeLaApi_LanzaConElMotivo()
        {
            var (servicioHttp, handler) = ServicioHttp();
            handler.Estado = HttpStatusCode.Forbidden;
            handler.Respuesta = "\"Solo los empleados pueden crear enlaces de descarga.\"";

            var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => servicioHttp.CrearEnlace("Pedido", "1", "929498"));

            Assert.AreEqual("Solo los empleados pueden crear enlaces de descarga.", ex.Message);
        }

        #endregion
    }
}
