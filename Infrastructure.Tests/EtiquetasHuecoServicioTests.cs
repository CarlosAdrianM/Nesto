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
    /// <summary>
    /// Etiquetas de hueco: Nesto no imprime en local, se lo pide a la API (POST api/Almacen/EtiquetasHueco/Imprimir),
    /// que es quien sabe la impresora de etiquetas de producto del usuario.
    /// </summary>
    [TestClass]
    public class EtiquetasHuecoServicioTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> Cuerpos = new List<string>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = "{}";

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

        [TestMethod]
        public async Task Imprimir_MandaLaPeticionALaApiConEmpresaAlmacenYEnsayo()
        {
            var handler = new HandlerFalso { Respuesta = "{\"Impresas\":0,\"Impresora\":\"\\\\\\\\RDS2016\\\\etiquetas2\",\"Huecos\":[\"002004001\",\"002004002\"],\"Mensaje\":\"Saldrían 2\"}" };
            var servicio = new ServicioEtiquetasHueco(new FactoriaFalsa(handler));

            ResultadoEtiquetasHueco resultado = await servicio.Imprimir("1  ", "ALG", new PeticionEtiquetasHueco
            {
                Pasillo = "002", FilaDesde = "004", FilaHasta = "004", ColumnaDesde = "001", ColumnaHasta = "002", SoloEnUso = true
            }, ensayo: true);

            Assert.AreEqual("POST /api/Almacen/EtiquetasHueco/Imprimir?empresa=1&almacen=ALG&ensayo=true", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual("002", (string)cuerpo["Pasillo"]);
            Assert.AreEqual(true, (bool)cuerpo["SoloEnUso"]);
            Assert.AreEqual(@"\\RDS2016\etiquetas2", resultado.Impresora);
            CollectionAssert.AreEqual(new[] { "002004001", "002004002" }, resultado.Huecos);
            Assert.AreEqual("Saldrían 2", resultado.Mensaje);
        }

        [TestMethod]
        public async Task Imprimir_DeVerdad_LlevaEnsayoFalse()
        {
            var handler = new HandlerFalso { Respuesta = "{\"Impresas\":1,\"Huecos\":[\"002004001\"]}" };
            var servicio = new ServicioEtiquetasHueco(new FactoriaFalsa(handler));

            ResultadoEtiquetasHueco resultado = await servicio.Imprimir("1", "ALG", new PeticionEtiquetasHueco { Huecos = new List<string> { "002004001" } }, ensayo: false);

            StringAssert.EndsWith(handler.Urls[0], "ensayo=false");
            Assert.AreEqual(1, resultado.Impresas);
        }

        [TestMethod]
        public async Task Imprimir_SinPermiso_DaElMotivoDeLaApiTalCual()
        {
            // Content(HttpStatusCode.Forbidden, mensaje) llega como una cadena JSON
            var handler = new HandlerFalso { Codigo = HttpStatusCode.Forbidden, Respuesta = "\"Para imprimir etiquetas de hueco hay que ser de Almacén.\"" };
            var servicio = new ServicioEtiquetasHueco(new FactoriaFalsa(handler));

            var ex = await Assert.ThrowsExceptionAsync<EtiquetasHuecoException>(() =>
                servicio.Imprimir("1", "ALG", new PeticionEtiquetasHueco { Huecos = new List<string> { "002004001" } }, ensayo: false));

            Assert.AreEqual("Para imprimir etiquetas de hueco hay que ser de Almacén.", ex.Message);
        }

        [TestMethod]
        public async Task Imprimir_PeticionMal_DaElMotivoDeBadRequest()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.BadRequest, Respuesta = "{\"Message\":\"«02» no es un hueco: tienen que ser 9 cifras.\"}" };
            var servicio = new ServicioEtiquetasHueco(new FactoriaFalsa(handler));

            var ex = await Assert.ThrowsExceptionAsync<EtiquetasHuecoException>(() =>
                servicio.Imprimir("1", "ALG", new PeticionEtiquetasHueco { Huecos = new List<string> { "02" } }, ensayo: true));

            Assert.AreEqual("«02» no es un hueco: tienen que ser 9 cifras.", ex.Message);
        }
    }
}
