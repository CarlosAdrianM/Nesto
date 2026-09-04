using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// NestoAPI#453: al descargar el PDF de un pedido, cualquier error del servidor se convertía
    /// en Nothing y acababa en <c>New MemoryStream(Nothing)</c>, así que el usuario veía
    /// "value cannot be null. (Parameter buffer)" en lugar del motivo real. El pedido 925368
    /// estuvo dos días sin poder imprimirse mientras el servidor decía, sin que nadie lo leyera,
    /// "No cuadran los vencimientos con el total de la factura".
    /// </summary>
    [TestClass]
    public class PedidoVentaServiceFacturaTests
    {
        private const string MENSAJE_DEL_SERVIDOR = "No cuadran los vencimientos con el total de la factura";

        /// <summary>Devuelve siempre la misma respuesta, sin salir a la red.</summary>
        private class RespuestaFija : HttpMessageHandler
        {
            private readonly HttpStatusCode _codigo;
            private readonly string _cuerpo;

            public RespuestaFija(HttpStatusCode codigo, string cuerpo)
            {
                _codigo = codigo;
                _cuerpo = cuerpo;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(_codigo)
                {
                    Content = new StringContent(_cuerpo)
                });
            }
        }

        private static PedidoVentaService CrearServicio(HttpStatusCode codigo, string cuerpo)
        {
            IClienteApiFactory factory = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factory.Crear()).ReturnsLazily(() => new HttpClient(new RespuestaFija(codigo, cuerpo))
            {
                BaseAddress = new Uri("https://api.nuevavision.es/api/")
            });

            IServicioAutenticacion autenticacion = A.Fake<IServicioAutenticacion>();
            A.CallTo(() => autenticacion.ConfigurarAutorizacion(A<HttpClient>._))
                .Returns(Task.FromResult(true));

            return new PedidoVentaService(A.Fake<IConfiguracion>(), autenticacion, factory);
        }

        [TestMethod]
        public async Task CargarFactura_ErrorDelServidor_LanzaConElMotivoReal()
        {
            // FacturasController devuelve el motivo como texto plano en un BadRequest
            PedidoVentaService servicio = CrearServicio(HttpStatusCode.BadRequest, MENSAJE_DEL_SERVIDOR);

            Exception ex = await Assert.ThrowsExceptionAsync<Exception>(
                () => servicio.CargarFactura("1", "PRO925368")).ConfigureAwait(false);

            Assert.AreEqual(MENSAJE_DEL_SERVIDOR, ex.Message);
        }

        [TestMethod]
        public async Task CargarFactura_ErrorEnJson_TambienSacaElMensajeLegible()
        {
            // Y como JSON cuando responde el GlobalExceptionFilter
            PedidoVentaService servicio = CrearServicio(HttpStatusCode.InternalServerError,
                "{\"Message\":\"" + MENSAJE_DEL_SERVIDOR + "\"}");

            Exception ex = await Assert.ThrowsExceptionAsync<Exception>(
                () => servicio.CargarFactura("1", "PRO925368")).ConfigureAwait(false);

            Assert.AreEqual(MENSAJE_DEL_SERVIDOR, ex.Message);
        }

        [TestMethod]
        public async Task CargarFactura_NuncaDevuelveNothingEnUnError()
        {
            // La raíz del "parameter buffer": devolver Nothing y que reviente más adelante,
            // lejos de donde estaba el problema y sin decir cuál era
            PedidoVentaService servicio = CrearServicio(HttpStatusCode.BadRequest, MENSAJE_DEL_SERVIDOR);

            try
            {
                byte[] pdf = await servicio.CargarFactura("1", "PRO925368").ConfigureAwait(false);
                Assert.Fail("Tenía que haber lanzado una excepción, y devolvió " +
                    (pdf == null ? "Nothing" : pdf.Length + " bytes"));
            }
            catch (AssertFailedException)
            {
                throw;
            }
            catch (Exception)
            {
                // Correcto: el error sale a la superficie
            }
        }

        [TestMethod]
        public async Task CargarFactura_SiVaBien_DevuelveElPdf()
        {
            PedidoVentaService servicio = CrearServicio(HttpStatusCode.OK, "%PDF-1.4 esto es un pdf");

            byte[] pdf = await servicio.CargarFactura("1", "PRO925368").ConfigureAwait(false);

            Assert.IsNotNull(pdf);
            Assert.IsTrue(pdf.Length > 0);
        }
    }
}
