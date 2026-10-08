using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Tests
{
    /// <summary>
    /// Nesto#519 (NestoAPI#616): el servicio de adjuntos habla con el contrato de la API (GET/DELETE
    /// api/Novedades/Adjuntos/{id}, POST multipart con el campo «fichero» por cada uno).
    /// </summary>
    [TestClass]
    public class ServicioAdjuntosNovedadesTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<(HttpMethod Metodo, string Url, string TipoContenido, string Cuerpo)> Peticiones
                = new List<(HttpMethod, string, string, string)>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public HttpContent Respuesta = new StringContent("[]", Encoding.UTF8, "application/json");

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string cuerpo = request.Content == null ? null : await request.Content.ReadAsStringAsync();
                Peticiones.Add((request.Method, request.RequestUri.PathAndQuery, request.Content?.Headers.ContentType?.MediaType, cuerpo));
                return new HttpResponseMessage(Codigo) { Content = Respuesta };
            }
        }

        private sealed class FactoriaFalsa : IClienteApiFactory
        {
            private readonly HttpMessageHandler _handler;
            public FactoriaFalsa(HttpMessageHandler handler) { _handler = handler; }
            public HttpClient Crear() => new HttpClient(_handler, false) { BaseAddress = new Uri("http://api.test/api/") };
        }

        private HandlerFalso handler;
        private ServicioAdjuntosNovedades servicio;
        private string carpeta;

        [TestInitialize]
        public void Setup()
        {
            handler = new HandlerFalso();
            servicio = new ServicioAdjuntosNovedades(new FactoriaFalsa(handler));
            carpeta = Path.Combine(Path.GetTempPath(), "NestoTestsAdjuntosServicio", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(carpeta);
        }

        [TestCleanup]
        public void Limpiar()
        {
            try { Directory.Delete(carpeta, true); } catch (Exception) { }
        }

        private string Fichero(string nombre, int bytes = 10)
        {
            string ruta = Path.Combine(carpeta, nombre);
            File.WriteAllBytes(ruta, new byte[bytes]);
            return ruta;
        }

        [TestMethod]
        public async Task Descargar_PideElAdjuntoYDevuelveLosBytes()
        {
            handler.Respuesta = new ByteArrayContent(new byte[] { 9, 8, 7 });

            byte[] bytes = await servicio.Descargar(42);

            Assert.AreEqual(HttpMethod.Get, handler.Peticiones[0].Metodo);
            Assert.AreEqual("/api/Novedades/Adjuntos/42", handler.Peticiones[0].Url);
            CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, bytes);
        }

        [TestMethod]
        public async Task Descargar_ConError_LanzaConElMotivo()
        {
            handler.Codigo = HttpStatusCode.NotFound;
            handler.Respuesta = new StringContent("{\"Message\":\"No existe\"}", Encoding.UTF8, "application/json");

            var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => servicio.Descargar(1));

            StringAssert.Contains(ex.Message, "descargar el adjunto");
        }

        [TestMethod]
        public async Task Subir_MultipartConUnCampoFicheroPorCadaUnoYSuTipo()
        {
            handler.Respuesta = new StringContent(
                "[{\"Id\":1,\"Nombre\":\"Normas.pdf\",\"Tipo\":\"application/pdf\",\"Tamano\":10},{\"Id\":2,\"Nombre\":\"cartel.png\",\"Tipo\":\"image/png\",\"Tamano\":10}]",
                Encoding.UTF8, "application/json");

            List<AdjuntoNovedad> creados = await servicio.Subir(7, new[] { Fichero("Normas.pdf"), Fichero("cartel.png") });

            var peticion = handler.Peticiones.Single();
            Assert.AreEqual(HttpMethod.Post, peticion.Metodo);
            Assert.AreEqual("/api/Novedades/7/Adjuntos", peticion.Url);
            Assert.AreEqual("multipart/form-data", peticion.TipoContenido);
            Assert.AreEqual(2, peticion.Cuerpo.Split(new[] { "name=fichero" }, StringSplitOptions.None).Length - 1);
            StringAssert.Contains(peticion.Cuerpo, "filename=Normas.pdf");
            StringAssert.Contains(peticion.Cuerpo, "Content-Type: application/pdf");
            StringAssert.Contains(peticion.Cuerpo, "Content-Type: image/png");
            CollectionAssert.AreEqual(new[] { 1, 2 }, creados.Select(c => c.Id).ToArray());
        }

        [TestMethod]
        public async Task Subir_TipoNoAdmitido_NoLlamaALaApi()
        {
            var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => servicio.Subir(7, new[] { Fichero("hoja.xlsx") }));

            StringAssert.Contains(ex.Message, "hoja.xlsx");
            Assert.AreEqual(0, handler.Peticiones.Count);
        }

        [TestMethod]
        public async Task Subir_MasDe10Mb_NoLlamaALaApi()
        {
            string grande = Fichero("grande.pdf", (int)ServicioAdjuntosNovedades.TAMANO_MAXIMO + 1);

            var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => servicio.Subir(7, new[] { grande }));

            StringAssert.Contains(ex.Message, "10 MB");
            Assert.AreEqual(0, handler.Peticiones.Count);
        }

        [TestMethod]
        public async Task Borrar_LlamaAlDelete()
        {
            handler.Respuesta = new StringContent(string.Empty);

            await servicio.Borrar(42);

            Assert.AreEqual(HttpMethod.Delete, handler.Peticiones[0].Metodo);
            Assert.AreEqual("/api/Novedades/Adjuntos/42", handler.Peticiones[0].Url);
        }

        [TestMethod]
        public void NovedadUsuario_SinLaPropiedad_AdjuntosNull_YConEllaLaLista()
        {
            var vieja = JsonConvert.DeserializeObject<NovedadUsuario>("{\"Id\":1,\"Titulo\":\"x\"}");
            var nueva = JsonConvert.DeserializeObject<NovedadUsuario>("{\"Id\":1,\"Titulo\":\"x\",\"Adjuntos\":[{\"Id\":3,\"Nombre\":\"a.pdf\",\"Tipo\":\"application/pdf\",\"Tamano\":245760}]}");

            Assert.IsNull(vieja.Adjuntos);
            Assert.AreEqual(245760, nueva.Adjuntos.Single().Tamano);
        }
    }
}
