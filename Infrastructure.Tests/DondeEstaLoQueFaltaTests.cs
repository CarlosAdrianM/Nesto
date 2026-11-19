using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Tests
{
    /// <summary>«¿Dónde está lo que falta?» (pedido 930942, 19/11/26): el cliente de la API y lo que se enseña.</summary>
    [TestClass]
    public class DondeEstaLoQueFaltaTests
    {
        private const string FRASE_930942 = "43818 MULTIVIT GLOW CREMA RICA (1 ud.): viene en la reposición 81463 de Alcobendas, recogida por Miguel " +
            "el miércoles 18/11 a las 12:53; pendiente de recibir en Algete.";

        private const string JSON_930942 = "{\"Empresa\":\"1\",\"Pedido\":930942,\"Mensaje\":\"Falta 1 producto en Algete.\",\"TodoEnAlgete\":false,\"Pickings\":[]," +
            "\"FechaEntregaAgencia\":\"2026-11-19T00:00:00\",\"Productos\":[{\"Producto\":\"43818\",\"Nombre\":\"MULTIVIT GLOW CREMA RICA\",\"Unidades\":1," +
            "\"Texto\":\"" + FRASE_930942 + "\",\"Situaciones\":[{\"Origen\":\"EnCamino\",\"Unidades\":1,\"Tienda\":\"ALC\",\"Traspaso\":81463," +
            "\"Seguimiento\":{\"Recogida\":\"2026-11-18T12:53:00\",\"RecogidaPor\":\"Miguel\",\"EnFurgonetaDe\":\"Miguel\",\"PendienteDeRecibir\":true}," +
            "\"Texto\":\"viene en la reposición 81463 de Alcobendas…\"}]}]}";

        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = JSON_930942;

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

        [TestMethod]
        public async Task Servicio_PideElPedido_YLeeLaFrase()
        {
            var handler = new HandlerFalso();

            DondeEstaLoQueFaltaDTO r = await new ServicioDondeEstaLoQueFalta(new FactoriaFalsa(handler)).Leer("1  ", 930942);

            Assert.AreEqual("GET /api/PedidosVenta/1/930942/DondeEstaLoQueFalta", handler.Urls.Single());
            Assert.AreEqual(FRASE_930942, r.Productos.Single().Texto);
            Assert.AreEqual(81463, r.Productos.Single().Situaciones.Single().Traspaso);
        }

        [TestMethod]
        public async Task Servicio_ApiAntiguaOError_Null()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.NotFound, Respuesta = "{\"Message\":\"No HTTP resource\"}" };

            Assert.IsNull(await new ServicioDondeEstaLoQueFalta(new FactoriaFalsa(handler)).Leer("1", 930942));
        }

        [TestMethod]
        public async Task Vista_UnaLineaPorProducto_YElMensaje()
        {
            var vista = new DondeEstaLoQueFaltaVista(() => new ServicioDondeEstaLoQueFalta(new FactoriaFalsa(new HandlerFalso())));

            await vista.MostrarAsync("1", 930942);

            Assert.AreEqual(FRASE_930942, vista.Lineas.Single());
            Assert.IsTrue(vista.HayLineas);
            Assert.AreEqual("Falta 1 producto en Algete.", vista.Mensaje);
            Assert.AreEqual(930942, vista.Pedido);
            Assert.IsFalse(vista.Cargando);
        }

        [TestMethod]
        public async Task Vista_TodoEnAlgete_SoloElMensaje()
        {
            var servicio = A.Fake<IServicioDondeEstaLoQueFalta>();
            A.CallTo(() => servicio.Leer("1", 930942)).Returns(new DondeEstaLoQueFaltaDTO { TodoEnAlgete = true, Mensaje = "Todo está en Algete: tiene el picking 100120." });
            var vista = new DondeEstaLoQueFaltaVista(() => servicio);

            await vista.MostrarAsync("1", 930942);

            Assert.IsFalse(vista.HayLineas);
            Assert.AreEqual("Todo está en Algete: tiene el picking 100120.", vista.Mensaje);
        }

        [TestMethod]
        public async Task Vista_SinRespuesta_LoDiceDeUsted()
        {
            var servicio = A.Fake<IServicioDondeEstaLoQueFalta>();
            A.CallTo(() => servicio.Leer(A<string>._, A<int>._)).Returns(Task.FromResult<DondeEstaLoQueFaltaDTO>(null));
            var vista = new DondeEstaLoQueFaltaVista(() => servicio);

            await vista.MostrarAsync("1", 930942);

            Assert.AreEqual(DondeEstaLoQueFaltaVista.SIN_RESPUESTA, vista.Mensaje);
        }

        [TestMethod]
        public async Task Vista_LaRespuestaDeUnPedidoAnterior_SeDescarta()
        {
            var servicio = A.Fake<IServicioDondeEstaLoQueFalta>();
            var lenta = new TaskCompletionSource<DondeEstaLoQueFaltaDTO>();
            A.CallTo(() => servicio.Leer("1", 1)).Returns(lenta.Task);
            A.CallTo(() => servicio.Leer("1", 2)).Returns(new DondeEstaLoQueFaltaDTO { Mensaje = "Todo está en Algete; aún no tiene picking." });
            var vista = new DondeEstaLoQueFaltaVista(() => servicio);

            Task primera = vista.MostrarAsync("1", 1);
            await vista.MostrarAsync("1", 2);
            lenta.SetResult(new DondeEstaLoQueFaltaDTO { Productos = { new ProductoQueFaltaDTO { Texto = "del pedido 1" } } });
            await primera;

            Assert.AreEqual(2, vista.Pedido);
            Assert.IsFalse(vista.HayLineas);
            Assert.AreEqual("Todo está en Algete; aún no tiene picking.", vista.Mensaje);
        }

        [TestMethod]
        public void Vista_Limpiar_NoQuedaNada()
        {
            var vista = new DondeEstaLoQueFaltaVista(() => null);
            vista.Aplicar(new DondeEstaLoQueFaltaDTO { Mensaje = "x", Productos = { new ProductoQueFaltaDTO { Texto = "y" } } });

            vista.Limpiar();

            Assert.IsFalse(vista.HayLineas);
            Assert.IsFalse(vista.HayMensaje);
            Assert.IsNull(vista.Pedido);
        }
    }
}
