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
    /// NestoAPI#593 (c5): el cheque regalo del cliente para la plantilla y el detalle del pedido. La API decide; Nesto lo
    /// pregunta (GET api/ChequesRegalo/Cliente), lo enseña y calcula de forma aproximada cuánto falta para el mínimo.
    /// </summary>
    [TestClass]
    public class ChequesRegaloTests
    {
        private const string DISPONIBLE = "{\"Campana\":\"CHEQUE50_OCT_2026\",\"Empresa\":\"1\",\"Cliente\":\"15191\",\"Producto\":\"CHEQUE50_OCT26\"," +
            "\"Importe\":50.00,\"MinimoCanje\":250.00,\"CanjeHasta\":\"2026-11-07T00:00:00\",\"PrefijosNombreExcluidosMinimo\":[\"PACK 26\"]," +
            "\"GruposExcluidosMinimo\":[\"PEL\"],\"Estado\":\"Disponible\",\"SePuedeUsar\":true," +
            "\"Mensaje\":\"Tiene un cheque regalo de 50,00 € + IVA para un pedido de más de 250,00 € de producto, hasta el 07/11/2026.\"," +
            "\"EmpresaFactura\":\"1\",\"FacturaOrigen\":\"NV2612345\",\"PedidoCanje\":null," +
            "\"TextoLinea\":\"Cheque regalo 50 € (campaña CHEQUE50_OCT_2026)\"}";

        internal static ChequeRegaloClienteDTO Cheque(string estado = ChequeRegaloClienteDTO.ESTADO_DISPONIBLE, int? pedidoCanje = null) => new ChequeRegaloClienteDTO
        {
            Campana = "CHEQUE50_OCT_2026",
            Empresa = "1",
            Cliente = "15191",
            Producto = "CHEQUE50_OCT26",
            Importe = 50M,
            MinimoCanje = 250M,
            CanjeHasta = new DateTime(2026, 11, 7),
            PrefijosNombreExcluidosMinimo = new List<string> { "PACK 26" },
            GruposExcluidosMinimo = new List<string> { "PEL" },
            Estado = estado,
            SePuedeUsar = estado == ChequeRegaloClienteDTO.ESTADO_DISPONIBLE,
            PedidoCanje = pedidoCanje,
            Mensaje = estado == ChequeRegaloClienteDTO.ESTADO_DISPONIBLE
                ? "Tiene un cheque regalo de 50,00 € + IVA para un pedido de más de 250,00 € de producto, hasta el 07/11/2026."
                : $"El cheque regalo ya está aplicado en el pedido {pedidoCanje}.",
            TextoLinea = "Cheque regalo 50 € (campaña CHEQUE50_OCT_2026)"
        };

        #region Servicio

        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = "null";
            public bool Lanzar;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.Method + " " + request.RequestUri.PathAndQuery);
                if (Lanzar)
                {
                    throw new HttpRequestException("Sin conexión");
                }
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
        public async Task LeerDelCliente_PideConEmpresaYCliente_YLoDevuelve()
        {
            var handler = new HandlerFalso { Respuesta = DISPONIBLE };
            var servicio = new ServicioChequesRegalo(new FactoriaFalsa(handler));

            ChequeRegaloClienteDTO cheque = await servicio.LeerDelCliente("1  ", " 15191 ");

            Assert.AreEqual("GET /api/ChequesRegalo/Cliente?empresa=1&cliente=15191", handler.Urls[0]);
            Assert.AreEqual("CHEQUE50_OCT26", cheque.Producto);
            Assert.AreEqual(50M, cheque.Importe);
            Assert.AreEqual(250M, cheque.MinimoCanje);
            Assert.IsTrue(cheque.SePuedeUsar);
            CollectionAssert.AreEqual(new[] { "PACK 26" }, cheque.PrefijosNombreExcluidosMinimo);
            CollectionAssert.AreEqual(new[] { "PEL" }, cheque.GruposExcluidosMinimo);
        }

        [TestMethod]
        public async Task LeerDelCliente_404ErrorOSinConexion_EsNull_SinLanzar()
        {
            var sinCheque = new ServicioChequesRegalo(new FactoriaFalsa(new HandlerFalso { Codigo = HttpStatusCode.NotFound, Respuesta = "" }));
            var error = new ServicioChequesRegalo(new FactoriaFalsa(new HandlerFalso { Codigo = HttpStatusCode.InternalServerError, Respuesta = "{}" }));
            var sinConexion = new ServicioChequesRegalo(new FactoriaFalsa(new HandlerFalso { Lanzar = true }));

            Assert.IsNull(await sinCheque.LeerDelCliente("1", "15191"), "404 = sin cheque o campaña apagada: no se enseña nada");
            Assert.IsNull(await error.LeerDelCliente("1", "15191"));
            Assert.IsNull(await sinConexion.LeerDelCliente("1", "15191"), "Es una ayuda: si falla, no se enseña nada");
        }

        [TestMethod]
        public async Task LeerDelCliente_SinCliente_NoLlamaALaApi()
        {
            var handler = new HandlerFalso { Respuesta = DISPONIBLE };
            var servicio = new ServicioChequesRegalo(new FactoriaFalsa(handler));

            Assert.IsNull(await servicio.LeerDelCliente("1", "  "));
            Assert.AreEqual(0, handler.Urls.Count);
        }

        #endregion

        #region Reglas

        [TestMethod]
        public void CuentaParaElMinimo_FueraElChequeLosFicticiosPack26YPel()
        {
            ChequeRegaloClienteDTO cheque = Cheque();

            Assert.IsTrue(ReglasChequeRegalo.CuentaParaElMinimo(cheque, "12345", "CREMA MASAJE 1L", "COS"));
            Assert.IsTrue(ReglasChequeRegalo.CuentaParaElMinimo(cheque, "12345", "CREMA MASAJE 1L", null), "Sin grupo conocido, cuenta (aproximado)");
            Assert.IsFalse(ReglasChequeRegalo.CuentaParaElMinimo(cheque, "cheque50_oct26 ", "Cheque regalo", "COS"), "El propio cheque");
            Assert.IsFalse(ReglasChequeRegalo.CuentaParaElMinimo(cheque, "12345", "PACK 26 NAVIDAD", "COS"));
            Assert.IsFalse(ReglasChequeRegalo.CuentaParaElMinimo(cheque, "12345", "pack 26 navidad", "COS"), "Sin mirar mayúsculas");
            Assert.IsFalse(ReglasChequeRegalo.CuentaParaElMinimo(cheque, "12345", "TINTE 60ML", "PEL "));
            Assert.IsFalse(ReglasChequeRegalo.CuentaParaElMinimo(cheque, "62400003", "PORTES", "COS", esFicticio: true));
        }

        [TestMethod]
        public void Falta_HayQueSuperarElMinimo_ComoLaApi()
        {
            ChequeRegaloClienteDTO cheque = Cheque();

            Assert.AreEqual(50.01M, ReglasChequeRegalo.Falta(cheque, 200M));
            Assert.AreEqual(0.01M, ReglasChequeRegalo.Falta(cheque, 250M), "250 justos no superan los 250");
            Assert.AreEqual(0M, ReglasChequeRegalo.Falta(cheque, 250.01M));
            Assert.IsNull(ReglasChequeRegalo.TextoFalta(cheque, 300M));
            StringAssert.Contains(ReglasChequeRegalo.TextoFalta(cheque, 200M), "Faltan 50,01 €");
            StringAssert.Contains(ReglasChequeRegalo.TextoFalta(cheque, 200M), "superar los 250 €");
        }

        [TestMethod]
        public void Textos_ConTildesYElImporte()
        {
            ChequeRegaloClienteDTO cheque = Cheque();

            Assert.AreEqual("Usar el cheque regalo de 50 €", ReglasChequeRegalo.TextoUsar(cheque));
            Assert.AreEqual("Añadir el cheque regalo de 50 €", ReglasChequeRegalo.TextoAnadir(cheque));
            Assert.AreEqual("Cheque regalo 50 € (campaña CHEQUE50_OCT_2026)", ReglasChequeRegalo.TextoLinea(cheque));
            Assert.AreEqual(cheque.Mensaje, ReglasChequeRegalo.Mensaje(cheque), "El mensaje de la API, tal cual");
        }

        [TestMethod]
        public void Vista_SinCheque_NoEnsenaNada_YConChequeEnPedido_SoloInforma()
        {
            var vista = new ChequeRegaloVista();
            Assert.IsFalse(vista.HayCheque);
            Assert.IsFalse(vista.SePuedeUsar);

            vista.Aplicar(Cheque(ChequeRegaloClienteDTO.ESTADO_EN_PEDIDO, 928123));
            Assert.IsTrue(vista.HayCheque);
            Assert.IsFalse(vista.SePuedeUsar);
            Assert.IsTrue(vista.NoSePuedeUsar);
            Assert.AreEqual("El cheque regalo ya está aplicado en el pedido 928123.", vista.Mensaje);

            vista.Limpiar();
            Assert.IsFalse(vista.HayCheque);
        }

        #endregion
    }
}
