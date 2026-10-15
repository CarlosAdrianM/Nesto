using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Tests
{
    /// <summary>Nesto#526 (NestoAPI#634): las diferencias de reposición contra api/Reposiciones/Diferencias.</summary>
    [TestClass]
    public class DiferenciasReposicionServicioTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> Cuerpos = new List<string>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = "[]";

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.Method + " " + request.RequestUri.PathAndQuery);
                Cuerpos.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync().ConfigureAwait(false));
                return new HttpResponseMessage(Codigo) { Content = new StringContent(Respuesta, Encoding.UTF8, "application/json") };
            }
        }

        private sealed class FactoriaFalsa : IClienteApiFactory
        {
            private readonly HttpMessageHandler _handler;
            public FactoriaFalsa(HttpMessageHandler handler) { _handler = handler; }
            public HttpClient Crear() => new HttpClient(_handler, false) { BaseAddress = new Uri("http://api.test/api/") };
        }

        private const string DIFERENCIA = "{\"Id\":7,\"Empresa\":\"1\",\"Traspaso\":80915,\"Origen\":\"ALG\",\"Destino\":\"ALC\",\"Producto\":\"38631\"," +
            "\"Enviado\":1,\"Leido\":0,\"Diferencia\":-1,\"Tipo\":\"Falta\",\"QuePaso\":\"La tienda recibió 0 de 1\",\"Huecos\":\"005/005/004\"," +
            "\"FechaRecepcion\":\"2026-10-08T12:07:00\",\"Ajuste\":\"Hecho\",\"NumOrdenAjuste\":123,\"Estado\":\"Pendiente\"," +
            "\"ResolucionesPosibles\":[\"Confirmada\",\"NoEstaba\",\"ErrorRecepcion\"],\"OrigenConControlUbicaciones\":true}";

        [TestMethod]
        public async Task Leer_PideElEstadoYElAlmacen()
        {
            var handler = new HandlerFalso { Respuesta = "[" + DIFERENCIA + "]" };

            List<DiferenciaReposicion> filas = await new ServicioDiferenciasReposicion(new FactoriaFalsa(handler)).Leer("1  ", "Resuelta", " ALG ");

            Assert.AreEqual("GET /api/Reposiciones/Diferencias?estado=Resuelta&almacen=ALG&empresa=1", handler.Urls[0]);
            Assert.AreEqual(1, filas.Count);
            Assert.AreEqual(-1, filas[0].Diferencia);
            Assert.AreEqual("Falta", filas[0].Tipo);
            CollectionAssert.AreEqual(new[] { "Confirmada", "NoEstaba", "ErrorRecepcion" }, filas[0].ResolucionesPosibles);
            Assert.IsTrue(filas[0].OrigenConControlUbicaciones);
        }

        [TestMethod]
        public async Task Leer_SinAlmacen_NoLoManda()
        {
            var handler = new HandlerFalso();

            await new ServicioDiferenciasReposicion(new FactoriaFalsa(handler)).Leer("1", "Pendiente", null);

            Assert.AreEqual("GET /api/Reposiciones/Diferencias?estado=Pendiente&empresa=1", handler.Urls[0]);
        }

        [TestMethod]
        public async Task ContarPendientes_LeeElContador()
        {
            var handler = new HandlerFalso { Respuesta = "{\"Almacen\":\"ALC\",\"Pendientes\":2}" };

            ContadorDiferenciasReposicion contador = await new ServicioDiferenciasReposicion(new FactoriaFalsa(handler)).ContarPendientes("1", "ALC");

            Assert.AreEqual("GET /api/Reposiciones/Diferencias/Contador?empresa=1&almacen=ALC", handler.Urls[0]);
            Assert.AreEqual(2, contador.Pendientes);
        }

        [TestMethod]
        public async Task Resolver_PostConLaResolucionYAvisaDelCambio()
        {
            var handler = new HandlerFalso { Respuesta = "{\"Diferencia\":" + DIFERENCIA + ",\"Mensaje\":\"Se ha dado de baja 1 ud. en ALG.\"}" };
            var servicio = new ServicioDiferenciasReposicion(new FactoriaFalsa(handler));
            int avisos = 0;
            servicio.DiferenciasCambiadas += (_, _) => avisos++;

            ResultadoResolverDiferenciaReposicion resultado = await servicio.Resolver("1", 7,
                new ResolverDiferenciaReposicion { Resolucion = "NoEstaba", Comentario = "No está en el hueco" });

            Assert.AreEqual("POST /api/Reposiciones/Diferencias/7/Resolver?empresa=1", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual("NoEstaba", (string)cuerpo["Resolucion"]);
            Assert.AreEqual("No está en el hueco", (string)cuerpo["Comentario"]);
            Assert.AreEqual("Se ha dado de baja 1 ud. en ALG.", resultado.Mensaje);
            Assert.AreEqual(7, resultado.Diferencia.Id);
            Assert.AreEqual(1, avisos);
        }

        [TestMethod]
        public async Task Resolver_409DeLaApi_LanzaConSuMensajeYNoAvisa()
        {
            var handler = new HandlerFalso
            {
                Codigo = HttpStatusCode.Conflict,
                Respuesta = "{\"Message\":\"La diferencia de 38631 de la reposición 80915 ya está resuelta.\"}"
            };
            var servicio = new ServicioDiferenciasReposicion(new FactoriaFalsa(handler));
            int avisos = 0;
            servicio.DiferenciasCambiadas += (_, _) => avisos++;

            DiferenciasReposicionException error = await Assert.ThrowsExceptionAsync<DiferenciasReposicionException>(
                () => servicio.Resolver("1", 7, new ResolverDiferenciaReposicion { Resolucion = "Confirmada" }));

            Assert.AreEqual(409, error.Codigo);
            StringAssert.Contains(error.Message, "ya está resuelta");
            Assert.AreEqual(0, avisos);
        }

        [TestMethod]
        public async Task Leer_403DeLaApi_LanzaConSuMensaje()
        {
            var handler = new HandlerFalso
            {
                Codigo = HttpStatusCode.Forbidden,
                Respuesta = "{\"Message\":\"Solo puede ver las diferencias de reposición de su almacén.\"}"
            };

            DiferenciasReposicionException error = await Assert.ThrowsExceptionAsync<DiferenciasReposicionException>(
                () => new ServicioDiferenciasReposicion(new FactoriaFalsa(handler)).Leer("1", "Pendiente", "ALG"));

            Assert.AreEqual(403, error.Codigo);
            StringAssert.Contains(error.Message, "Solo puede ver las diferencias de reposición de su almacén.");
        }
    }
}
