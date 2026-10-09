using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Tests
{
    /// <summary>
    /// Nesto#522: en el detalle del pedido se ven, se descargan y se mandan al cliente las fotos del packing de cada bulto.
    /// </summary>
    [TestClass]
    public class FotosBultosPedidoServicioTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<AuthenticationHeaderValue> Autorizaciones = new List<AuthenticationHeaderValue>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public HttpContent Respuesta = new StringContent("[]");

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.RequestUri.AbsoluteUri);
                Autorizaciones.Add(request.Headers.Authorization);
                return Task.FromResult(new HttpResponseMessage(Codigo) { Content = Respuesta });
            }
        }

        private sealed class FactoriaFalsa : IClienteApiFactory
        {
            private readonly HttpMessageHandler _handler;
            public FactoriaFalsa(HttpMessageHandler handler) { _handler = handler; }
            public HttpClient Crear()
            {
                var cliente = new HttpClient(_handler, false) { BaseAddress = new Uri("http://api.test/api/") };
                cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token-de-nesto");
                return cliente;
            }
        }

        private static StringContent Json(string json) => new StringContent(json, Encoding.UTF8, "application/json");

        [TestMethod]
        public async Task LeerBultosDelPedido_TraeLaRutaPublicaDeLaFoto()
        {
            var api = new HandlerFalso
            {
                Respuesta = Json("[{\"Id\":17,\"Pedido\":928123,\"Bulto\":2,\"TieneFoto\":true,\"RutaFotoPublica\":\"api/Almacen/Fotos/17-abc\"}]")
            };
            var servicio = new ServicioBultosAriadna(new FactoriaFalsa(api));

            List<BultoAriadna> bultos = await servicio.LeerBultosDelPedido("1", 928123);

            Assert.AreEqual("api/Almacen/Fotos/17-abc", bultos[0].RutaFotoPublica);
        }

        [TestMethod]
        public async Task DescargarFoto_PideUnEnlaceNuevoYBajaLaImagenSinElTokenDeLaApi()
        {
            var api = new HandlerFalso { Respuesta = Json("{\"Url\":\"https://fotos.test/17.jpg?sig=x\",\"MinutosDeVigencia\":15}") };
            var almacenFotos = new HandlerFalso { Respuesta = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF }) };
            var servicio = new ServicioBultosAriadna(new FactoriaFalsa(api), () => new HttpClient(almacenFotos, false));

            byte[] foto = await servicio.DescargarFoto(17);
            byte[] otraVez = await servicio.DescargarFoto(17);

            CollectionAssert.AreEqual(new byte[] { 0xFF, 0xD8, 0xFF }, foto);
            Assert.IsNotNull(otraVez);
            // Cada descarga pide su enlace: nunca se usa uno caducado
            CollectionAssert.AreEqual(new[] { "http://api.test/api/Almacen/Bultos/17/Foto", "http://api.test/api/Almacen/Bultos/17/Foto" }, api.Urls);
            Assert.AreEqual("https://fotos.test/17.jpg?sig=x", almacenFotos.Urls[0]);
            Assert.IsNull(almacenFotos.Autorizaciones[0], "El enlace temporal ya lleva su permiso; el token de la API no debe salir de casa");
        }

        [TestMethod]
        public async Task DescargarFoto_BultoSinFoto_DevuelveNullSinDescargarNada()
        {
            var api = new HandlerFalso { Codigo = HttpStatusCode.NotFound, Respuesta = Json("") };
            var almacenFotos = new HandlerFalso();
            var servicio = new ServicioBultosAriadna(new FactoriaFalsa(api), () => new HttpClient(almacenFotos, false));

            Assert.IsNull(await servicio.DescargarFoto(18));
            Assert.AreEqual(0, almacenFotos.Urls.Count);
        }

        [TestMethod]
        public async Task DescargarFoto_ElAlmacenDeFotosFalla_Lanza()
        {
            var api = new HandlerFalso { Respuesta = Json("{\"Url\":\"https://fotos.test/17.jpg?sig=x\"}") };
            var almacenFotos = new HandlerFalso { Codigo = HttpStatusCode.Forbidden };
            var servicio = new ServicioBultosAriadna(new FactoriaFalsa(api), () => new HttpClient(almacenFotos, false));

            _ = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => servicio.DescargarFoto(17));
        }

        [TestMethod]
        public void EnlacePublico_QuitaElApiDelServidorYPoneLaRuta()
        {
            Assert.AreEqual("http://api.nuevavision.es/api/Almacen/Fotos/17-abc",
                EnlacePublicoFotoBulto.Componer("http://api.nuevavision.es/api/", "api/Almacen/Fotos/17-abc"));
            Assert.AreEqual("http://localhost:53364/api/Almacen/Fotos/17-abc",
                EnlacePublicoFotoBulto.Componer("http://localhost:53364/api", "/api/Almacen/Fotos/17-abc"));
        }

        [TestMethod]
        public void EnlacePublico_SinRutaOServidor_Null_YUnaDireccionCompletaTalCual()
        {
            Assert.IsNull(EnlacePublicoFotoBulto.Componer("http://api.nuevavision.es/api/", null));
            Assert.IsNull(EnlacePublicoFotoBulto.Componer("", "api/Almacen/Fotos/17-abc"));
            Assert.AreEqual("https://otro.test/api/Almacen/Fotos/17-abc",
                EnlacePublicoFotoBulto.Componer("http://api.nuevavision.es/api/", "https://otro.test/api/Almacen/Fotos/17-abc"));
        }
    }
}
