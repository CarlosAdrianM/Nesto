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
    /// <summary>NestoAPI#577: el calendario de reposiciones contra api/Reposiciones/Calendario.</summary>
    [TestClass]
    public class CalendarioReposicionesServicioTests
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

        private const string CALENDARIO = "[{\"Id\":1,\"Empresa\":\"1\",\"Origen\":\"REI\",\"Destino\":\"ALG\",\"DiaSemana\":1,\"HoraCierre\":\"09:00:00\"," +
            "\"HoraLlegadaHabitual\":\"13:30:00\",\"LaborablesAntelacionCierre\":0,\"Activo\":true,\"Usuario\":\"Carlos\",\"FechaModificacion\":\"2026-10-09T08:00:00\"}]";

        [TestMethod]
        public async Task LeerCalendario_PideElDeLaEmpresaYLeeLasHoras()
        {
            var handler = new HandlerFalso { Respuesta = CALENDARIO };

            List<FilaCalendarioReposicion> filas = await new ServicioCalendarioReposiciones(new FactoriaFalsa(handler)).LeerCalendario("1  ");

            Assert.AreEqual("GET /api/Reposiciones/Calendario?empresa=1", handler.Urls[0]);
            Assert.AreEqual(new TimeSpan(9, 0, 0), filas[0].HoraCierre);
            Assert.AreEqual(new TimeSpan(13, 30, 0), filas[0].HoraLlegadaHabitual);
            Assert.AreEqual("Carlos", filas[0].Usuario);
        }

        [TestMethod]
        public async Task PuedeEditar_LoQueDiceLaApi_YFalseSiFalla()
        {
            var si = new HandlerFalso { Respuesta = "true" };
            var no = new HandlerFalso { Codigo = HttpStatusCode.NotFound, Respuesta = "" };

            Assert.IsTrue(await new ServicioCalendarioReposiciones(new FactoriaFalsa(si)).PuedeEditar());
            Assert.AreEqual("GET /api/Reposiciones/Calendario/PuedeEditar", si.Urls[0]);
            Assert.IsFalse(await new ServicioCalendarioReposiciones(new FactoriaFalsa(no)).PuedeEditar(), "API anterior: solo lectura");
        }

        [TestMethod]
        public async Task Guardar_PutConLasFilasSueltas()
        {
            var handler = new HandlerFalso { Respuesta = CALENDARIO };
            var peticion = new GuardarCalendarioReposiciones
            {
                Empresa = "1",
                Filas = new List<FilaCalendarioReposicion>
                {
                    new FilaCalendarioReposicion { Id = 1, Origen = "REI", Destino = "ALG", DiaSemana = 1, HoraCierre = new TimeSpan(9, 30, 0),
                        HoraLlegadaHabitual = new TimeSpan(13, 30, 0), Activo = false }
                }
            };

            List<FilaCalendarioReposicion> devuelto = await new ServicioCalendarioReposiciones(new FactoriaFalsa(handler)).Guardar(peticion);

            Assert.AreEqual("PUT /api/Reposiciones/Calendario", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.IsNull(cuerpo["Origen"], "Filas sueltas: sin Origen ni Destino arriba (si no, la API desactivaría el resto de la ruta)");
            Assert.AreEqual("09:30:00", (string)cuerpo["Filas"][0]["HoraCierre"]);
            Assert.AreEqual(false, (bool)cuerpo["Filas"][0]["Activo"]);
            Assert.AreEqual(1, devuelto.Count);
        }

        [TestMethod]
        public async Task Guardar_400DeLaApi_LanzaConSuMensaje()
        {
            var handler = new HandlerFalso
            {
                Codigo = HttpStatusCode.BadRequest,
                Respuesta = "{\"error\":{\"message\":\"Ya hay una reposición de REI a ALG que llega el miércoles.\"}}"
            };

            CalendarioReposicionesException error = await Assert.ThrowsExceptionAsync<CalendarioReposicionesException>(
                () => new ServicioCalendarioReposiciones(new FactoriaFalsa(handler)).Guardar(new GuardarCalendarioReposiciones()));

            Assert.AreEqual(400, error.Codigo);
            StringAssert.Contains(error.Message, "Ya hay una reposición de REI a ALG que llega el miércoles.");
        }

        [TestMethod]
        public async Task Guardar_403DeLaApi_LanzaConSuMensaje()
        {
            var handler = new HandlerFalso
            {
                Codigo = HttpStatusCode.Forbidden,
                Respuesta = "{\"Message\":\"El calendario de reposiciones solo lo pueden cambiar las personas autorizadas a rellenar reposiciones a mano.\"}"
            };

            CalendarioReposicionesException error = await Assert.ThrowsExceptionAsync<CalendarioReposicionesException>(
                () => new ServicioCalendarioReposiciones(new FactoriaFalsa(handler)).Guardar(new GuardarCalendarioReposiciones()));

            Assert.AreEqual(403, error.Codigo);
            StringAssert.Contains(error.Message, "personas autorizadas");
        }

        private const string EXCEPCION = "{\"Id\":7,\"Empresa\":\"1\",\"Origen\":\"ALG\",\"Destino\":\"REI\",\"FechaLlegada\":\"2026-11-20T00:00:00\"," +
            "\"Tipo\":\"SinReposicion\",\"HoraCierre\":null,\"LaborablesAntelacionCierre\":null,\"HoraLlegadaHabitual\":null," +
            "\"Motivo\":\"Inventario del sábado 21\",\"Usuario\":\"Carlos\",\"FechaModificacion\":\"2026-11-16T10:00:00\",\"CierraEl\":\"2026-11-19T16:00:00\"}";

        [TestMethod]
        public async Task LeerExcepciones_PideLasDeLaEmpresa()
        {
            var handler = new HandlerFalso { Respuesta = "[" + EXCEPCION + "]" };

            List<ExcepcionCalendarioReposicion> excepciones = await new ServicioCalendarioReposiciones(new FactoriaFalsa(handler)).LeerExcepciones("1  ");

            Assert.AreEqual("GET /api/Reposiciones/Calendario/Excepciones?empresa=1", handler.Urls[0]);
            Assert.AreEqual(new DateTime(2026, 11, 20), excepciones[0].FechaLlegada);
            Assert.AreEqual(TiposExcepcionCalendario.SIN_REPOSICION, excepciones[0].Tipo);
            Assert.AreEqual(new DateTime(2026, 11, 19, 16, 0, 0), excepciones[0].CierraEl);
        }

        [TestMethod]
        public async Task GuardarExcepcion_PostConLaRutaElDiaYElMotivo()
        {
            var handler = new HandlerFalso { Respuesta = EXCEPCION };

            ExcepcionCalendarioReposicion guardada = await new ServicioCalendarioReposiciones(new FactoriaFalsa(handler)).GuardarExcepcion(
                new ExcepcionCalendarioReposicion
                {
                    Empresa = "1", Origen = "ALG", Destino = "REI", FechaLlegada = new DateTime(2026, 11, 20),
                    Tipo = TiposExcepcionCalendario.REPOSICION_EXTRA, HoraCierre = new TimeSpan(9, 0, 0), Motivo = "Feria"
                });

            Assert.AreEqual("POST /api/Reposiciones/Calendario/Excepciones", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual("ReposicionExtra", (string)cuerpo["Tipo"]);
            Assert.AreEqual("09:00:00", (string)cuerpo["HoraCierre"]);
            Assert.AreEqual("Feria", (string)cuerpo["Motivo"]);
            Assert.AreEqual(7, guardada.Id);
        }

        [TestMethod]
        public async Task GuardarExcepcion_409DeLaApi_LanzaConSuMensaje()
        {
            var handler = new HandlerFalso
            {
                Codigo = HttpStatusCode.Conflict,
                Respuesta = "{\"Message\":\"La reposición de ALG a REI que llega el viernes 20/11/2026 cerraba el jueves 19/11/2026 a las 16:00 y esa hora ya ha pasado.\"}"
            };

            CalendarioReposicionesException error = await Assert.ThrowsExceptionAsync<CalendarioReposicionesException>(
                () => new ServicioCalendarioReposiciones(new FactoriaFalsa(handler)).GuardarExcepcion(new ExcepcionCalendarioReposicion()));

            Assert.AreEqual(409, error.Codigo);
            StringAssert.Contains(error.Message, "ya ha pasado");
        }

        [TestMethod]
        public async Task BorrarExcepcion_DeletePorId()
        {
            var handler = new HandlerFalso { Respuesta = EXCEPCION };

            await new ServicioCalendarioReposiciones(new FactoriaFalsa(handler)).BorrarExcepcion(7);

            Assert.AreEqual("DELETE /api/Reposiciones/Calendario/Excepciones/7", handler.Urls[0]);
        }
    }
}
