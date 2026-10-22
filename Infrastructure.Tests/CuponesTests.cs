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
    /// Nesto#518 (NestoAPI#613/#614): los cupones de descuento. La API decide; Nesto pregunta, enseña, emite (manuales), anula y
    /// manda la línea con el código.
    /// </summary>
    [TestClass]
    public class CuponesTests
    {
        private const string ACTIVO = "{\"Id\":7,\"Codigo\":\"EV-2610-7K3Q\",\"Empresa\":\"1\",\"Cliente\":\"15191\",\"Contacto\":\"0\",\"Importe\":50.00," +
            "\"PedidoMinimo\":100.00,\"Origen\":\"Evento\",\"EventoId\":1,\"Evento\":\"Masterclass Cloasma\",\"Motivo\":null," +
            "\"FechaEmision\":\"2026-10-15T10:00:00\",\"ValidoDesde\":\"2026-10-30T00:00:00\",\"Caducidad\":\"2027-04-30T00:00:00\"," +
            "\"Estado\":\"Activo\",\"SePuedeUsar\":true," +
            "\"Mensaje\":\"Tiene un cupón de 50,00 € (EV-2610-7K3Q) para un pedido de al menos 100,00 € de productos, hasta el 30/04/2027.\"," +
            "\"Producto\":\"CUPONEVENTO\",\"TextoLinea\":\"Cupón EV-2610-7K3Q (Masterclass Cloasma)\",\"UsadoEnPedido\":null,\"FacturaUso\":null," +
            "\"FechaUso\":null,\"FacturaOrigen\":\"CV2600700\"}";

        internal static CuponDTO Cupon(string estado = ReglasCupon.ESTADO_ACTIVO, int? usadoEnPedido = null, string codigo = "EV-2610-7K3Q") => new CuponDTO
        {
            Id = 7,
            Codigo = codigo,
            Empresa = "1",
            Cliente = "15191",
            Contacto = "0",
            Importe = 50M,
            PedidoMinimo = 100M,
            Origen = ReglasCupon.ORIGEN_EVENTO,
            Evento = "Masterclass Cloasma",
            ValidoDesde = new DateTime(2026, 10, 30),
            Caducidad = new DateTime(2027, 4, 30),
            Estado = estado,
            SePuedeUsar = estado == ReglasCupon.ESTADO_ACTIVO,
            UsadoEnPedido = usadoEnPedido,
            Producto = "CUPONEVENTO",
            TextoLinea = $"Cupón {codigo} (Masterclass Cloasma)",
            Mensaje = estado == ReglasCupon.ESTADO_ACTIVO
                ? $"Tiene un cupón de 50,00 € ({codigo}) para un pedido de al menos 100,00 € de productos, hasta el 30/04/2027."
                : $"El cupón {codigo} está {estado}."
        };

        #region Servicio

        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> Cuerpos = new List<string>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = "null";
            public bool Lanzar;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.Method + " " + request.RequestUri.PathAndQuery);
                Cuerpos.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync());
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

        [TestMethod]
        public async Task LeerDelCliente_PideLaRutaDelClienteConTodos_YLaDevuelve()
        {
            var handler = new HandlerFalso { Respuesta = "[" + ACTIVO + "]" };
            var servicio = new ServicioCupones(new FactoriaFalsa(handler));

            List<CuponDTO> cupones = await servicio.LeerDelCliente("1", "15191  ", true);

            Assert.AreEqual("GET /api/Clientes/1/15191/Cupones?todos=true", handler.Urls[0]);
            Assert.AreEqual(1, cupones.Count);
            Assert.AreEqual("EV-2610-7K3Q", cupones[0].Codigo);
            Assert.AreEqual(50M, cupones[0].Importe);
            Assert.AreEqual(100M, cupones[0].PedidoMinimo);
            Assert.IsTrue(cupones[0].SePuedeUsar);
            Assert.AreEqual("CUPONEVENTO", cupones[0].Producto);
            Assert.AreEqual(new DateTime(2027, 4, 30), cupones[0].Caducidad);
        }

        [TestMethod]
        public async Task LeerDelCliente_SinEmpresa_LaDeDefecto_YSinTodos()
        {
            var handler = new HandlerFalso { Respuesta = "[]" };
            var servicio = new ServicioCupones(new FactoriaFalsa(handler));

            List<CuponDTO> cupones = await servicio.LeerDelCliente(null, "15191", false);

            Assert.AreEqual("GET /api/Clientes/1/15191/Cupones?todos=false", handler.Urls[0]);
            Assert.AreEqual(0, cupones.Count);
        }

        [TestMethod]
        public async Task LeerDelCliente_ApiSinCupones404_ListaVacia_YErrorOSinConexion_Null()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.NotFound, Respuesta = "" };
            var servicio = new ServicioCupones(new FactoriaFalsa(handler));
            Assert.AreEqual(0, (await servicio.LeerDelCliente("1", "15191", false)).Count, "API anterior a #613: como si no tuviera");

            handler.Codigo = HttpStatusCode.InternalServerError;
            Assert.IsNull(await servicio.LeerDelCliente("1", "15191", false), "Null = no se han podido leer");

            handler.Lanzar = true;
            Assert.IsNull(await servicio.LeerDelCliente("1", "15191", false));
        }

        [TestMethod]
        public async Task LeerDelCliente_SinCliente_NoLlama()
        {
            var handler = new HandlerFalso();
            var servicio = new ServicioCupones(new FactoriaFalsa(handler));

            Assert.AreEqual(0, (await servicio.LeerDelCliente("1", " ", false)).Count);
            Assert.AreEqual(0, handler.Urls.Count);
        }

        [TestMethod]
        public async Task LeerPorCodigo_PideElCodigo_Y404EsNull()
        {
            var handler = new HandlerFalso { Respuesta = ACTIVO };
            var servicio = new ServicioCupones(new FactoriaFalsa(handler));

            CuponDTO cupon = await servicio.LeerPorCodigo(" EV-2610-7K3Q ");
            Assert.AreEqual("GET /api/Cupones/EV-2610-7K3Q", handler.Urls[0]);
            Assert.AreEqual("15191", cupon.Cliente);

            handler.Codigo = HttpStatusCode.NotFound;
            Assert.IsNull(await servicio.LeerPorCodigo("EV-2610-XXXX"));
            handler.Lanzar = true;
            Assert.IsNull(await servicio.LeerPorCodigo("EV-2610-XXXX"));
        }

        [TestMethod]
        public async Task CrearManual_MandaElCuerpo_YDevuelveElCupon()
        {
            var handler = new HandlerFalso { Respuesta = ACTIVO.Replace("EV-2610-7K3Q", "MN-2610-AB2C") };
            var servicio = new ServicioCupones(new FactoriaFalsa(handler));

            CuponDTO cupon = await servicio.CrearManual(new CrearCuponManualDTO
            {
                Empresa = "1", Cliente = "15191", Contacto = "0", Importe = 30M, Motivo = "Compensación por el retraso", ValidoDesde = new DateTime(2026, 10, 22)
            });

            Assert.AreEqual("POST /api/Cupones", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual("15191", (string)cuerpo["Cliente"]);
            Assert.AreEqual(30M, (decimal)cuerpo["Importe"]);
            Assert.AreEqual("Compensación por el retraso", (string)cuerpo["Motivo"]);
            Assert.AreEqual("MN-2610-AB2C", cupon.Codigo);
        }

        [TestMethod]
        public async Task CrearManual_SinPermiso_LanzaConElMotivoDeLaApi()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.Forbidden, Respuesta = "{\"Message\":\"Solo Dirección puede emitir cupones manuales.\"}" };
            var servicio = new ServicioCupones(new FactoriaFalsa(handler));

            var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                servicio.CrearManual(new CrearCuponManualDTO { Empresa = "1", Cliente = "15191", Importe = 30M, Motivo = "x" }));
            StringAssert.Contains(ex.Message, "Solo Dirección puede emitir cupones manuales.");
        }

        [TestMethod]
        public async Task Anular_MandaElMotivo_YSiYaSeUso_LanzaConElMotivo()
        {
            var handler = new HandlerFalso { Respuesta = ACTIVO.Replace("\"Activo\"", "\"Anulado\"") };
            var servicio = new ServicioCupones(new FactoriaFalsa(handler));

            CuponDTO cupon = await servicio.Anular("EV-2610-7K3Q", " Devolución del curso. ");
            Assert.AreEqual("POST /api/Cupones/EV-2610-7K3Q/Anular", handler.Urls[0]);
            Assert.AreEqual("Devolución del curso.", (string)JObject.Parse(handler.Cuerpos[0])["Motivo"]);
            Assert.AreEqual("Anulado", cupon.Estado);

            handler.Codigo = HttpStatusCode.Conflict;
            handler.Respuesta = "{\"error\":{\"message\":\"El cupón EV-2610-7K3Q ya se usó en el pedido 930001: no se anula.\"}}";
            var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => servicio.Anular("EV-2610-7K3Q", null));
            StringAssert.Contains(ex.Message, "ya se usó en el pedido 930001");
        }

        [TestMethod]
        public async Task AvisarUsoEnTienda_PostUsar_Y404EsFalseSinLanzar()
        {
            var handler = new HandlerFalso { Respuesta = "{}" };
            var servicio = new ServicioCupones(new FactoriaFalsa(handler));

            bool ok = await servicio.AvisarUsoEnTienda("EV-2610-7K3Q", new UsoCuponTiendaDTO { Empresa = "1", Pedido = 930001, Canal = "PrestaShop", PedidoCanal = "ABCDEF", ImporteDescontado = 50M });

            Assert.IsTrue(ok);
            Assert.AreEqual("POST /api/Cupones/EV-2610-7K3Q/Usar", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual(930001, (int)cuerpo["Pedido"]);
            Assert.AreEqual("PrestaShop", (string)cuerpo["Canal"]);

            handler.Codigo = HttpStatusCode.NotFound;
            Assert.IsFalse(await servicio.AvisarUsoEnTienda("EV-2610-7K3Q", new UsoCuponTiendaDTO()), "API sin #614");
            handler.Lanzar = true;
            Assert.IsFalse(await servicio.AvisarUsoEnTienda("EV-2610-7K3Q", new UsoCuponTiendaDTO()));
        }

        #endregion

        #region Reglas

        [TestMethod]
        public void BaseLinea_DescuentaElImporteExacto_ConIvaConRecargoYSinIva()
        {
            Assert.AreEqual(41.3223M, ReglasCupon.BaseLinea(50M, true), "50 € al 21 %: base 41,32");
            Assert.AreEqual(39.6197M, ReglasCupon.BaseLinea(50M, true, 21M, 5.2M), "Con recargo, la base que da 50 € exactos");
            Assert.AreEqual(50M, ReglasCupon.BaseLinea(50M, false), "Pedido sin IVA: la base es el importe");
            Assert.AreEqual(50.00M, Math.Round(ReglasCupon.BaseLinea(50M, true) * 1.21M, 2, MidpointRounding.AwayFromZero));
        }

        [TestMethod]
        public void EsCodigoCupon_EventoYManual_SinMirarMayusculas()
        {
            Assert.IsTrue(ReglasCupon.EsCodigoCupon("EV-2610-7K3Q"));
            Assert.IsTrue(ReglasCupon.EsCodigoCupon(" mn-2610-ab2c "));
            Assert.IsFalse(ReglasCupon.EsCodigoCupon("NVCHEQUE-CHEQUE50_OCT_2026-15191"), "El cheque regalo no es un cupón");
            Assert.IsFalse(ReglasCupon.EsCodigoCupon("VERANO10"));
            Assert.IsFalse(ReglasCupon.EsCodigoCupon(null));
        }

        [TestMethod]
        public void EsLineaDeCupon_ProductoDelDtoOElDeSiempre()
        {
            Assert.IsTrue(ReglasCupon.EsLineaDeCupon("CUPONEVENTO   "));
            Assert.IsTrue(ReglasCupon.EsLineaDeCupon("cuponevento", Cupon()));
            Assert.IsFalse(ReglasCupon.EsLineaDeCupon("TiCKET"));
            var otro = Cupon();
            otro.Producto = "CUPON2";
            Assert.IsTrue(ReglasCupon.EsLineaDeCupon("CUPON2", otro));
            Assert.IsFalse(ReglasCupon.EsLineaDeCupon("CUPONEVENTO", otro));
        }

        [TestMethod]
        public void ElegirParaPedido_ElDelPedido_LuegoElQueSePuedeUsar_LuegoElQueValdra()
        {
            CuponDTO usadoAqui = Cupon(ReglasCupon.ESTADO_USADO, 930001, "EV-2610-AAAA");
            CuponDTO usadoOtro = Cupon(ReglasCupon.ESTADO_USADO, 929999, "EV-2610-BBBB");
            CuponDTO activo = Cupon(ReglasCupon.ESTADO_ACTIVO, null, "EV-2610-CCCC");
            CuponDTO emitido = Cupon(ReglasCupon.ESTADO_EMITIDO, null, "EV-2610-DDDD");
            CuponDTO caducado = Cupon(ReglasCupon.ESTADO_CADUCADO, null, "EV-2610-EEEE");

            Assert.AreSame(usadoAqui, ReglasCupon.ElegirParaPedido(new[] { activo, usadoOtro, usadoAqui }, 930001));
            Assert.AreSame(activo, ReglasCupon.ElegirParaPedido(new[] { emitido, usadoOtro, activo }, null));
            Assert.AreSame(emitido, ReglasCupon.ElegirParaPedido(new[] { caducado, usadoOtro, emitido }, 930001));
            Assert.IsNull(ReglasCupon.ElegirParaPedido(new[] { caducado, usadoOtro }, null), "Ni usados en otro pedido ni caducados");
            Assert.IsNull(ReglasCupon.ElegirParaPedido(null, null));
        }

        [TestMethod]
        public void Falta_100JustosValen_YElTextoDiceCuanto()
        {
            Assert.AreEqual(0M, ReglasCupon.Falta(Cupon(), 100M), "Como la API: 100,00 € justos valen");
            Assert.AreEqual(20M, ReglasCupon.Falta(Cupon(), 80M));
            Assert.IsNull(ReglasCupon.TextoFalta(Cupon(), 120M));
            StringAssert.StartsWith(ReglasCupon.TextoFalta(Cupon(), 80M), "Faltan 20,00 € de productos (IVA incluido, sin cursos ni portes) para el pedido mínimo del cupón, 100 €");
        }

        [TestMethod]
        public void Textos_DeUsted_ConElImporte()
        {
            Assert.AreEqual("Usar el cupón de 50 €", ReglasCupon.TextoUsar(Cupon()));
            Assert.AreEqual("Añadir el cupón de 50 €", ReglasCupon.TextoAnadir(Cupon()));
            Assert.AreEqual("Cupón EV-2610-7K3Q (Masterclass Cloasma)", ReglasCupon.TextoLinea(Cupon()));
            var sinTexto = Cupon();
            sinTexto.TextoLinea = null;
            sinTexto.Mensaje = null;
            Assert.AreEqual("Cupón EV-2610-7K3Q", ReglasCupon.TextoLinea(sinTexto));
            StringAssert.StartsWith(ReglasCupon.Mensaje(sinTexto), "Tiene un cupón de 50 € (EV-2610-7K3Q) para un pedido de al menos 100 €");
        }

        [TestMethod]
        public void Situacion_ParaLaFicha()
        {
            var usado = Cupon(ReglasCupon.ESTADO_USADO, 930001);
            usado.FacturaUso = "NV2612345";
            usado.FechaUso = new DateTime(2026, 11, 3);
            Assert.AreEqual("Usado en el pedido 930001 (factura NV2612345) el 03/11/2026", ReglasCupon.Situacion(usado));
            Assert.AreEqual("Vale desde el 30/10/2026", ReglasCupon.Situacion(Cupon(ReglasCupon.ESTADO_EMITIDO)));
            Assert.AreEqual("Se puede usar hasta el 30/04/2027", ReglasCupon.Situacion(Cupon()));
            Assert.AreEqual("Caducó el 30/04/2027", ReglasCupon.Situacion(Cupon(ReglasCupon.ESTADO_CADUCADO)));
            Assert.IsTrue(ReglasCupon.SePuedeAnular(Cupon()));
            Assert.IsTrue(ReglasCupon.SePuedeAnular(Cupon(ReglasCupon.ESTADO_EMITIDO)));
            Assert.IsFalse(ReglasCupon.SePuedeAnular(usado));
        }

        [TestMethod]
        public void EstaEnElPedido_SoloUsadoEnEsePedido()
        {
            Assert.IsTrue(ReglasCupon.EstaEnElPedido(Cupon(ReglasCupon.ESTADO_USADO, 930001), 930001));
            Assert.IsFalse(ReglasCupon.EstaEnElPedido(Cupon(ReglasCupon.ESTADO_USADO, 930001), 929999));
            Assert.IsFalse(ReglasCupon.EstaEnElPedido(Cupon(ReglasCupon.ESTADO_USADO, 930001), null));
            Assert.IsFalse(ReglasCupon.EstaEnElPedido(Cupon(), 930001));
        }

        [TestMethod]
        public void Vista_SinCuponNadaYConCuponLoQueDiceLaApi()
        {
            var vista = new CuponVista();
            Assert.IsFalse(vista.HayCupon);
            Assert.AreEqual(0M, vista.Importe);

            vista.Aplicar(Cupon(ReglasCupon.ESTADO_EMITIDO));
            Assert.IsTrue(vista.HayCupon);
            Assert.IsFalse(vista.SePuedeUsar);
            Assert.IsTrue(vista.NoSePuedeUsar);

            vista.Aplicar(Cupon());
            Assert.IsTrue(vista.SePuedeUsar);
            Assert.AreEqual("EV-2610-7K3Q", vista.Codigo);
            Assert.AreEqual(50M, vista.Importe);

            vista.Limpiar();
            Assert.IsFalse(vista.HayCupon);
        }

        #endregion
    }
}
