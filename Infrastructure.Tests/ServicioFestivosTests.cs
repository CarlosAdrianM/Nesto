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
    /// <summary>Nesto#616 (NestoAPI#779): los festivos contra api/Festivos.</summary>
    [TestClass]
    public class ServicioFestivosTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> Cuerpos = new List<string>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = "[]";

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.Method + " " + request.RequestUri.AbsolutePath + request.RequestUri.Query);
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

        [TestMethod]
        public async Task Leer_PideElAnioYLeeElOrigen()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "[{\"Fecha\":\"2027-03-19T00:00:00\",\"Tipo\":\"Autonómica\",\"Descripcion\":\"San José\",\"Usuario\":null,\"FechaModificacion\":null,\"Origen\":\"Código\"}]"
            };

            List<Festivo> festivos = await new ServicioFestivos(new FactoriaFalsa(handler)).Leer(2027);

            Assert.AreEqual("GET /api/Festivos?anio=2027", handler.Urls[0]);
            Assert.AreEqual(new DateTime(2027, 3, 19), festivos[0].Fecha);
            Assert.AreEqual(TiposFestivo.AUTONOMICA, festivos[0].Tipo);
            Assert.IsTrue(festivos[0].VieneDelCodigo);
            Assert.AreEqual("Viernes", festivos[0].DiaSemana);
        }

        [TestMethod]
        public async Task Guardar_MandaFechaTipoYDescripcion()
        {
            var handler = new HandlerFalso { Respuesta = "{\"Fecha\":\"2027-12-08T00:00:00\",\"Tipo\":\"Nacional\",\"Descripcion\":\"Inmaculada\",\"Origen\":\"Tabla\"}" };

            Festivo festivo = await new ServicioFestivos(new FactoriaFalsa(handler)).Guardar(new PeticionFestivo
            {
                Fecha = new DateTime(2027, 12, 8, 10, 30, 0),
                Tipo = TiposFestivo.NACIONAL,
                Descripcion = "  Inmaculada  "
            });

            Assert.AreEqual("POST /api/Festivos", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual(new DateTime(2027, 12, 8), (DateTime)cuerpo["Fecha"]);
            Assert.AreEqual("Nacional", (string)cuerpo["Tipo"]);
            Assert.AreEqual("Inmaculada", (string)cuerpo["Descripcion"]);
            Assert.IsFalse(festivo.VieneDelCodigo);
        }

        [TestMethod]
        public async Task Borrar_LaFechaSinHoraYElTipoEscapado()
        {
            var handler = new HandlerFalso { Respuesta = string.Empty };

            await new ServicioFestivos(new FactoriaFalsa(handler)).Borrar(new DateTime(2027, 3, 19), TiposFestivo.AUTONOMICA);

            Assert.AreEqual("DELETE /api/Festivos/2027-03-19/Auton%C3%B3mica", handler.Urls[0]);
        }

        [TestMethod]
        public async Task SinPermiso_LanzaConElMensajeDeLaApi()
        {
            var handler = new HandlerFalso
            {
                Codigo = HttpStatusCode.Forbidden,
                Respuesta = "\"Solo Administración, Dirección o Informática pueden cambiar los festivos.\""
            };

            FestivosException ex = await Assert.ThrowsExceptionAsync<FestivosException>(() =>
                new ServicioFestivos(new FactoriaFalsa(handler)).Borrar(new DateTime(2027, 3, 19), TiposFestivo.NACIONAL));

            Assert.AreEqual(403, ex.Codigo);
            Assert.AreEqual("Solo Administración, Dirección o Informática pueden cambiar los festivos.", ex.Message);
        }

        [TestMethod]
        public async Task SinTabla_LanzaUn409ConElMensajeDeLaApi()
        {
            var handler = new HandlerFalso
            {
                Codigo = HttpStatusCode.Conflict,
                Respuesta = "\"Todavía no existe la tabla Festivos: hay que lanzar Scripts/Issue779_Festivos.sql.\""
            };

            FestivosException ex = await Assert.ThrowsExceptionAsync<FestivosException>(() =>
                new ServicioFestivos(new FactoriaFalsa(handler)).Guardar(new PeticionFestivo { Fecha = new DateTime(2027, 1, 1), Tipo = "Nacional", Descripcion = "Año Nuevo" }));

            Assert.AreEqual(409, ex.Codigo);
            Assert.AreEqual("Todavía no existe la tabla Festivos: hay que lanzar Scripts/Issue779_Festivos.sql.", ex.Message);
        }
    }
}
