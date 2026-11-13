using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Aviso vertical de bloqueos (13/11/26): la API dice quién bloquea la base de datos en la cabecera X-Nesto-Bloqueo
    /// (Uri.EscapeDataString). El manejador de ClienteApiFactory la publica en el canal, el canal no repite el mismo texto en 60 s
    /// y la ventana principal lo enseña en una sola banda.
    /// </summary>
    [TestClass]
    public class AvisoBloqueosTests
    {
        private const string DIAGNOSTICO = "Puede estar bloqueado por: NUEVAVISION\\Santiago (Nesto, transacción abierta desde 10:59, " +
            "bloquea a 2 sesión(es)). Si persiste, avisa a ese usuario para que termine o cierre la operación que tiene a medias.";

        private static readonly DateTime AHORA = new DateTime(2026, 11, 13, 11, 5, 0, DateTimeKind.Utc);

        /// <summary>El transporte de mentira: cuenta las llamadas y devuelve lo que diga la función.</summary>
        private sealed class TransporteFalso : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public int Llamadas;

            public TransporteFalso(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                _responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _ = Interlocked.Increment(ref Llamadas);
                return Task.FromResult(_responder(request));
            }
        }

        private static HttpResponseMessage Respuesta(HttpStatusCode estado, string cabecera = null, string cuerpo = "{\"Message\":\"error\"}")
        {
            var respuesta = new HttpResponseMessage(estado) { Content = new StringContent(cuerpo) };
            if (cabecera != null)
            {
                _ = respuesta.Headers.TryAddWithoutValidation(AvisoBloqueosHandler.CABECERA, cabecera);
            }
            return respuesta;
        }

        private static (HttpClient cliente, List<string> avisos, CanalAvisosBloqueo canal) ClienteCon(HttpResponseMessage respuesta)
        {
            var canal = new CanalAvisosBloqueo(() => AHORA, TimeSpan.Zero);
            var avisos = new List<string>();
            canal.BloqueoDetectado += (s, e) => avisos.Add(e.Diagnostico);
            var manejador = new AvisoBloqueosHandler(canal) { InnerHandler = new TransporteFalso(_ => respuesta) };
            return (new HttpClient(manejador) { BaseAddress = new Uri("http://localhost/") }, avisos, canal);
        }

        // ---- El manejador ----

        [TestMethod]
        public async Task Manejador_ConCabecera_PublicaElDiagnosticoDecodificado()
        {
            var (cliente, avisos, _) = ClienteCon(Respuesta(HttpStatusCode.InternalServerError, Uri.EscapeDataString(DIAGNOSTICO)));

            _ = await cliente.GetAsync("api/Bancos");

            CollectionAssert.AreEqual(new[] { DIAGNOSTICO }, avisos);
        }

        [TestMethod]
        public async Task Manejador_SinCabecera_NoPublicaNada()
        {
            var (cliente, avisos, _) = ClienteCon(Respuesta(HttpStatusCode.InternalServerError));

            _ = await cliente.GetAsync("api/Bancos");

            Assert.AreEqual(0, avisos.Count);
        }

        [TestMethod]
        public async Task Manejador_CabeceraVacia_NoPublicaNada()
        {
            var (cliente, avisos, _) = ClienteCon(Respuesta(HttpStatusCode.InternalServerError, "   "));

            _ = await cliente.GetAsync("api/Bancos");

            Assert.AreEqual(0, avisos.Count);
        }

        [TestMethod]
        public async Task Manejador_NoAlteraLaRespuesta()
        {
            HttpResponseMessage original = Respuesta(HttpStatusCode.Conflict, Uri.EscapeDataString(DIAGNOSTICO), "{\"Message\":\"No se pudo contabilizar\"}");
            var (cliente, _, _) = ClienteCon(original);

            HttpResponseMessage recibida = await cliente.PostAsync("api/Contabilidad", new StringContent("{}"));

            Assert.AreSame(original, recibida);
            Assert.AreEqual(HttpStatusCode.Conflict, recibida.StatusCode);
            Assert.AreEqual("{\"Message\":\"No se pudo contabilizar\"}", await recibida.Content.ReadAsStringAsync());
            Assert.IsTrue(recibida.Headers.Contains(AvisoBloqueosHandler.CABECERA), "La cabecera sigue en la respuesta");
        }

        [TestMethod]
        public async Task Manejador_TambienMiraLasRespuestasCorrectas()
        {
            // «En cada respuesta»: no depende del código de estado, solo de que venga la cabecera
            var (cliente, avisos, _) = ClienteCon(Respuesta(HttpStatusCode.OK, Uri.EscapeDataString("Puede estar bloqueado por: X")));

            _ = await cliente.GetAsync("api/Bancos");

            Assert.AreEqual(1, avisos.Count);
        }

        [TestMethod]
        public async Task Manejador_ValorMalCodificado_NoLanzaYEnseñaLoQueSePuedeLeer()
        {
            // «%ZZ» y un «%» suelto no son secuencias válidas; «%E0%A4» es un UTF-8 a medias
            var (cliente, avisos, _) = ClienteCon(Respuesta(HttpStatusCode.InternalServerError, "Bloqueado%20por%ZZ%20Santiago%E0%A4%"));

            HttpResponseMessage recibida = await cliente.GetAsync("api/Bancos");

            Assert.AreEqual(HttpStatusCode.InternalServerError, recibida.StatusCode);
            Assert.AreEqual(1, avisos.Count);
            StringAssert.StartsWith(avisos[0], "Bloqueado por");
            StringAssert.Contains(avisos[0], "Santiago");
        }

        [TestMethod]
        public async Task Manejador_SiElOyenteLanza_NoLanzaNiCambiaLaRespuesta()
        {
            var canal = new CanalAvisosBloqueo(() => AHORA, TimeSpan.Zero);
            canal.BloqueoDetectado += (s, e) => throw new InvalidOperationException("la vista ha fallado");
            HttpResponseMessage original = Respuesta(HttpStatusCode.InternalServerError, Uri.EscapeDataString(DIAGNOSTICO));
            var cliente = new HttpClient(new AvisoBloqueosHandler(canal) { InnerHandler = new TransporteFalso(_ => original) });

            HttpResponseMessage recibida = await cliente.GetAsync("http://localhost/api/Bancos");

            Assert.AreSame(original, recibida);
        }

        [TestMethod]
        public async Task Manejador_SiLaLlamadaFalla_DejaPasarEseError()
        {
            // No se traga los errores de red: solo los suyos
            var canal = new CanalAvisosBloqueo(() => AHORA, TimeSpan.Zero);
            var cliente = new HttpClient(new AvisoBloqueosHandler(canal)
            {
                InnerHandler = new TransporteFalso(_ => throw new HttpRequestException("sin red"))
            });

            _ = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => cliente.GetAsync("http://localhost/api/Bancos"));
        }

        [TestMethod]
        public void LeerDiagnostico_RespuestaNula_DevuelveNull()
        {
            Assert.IsNull(AvisoBloqueosHandler.LeerDiagnostico(null));
        }

        [TestMethod]
        public void Decodificar_QuitaSaltosYRecortaLoDesmesurado()
        {
            Assert.AreEqual("uno dos", AvisoBloqueosHandler.Decodificar("uno%0Ados"));
            Assert.IsNull(AvisoBloqueosHandler.Decodificar("%20%0A"));

            string largo = AvisoBloqueosHandler.Decodificar(new string('a', AvisoBloqueosHandler.LONGITUD_MAXIMA + 500));
            Assert.AreEqual(AvisoBloqueosHandler.LONGITUD_MAXIMA + 1, largo.Length);
            StringAssert.EndsWith(largo, "…");
        }

        // ---- Su sitio en la cadena de ClienteApiFactory ----

        [TestMethod]
        public async Task Factoria_GetReintentadoTresVeces_DaUnSoloAviso()
        {
            // Sin antirrebote en el canal: lo que se prueba es que el manejador va por FUERA de los reintentos
            var canal = new CanalAvisosBloqueo(() => AHORA, TimeSpan.Zero);
            var avisos = new List<string>();
            canal.BloqueoDetectado += (s, e) => avisos.Add(e.Diagnostico);
            var transporte = new TransporteFalso(_ => Respuesta(HttpStatusCode.InternalServerError, Uri.EscapeDataString(DIAGNOSTICO)));
            var factoria = new ClienteApiFactory("http://localhost/", null, canal);
            var cliente = new HttpClient(factoria.CrearCadena(transporte)) { BaseAddress = new Uri("http://localhost/") };

            HttpResponseMessage respuesta = await cliente.GetAsync("api/Bancos");

            Assert.AreEqual(HttpStatusCode.InternalServerError, respuesta.StatusCode);
            Assert.AreEqual(3, transporte.Llamadas, "El GET se intenta 3 veces (la primera y 2 reintentos)");
            CollectionAssert.AreEqual(new[] { DIAGNOSTICO }, avisos);
        }

        [TestMethod]
        public async Task Factoria_GetQueSaleBienAlReintentar_NoAvisa()
        {
            var canal = new CanalAvisosBloqueo(() => AHORA, TimeSpan.Zero);
            var avisos = new List<string>();
            canal.BloqueoDetectado += (s, e) => avisos.Add(e.Diagnostico);
            int intento = 0;
            var transporte = new TransporteFalso(_ => ++intento == 1
                ? Respuesta(HttpStatusCode.InternalServerError, Uri.EscapeDataString(DIAGNOSTICO))
                : Respuesta(HttpStatusCode.OK, null, "[]"));
            var factoria = new ClienteApiFactory("http://localhost/", null, canal);
            var cliente = new HttpClient(factoria.CrearCadena(transporte)) { BaseAddress = new Uri("http://localhost/") };

            HttpResponseMessage respuesta = await cliente.GetAsync("api/Bancos");

            Assert.AreEqual(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.AreEqual(2, transporte.Llamadas);
            Assert.AreEqual(0, avisos.Count, "El usuario no ha visto ningún error: no hay nada que avisar");
        }

        [TestMethod]
        public async Task Factoria_PostConBloqueo_AvisaSinReintentar()
        {
            var canal = new CanalAvisosBloqueo(() => AHORA, TimeSpan.Zero);
            var avisos = new List<string>();
            canal.BloqueoDetectado += (s, e) => avisos.Add(e.Diagnostico);
            var transporte = new TransporteFalso(_ => Respuesta(HttpStatusCode.InternalServerError, Uri.EscapeDataString(DIAGNOSTICO)));
            var factoria = new ClienteApiFactory("http://localhost/", null, canal);
            var cliente = new HttpClient(factoria.CrearCadena(transporte)) { BaseAddress = new Uri("http://localhost/") };

            _ = await cliente.PostAsync("api/Contabilidad", new StringContent("{}"));

            Assert.AreEqual(1, transporte.Llamadas);
            Assert.AreEqual(1, avisos.Count);
        }

        [TestMethod]
        public void Factoria_Crear_UsaElCanalComunYElManejadorEsElMasExterno()
        {
            var factoria = new ClienteApiFactory("http://localhost/", null);

            HttpMessageHandler cadena = factoria.CrearCadena(new TransporteFalso(_ => Respuesta(HttpStatusCode.OK)));

            Assert.IsInstanceOfType(cadena, typeof(AvisoBloqueosHandler));
            using HttpClient cliente = factoria.Crear();
            Assert.AreEqual(new Uri("http://localhost/"), cliente.BaseAddress);
        }

        // ---- El canal: antirrebote ----

        [TestMethod]
        public void Canal_MismoTextoAntesDe60Segundos_NoSeRepite()
        {
            DateTime reloj = AHORA;
            var canal = new CanalAvisosBloqueo(() => reloj, CanalAvisosBloqueo.ANTIRREBOTE);
            var avisos = new List<string>();
            canal.BloqueoDetectado += (s, e) => avisos.Add(e.Diagnostico);

            Assert.IsTrue(canal.Publicar(DIAGNOSTICO));
            for (int i = 0; i < 10; i++)
            {
                reloj = reloj.AddSeconds(5);
                Assert.IsFalse(canal.Publicar(DIAGNOSTICO));
            }
            reloj = AHORA.AddSeconds(59);
            Assert.IsFalse(canal.Publicar("  " + DIAGNOSTICO + " "), "Los espacios de alrededor no lo hacen otro texto");

            Assert.AreEqual(1, avisos.Count);
        }

        [TestMethod]
        public void Canal_MismoTextoPasados60Segundos_SeVuelveAPublicar()
        {
            DateTime reloj = AHORA;
            var canal = new CanalAvisosBloqueo(() => reloj, CanalAvisosBloqueo.ANTIRREBOTE);
            var avisos = new List<string>();
            canal.BloqueoDetectado += (s, e) => avisos.Add(e.Diagnostico);

            _ = canal.Publicar(DIAGNOSTICO);
            reloj = AHORA.AddSeconds(60);
            Assert.IsTrue(canal.Publicar(DIAGNOSTICO));
            // El plazo cuenta desde la última vez que se PUBLICÓ, no desde el último intento
            reloj = AHORA.AddSeconds(119);
            Assert.IsFalse(canal.Publicar(DIAGNOSTICO));
            reloj = AHORA.AddSeconds(120);
            Assert.IsTrue(canal.Publicar(DIAGNOSTICO));

            Assert.AreEqual(3, avisos.Count);
        }

        [TestMethod]
        public void Canal_TextoDistinto_SePublicaAunqueSeaEnseguida()
        {
            var canal = new CanalAvisosBloqueo(() => AHORA, CanalAvisosBloqueo.ANTIRREBOTE);
            var avisos = new List<string>();
            canal.BloqueoDetectado += (s, e) => avisos.Add(e.Diagnostico);

            _ = canal.Publicar("Puede estar bloqueado por: A");
            _ = canal.Publicar("Puede estar bloqueado por: B");

            CollectionAssert.AreEqual(new[] { "Puede estar bloqueado por: A", "Puede estar bloqueado por: B" }, avisos);
        }

        [TestMethod]
        public void Canal_VacioONulo_NoPublica()
        {
            var canal = new CanalAvisosBloqueo(() => AHORA, CanalAvisosBloqueo.ANTIRREBOTE);
            int avisos = 0;
            canal.BloqueoDetectado += (s, e) => avisos++;

            Assert.IsFalse(canal.Publicar(null));
            Assert.IsFalse(canal.Publicar(""));
            Assert.IsFalse(canal.Publicar("   "));
            Assert.AreEqual(0, avisos);
        }

        [TestMethod]
        public void Canal_UnOyenteQueLanza_NoDejaSinAvisoAlSiguienteNiLanza()
        {
            var canal = new CanalAvisosBloqueo(() => AHORA, CanalAvisosBloqueo.ANTIRREBOTE);
            int avisos = 0;
            canal.BloqueoDetectado += (s, e) => throw new InvalidOperationException();
            canal.BloqueoDetectado += (s, e) => avisos++;

            Assert.IsTrue(canal.Publicar(DIAGNOSTICO));
            Assert.AreEqual(1, avisos);
        }

        [TestMethod]
        public void Canal_SinOyentes_NoLanza()
        {
            Assert.IsTrue(new CanalAvisosBloqueo(() => AHORA, CanalAvisosBloqueo.ANTIRREBOTE).Publicar(DIAGNOSTICO));
        }

        [TestMethod]
        public void Canal_RelojQueVaHaciaAtras_NoCallaElAviso()
        {
            DateTime reloj = AHORA;
            var canal = new CanalAvisosBloqueo(() => reloj, CanalAvisosBloqueo.ANTIRREBOTE);

            _ = canal.Publicar(DIAGNOSTICO);
            reloj = AHORA.AddHours(-1);

            Assert.IsTrue(canal.Publicar(DIAGNOSTICO));
        }

        // ---- La banda de la ventana principal ----

        private sealed class Programado : IDisposable
        {
            public TimeSpan Espera;
            public Action Accion;
            public bool Cancelado;
            public void Dispose() => Cancelado = true;
        }

        private static (AvisoBloqueoViewModel vm, CanalAvisosBloqueo canal, List<Programado> programados, List<string> cambios, Func<int> pasosPorUi) Banda()
        {
            var canal = new CanalAvisosBloqueo(() => AHORA, TimeSpan.Zero);
            var programados = new List<Programado>();
            int pasos = 0;
            var vm = new AvisoBloqueoViewModel(canal,
                accion => { pasos++; accion(); },
                (espera, accion) =>
                {
                    var p = new Programado { Espera = espera, Accion = accion };
                    programados.Add(p);
                    return p;
                });
            var cambios = new List<string>();
            vm.PropertyChanged += (object s, PropertyChangedEventArgs e) => cambios.Add(e.PropertyName);
            vm.Iniciar();
            return (vm, canal, programados, cambios, () => pasos);
        }

        [TestMethod]
        public void Banda_AlLlegarUnBloqueo_SeEnseñaConElTituloYElDiagnosticoTalCual()
        {
            var (vm, canal, programados, cambios, pasosPorUi) = Banda();
            Assert.IsFalse(vm.Visible);

            _ = canal.Publicar(DIAGNOSTICO);

            Assert.IsTrue(vm.Visible);
            Assert.AreEqual("La base de datos está ocupada.", vm.Titulo);
            Assert.AreEqual(DIAGNOSTICO, vm.Detalle);
            Assert.AreEqual(1, pasosPorUi(), "El aviso llega en un hilo de fondo: se pinta pasando por el de la interfaz");
            CollectionAssert.Contains(cambios, nameof(AvisoBloqueoViewModel.Visible));
            CollectionAssert.Contains(cambios, nameof(AvisoBloqueoViewModel.Detalle));
            Assert.AreEqual(1, programados.Count);
            Assert.AreEqual(AvisoBloqueoViewModel.DURACION, programados[0].Espera);
        }

        [TestMethod]
        public void Banda_SeCierraSolaAlVencerElPlazo()
        {
            var (vm, canal, programados, _, _) = Banda();
            _ = canal.Publicar(DIAGNOSTICO);

            programados[0].Accion();

            Assert.IsFalse(vm.Visible);
        }

        [TestMethod]
        public void Banda_SeCierraConSuBoton()
        {
            var (vm, canal, programados, _, _) = Banda();
            _ = canal.Publicar(DIAGNOSTICO);

            Assert.IsTrue(vm.CerrarCommand.CanExecute(null));
            vm.CerrarCommand.Execute(null);

            Assert.IsFalse(vm.Visible);
            Assert.IsTrue(programados[0].Cancelado, "Cerrada a mano, el temporizador sobra");
        }

        [TestMethod]
        public void Banda_OtroBloqueoConLaBandaALaVista_CambiaElTextoYEmpiezaElPlazoDeNuevo()
        {
            var (vm, canal, programados, _, _) = Banda();
            _ = canal.Publicar("Puede estar bloqueado por: A");

            _ = canal.Publicar("Puede estar bloqueado por: B");

            Assert.IsTrue(vm.Visible);
            Assert.AreEqual("Puede estar bloqueado por: B", vm.Detalle, "Una sola banda: se cambia el texto");
            Assert.AreEqual(2, programados.Count);
            Assert.IsTrue(programados[0].Cancelado);
            Assert.IsFalse(programados[1].Cancelado);

            // El temporizador del primero, si ya venía de camino, no cierra el segundo
            programados[0].Accion();
            Assert.IsTrue(vm.Visible);

            programados[1].Accion();
            Assert.IsFalse(vm.Visible);
        }

        [TestMethod]
        public void Banda_CerradaYVuelveABloquearse_SeVuelveAEnseñar()
        {
            var (vm, canal, _, _, _) = Banda();
            _ = canal.Publicar(DIAGNOSTICO);
            vm.Cerrar();

            _ = canal.Publicar(DIAGNOSTICO);

            Assert.IsTrue(vm.Visible);
        }

        [TestMethod]
        public void Banda_SinIniciarOLiberada_NoEscucha()
        {
            var canal = new CanalAvisosBloqueo(() => AHORA, TimeSpan.Zero);
            var vm = new AvisoBloqueoViewModel(canal, accion => accion(), (espera, accion) => new Programado());

            _ = canal.Publicar(DIAGNOSTICO);
            Assert.IsFalse(vm.Visible, "Sin Iniciar no escucha");

            vm.Iniciar();
            vm.Iniciar();
            _ = canal.Publicar(DIAGNOSTICO);
            Assert.IsTrue(vm.Visible);

            vm.Cerrar();
            vm.Dispose();
            _ = canal.Publicar(DIAGNOSTICO);
            Assert.IsFalse(vm.Visible, "Liberada (ventana cerrada) ya no escucha");
        }

        [TestMethod]
        public void Banda_AvisoQueLlegaAlHiloDeLaInterfazTrasLiberarse_NoHaceNada()
        {
            var canal = new CanalAvisosBloqueo(() => AHORA, TimeSpan.Zero);
            var pendientes = new List<Action>();
            var vm = new AvisoBloqueoViewModel(canal, pendientes.Add, (espera, accion) => new Programado());
            vm.Iniciar();
            _ = canal.Publicar(DIAGNOSTICO);
            Assert.IsFalse(vm.Visible, "Aún no ha pasado por el hilo de la interfaz");

            vm.Dispose();
            pendientes.ForEach(a => a());

            Assert.IsFalse(vm.Visible);
        }

        [TestMethod]
        public async Task DePuntaAPunta_UnaLlamadaConBloqueoEnseñaLaBanda_YDiezSeguidasUnaSolaVez()
        {
            DateTime reloj = AHORA;
            var canal = new CanalAvisosBloqueo(() => reloj, CanalAvisosBloqueo.ANTIRREBOTE);
            int publicados = 0;
            canal.BloqueoDetectado += (s, e) => publicados++;
            var vm = new AvisoBloqueoViewModel(canal, accion => accion(), (espera, accion) => new Programado());
            vm.Iniciar();
            var cliente = new HttpClient(new AvisoBloqueosHandler(canal)
            {
                InnerHandler = new TransporteFalso(_ => Respuesta(HttpStatusCode.InternalServerError, Uri.EscapeDataString(DIAGNOSTICO)))
            });

            for (int i = 0; i < 10; i++)
            {
                _ = await cliente.PostAsync("http://localhost/api/Contabilidad", new StringContent("{}"));
                reloj = reloj.AddSeconds(2);
            }

            Assert.IsTrue(vm.Visible);
            Assert.AreEqual(DIAGNOSTICO, vm.Detalle);
            Assert.AreEqual(1, publicados);
        }
    }
}
