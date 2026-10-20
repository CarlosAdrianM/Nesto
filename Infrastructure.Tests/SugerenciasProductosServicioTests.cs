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
    /// Nesto#537 (API NestoAPI#670): el cliente de POST api/SugerenciasProductos/Calcular y .../{id}/Respuesta. Es una ayuda:
    /// cualquier fallo devuelve null (sin excepción) para que la plantilla siga igual.
    /// </summary>
    [TestClass]
    public class SugerenciasProductosServicioTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> Cuerpos = new List<string>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = "{}";
            public bool Lanzar;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.Method + " " + request.RequestUri.PathAndQuery);
                Cuerpos.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync().ConfigureAwait(false));
                if (Lanzar)
                {
                    throw new HttpRequestException("Sin conexión");
                }
                return new HttpResponseMessage(Codigo) { Content = new StringContent(Respuesta, Encoding.UTF8, "application/json") };
            }
        }

        private sealed class FactoriaFalsa : IClienteApiFactory
        {
            private readonly HttpMessageHandler _handler;
            public FactoriaFalsa(HttpMessageHandler handler) { _handler = handler; }
            public HttpClient Crear() => new HttpClient(_handler, false) { BaseAddress = new Uri("http://api.test/api/") };
        }

        private const string RESPUESTA_CALCULAR = "{\"IdSesion\":\"6c1f8f0e-3c55-4a8e-9c1e-0b5d1a9a5370\",\"Sugerencias\":[" +
            "{\"IdSugerencia\":10,\"Producto\":\"38000\",\"Nombre\":\"CHAMPU\",\"Motivo\":\"LeToca\",\"Texto\":\"Lo compra cada ~45 días; el último, hace 52.\",\"Posicion\":1}]}";

        private static CalcularSugerenciasProductosDTO Peticion() => new CalcularSugerenciasProductosDTO
        {
            Empresa = "1",
            Cliente = "15191",
            Contacto = "0",
            Vendedor = "ASH",
            Productos = new List<ProductoCarritoSugerenciaDTO> { new ProductoCarritoSugerenciaDTO { Producto = "17404", Cantidad = 2 } }
        };

        [TestMethod]
        public async Task Calcular_MandaElCarritoPorPost_YLeeLasFichas()
        {
            var handler = new HandlerFalso { Respuesta = RESPUESTA_CALCULAR };
            var servicio = new ServicioSugerenciasProductos(new FactoriaFalsa(handler));

            SugerenciasProductosDTO resultado = await servicio.Calcular(Peticion());

            Assert.AreEqual("POST /api/SugerenciasProductos/Calcular", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual("15191", (string)cuerpo["Cliente"]);
            Assert.AreEqual("ASH", (string)cuerpo["Vendedor"]);
            Assert.AreEqual("Nesto", (string)cuerpo["Aplicacion"]);
            Assert.AreEqual("17404", (string)cuerpo["Productos"][0]["Producto"]);
            Assert.AreEqual(new Guid("6c1f8f0e-3c55-4a8e-9c1e-0b5d1a9a5370"), resultado.IdSesion);
            Assert.AreEqual("38000", resultado.Sugerencias[0].Producto);
            Assert.AreEqual("🔁", resultado.Sugerencias[0].Icono);
        }

        [TestMethod]
        public async Task Calcular_ErrorDeLaApi_DevuelveNull()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.InternalServerError };
            var servicio = new ServicioSugerenciasProductos(new FactoriaFalsa(handler));

            Assert.IsNull(await servicio.Calcular(Peticion()));
        }

        [TestMethod]
        public async Task Calcular_SinConexion_DevuelveNullSinLanzar()
        {
            var handler = new HandlerFalso { Lanzar = true };
            var servicio = new ServicioSugerenciasProductos(new FactoriaFalsa(handler));

            Assert.IsNull(await servicio.Calcular(Peticion()));
        }

        [TestMethod]
        public async Task Calcular_SinCliente_NiSePregunta()
        {
            var handler = new HandlerFalso();
            var servicio = new ServicioSugerenciasProductos(new FactoriaFalsa(handler));

            Assert.IsNull(await servicio.Calcular(new CalcularSugerenciasProductosDTO()));
            Assert.AreEqual(0, handler.Urls.Count);
        }

        [TestMethod]
        public async Task Responder_MandaLaRespuestaYElCarrito_YLeeLaSustituta()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "{\"IdSugerencia\":10,\"Respuesta\":\"NoLeInteresa\",\"Sustituta\":{\"IdSugerencia\":13,\"Producto\":\"41000\",\"Motivo\":\"Potenciar\",\"Posicion\":1},\"Sugerencias\":[]}"
            };
            var servicio = new ServicioSugerenciasProductos(new FactoriaFalsa(handler));

            ResultadoRespuestaSugerenciaProductoDTO resultado = await servicio.Responder(10, RespuestasSugerenciaProducto.NO_LE_INTERESA,
                new List<ProductoCarritoSugerenciaDTO> { new ProductoCarritoSugerenciaDTO { Producto = "17404", Cantidad = 2 } });

            Assert.AreEqual("POST /api/SugerenciasProductos/10/Respuesta", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual("NoLeInteresa", (string)cuerpo["Respuesta"]);
            Assert.AreEqual("17404", (string)cuerpo["Productos"][0]["Producto"]);
            Assert.AreEqual("41000", resultado.Sustituta.Producto);
            Assert.AreEqual("⭐", resultado.Sustituta.Icono);
        }

        [TestMethod]
        public async Task Responder_SugerenciaQueNoExiste_DevuelveNull()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.NotFound };
            var servicio = new ServicioSugerenciasProductos(new FactoriaFalsa(handler));

            Assert.IsNull(await servicio.Responder(99, RespuestasSugerenciaProducto.ANADIDO, null));
        }

        [TestMethod]
        public void Ficha_NombreLargo_SeRecortaYElEnteroVaEnLaAyuda()
        {
            var ficha = new SugerenciaProductoDTO
            {
                Producto = "38000",
                Nombre = "CHAMPU REPARADOR KERATINA PROFESIONAL 1000 ML",
                Motivo = MotivosSugerenciaProducto.COMPRADO_JUNTO,
                Texto = "Lo compran con el 17404."
            };

            Assert.AreEqual(SugerenciaProductoDTO.LARGO_NOMBRE_CORTO, ficha.NombreCorto.Length);
            Assert.IsTrue(ficha.NombreCorto.EndsWith("…"));
            Assert.AreEqual("🔗", ficha.Icono);
            StringAssert.Contains(ficha.Ayuda, "CHAMPU REPARADOR KERATINA PROFESIONAL 1000 ML");
            StringAssert.Contains(ficha.Ayuda, "Comprado junto: Lo compran con el 17404.");
        }

        // ---------------- Nesto#537 (segunda vuelta): tarjetas más ricas y «🚫 Quitar» ----------------

        [TestMethod]
        public async Task Calcular_LeeLaFotoElTamanoYLaFamilia()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "{\"IdSesion\":\"6c1f8f0e-3c55-4a8e-9c1e-0b5d1a9a5370\",\"Sugerencias\":[{\"IdSugerencia\":10,\"Producto\":\"38000\"," +
                    "\"Nombre\":\"Crema Reafirmante\",\"Motivo\":\"Parecidos\",\"Posicion\":1,\"UrlFoto\":\"https://tienda/38000.jpg\"," +
                    "\"Tamano\":500,\"UnidadMedida\":\"ml\",\"Familia\":\"Lisap Milano\",\"NombreConTamano\":\"Crema Reafirmante 500 ml\"}]}"
            };
            var servicio = new ServicioSugerenciasProductos(new FactoriaFalsa(handler));

            SugerenciaProductoDTO ficha = (await servicio.Calcular(Peticion())).Sugerencias[0];

            Assert.AreEqual("https://tienda/38000.jpg", ficha.UrlFoto);
            Assert.IsTrue(ficha.TieneFoto);
            Assert.AreEqual((short)500, ficha.Tamano);
            Assert.AreEqual("Crema Reafirmante 500 ml", ficha.NombreMostrar);
            Assert.AreEqual("38000 · Lisap Milano", ficha.ProductoYFamilia);
            Assert.AreEqual("👥", ficha.Icono);
            Assert.AreEqual("Clientes parecidos", ficha.NombreMotivo);
            StringAssert.Contains(ficha.Ayuda, "38000 Crema Reafirmante 500 ml (Lisap Milano)");
        }

        [TestMethod]
        public void Ficha_ApiAnterior_SinCamposNuevos_SeFormaIgual()
        {
            var conTamano = new SugerenciaProductoDTO { Producto = "38000", Nombre = " Crema Reafirmante ", Tamano = 500, UnidadMedida = "ml " };
            var sinTamano = new SugerenciaProductoDTO { Producto = "38000", Nombre = "Crema", Tamano = 0, UnidadMedida = "ml" };
            var sinNombre = new SugerenciaProductoDTO { Producto = "38000" };

            Assert.AreEqual("Crema Reafirmante 500 ml", conTamano.NombreMostrar);
            Assert.AreEqual("Crema", sinTamano.NombreMostrar);
            Assert.AreEqual("38000", sinNombre.NombreMostrar);
            Assert.AreEqual("38000", sinNombre.ProductoYFamilia);
            Assert.IsFalse(sinNombre.TieneFoto);
            Assert.IsTrue(sinNombre.Ayuda.StartsWith("38000" + Environment.NewLine), "Sin repetir el código");
        }

        [TestMethod]
        public async Task Responder_Quitar_MandaDescartadoVendedor()
        {
            var handler = new HandlerFalso { Respuesta = "{\"IdSugerencia\":10,\"Respuesta\":\"DescartadoVendedor\"}" };
            var servicio = new ServicioSugerenciasProductos(new FactoriaFalsa(handler));

            ResultadoRespuestaSugerenciaProductoDTO r = await servicio.Responder(10, RespuestasSugerenciaProducto.DESCARTADO_VENDEDOR, null);

            Assert.AreEqual("POST /api/SugerenciasProductos/10/Respuesta", handler.Urls[0]);
            Assert.AreEqual("DescartadoVendedor", (string)JObject.Parse(handler.Cuerpos[0])["Respuesta"]);
            Assert.AreEqual(RespuestasSugerenciaProducto.DESCARTADO_VENDEDOR, r.Respuesta);
        }
    }
}
