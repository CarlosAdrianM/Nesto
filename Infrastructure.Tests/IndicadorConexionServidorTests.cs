using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Infrastructure.Tests
{
    /// <summary>
    /// Nesto#492: la raya de la ventana principal. Sin sondeos: se recalcula por eventos (token y conexión
    /// en tiempo real) y con un único temporizador al vencer el token. Reloj, temporizador y Dispatcher inyectados.
    /// </summary>
    [TestClass]
    public class IndicadorConexionServidorTests
    {
        private static readonly DateTime AHORA = new DateTime(2026, 9, 24, 15, 0, 0, DateTimeKind.Utc);

        private sealed class AvisosFalsos : IAvisosEnTiempoReal
        {
            private EstadoConexionTiempoReal _estado;
            public event EventHandler HayNotificacionesNuevas { add { } remove { } }
            public event EventHandler EstadoCambiado;
            public EstadoConexionTiempoReal Estado => _estado;
            public int Suscriptores => EstadoCambiado?.GetInvocationList().Length ?? 0;

            public void Pasar(EstadoConexionTiempoReal estado)
            {
                _estado = estado;
                EstadoCambiado?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class Programado : IDisposable
        {
            public TimeSpan Espera;
            public Action Accion;
            public bool Cancelado;
            public void Dispose() => Cancelado = true;
        }

        private IServicioAutenticacion auth;
        private DateTime? validoHasta;
        private AvisosFalsos avisos;
        private DateTime reloj;
        private List<Programado> programados;
        private int pasosPorUI;
        private IndicadorConexionServidor indicador;

        [TestInitialize]
        public void Inicializar()
        {
            reloj = AHORA;
            validoHasta = AHORA.AddHours(8);
            auth = A.Fake<IServicioAutenticacion>();
            A.CallTo(() => auth.TokenValidoHastaUtc).ReturnsLazily(() => validoHasta);
            A.CallTo(() => auth.ObtenerTokenValidoAsync()).Returns(Task.FromResult<string>(null));
            avisos = new AvisosFalsos();
            programados = new List<Programado>();
            pasosPorUI = 0;
            indicador = new IndicadorConexionServidor(auth, avisos,
                accion => { pasosPorUI++; accion(); },
                () => reloj,
                (espera, accion) =>
                {
                    var p = new Programado { Espera = espera, Accion = accion };
                    programados.Add(p);
                    return p;
                });
        }

        private void TokenCambia(DateTime? nuevoValidoHasta)
        {
            validoHasta = nuevoValidoHasta;
            auth.TokenCambiado += Raise.WithEmpty();
        }

        // ---- Qué se pinta ----

        [TestMethod]
        public void Calcular_CombinaTokenYConexionEnTiempoReal()
        {
            DateTime valido = AHORA.AddHours(1);
            var casos = new (DateTime? hasta, EstadoConexionTiempoReal estado, NivelConexionServidor nivel, string texto)[]
            {
                (valido, EstadoConexionTiempoReal.Conectado, NivelConexionServidor.Correcto, IndicadorConexionServidor.TEXTO_CONECTADO),
                (valido, EstadoConexionTiempoReal.Desactivado, NivelConexionServidor.Correcto, IndicadorConexionServidor.TEXTO_SIN_TIEMPO_REAL),
                (valido, EstadoConexionTiempoReal.Conectando, NivelConexionServidor.Aviso, IndicadorConexionServidor.TEXTO_CONECTANDO),
                (valido, EstadoConexionTiempoReal.Reintentando, NivelConexionServidor.Aviso, IndicadorConexionServidor.TEXTO_REINTENTANDO),
                (null, EstadoConexionTiempoReal.Conectado, NivelConexionServidor.Error, IndicadorConexionServidor.TEXTO_SIN_SESION),
                (AHORA, EstadoConexionTiempoReal.Conectado, NivelConexionServidor.Error, IndicadorConexionServidor.TEXTO_SESION_CADUCADA),
                (AHORA.AddSeconds(-1), EstadoConexionTiempoReal.Desactivado, NivelConexionServidor.Error, IndicadorConexionServidor.TEXTO_SESION_CADUCADA),
            };
            foreach (var caso in casos)
            {
                EstadoIndicadorConexion r = IndicadorConexionServidor.Calcular(caso.hasta, AHORA, caso.estado);
                Assert.AreEqual(caso.nivel, r.Nivel, $"{caso.hasta} / {caso.estado}");
                Assert.AreEqual(caso.texto, r.Descripcion, $"{caso.hasta} / {caso.estado}");
            }
        }

        [TestMethod]
        public void SinTiempoRealYConToken_Verde_ComoAntes()
        {
            // Implementación nula o parámetro AvisosTiempoReal = NO: la raya significa lo de siempre (token válido).
            indicador.Iniciar();

            Assert.AreEqual(NivelConexionServidor.Correcto, indicador.Nivel);
            Assert.AreEqual(IndicadorConexionServidor.TEXTO_SIN_TIEMPO_REAL, indicador.Descripcion);
        }

        [TestMethod]
        public void SinToken_Rojo_YNoProgramaNada()
        {
            validoHasta = null;

            indicador.Iniciar();

            Assert.AreEqual(NivelConexionServidor.Error, indicador.Nivel);
            Assert.AreEqual(0, programados.Count);
        }

        // ---- Eventos, siempre por el hilo de la interfaz ----

        [TestMethod]
        public void CambioDeLaConexionEnTiempoReal_RepintaPorElHiloDeLaInterfaz()
        {
            indicador.Iniciar();
            var cambios = new List<string>();
            indicador.PropertyChanged += (s, e) => cambios.Add(e.PropertyName);
            int pasosAntes = pasosPorUI;

            avisos.Pasar(EstadoConexionTiempoReal.Reintentando);

            Assert.AreEqual(pasosAntes + 1, pasosPorUI, "El evento llega de otro hilo: se pasa por el Dispatcher");
            Assert.AreEqual(NivelConexionServidor.Aviso, indicador.Nivel);
            Assert.AreEqual(IndicadorConexionServidor.TEXTO_REINTENTANDO, indicador.Descripcion);
            CollectionAssert.Contains(cambios, nameof(IndicadorConexionServidor.Nivel));

            avisos.Pasar(EstadoConexionTiempoReal.Conectado);

            Assert.AreEqual(NivelConexionServidor.Correcto, indicador.Nivel);
            Assert.AreEqual(IndicadorConexionServidor.TEXTO_CONECTADO, indicador.Descripcion);
        }

        [TestMethod]
        public void SinToken_AlObtenerlo_SePoneVerde()
        {
            validoHasta = null;
            indicador.Iniciar();

            TokenCambia(AHORA.AddHours(8));

            Assert.AreEqual(NivelConexionServidor.Correcto, indicador.Nivel);
            Assert.AreEqual(1, programados.Count);
        }

        // ---- Un único temporizador, al vencer el token (no un sondeo) ----

        [TestMethod]
        public void ProgramaUnSoloTemporizadorParaCuandoVenceElToken()
        {
            indicador.Iniciar();
            avisos.Pasar(EstadoConexionTiempoReal.Conectando);
            avisos.Pasar(EstadoConexionTiempoReal.Conectado);

            Assert.AreEqual(1, programados.Count, "Recalcular con la misma caducidad no reprograma");
            Assert.AreEqual(TimeSpan.FromHours(8) + IndicadorConexionServidor.HOLGURA_TEMPORIZADOR, programados[0].Espera);
        }

        [TestMethod]
        public void AlRenovarseElToken_CancelaElTemporizadorViejoYProgramaOtro()
        {
            indicador.Iniciar();

            TokenCambia(AHORA.AddHours(10));

            Assert.AreEqual(2, programados.Count);
            Assert.IsTrue(programados[0].Cancelado);
            Assert.IsFalse(programados[1].Cancelado);
            Assert.AreEqual(TimeSpan.FromHours(10) + IndicadorConexionServidor.HOLGURA_TEMPORIZADOR, programados[1].Espera);
        }

        [TestMethod]
        public void AlVencerElToken_SePoneRojoEIntentaRenovarlo_YSiLoRenuevaVuelveAVerde()
        {
            avisos.Pasar(EstadoConexionTiempoReal.Conectado);
            indicador.Iniciar();
            Assert.AreEqual(NivelConexionServidor.Correcto, indicador.Nivel);

            reloj = validoHasta.Value.AddSeconds(1);
            programados[0].Accion();

            Assert.AreEqual(NivelConexionServidor.Error, indicador.Nivel);
            Assert.AreEqual(IndicadorConexionServidor.TEXTO_SESION_CADUCADA, indicador.Descripcion);
            A.CallTo(() => auth.ObtenerTokenValidoAsync()).MustHaveHappenedOnceExactly();

            TokenCambia(reloj.AddHours(8));

            Assert.AreEqual(NivelConexionServidor.Correcto, indicador.Nivel);
            Assert.AreEqual(IndicadorConexionServidor.TEXTO_CONECTADO, indicador.Descripcion);
        }

        [TestMethod]
        public void SiElTokenYaSeRenovoAlSaltarElTemporizador_SigueVerdeYNoPideOtro()
        {
            indicador.Iniciar();
            validoHasta = AHORA.AddHours(16); // renovado sin que haya llegado el evento todavía
            reloj = AHORA.AddHours(8).AddSeconds(1);

            programados[0].Accion();

            Assert.AreEqual(NivelConexionServidor.Correcto, indicador.Nivel);
            A.CallTo(() => auth.ObtenerTokenValidoAsync()).MustNotHaveHappened();
            Assert.AreEqual(2, programados.Count, "Se programa el nuevo vencimiento");
        }

        [TestMethod]
        public void UnTokenMuyLargo_NoDesbordaElTemporizador()
        {
            validoHasta = AHORA.AddDays(365);

            indicador.Iniciar();

            Assert.AreEqual(IndicadorConexionServidor.ESPERA_MAXIMA_TEMPORIZADOR, programados[0].Espera);
        }

        [TestMethod]
        public void Dispose_CancelaElTemporizadorYDejaDeEscuchar()
        {
            indicador.Iniciar();

            indicador.Dispose();
            avisos.Pasar(EstadoConexionTiempoReal.Reintentando);

            Assert.IsTrue(programados[0].Cancelado);
            Assert.AreEqual(0, avisos.Suscriptores);
            Assert.AreEqual(NivelConexionServidor.Correcto, indicador.Nivel);
        }
    }
}
