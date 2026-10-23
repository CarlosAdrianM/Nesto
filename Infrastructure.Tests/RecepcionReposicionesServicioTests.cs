using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Newtonsoft.Json.Linq;
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
    /// <summary>
    /// Recibir una reposición en la tienda (NestoAPI#553): Nesto usa exactamente la misma API que Ariadna
    /// (api/Almacen/Recepciones, tipo REPO). Entra lo leído y el servidor informa de las diferencias.
    /// </summary>
    [TestClass]
    public class RecepcionReposicionesServicioTests
    {
        private sealed class HandlerFalso : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> Cuerpos = new List<string>();
            public readonly List<HttpRequestMessage> Peticiones = new List<HttpRequestMessage>();
            public HttpStatusCode Codigo = HttpStatusCode.OK;
            public string Respuesta = "{}";

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.Method + " " + request.RequestUri.PathAndQuery);
                Peticiones.Add(request);
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
        public async Task LeerPendientes_PideLasDelAlmacenYSoloDevuelveLasReposiciones()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "[{\"Tipo\":\"COMP\",\"Documento\":\"65\",\"Titulo\":\"Maystar\"}," +
                            "{\"Tipo\":\"REPO\",\"Documento\":\"80878\",\"Titulo\":\"Reposición 80878\",\"Lineas\":23,\"Unidades\":40}]"
            };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            List<RecepcionPendiente> pendientes = await servicio.LeerPendientes("1  ", "ALC");

            Assert.AreEqual("GET /api/Almacen/Recepciones?almacen=ALC&empresa=1", handler.Urls[0]);
            Assert.AreEqual(1, pendientes.Count, "Las compras de proveedor no se reciben desde aquí");
            Assert.AreEqual("80878", pendientes[0].Documento);
            Assert.AreEqual(23, pendientes[0].Lineas);
        }

        [TestMethod]
        public async Task LeerRecepcion_PideLaReposicionDeEseTraspaso()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "{\"Tipo\":\"REPO\",\"Documento\":\"80878\",\"PuedeTerminar\":true,\"SeTerminaDesdeAqui\":true," +
                            "\"Lineas\":[{\"Producto\":\"17404\",\"Descripcion\":\"Cera\",\"CodigoBarras\":\"8411\",\"Cantidad\":2}]}"
            };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            RecepcionReposicion recepcion = await servicio.LeerRecepcion("1", "ALC", "80878");

            Assert.AreEqual("GET /api/Almacen/Recepciones/REPO/80878?almacen=ALC&empresa=1", handler.Urls[0]);
            Assert.IsTrue(recepcion.PuedeTerminar);
            Assert.AreEqual("8411", recepcion.Lineas[0].CodigoBarras);
            Assert.AreEqual(2, recepcion.Lineas[0].Cantidad);
        }

        [TestMethod]
        public async Task Terminar_MandaLoLeidoConSuIdentificador()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "{\"Tipo\":\"REPO\",\"Documento\":\"80878\",\"Diferencias\":[{\"Producto\":\"17404\",\"Esperado\":2,\"Leido\":1}]," +
                            "\"AvisadoA\":\"NUEVAVISION\\\\Andre\",\"Avisos\":[\"Ha entrado lo leído\"]}"
            };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));
            var id = Guid.NewGuid();

            ResultadoRecepcionReposicion resultado = await servicio.Terminar("1", "ALC", "80878", new TerminarRecepcionReposicion
            {
                IdRecepcion = id,
                Lecturas = new List<LecturaRecepcionReposicion> { new LecturaRecepcionReposicion { Producto = "17404", Cantidad = 1 } }
            });

            Assert.AreEqual("POST /api/Almacen/Recepciones/REPO/80878/Terminar?almacen=ALC&empresa=1", handler.Urls[0]);
            JObject cuerpo = JObject.Parse(handler.Cuerpos[0]);
            Assert.AreEqual(id.ToString(), (string)cuerpo["IdRecepcion"]);
            Assert.AreEqual("17404", (string)cuerpo["Lecturas"][0]["Producto"]);
            Assert.AreEqual(-1, resultado.Diferencias[0].Diferencia);
            Assert.AreEqual(@"NUEVAVISION\Andre", resultado.AvisadoA);
        }

        [TestMethod]
        public async Task Terminar_SinPermiso_DaElMotivoDelServidor()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.Forbidden, Respuesta = "{\"Message\":\"Solo la puede recibir la tienda de destino.\"}" };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            RecepcionReposicionException ex = await Assert.ThrowsExceptionAsync<RecepcionReposicionException>(() =>
                servicio.Terminar("1", "ALC", "80878", new TerminarRecepcionReposicion { IdRecepcion = Guid.NewGuid() }));

            StringAssert.Contains(ex.Message, "tienda de destino");
        }

        // NestoAPI#673: recibir por partes
        [TestMethod]
        public async Task Terminar_Parcial_MandaParcialYLeeLoQueQuedaPendiente()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "{\"Tipo\":\"REPO\",\"Documento\":\"80878\",\"Parcial\":true,\"LineasPendientes\":2,\"UnidadesPendientes\":4}"
            };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            ResultadoRecepcionReposicion resultado = await servicio.Terminar("1", "ALC", "80878", new TerminarRecepcionReposicion
            {
                IdRecepcion = Guid.NewGuid(),
                Parcial = true,
                Lecturas = new List<LecturaRecepcionReposicion> { new LecturaRecepcionReposicion { Producto = "17404", Cantidad = 1 } }
            });

            Assert.IsTrue((bool)JObject.Parse(handler.Cuerpos[0])["Parcial"]);
            Assert.IsTrue(resultado.Parcial);
            Assert.AreEqual(2, resultado.LineasPendientes);
            Assert.AreEqual(4, resultado.UnidadesPendientes);
        }

        [TestMethod]
        public async Task LeerPendientes_TraeLoRecibidoEnParteYElAviso()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "[{\"Tipo\":\"REPO\",\"Documento\":\"80878\",\"RecibidaEnParte\":true," +
                            "\"TextoRecibidaEnParte\":\"Recibido en parte: 1 línea, 2 ud.\",\"AvisarRecibidaEnParte\":true," +
                            "\"AvisoRecibidaEnParte\":\"Lleva más de 2 días\"}]"
            };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            RecepcionPendiente pendiente = (await servicio.LeerPendientes("1", "ALC")).Single();

            Assert.IsTrue(pendiente.RecibidaEnParte);
            Assert.AreEqual("Recibido en parte: 1 línea, 2 ud.", pendiente.TextoRecibidaEnParte);
            Assert.IsTrue(pendiente.AvisarRecibidaEnParte);
            Assert.AreEqual("Lleva más de 2 días", pendiente.AvisoRecibidaEnParte);
        }

        // ---- Nesto#538: de qué producto es lo leído que no está en la reposición ----

        /// <summary>Contesta según el principio de la ruta pedida; lo que no conoce, 404.</summary>
        private sealed class HandlerPorRuta : HttpMessageHandler
        {
            public readonly List<string> Urls = new List<string>();
            public readonly Dictionary<string, (HttpStatusCode Codigo, string Respuesta)> Respuestas = new Dictionary<string, (HttpStatusCode, string)>();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri.PathAndQuery;
                Urls.Add(url);
                foreach (KeyValuePair<string, (HttpStatusCode Codigo, string Respuesta)> r in Respuestas)
                {
                    if (url.StartsWith(r.Key, StringComparison.Ordinal))
                    {
                        return Task.FromResult(new HttpResponseMessage(r.Value.Codigo) { Content = new StringContent(r.Value.Respuesta, Encoding.UTF8, "application/json") });
                    }
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"Message\":\"No encontrado\"}", Encoding.UTF8, "application/json") });
            }
        }

        [TestMethod]
        public async Task BuscarProductosPorCodigo_UnaReferencia_DevuelveEseProductoSinMirarLosCodigos()
        {
            var handler = new HandlerPorRuta();
            handler.Respuestas["/api/Productos?empresa=1&id=44724"] = (HttpStatusCode.OK, "{\"producto\":\"44724   \",\"nombre\":\"SERUM LEVEL LASH  \",\"precio\":10}");
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            List<ProductoCodigoRecepcion> productos = await servicio.BuscarProductosPorCodigo("1", " 44724 ");

            Assert.AreEqual(1, handler.Urls.Count);
            Assert.AreEqual("44724", productos.Single().Producto);
            Assert.AreEqual("SERUM LEVEL LASH", productos.Single().Nombre);
        }

        [TestMethod]
        public async Task BuscarProductosPorCodigo_UnCodigoDeBarras_PreguntaPorTodosLosCodigos()
        {
            var handler = new HandlerPorRuta();
            handler.Respuestas["/api/Productos/PorCodigoBarras?codigo=8437020512176&empresa=1"] =
                (HttpStatusCode.OK, "[{\"Producto\":\"44724\",\"Nombre\":\"SERUM LEVEL LASH\",\"Cantidad\":1,\"Principal\":true}]");
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            List<ProductoCodigoRecepcion> productos = await servicio.BuscarProductosPorCodigo("1", "8437020512176");

            Assert.AreEqual("/api/Productos?empresa=1&id=8437020512176", handler.Urls[0], "Primero, por si es una referencia");
            Assert.AreEqual("44724", productos.Single().Producto);
            Assert.AreEqual("SERUM LEVEL LASH", productos.Single().Nombre);
        }

        [TestMethod]
        public async Task BuscarProductosPorCodigo_DeNingunProducto_DevuelveVacia()
        {
            var handler = new HandlerPorRuta();
            handler.Respuestas["/api/Productos/PorCodigoBarras"] = (HttpStatusCode.OK, "[]");
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            List<ProductoCodigoRecepcion> productos = await servicio.BuscarProductosPorCodigo("1", "999");

            Assert.AreEqual(0, productos.Count);
        }

        [TestMethod]
        public async Task BuscarProductosPorCodigo_SiLaApiFalla_Lanza()
        {
            var handler = new HandlerPorRuta();
            handler.Respuestas["/api/Productos"] = (HttpStatusCode.InternalServerError, "{\"Message\":\"Error\"}");
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            await Assert.ThrowsExceptionAsync<RecepcionReposicionException>(() => servicio.BuscarProductosPorCodigo("1", "8437020512176"));
        }
    

        // ---------------------------------------------------------------- Ariadna#51 (corte 2): lo leído, en el servidor

        [TestMethod]
        public async Task LeerLecturas_PideLoLeidoDeLaRecepcionEnCurso()
        {
            var handler = new HandlerFalso
            {
                Respuesta = "{\"IdRecepcion\":\"5b1d0c1e-51c2-4a51-9b11-811190000001\",\"Lecturas\":[{\"Producto\":\"17404\",\"Cantidad\":2}]," +
                            "\"CodigosBulto\":[\"R81119-1\"],\"Equipos\":[{\"Usuario\":\"Andre\",\"Dispositivo\":\"Ariadna\"}]}"
            };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            LecturasRecepcionReposicion lecturas = await servicio.LeerLecturas("1", "REI", "81119");

            Assert.AreEqual("GET /api/Almacen/Recepciones/REPO/81119/Lecturas?almacen=REI&empresa=1", handler.Urls[0]);
            Assert.AreEqual(new Guid("5b1d0c1e-51c2-4a51-9b11-811190000001"), lecturas.IdRecepcion);
            Assert.AreEqual(2, lecturas.Lecturas.Single().Cantidad);
            Assert.AreEqual("R81119-1", lecturas.CodigosBulto.Single());
            Assert.AreEqual("Ariadna", lecturas.Equipos.Single().Dispositivo);
        }

        [TestMethod]
        public async Task LeerLecturas_ConUnaApiAnterior_DevuelveNull()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.NotFound, Respuesta = "" };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            Assert.IsNull(await servicio.LeerLecturas("1", "REI", "81119"));
        }

        [TestMethod]
        public async Task LeerLecturas_SiLaApiFalla_Lanza()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.InternalServerError, Respuesta = "{\"Message\":\"Error\"}" };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            await Assert.ThrowsExceptionAsync<RecepcionReposicionException>(() => servicio.LeerLecturas("1", "REI", "81119"));
        }

        [TestMethod]
        public async Task GuardarLecturas_MandaLosApuntesConLaConexionYElEquipo()
        {
            var handler = new HandlerFalso { Respuesta = "{\"IdRecepcion\":\"5b1d0c1e-51c2-4a51-9b11-811190000001\",\"Lecturas\":[]}" };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));
            var id = Guid.NewGuid();

            LecturasRecepcionReposicion lecturas = await servicio.GuardarLecturas("1", "REI", "81119", new List<LecturaEnCursoRecepcion>
            {
                new LecturaEnCursoRecepcion { IdCliente = id, Producto = "17404", Cantidad = -1, Dispositivo = "RDS2016" }
            }, "conexion-1");

            Assert.AreEqual("POST /api/Almacen/Recepciones/REPO/81119/Lecturas?almacen=REI&empresa=1", handler.Urls[0]);
            JArray cuerpo = JArray.Parse(handler.Cuerpos[0]);
            Assert.AreEqual(id.ToString(), (string)cuerpo[0]["IdCliente"]);
            Assert.AreEqual(-1, (int)cuerpo[0]["Cantidad"]);
            Assert.AreEqual("conexion-1", handler.Peticiones[0].Headers.GetValues("X-Conexion-SignalR").Single());
            Assert.AreEqual(Environment.MachineName, handler.Peticiones[0].Headers.GetValues("X-Dispositivo").Single());
            Assert.AreEqual(new Guid("5b1d0c1e-51c2-4a51-9b11-811190000001"), lecturas.IdRecepcion);
        }

        [TestMethod]
        public async Task GuardarLecturas_SiLaApiNoLaGuarda_Lanza()
        {
            var handler = new HandlerFalso { Codigo = HttpStatusCode.BadRequest, Respuesta = "\"La reposición ya está recibida\"" };
            var servicio = new ServicioRecepcionReposiciones(new FactoriaFalsa(handler));

            var ex = await Assert.ThrowsExceptionAsync<RecepcionReposicionException>(() =>
                servicio.GuardarLecturas("1", "REI", "81119", new List<LecturaEnCursoRecepcion>(), null));
            Assert.AreEqual("La reposición ya está recibida", ex.Message);
        }
    }
}
