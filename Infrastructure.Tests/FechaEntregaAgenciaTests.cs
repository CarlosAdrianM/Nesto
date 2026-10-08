using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Models;
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
    /// NestoAPI#606 (corte 2): qué día entregamos el pedido a la agencia. La API lo calcula; Nesto lo pide
    /// (POST desde la plantilla, GET desde el detalle) y lo redacta para el usuario.
    /// </summary>
    [TestClass]
    public class FechaEntregaAgenciaTests
    {
        // Martes 13/10/2026: «hoy» fijo para que los textos no dependan del día en que se pasan los tests.
        private static readonly DateTime Hoy = new DateTime(2026, 10, 13);

        private static FechaEntregaAgenciaDTO Fecha(DateTime? aplica, string cual = FechaEntregaAgenciaDTO.APLICA_PRIMERA,
            DateTime? primera = null, DateTime? completa = null, DateTime? prometida = null, string motivo = "Motivo de la API.")
        {
            return new FechaEntregaAgenciaDTO
            {
                FechaEntregaAgencia = aplica,
                Aplica = cual,
                PrimeraEntrega = primera ?? aplica,
                EntregaCompleta = completa ?? aplica,
                FechaPrometida = prometida,
                Motivo = motivo
            };
        }

        #region Textos

        [TestMethod]
        public void Texto_UnDiaCualquiera_DiceElDiaDeLaSemanaYLaFecha()
        {
            Assert.AreEqual("Se entrega a la agencia el jueves 15/10",
                TextosFechaEntregaAgencia.Texto(Fecha(new DateTime(2026, 10, 15)), Hoy));
        }

        [TestMethod]
        public void Texto_Hoy_DiceHoy()
        {
            Assert.AreEqual("Se entrega a la agencia hoy", TextosFechaEntregaAgencia.Texto(Fecha(Hoy), Hoy));
        }

        [TestMethod]
        public void Texto_Manana_DiceManana()
        {
            Assert.AreEqual("Se entrega a la agencia mañana", TextosFechaEntregaAgencia.Texto(Fecha(Hoy.AddDays(1)), Hoy));
        }

        [TestMethod]
        public void Texto_LaHoraNoCuenta()
        {
            Assert.AreEqual("Se entrega a la agencia mañana",
                TextosFechaEntregaAgencia.Texto(Fecha(Hoy.AddDays(1).AddHours(9)), Hoy.AddHours(17)));
        }

        [TestMethod]
        public void Texto_SinFecha_DiceSinFechaTodavia()
        {
            Assert.AreEqual("Sin fecha todavía", TextosFechaEntregaAgencia.Texto(Fecha(null), Hoy));
        }

        [TestMethod]
        public void Texto_SinRespuesta_NoHayTexto()
        {
            Assert.IsNull(TextosFechaEntregaAgencia.Texto(null, Hoy));
        }

        [TestMethod]
        public void Texto_PrimeraConLaCompletaOtroDia_AnadeCuandoQuedaCompleto()
        {
            var dto = Fecha(new DateTime(2026, 10, 14), FechaEntregaAgenciaDTO.APLICA_PRIMERA, completa: new DateTime(2026, 10, 19));

            Assert.AreEqual("Se entrega a la agencia mañana · completo el lunes 19/10", TextosFechaEntregaAgencia.Texto(dto, Hoy));
        }

        [TestMethod]
        public void Texto_PrimeraConLaCompletaElMismoDia_NoRepiteLaFecha()
        {
            var dto = Fecha(new DateTime(2026, 10, 15), FechaEntregaAgenciaDTO.APLICA_PRIMERA, completa: new DateTime(2026, 10, 15));

            Assert.AreEqual("Se entrega a la agencia el jueves 15/10", TextosFechaEntregaAgencia.Texto(dto, Hoy));
        }

        [TestMethod]
        public void Texto_Completa_NoAnadeNada()
        {
            var dto = Fecha(new DateTime(2026, 10, 19), FechaEntregaAgenciaDTO.APLICA_COMPLETA, primera: new DateTime(2026, 10, 14));

            Assert.AreEqual("Se entrega a la agencia el lunes 19/10", TextosFechaEntregaAgencia.Texto(dto, Hoy));
        }

        [TestMethod]
        public void Texto_OtroAnno_LlevaElAnno()
        {
            Assert.AreEqual("Se entrega a la agencia el lunes 04/01/2027",
                TextosFechaEntregaAgencia.Texto(Fecha(new DateTime(2027, 1, 4)), Hoy));
        }

        [TestMethod]
        public void Prometida_DistintaDeLaCalculada_SeEnsena()
        {
            var dto = Fecha(new DateTime(2026, 10, 16), prometida: new DateTime(2026, 10, 15));

            Assert.AreEqual("Prometida: jueves 15/10", TextosFechaEntregaAgencia.TextoPrometida(dto, Hoy));
        }

        [TestMethod]
        public void Prometida_HoyOManana_TambienSeDiceAsi()
        {
            Assert.AreEqual("Prometida: hoy", TextosFechaEntregaAgencia.TextoPrometida(Fecha(Hoy.AddDays(2), prometida: Hoy), Hoy));
            Assert.AreEqual("Prometida: mañana", TextosFechaEntregaAgencia.TextoPrometida(Fecha(Hoy.AddDays(2), prometida: Hoy.AddDays(1)), Hoy));
        }

        [TestMethod]
        public void Prometida_IgualQueLaCalculada_NoSeEnsena()
        {
            Assert.IsNull(TextosFechaEntregaAgencia.TextoPrometida(Fecha(new DateTime(2026, 10, 15), prometida: new DateTime(2026, 10, 15)), Hoy));
        }

        [TestMethod]
        public void Prometida_SinPrometida_NoSeEnsena()
        {
            Assert.IsNull(TextosFechaEntregaAgencia.TextoPrometida(Fecha(new DateTime(2026, 10, 15)), Hoy));
            Assert.IsNull(TextosFechaEntregaAgencia.TextoPrometida(null, Hoy));
        }

        [TestMethod]
        public void Prometida_ConLaCalculadaSinFecha_SeEnsena()
        {
            Assert.AreEqual("Prometida: jueves 15/10", TextosFechaEntregaAgencia.TextoPrometida(Fecha(null, prometida: new DateTime(2026, 10, 15)), Hoy));
        }

        [TestMethod]
        public void Vista_AplicarYLimpiar()
        {
            var vista = new FechaEntregaAgenciaVista();
            Assert.IsFalse(vista.HayTexto);

            vista.Aplicar(Fecha(new DateTime(2026, 10, 16), prometida: new DateTime(2026, 10, 15), motivo: "Falta la reposición de Reina."), Hoy);

            Assert.IsTrue(vista.HayTexto);
            Assert.AreEqual("Se entrega a la agencia el viernes 16/10", vista.Texto);
            Assert.AreEqual("Falta la reposición de Reina.", vista.Motivo);
            Assert.IsTrue(vista.HayPrometida);
            Assert.AreEqual("Prometida: jueves 15/10", vista.TextoPrometida);

            vista.Aplicar(null, Hoy);

            Assert.IsFalse(vista.HayTexto);
            Assert.IsNull(vista.Texto);
            Assert.IsFalse(vista.HayPrometida);
        }

        [TestMethod]
        public void Vista_AvisaDeLosCambios()
        {
            var vista = new FechaEntregaAgenciaVista();
            var cambios = new List<string>();
            vista.PropertyChanged += (s, e) => cambios.Add(e.PropertyName);

            vista.Aplicar(Fecha(Hoy), Hoy);

            CollectionAssert.Contains(cambios, nameof(FechaEntregaAgenciaVista.Texto));
            CollectionAssert.Contains(cambios, nameof(FechaEntregaAgenciaVista.HayTexto));
        }

        #endregion

        #region Servicio

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

        private const string RESPUESTA = "{\"PrimeraEntrega\":\"2026-10-14T00:00:00\",\"EntregaCompleta\":\"2026-10-19T00:00:00\"," +
            "\"FechaEntregaAgencia\":\"2026-10-14T00:00:00\",\"Aplica\":\"Primera\",\"Entregas\":[\"2026-10-14T00:00:00\",\"2026-10-19T00:00:00\"]," +
            "\"Motivo\":\"Lo que hay sale mañana.\",\"FechaPrometida\":\"2026-10-15T00:00:00\"}";

        [TestMethod]
        public async Task CalcularPlantilla_MandaElPedidoPorPost()
        {
            var handler = new HandlerFalso { Respuesta = RESPUESTA };
            var servicio = new ServicioFechaEntregaAgencia(new FactoriaFalsa(handler));

            FechaEntregaAgenciaDTO fecha = await servicio.CalcularPlantilla(new PedidoVentaDTO { empresa = "1", cliente = "15191", modoServicio = 3 });

            Assert.AreEqual("POST /api/PedidosVenta/FechaEntregaAgencia", handler.Urls[0]);
            Assert.AreEqual("15191", (string)JObject.Parse(handler.Cuerpos[0])["cliente"]);
            Assert.AreEqual(new DateTime(2026, 10, 14), fecha.FechaEntregaAgencia);
            Assert.AreEqual(new DateTime(2026, 10, 19), fecha.EntregaCompleta);
            Assert.AreEqual("Primera", fecha.Aplica);
            Assert.AreEqual(2, fecha.Entregas.Count);
            Assert.AreEqual("Lo que hay sale mañana.", fecha.Motivo);
            Assert.AreEqual(new DateTime(2026, 10, 15), fecha.FechaPrometida);
        }

        [TestMethod]
        public async Task CalcularPedido_PideElPedidoPorGet()
        {
            var handler = new HandlerFalso { Respuesta = RESPUESTA };
            var servicio = new ServicioFechaEntregaAgencia(new FactoriaFalsa(handler));

            FechaEntregaAgenciaDTO fecha = await servicio.CalcularPedido("1  ", 928020);

            Assert.AreEqual("GET /api/PedidosVenta/1/928020/FechaEntregaAgencia", handler.Urls[0]);
            Assert.AreEqual(new DateTime(2026, 10, 14), fecha.FechaEntregaAgencia);
        }

        [TestMethod]
        public async Task ApiSinElEndpoint_404_DevuelveNullSinLanzar()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.NotFound, Respuesta = "{\"Message\":\"No HTTP resource was found\"}" };
            var servicio = new ServicioFechaEntregaAgencia(new FactoriaFalsa(handler));

            Assert.IsNull(await servicio.CalcularPlantilla(new PedidoVentaDTO()));
            Assert.IsNull(await servicio.CalcularPedido("1", 1));
        }

        [TestMethod]
        public async Task ErrorDelServidorOSinConexion_DevuelveNullSinLanzar()
        {
            var servicio500 = new ServicioFechaEntregaAgencia(new FactoriaFalsa(new HandlerFalso { Codigo = HttpStatusCode.InternalServerError }));
            var servicioCaido = new ServicioFechaEntregaAgencia(new FactoriaFalsa(new HandlerFalso { Lanzar = true }));

            Assert.IsNull(await servicio500.CalcularPlantilla(new PedidoVentaDTO()));
            Assert.IsNull(await servicioCaido.CalcularPedido("1", 1));
        }

        [TestMethod]
        public async Task RespuestaNull_EsSinFecha_NoSinRespuesta()
        {
            // La API contesta 200 con null: el pedido no tiene fecha (se enseña «Sin fecha todavía»), no es un fallo.
            var servicio = new ServicioFechaEntregaAgencia(new FactoriaFalsa(new HandlerFalso { Respuesta = "null" }));

            FechaEntregaAgenciaDTO fecha = await servicio.CalcularPlantilla(new PedidoVentaDTO());

            Assert.IsNotNull(fecha);
            Assert.IsNull(fecha.FechaEntregaAgencia);
            Assert.AreEqual("Sin fecha todavía", TextosFechaEntregaAgencia.Texto(fecha, Hoy));
        }

        [TestMethod]
        public async Task SinPedido_NoSePregunta()
        {
            var handler = new HandlerFalso();
            var servicio = new ServicioFechaEntregaAgencia(new FactoriaFalsa(handler));

            Assert.IsNull(await servicio.CalcularPlantilla(null));
            Assert.IsNull(await servicio.CalcularPedido("1", 0));
            Assert.AreEqual(0, handler.Urls.Count);
        }

        #endregion
    }
}
