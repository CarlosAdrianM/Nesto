using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PedidoVenta.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#640 (NestoAPI#864): las cuatro llamadas de api/PedidosVenta/{empresa}/{pedido}/PagoAPlazos y los errores de la API
    /// ({ "Message": "…" }) tal cual.
    /// </summary>
    [TestClass]
    public class ServicioPagoAPlazosTests
    {
        private class RespuestaFija : HttpMessageHandler
        {
            private readonly HttpStatusCode _codigo;
            private readonly string _cuerpo;
            public HttpMethod Metodo { get; private set; }
            public string Url { get; private set; }
            public string Cuerpo { get; private set; }

            public RespuestaFija(HttpStatusCode codigo, string cuerpo)
            {
                _codigo = codigo;
                _cuerpo = cuerpo;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Metodo = request.Method;
                Url = request.RequestUri.ToString();
                Cuerpo = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(_codigo) { Content = new StringContent(_cuerpo ?? string.Empty, Encoding.UTF8, "application/json") };
            }
        }

        private static ServicioPagoAPlazos Servicio(RespuestaFija handler)
        {
            IClienteApiFactory factory = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factory.Crear()).ReturnsLazily(() => new HttpClient(handler, false) { BaseAddress = new Uri("https://api.prueba.test/api/") });
            return new ServicioPagoAPlazos(factory);
        }

        [TestMethod]
        public async Task LeerDisponible_GetDeDisponible_YLeeLaRespuesta()
        {
            var handler = new RespuestaFija(HttpStatusCode.OK,
                "{\"Disponible\":true,\"Motivo\":null,\"Proveedor\":\"Prueba\",\"NombreProveedor\":\"Financiera Prueba\",\"Importe\":250.5," +
                "\"ImporteMinimo\":50,\"ImporteMaximo\":2500,\"DiasPlazoCaptura\":15,\"ClienteTieneApp\":true,\"Canal\":\"App\"," +
                "\"Aviso\":\"El pedido va contra reembolso.\",\"Pruebas\":true}");

            DisponibilidadPagoAPlazosModel disponibilidad = await Servicio(handler).LeerDisponible("1  ", 930500);

            Assert.AreEqual(HttpMethod.Get, handler.Metodo);
            Assert.AreEqual("https://api.prueba.test/api/PedidosVenta/1/930500/PagoAPlazos/Disponible", handler.Url);
            Assert.IsTrue(disponibilidad.Disponible);
            Assert.AreEqual("Financiera Prueba", disponibilidad.NombreProveedor);
            Assert.AreEqual(250.5m, disponibilidad.Importe);
            Assert.AreEqual(15, disponibilidad.DiasPlazoCaptura);
            Assert.IsTrue(disponibilidad.ClienteTieneApp);
            Assert.AreEqual("App", disponibilidad.Canal);
            Assert.AreEqual("El pedido va contra reembolso.", disponibilidad.Aviso);
            Assert.IsTrue(disponibilidad.Pruebas);
        }

        [TestMethod]
        public async Task LeerDisponible_SinEmpresa_LaPorDefecto()
        {
            var handler = new RespuestaFija(HttpStatusCode.OK, "{\"Disponible\":false}");

            await Servicio(handler).LeerDisponible(null, 930500);

            Assert.AreEqual("https://api.prueba.test/api/PedidosVenta/1/930500/PagoAPlazos/Disponible", handler.Url);
        }

        [TestMethod]
        public async Task Solicitar_PostConCanalYCorreo_YLeeElEnlaceYLosDispositivos()
        {
            var handler = new RespuestaFija(HttpStatusCode.OK,
                "{\"HaySolicitud\":true,\"IdSolicitud\":7,\"Estado\":\"Pendiente\",\"Canal\":\"Enlace\",\"NombreProveedor\":\"Financiera Prueba\"," +
                "\"UrlPago\":\"https://pago.prueba.test/PAP-7\",\"DispositivosAvisados\":0,\"Aviso\":\"El cliente no tiene la app.\"}");

            SolicitudPagoAPlazosModel solicitud = await Servicio(handler).Solicitar("1", 930500, "Enlace", " cliente@prueba.test ");

            Assert.AreEqual(HttpMethod.Post, handler.Metodo);
            Assert.AreEqual("https://api.prueba.test/api/PedidosVenta/1/930500/PagoAPlazos", handler.Url);
            Assert.AreEqual("{\"Canal\":\"Enlace\",\"Correo\":\"cliente@prueba.test\"}", handler.Cuerpo);
            Assert.AreEqual(7, solicitud.IdSolicitud);
            Assert.AreEqual("Pendiente", solicitud.Estado);
            Assert.AreEqual("https://pago.prueba.test/PAP-7", solicitud.UrlPago);
            Assert.AreEqual("El cliente no tiene la app.", solicitud.Aviso);
        }

        [TestMethod]
        public async Task Solicitar_SinCanalNiCorreo_ViajanANull()
        {
            var handler = new RespuestaFija(HttpStatusCode.OK, "{\"HaySolicitud\":true,\"Estado\":\"Pendiente\",\"DispositivosAvisados\":2}");

            SolicitudPagoAPlazosModel solicitud = await Servicio(handler).Solicitar("1", 930500, null, "  ");

            Assert.AreEqual("{\"Canal\":null,\"Correo\":null}", handler.Cuerpo);
            Assert.AreEqual(2, solicitud.DispositivosAvisados);
        }

        [TestMethod]
        public async Task LeerEstado_GetDeEstado_YLeeLasFechas()
        {
            var handler = new RespuestaFija(HttpStatusCode.OK,
                "{\"Empresa\":\"1\",\"Pedido\":930500,\"HaySolicitud\":true,\"Estado\":\"Aprobado\",\"NombreProveedor\":\"Financiera Prueba\"," +
                "\"Importe\":250.5,\"FechaLimiteCaptura\":\"2026-11-20T00:00:00\",\"DiasParaCapturar\":7}");

            EstadoPagoAPlazosModel estado = await Servicio(handler).LeerEstado("1", 930500);

            Assert.AreEqual(HttpMethod.Get, handler.Metodo);
            Assert.AreEqual("https://api.prueba.test/api/PedidosVenta/1/930500/PagoAPlazos/Estado", handler.Url);
            Assert.IsTrue(estado.HaySolicitud);
            Assert.AreEqual("Aprobado", estado.Estado);
            Assert.AreEqual(new DateTime(2026, 11, 20), estado.FechaLimiteCaptura);
            Assert.AreEqual(7, estado.DiasParaCapturar);
        }

        [TestMethod]
        public async Task Cancelar_DeleteConElMotivoEnLaConsulta()
        {
            var handler = new RespuestaFija(HttpStatusCode.OK, "{\"HaySolicitud\":true,\"Estado\":\"Cancelado\"}");

            EstadoPagoAPlazosModel estado = await Servicio(handler).Cancelar("1", 930500, " El cliente prefiere tarjeta & transferencia ");

            Assert.AreEqual(HttpMethod.Delete, handler.Metodo);
            Assert.AreEqual("https://api.prueba.test/api/PedidosVenta/1/930500/PagoAPlazos?motivo=El%20cliente%20prefiere%20tarjeta%20%26%20transferencia",
                new Uri(handler.Url).AbsoluteUri);
            Assert.AreEqual("Cancelado", estado.Estado);
        }

        [TestMethod]
        public async Task Cancelar_SinMotivo_SinConsulta()
        {
            var handler = new RespuestaFija(HttpStatusCode.OK, "{\"HaySolicitud\":true,\"Estado\":\"Cancelado\"}");

            await Servicio(handler).Cancelar("1", 930500, null);

            Assert.AreEqual("https://api.prueba.test/api/PedidosVenta/1/930500/PagoAPlazos", handler.Url);
        }

        [TestMethod]
        public async Task Error_ConMessage_SeLanzaElMensajeDeLaApiTalCual()
        {
            var handler = new RespuestaFija(HttpStatusCode.Conflict, "{\"Message\":\"El pedido ya está pagado.\"}");

            Exception ex = await Assert.ThrowsExceptionAsync<Exception>(() => Servicio(handler).Solicitar("1", 930500, "App", null));

            Assert.AreEqual("El pedido ya está pagado.", ex.Message);
        }

        [TestMethod]
        public async Task Error_SinCuerpo_DiceElCodigo()
        {
            var handler = new RespuestaFija(HttpStatusCode.ServiceUnavailable, string.Empty);

            Exception ex = await Assert.ThrowsExceptionAsync<Exception>(() => Servicio(handler).LeerEstado("1", 930500));

            Assert.AreEqual("Error 503 de la API.", ex.Message);
        }
    }
}
