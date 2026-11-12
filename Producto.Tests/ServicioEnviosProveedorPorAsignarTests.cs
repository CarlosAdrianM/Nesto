using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#636 (NestoAPI#857, fase 3): las llamadas a api/Compras/AgenciasEntrada (PorAsignar, Asignar, Descartar y RemitentesPaquete):
    /// URLs, el cuerpo de Asignar, la lectura con los nombres de la API y los errores con su mensaje y su código.
    /// </summary>
    [TestClass]
    public class ServicioEnviosProveedorPorAsignarTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public List<string> Urls { get; } = new();
            public List<string> Metodos { get; } = new();
            public List<string> Cuerpos { get; } = new();

            public HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.RequestUri!.PathAndQuery);
                Metodos.Add(request.Method.Method);
                Cuerpos.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
                return _responder(request);
            }
        }

        private static ServicioEnviosProveedorPorAsignar Crear(HandlerFalso handler)
        {
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() =>
                new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.local/api/") });
            return new ServicioEnviosProveedorPorAsignar(factoria);
        }

        private static HttpResponseMessage Json(HttpStatusCode codigo, string json)
            => new(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        [TestMethod]
        public async Task LeerPorAsignar_PideLosDias_YLeeEnviosCandidatosYPedidosEnElAire()
        {
            const string json = @"{""Envios"":[{""Clave"":101,""PrimerCorreo"":""2026-11-12T08:00:00"",""Codigos"":[""1Z1"",""1Z2""],""Agencia"":""UPS"",
                ""EntregaEstimadaDia"":""2026-11-14T00:00:00"",""FechaRecepcionPropuesta"":""2026-11-14T00:00:00"",""RemitentePaquete"":""mbe store"",
                ""ProveedorPropuesto"":""1234"",""Correos"":[{""Id"":101,""Recibido"":""2026-11-12T08:00:00"",""Asunto"":""Tu paquete"",""Extracto"":""De MBE""}],
                ""Candidatos"":[{""Empresa"":""1"",""Pedido"":912001,""Proveedor"":""1234"",""NombreProveedor"":""EJEMPLO"",""Motivo"":""Dominio"",
                ""FechaEnvio"":""2026-11-10T09:30:00"",""LineasPendientes"":3,""LineasYaRecibidas"":2,""Sugerido"":true}]}],
                ""PedidosEnAire"":[{""Empresa"":""1"",""Pedido"":912001,""Proveedor"":""1234"",""NombreProveedor"":""EJEMPLO"",""LineasPendientes"":3,
                ""LineasYaRecibidas"":2,""EsCandidato"":true,""Envios"":[101]}]}";
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK, json));

            EnviosProveedorPorAsignar leido = await Crear(handler).LeerPorAsignar(7);

            Assert.AreEqual("/api/Compras/AgenciasEntrada/PorAsignar?dias=7", handler.Urls.Single());
            EnvioProveedorPorAsignar envio = leido.Envios.Single();
            Assert.AreEqual(101, envio.Clave);
            Assert.AreEqual("1Z1, 1Z2", envio.CodigosTexto);
            Assert.AreEqual(new DateTime(2026, 11, 14), envio.FechaRecepcionPropuesta);
            Assert.AreEqual("mbe store", envio.RemitentePaquete);
            Assert.AreEqual("Tu paquete", envio.Correos.Single().Asunto);
            PedidoCompraPorAsignar candidato = envio.Candidatos.Single();
            Assert.IsTrue(candidato.Sugerido);
            Assert.AreEqual(912001, candidato.Pedido);
            Assert.AreEqual(2, candidato.LineasYaRecibidas);
            PedidoCompraPorAsignar enAire = leido.PedidosEnAire.Single();
            Assert.IsTrue(enAire.EsCandidato);
            CollectionAssert.AreEqual(new[] { 101 }, enAire.Envios);
        }

        [TestMethod]
        public async Task Asignar_PostConElCuerpoDelContratoYReasignarEnLaUrl()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK,
                @"{""CorreoId"":101,""Empresa"":""1"",""Pedido"":912001,""LineasCambiadas"":3,""FechasAnteriores"":[null,""2026-11-20T00:00:00""],
                ""FechaNueva"":""2026-11-14T00:00:00"",""RemitenteRecordado"":true,""CorreosDelMismoEnvio"":2,""EligioSugerido"":true,""Mensaje"":""Hecho.""}"));
            ServicioEnviosProveedorPorAsignar servicio = Crear(handler);

            RespuestaAsignacionEnvioProveedor respuesta = await servicio.Asignar(101, "1 ", 912001, true, false);
            await servicio.Asignar(101, "1", 912001, false, true);

            Assert.AreEqual("POST", handler.Metodos[0]);
            Assert.AreEqual("/api/Compras/AgenciasEntrada/Correos/101/Asignar?reasignar=false", handler.Urls[0]);
            Assert.AreEqual("/api/Compras/AgenciasEntrada/Correos/101/Asignar?reasignar=true", handler.Urls[1]);
            var cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual("1", (string?)cuerpo["Empresa"]);
            Assert.AreEqual(912001, (int)cuerpo["Pedido"]!);
            Assert.IsTrue((bool)cuerpo["RecordarRemitente"]!);
            Assert.AreEqual("Hecho.", respuesta.Mensaje);
            Assert.AreEqual(2, respuesta.FechasAnteriores.Count);
            Assert.IsNull(respuesta.FechasAnteriores[0]);
            Assert.AreEqual(true, respuesta.EligioSugerido);
            Assert.AreEqual(2, respuesta.CorreosDelMismoEnvio);
        }

        [TestMethod]
        public async Task Asignar_Con409_LanzaConElMensajeDeLaApiYElCodigo()
        {
            // Content(HttpStatusCode.Conflict, "texto") llega como una cadena JSON
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.Conflict, "\"El correo ya está aplicado al pedido 911000.\""));

            var ex = await Assert.ThrowsExceptionAsync<EnvioProveedorPorAsignarException>(() => Crear(handler).Asignar(101, "1", 912001, true, false));

            Assert.AreEqual(409, ex.Codigo);
            Assert.AreEqual("El correo ya está aplicado al pedido 911000.", ex.Message);
        }

        [TestMethod]
        public async Task Con400_ElMessageDeBadRequest()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.BadRequest, "{\"Message\":\"El pedido no está en el aire.\"}"));

            var ex = await Assert.ThrowsExceptionAsync<EnvioProveedorPorAsignarException>(() => Crear(handler).Asignar(101, "1", 1, false, false));

            Assert.AreEqual(400, ex.Codigo);
            Assert.AreEqual("El pedido no está en el aire.", ex.Message);
        }

        [TestMethod]
        public async Task Con403SinCuerpo_ExplicaQuienPuede()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("") });

            var ex = await Assert.ThrowsExceptionAsync<EnvioProveedorPorAsignarException>(() => Crear(handler).LeerPorAsignar(7));

            Assert.AreEqual(403, ex.Codigo);
            Assert.AreEqual(ServicioEnviosProveedorPorAsignar.MENSAJE_SIN_PERMISO, ex.Message);
        }

        [TestMethod]
        public async Task Descartar_PostALaUrlDelCorreo()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK, "{\"CorreoId\":101,\"CorreosDelMismoEnvio\":2,\"Mensaje\":\"Descartado.\"}"));

            RespuestaDescarteEnvioProveedor respuesta = await Crear(handler).Descartar(101);

            Assert.AreEqual("POST", handler.Metodos.Single());
            Assert.AreEqual("/api/Compras/AgenciasEntrada/Correos/101/Descartar", handler.Urls.Single());
            Assert.AreEqual("Descartado.", respuesta.Mensaje);
            Assert.AreEqual(2, respuesta.CorreosDelMismoEnvio);
        }

        [TestMethod]
        public async Task RemitentesPaquete_LeerYBorrar()
        {
            var handler = new HandlerFalso(r => r.Method == HttpMethod.Delete
                ? new HttpResponseMessage(HttpStatusCode.OK)
                : Json(HttpStatusCode.OK, "[{\"Id\":5,\"RemitentePaquete\":\"mbe store\",\"Empresa\":\"1\",\"Proveedor\":\"1234\",\"Usuario\":\"compras1\",\"FechaModificacion\":\"2026-11-12T10:00:00\"}]"));
            ServicioEnviosProveedorPorAsignar servicio = Crear(handler);

            List<RemitentePaqueteRecordado> remitentes = await servicio.LeerRemitentesPaquete();
            await servicio.BorrarRemitentePaquete(5);

            Assert.AreEqual("/api/Compras/AgenciasEntrada/RemitentesPaquete", handler.Urls[0]);
            Assert.AreEqual("DELETE", handler.Metodos[1]);
            Assert.AreEqual("/api/Compras/AgenciasEntrada/RemitentesPaquete/5", handler.Urls[1]);
            Assert.AreEqual("mbe store", remitentes.Single().RemitentePaquete);
            Assert.AreEqual("12/11/26 10:00", remitentes.Single().FechaModificacionTexto);
        }

        [TestMethod]
        public async Task BorrarRemitentePaquete_Con404_Lanza()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

            var ex = await Assert.ThrowsExceptionAsync<EnvioProveedorPorAsignarException>(() => Crear(handler).BorrarRemitentePaquete(5));

            Assert.AreEqual(404, ex.Codigo);
        }
    }
}
