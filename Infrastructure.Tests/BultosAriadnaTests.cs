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
    /// <summary>
    /// Nesto#507: Agencias propone el número de bultos del packing de Ariadna y deja ver sus fotos.
    /// </summary>
    [TestClass]
    public class BultosAriadnaTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = "[]";

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.RequestUri.PathAndQuery);
                return Task.FromResult(new HttpResponseMessage(Codigo) { Content = new StringContent(Respuesta, Encoding.UTF8, "application/json") });
            }
        }

        private sealed class FactoriaFalsa : IClienteApiFactory
        {
            private readonly HttpMessageHandler _handler;
            public FactoriaFalsa(HttpMessageHandler handler) { _handler = handler; }
            public HttpClient Crear() => new HttpClient(_handler, false) { BaseAddress = new Uri("http://api.test/api/") };
        }

        private static BultoAriadna Bulto(int id, bool foto = true) => new BultoAriadna { Id = id, Bulto = id, TieneFoto = foto };

        [TestMethod]
        public async Task LeerBultosDelPedido_PideLosDelPedidoConSuEmpresaSinRelleno()
        {
            var handler = new HandlerFalso { Respuesta = "[{\"Id\":17,\"Pedido\":927646,\"Picking\":99739,\"Bulto\":1,\"TieneFoto\":true}]" };
            var servicio = new ServicioBultosAriadna(new FactoriaFalsa(handler));

            List<BultoAriadna> bultos = await servicio.LeerBultosDelPedido("1  ", 927646);

            Assert.AreEqual("/api/Almacen/Pedidos/927646/Bultos?empresa=1", handler.Urls[0]);
            Assert.AreEqual(17, bultos[0].Id);
            Assert.IsTrue(bultos[0].TieneFoto);
        }

        [TestMethod]
        public async Task EnlaceFoto_DevuelveLaUrlTemporal_YNullSiNoHayFoto()
        {
            var handler = new HandlerFalso { Respuesta = "{\"Url\":\"https://fotos.test/17.jpg?sig=x\",\"MinutosDeVigencia\":15}" };
            var servicio = new ServicioBultosAriadna(new FactoriaFalsa(handler));

            Assert.AreEqual("https://fotos.test/17.jpg?sig=x", await servicio.EnlaceFoto(17));
            Assert.AreEqual("/api/Almacen/Bultos/17/Foto", handler.Urls[0]);

            handler.Codigo = HttpStatusCode.NotFound;
            Assert.IsNull(await servicio.EnlaceFoto(18));
        }

        [TestMethod]
        public void Proponer_ConElValorPorDefecto_PoneLosDeAriadna()
        {
            (int bultos, string aviso) = PropuestaBultosAriadna.Proponer(1, new[] { Bulto(17), Bulto(18) });

            Assert.AreEqual(2, bultos);
            Assert.IsNull(aviso);
        }

        [TestMethod]
        public void Proponer_UnBultoCompartidoQueLlegaDosVeces_CuentaUnaVez()
        {
            (int bultos, _) = PropuestaBultosAriadna.Proponer(1, new[] { Bulto(17), Bulto(17), Bulto(18) });

            Assert.AreEqual(2, bultos);
        }

        [TestMethod]
        public void Proponer_YaTecleadoOtroNumero_NoLoPisaYAvisa()
        {
            (int bultos, string aviso) = PropuestaBultosAriadna.Proponer(3, new[] { Bulto(17), Bulto(18) });

            Assert.AreEqual(3, bultos);
            StringAssert.Contains(aviso, "2 bultos");
            StringAssert.Contains(aviso, "lleva 3");
        }

        [TestMethod]
        public void Proponer_SinBultosDeAriadna_TodoComoHoy()
        {
            (int bultos, string aviso) = PropuestaBultosAriadna.Proponer(1, new List<BultoAriadna>());

            Assert.AreEqual(1, bultos);
            Assert.IsNull(aviso);
            Assert.AreEqual(string.Empty, PropuestaBultosAriadna.Texto(null));
        }

        [TestMethod]
        public void Texto_DiceCuantosBultosYCuantosConFoto()
        {
            Assert.AreEqual("Ariadna: 2 bultos (1 con foto)", PropuestaBultosAriadna.Texto(new[] { Bulto(17), Bulto(18, foto: false) }));
        }
    }
}
