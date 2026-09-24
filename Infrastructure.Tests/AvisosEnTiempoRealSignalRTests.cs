using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Tests
{
    /// <summary>
    /// NestoAPI#536: avisos en tiempo real por SignalR para la campana. La conexión va detrás de
    /// <see cref="IConexionAvisos"/> y las esperas son inyectadas: los tests no tocan red ni relojes.
    /// </summary>
    [TestClass]
    public class AvisosEnTiempoRealSignalRTests
    {
        private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(10);

        private sealed class ConexionFalsa : IConexionAvisos
        {
            public string Token;
            public Exception FalloAlIniciar;
            public readonly TaskCompletionSource<bool> Iniciada = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool Liberada;

            public event EventHandler HayNotificacionesNuevas;
            public event EventHandler Reconectada;
            public event EventHandler Cerrada;

            public Task Iniciar()
            {
                if (FalloAlIniciar != null)
                {
                    return Task.FromException(FalloAlIniciar);
                }
                _ = Iniciada.TrySetResult(true);
                return Task.CompletedTask;
            }

            public void LlegaAviso() => HayNotificacionesNuevas?.Invoke(this, EventArgs.Empty);
            public void Reconecta() => Reconectada?.Invoke(this, EventArgs.Empty);
            public void SeCierra() => Cerrada?.Invoke(this, EventArgs.Empty);
            public void Dispose() => Liberada = true;
        }

        private static IServicioAutenticacion AutenticacionCon(params string[] tokens)
        {
            var auth = A.Fake<IServicioAutenticacion>();
            int llamada = 0;
            A.CallTo(() => auth.ObtenerTokenValidoAsync())
                .ReturnsLazily(() => Task.FromResult(tokens[Math.Min(Interlocked.Increment(ref llamada), tokens.Length) - 1]));
            return auth;
        }

        /// <summary>Las esperas de revisión del token no terminan nunca (salvo cancelación).</summary>
        private static Task EsperaInfinita(CancellationToken ct) => Task.Delay(Timeout.Infinite, ct);

        [TestMethod]
        public void EsperaTrasFallos_CreceHastaCincoMinutosYSeQuedaAhi()
        {
            var esperadas = new[] { 5, 15, 30, 60, 120, 300, 300, 300 }.Select(s => TimeSpan.FromSeconds(s)).ToArray();
            for (int fallos = 1; fallos <= esperadas.Length; fallos++)
            {
                Assert.AreEqual(esperadas[fallos - 1], AvisosEnTiempoRealSignalR.EsperaTrasFallos(fallos), $"fallos = {fallos}");
            }
            Assert.AreEqual(AvisosEnTiempoRealSignalR.ESPERA_MAXIMA, AvisosEnTiempoRealSignalR.EsperaTrasFallos(1000));
            Assert.AreEqual(TimeSpan.Zero, AvisosEnTiempoRealSignalR.EsperaTrasFallos(0));
        }

        [TestMethod]
        public void UrlDelHub_CuelgaDeLaRaizDelSitioDeLaApi()
        {
            Assert.AreEqual("http://api.nuevavision.es/signalr", AvisosEnTiempoRealSignalR.UrlDelHub("http://api.nuevavision.es/api/"));
            Assert.AreEqual("http://localhost:53364/signalr", AvisosEnTiempoRealSignalR.UrlDelHub("http://localhost:53364/api/"));
        }

        [TestMethod]
        public async Task SiNoConecta_ReintentaConEsperasCrecientesYSoloRegistraUnErrorCuandoDura()
        {
            var esperas = new List<TimeSpan>();
            var errores = new ConcurrentBag<Exception>();
            var conexiones = new ConcurrentBag<ConexionFalsa>();
            var avisos = new AvisosEnTiempoRealSignalR("http://api/signalr", AutenticacionCon("t1"),
                (url, token) =>
                {
                    var c = new ConexionFalsa { Token = token, FalloAlIniciar = new InvalidOperationException("sin servidor") };
                    conexiones.Add(c);
                    return c;
                },
                (espera, ct) =>
                {
                    esperas.Add(espera);
                    // A la octava espera se corta el bucle (como si se cerrase Nesto).
                    return esperas.Count >= 8 ? Task.FromCanceled(new CancellationToken(true)) : Task.CompletedTask;
                },
                errores.Add);

            avisos.Iniciar();
            await avisos.BucleEnCurso.WaitAsync(TIMEOUT);

            CollectionAssert.AreEqual(new[] { 5, 15, 30, 60, 120, 300, 300, 300 }.Select(s => TimeSpan.FromSeconds(s)).ToList(), esperas);
            Assert.AreEqual(1, errores.Count, "Un único error por sesión, aunque falle muchas veces");
            Assert.IsTrue(conexiones.All(c => c.Liberada), "Cada conexión fallida se libera");
            Assert.IsTrue(conexiones.All(c => c.Token == "t1"));
        }

        [TestMethod]
        public async Task SiFallaPocasVeces_NoRegistraNada()
        {
            var errores = new ConcurrentBag<Exception>();
            int esperas = 0;
            var avisos = new AvisosEnTiempoRealSignalR("http://api/signalr", AutenticacionCon("t1"),
                (url, token) => new ConexionFalsa { FalloAlIniciar = new InvalidOperationException("sin servidor") },
                (espera, ct) => ++esperas >= 3 ? Task.FromCanceled(new CancellationToken(true)) : Task.CompletedTask,
                errores.Add);

            avisos.Iniciar();
            await avisos.BucleEnCurso.WaitAsync(TIMEOUT);

            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task AvisoDelServidor_DisparaHayNotificacionesNuevas()
        {
            var conexion = new ConexionFalsa();
            var avisos = new AvisosEnTiempoRealSignalR("http://api/signalr", AutenticacionCon("t1"),
                (url, token) => { conexion.Token = token; return conexion; },
                (espera, ct) => EsperaInfinita(ct), null);
            int recibidos = 0;
            avisos.HayNotificacionesNuevas += (s, e) => Interlocked.Increment(ref recibidos);

            avisos.Iniciar();
            await conexion.Iniciada.Task.WaitAsync(TIMEOUT);
            Assert.AreEqual(0, recibidos, "Conectar la primera vez no avisa (la campana ya refresca al arrancar)");

            conexion.LlegaAviso();

            Assert.AreEqual(1, recibidos);
            Assert.AreEqual("t1", conexion.Token);
            avisos.Detener();
            Assert.IsTrue(conexion.Liberada, "Al detener se cierra la conexión");
        }

        [TestMethod]
        public async Task SiSignalRReconectaSolo_AvisaPorLoQuePudoPerderse()
        {
            var conexion = new ConexionFalsa();
            var avisos = new AvisosEnTiempoRealSignalR("http://api/signalr", AutenticacionCon("t1"),
                (url, token) => conexion, (espera, ct) => EsperaInfinita(ct), null);
            int recibidos = 0;
            avisos.HayNotificacionesNuevas += (s, e) => Interlocked.Increment(ref recibidos);

            avisos.Iniciar();
            await conexion.Iniciada.Task.WaitAsync(TIMEOUT);
            conexion.Reconecta();

            Assert.AreEqual(1, recibidos);
            avisos.Detener();
        }

        [TestMethod]
        public async Task SiLaConexionSeCierra_EsperaCincoSegundosReconectaYAvisa()
        {
            var primera = new ConexionFalsa();
            var segunda = new ConexionFalsa();
            var cola = new Queue<ConexionFalsa>(new[] { primera, segunda });
            var esperas = new ConcurrentQueue<TimeSpan>();
            var avisos = new AvisosEnTiempoRealSignalR("http://api/signalr", AutenticacionCon("t1"),
                (url, token) => cola.Dequeue(),
                (espera, ct) =>
                {
                    if (espera == AvisosEnTiempoRealSignalR.INTERVALO_REVISION_TOKEN)
                    {
                        return EsperaInfinita(ct);
                    }
                    esperas.Enqueue(espera);
                    return Task.CompletedTask;
                }, null);
            var avisado = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            avisos.HayNotificacionesNuevas += (s, e) => avisado.TrySetResult(true);

            avisos.Iniciar();
            await primera.Iniciada.Task.WaitAsync(TIMEOUT);
            primera.SeCierra();
            await segunda.Iniciada.Task.WaitAsync(TIMEOUT);
            await avisado.Task.WaitAsync(TIMEOUT);

            CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(5) }, esperas.ToArray());
            Assert.IsTrue(primera.Liberada);
            avisos.Detener();
        }

        [TestMethod]
        public async Task SiElTokenSeRenueva_ReconectaConElNuevo()
        {
            var primera = new ConexionFalsa();
            var segunda = new ConexionFalsa();
            var cola = new Queue<ConexionFalsa>(new[] { primera, segunda });
            int revisiones = 0;
            // 1ª llamada: conectar (t1); 2ª: revisión del token (t2, renovado); 3ª: reconectar (t2).
            var avisos = new AvisosEnTiempoRealSignalR("http://api/signalr", AutenticacionCon("t1", "t2"),
                (url, token) => { var c = cola.Dequeue(); c.Token = token; return c; },
                (espera, ct) => Interlocked.Increment(ref revisiones) == 1 ? Task.CompletedTask : EsperaInfinita(ct),
                null);

            avisos.Iniciar();
            await segunda.Iniciada.Task.WaitAsync(TIMEOUT);

            Assert.AreEqual("t1", primera.Token);
            Assert.IsTrue(primera.Liberada, "La conexión con el token viejo se cierra");
            Assert.AreEqual("t2", segunda.Token);
            avisos.Detener();
        }
    }
}
