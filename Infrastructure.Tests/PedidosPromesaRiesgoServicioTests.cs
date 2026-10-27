using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Tests
{
    /// <summary>Nesto#546 (NestoAPI#693): los pedidos con la fecha en riesgo contra api/PedidosVenta/PromesaEnRiesgo.</summary>
    [TestClass]
    public class PedidosPromesaRiesgoServicioTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = "[]";

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.Method + " " + request.RequestUri.PathAndQuery);
                return Task.FromResult(new HttpResponseMessage(Codigo) { Content = new StringContent(Respuesta, Encoding.UTF8, "application/json") });
            }
        }

        private sealed class FactoriaFalsa : IClienteApiFactory
        {
            private readonly HttpMessageHandler _handler;
            public FactoriaFalsa(HttpMessageHandler handler) { _handler = handler; }
            public HttpClient Crear() => new HttpClient(_handler, false) { BaseAddress = new Uri("http://api.test/api/") };
        }

        private const string RIESGO = "{\"Id\":3,\"Empresa\":\"1\",\"Pedido\":928908,\"Cliente\":\"40353\",\"Contacto\":\"0\",\"NombreCliente\":\"Peluquería Prueba\"," +
            "\"Vendedor\":\"JGP\",\"VendedorPeluqueria\":null,\"Ruta\":\"16\",\"FechaPrometida\":\"2026-10-22T00:00:00\",\"FechaNueva\":null,\"SinFecha\":true," +
            "\"DiasRetraso\":null,\"Motivo\":\"Faltan 2 de 3 del producto 42485: Reina mandó 1 de 3 en la reposición 81094.\",\"Evento\":\"reposición 81094 de Reina\"," +
            "\"Detectado\":\"2026-10-21T10:05:00\",\"Actualizado\":\"2026-10-21T10:05:00\",\"AvisadoCampana\":\"2026-10-21T10:05:01\",\"AvisadoCorreo\":null," +
            "\"Resuelto\":null,\"MotivoResolucion\":null,\"EstaResuelto\":false}";

        [TestMethod]
        public async Task Leer_PideLaEmpresa_YPorDefectoSoloPendientes()
        {
            var handler = new HandlerFalso { Respuesta = "[" + RIESGO + "]" };

            List<PedidoPromesaRiesgo> filas = await new ServicioPedidosPromesaRiesgo(new FactoriaFalsa(handler)).Leer("1  ", false);

            Assert.AreEqual("GET /api/PedidosVenta/PromesaEnRiesgo?empresa=1", handler.Urls[0]);
            Assert.AreEqual(1, filas.Count);
            Assert.AreEqual(928908, filas[0].Pedido);
            Assert.AreEqual("Peluquería Prueba", filas[0].NombreCliente);
            Assert.IsTrue(filas[0].SinFecha);
            Assert.IsNull(filas[0].DiasRetraso);
            Assert.IsFalse(filas[0].EstaResuelto);
            Assert.AreEqual(new DateTime(2026, 10, 22), filas[0].FechaPrometida);
        }

        [TestMethod]
        public async Task Leer_ConResueltos_LoPide()
        {
            var handler = new HandlerFalso();

            await new ServicioPedidosPromesaRiesgo(new FactoriaFalsa(handler)).Leer("1", true);

            Assert.AreEqual("GET /api/PedidosVenta/PromesaEnRiesgo?empresa=1&resueltos=true", handler.Urls[0]);
        }

        [TestMethod]
        public async Task Leer_Si403_LanzaConElMensajeDeLaApi()
        {
            var handler = new HandlerFalso
            {
                Codigo = HttpStatusCode.Forbidden,
                Respuesta = "{\"Message\":\"Solo puede ver los pedidos de su vendedor o de los vendedores de su equipo.\"}"
            };

            try
            {
                await new ServicioPedidosPromesaRiesgo(new FactoriaFalsa(handler)).Leer("1", false);
                Assert.Fail("Tenía que lanzar");
            }
            catch (PedidosPromesaRiesgoException ex)
            {
                Assert.AreEqual(403, ex.Codigo);
                StringAssert.Contains(ex.Message, "Solo puede ver los pedidos de su vendedor");
            }
        }

        [TestMethod]
        public void DiasRetraso_EsLaDiferenciaEnDias()
        {
            var riesgo = new PedidoPromesaRiesgo { FechaPrometida = new DateTime(2026, 10, 22), FechaNueva = new DateTime(2026, 10, 24, 15, 0, 0) };

            Assert.AreEqual(2, riesgo.DiasRetraso);
            Assert.IsFalse(riesgo.SinFecha);
        }
    }
}
