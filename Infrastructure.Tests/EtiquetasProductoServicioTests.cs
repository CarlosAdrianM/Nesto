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
    /// Nesto#534 (NestoAPI#654): la etiqueta de código de barras de producto. Nesto no imprime en local, se lo pide a la API
    /// (POST api/Almacen/EtiquetasProducto/Imprimir), que sabe la impresora de etiquetas del usuario.
    /// </summary>
    [TestClass]
    public class EtiquetasProductoServicioTests
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
        public async Task Imprimir_MandaElProductoYLasCopiasALaApi()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "{\"Producto\":\"17404\",\"Lineas\":[\"ROLLO PAPEL CAMILLA\",\"Desechables\",\"100 m\"],\"Copias\":2,\"Impresas\":2,"
                    + "\"Impresora\":\"\\\\\\\\RDS2016\\\\etiquetasalcobendas\",\"Mensaje\":\"2 etiquetas de 17404 mandadas.\"}"
            };
            var servicio = new ServicioEtiquetasProducto(new FactoriaFalsa(handler));

            ResultadoEtiquetasProducto resultado = await servicio.Imprimir("1  ", " 17404 ", 2);

            Assert.AreEqual("POST /api/Almacen/EtiquetasProducto/Imprimir?empresa=1&ensayo=false", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual("17404", (string)cuerpo["Producto"]);
            Assert.AreEqual(2, (int)cuerpo["Copias"]);
            Assert.AreEqual(2, resultado.Impresas);
            Assert.AreEqual(@"\\RDS2016\etiquetasalcobendas", resultado.Impresora);
            CollectionAssert.AreEqual(new[] { "ROLLO PAPEL CAMILLA", "Desechables", "100 m" }, resultado.Lineas);
            Assert.AreEqual("2 etiquetas de 17404 mandadas.", resultado.Mensaje);
        }

        [TestMethod]
        public async Task Imprimir_SinFormato_NoPideNingunoComoSiempre()
        {
            var handler = new HandlerFalso();
            var servicio = new ServicioEtiquetasProducto(new FactoriaFalsa(handler));

            _ = await servicio.Imprimir("1", "17404", 1);

            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual(JTokenType.Null, cuerpo["Formato"].Type, "La de siempre (la referencia): la API no cambia nada");
        }

        [TestMethod]
        public async Task Imprimir_Ean13_MandaElFormatoYLeeElCodigoQueHaImpreso()
        {
            // NestoAPI#762: la etiqueta del EAN-13; si no tiene, la API le asigna el interno (290…) y lo dice
            var handler = new HandlerFalso
            {
                Respuesta = "{\"Producto\":\"17404\",\"Lineas\":[\"ROLLO PAPEL CAMILLA\"],\"Copias\":3,\"Impresas\":3,\"Impresora\":\"etiquetas4\","
                    + "\"Mensaje\":\"3 etiquetas de 17404 (2900000174047) mandadas a etiquetas4\",\"CodigoBarras\":\"2900000174047\","
                    + "\"CodigoInterno\":true,\"CodigoAsignado\":true}"
            };
            var servicio = new ServicioEtiquetasProducto(new FactoriaFalsa(handler));

            ResultadoEtiquetasProducto resultado = await servicio.Imprimir("1", "17404", 3, PeticionEtiquetasProducto.FORMATO_EAN13);

            Assert.AreEqual("POST /api/Almacen/EtiquetasProducto/Imprimir?empresa=1&ensayo=false", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual("17404", (string)cuerpo["Producto"]);
            Assert.AreEqual(3, (int)cuerpo["Copias"]);
            Assert.AreEqual("Ean13", (string)cuerpo["Formato"]);
            Assert.AreEqual("2900000174047", resultado.CodigoBarras);
            Assert.IsTrue(resultado.CodigoInterno);
            Assert.IsTrue(resultado.CodigoAsignado);
        }

        [TestMethod]
        public async Task Imprimir_Ean13_ConUnaApiAnterior_SinLosCamposNuevos()
        {
            var handler = new HandlerFalso { Respuesta = "{\"Producto\":\"17404\",\"Copias\":1,\"Impresas\":1}" };
            var servicio = new ServicioEtiquetasProducto(new FactoriaFalsa(handler));

            ResultadoEtiquetasProducto resultado = await servicio.Imprimir("1", "17404", 1, PeticionEtiquetasProducto.FORMATO_EAN13);

            Assert.IsNull(resultado.CodigoBarras);
            Assert.IsFalse(resultado.CodigoInterno);
            Assert.IsFalse(resultado.CodigoAsignado);
        }

        [TestMethod]
        public async Task Imprimir_Ensayo_LlevaEnsayoTrue()
        {
            var handler = new HandlerFalso();
            var servicio = new ServicioEtiquetasProducto(new FactoriaFalsa(handler));

            _ = await servicio.Imprimir("1", "17404", 1, ensayo: true);

            StringAssert.EndsWith(handler.Urls[0], "ensayo=true");
        }

        [TestMethod]
        public async Task Imprimir_SinPermiso_DaElMotivoDeLaApiTalCual()
        {
            var handler = new HandlerFalso
            {
                Codigo = HttpStatusCode.Forbidden,
                Respuesta = "\"Para imprimir etiquetas de producto hay que ser de Compras, Almacén, Tiendas, Dirección o Informática.\""
            };
            var servicio = new ServicioEtiquetasProducto(new FactoriaFalsa(handler));

            var ex = await Assert.ThrowsExceptionAsync<EtiquetasProductoException>(() => servicio.Imprimir("1", "17404", 1));

            Assert.AreEqual("Para imprimir etiquetas de producto hay que ser de Compras, Almacén, Tiendas, Dirección o Informática.", ex.Message);
        }

        [TestMethod]
        public async Task Imprimir_ProductoQueNoExiste_DaElMotivoDeBadRequest()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.BadRequest, Respuesta = "{\"Message\":\"No existe el producto 99999.\"}" };
            var servicio = new ServicioEtiquetasProducto(new FactoriaFalsa(handler));

            var ex = await Assert.ThrowsExceptionAsync<EtiquetasProductoException>(() => servicio.Imprimir("1", "99999", 1));

            Assert.AreEqual("No existe el producto 99999.", ex.Message);
        }
    }
}
