using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Menu;
using System;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#655 (Carlos 26/11/26: «no veo ninguna utilidad a tenerlo abierto»): abierto y sin la chincheta, el menú lateral se pliega
    /// solo a los 4 segundos de que el ratón salga de él; no si vuelve a entrar, ni con la chincheta, ni si al vencer no toca (ratón
    /// encima, buscador, popup de informes, Ctrl+K). Con un reloj falso.
    /// </summary>
    [TestClass]
    public class PlegadoMenuLateralPorTiempoTests
    {
        /// <summary>Un temporizador de un disparo con un reloj que se avanza a mano.</summary>
        private sealed class TemporizadorFalso : ITemporizadorUnDisparo
        {
            private TimeSpan _ahora = TimeSpan.Zero;
            private TimeSpan _vence;
            private Action _alVencer;

            public TimeSpan UltimaEspera { get; private set; }

            public bool Programado => _alVencer != null;

            public void Programar(TimeSpan espera, Action alVencer)
            {
                UltimaEspera = espera;
                _vence = _ahora + espera;
                _alVencer = alVencer;
            }

            public void Cancelar() => _alVencer = null;

            public void Avanzar(TimeSpan cuanto)
            {
                _ahora += cuanto;
                if (_alVencer != null && _ahora >= _vence)
                {
                    Action accion = _alVencer;
                    _alVencer = null;
                    accion();
                }
            }
        }

        private static (PlegadoMenuLateral plegado, TemporizadorFalso reloj) Crear(string fija = "0", List<string> errores = null)
        {
            var reloj = new TemporizadorFalso();
            var plegado = new PlegadoMenuLateral(() => fija, null, (ex, contexto) => errores?.Add(contexto)) { Temporizador = reloj };
            return (plegado, reloj);
        }

        [TestMethod]
        public void SalirElRaton_ALosCuatroSegundos_SePliega()
        {
            var (plegado, reloj) = Crear();
            int plegados = 0;

            plegado.AlSalirElRaton(true, () => true, () => plegados++);
            reloj.Avanzar(TimeSpan.FromSeconds(3.9));
            Assert.AreEqual(0, plegados, "Antes de los 4 segundos, no");
            reloj.Avanzar(TimeSpan.FromSeconds(0.1));

            Assert.AreEqual(1, plegados);
            Assert.AreEqual(TimeSpan.FromSeconds(PlegadoMenuLateral.SEGUNDOS_PLEGADO_AL_SALIR), reloj.UltimaEspera);
            Assert.AreEqual(4, PlegadoMenuLateral.SEGUNDOS_PLEGADO_AL_SALIR);
            Assert.IsFalse(plegado.PlegadoPorTiempoPendiente, "Un solo disparo");
        }

        [TestMethod]
        public void VolverAEntrar_CancelaElPlegado()
        {
            var (plegado, reloj) = Crear();
            int plegados = 0;

            plegado.AlSalirElRaton(true, () => true, () => plegados++);
            reloj.Avanzar(TimeSpan.FromSeconds(2));
            plegado.CancelarPlegadoPorTiempo(); // el ratón vuelve a entrar
            reloj.Avanzar(TimeSpan.FromSeconds(10));

            Assert.AreEqual(0, plegados);
        }

        [TestMethod]
        public void SalirOtraVez_EmpiezaDeCero()
        {
            var (plegado, reloj) = Crear();
            int plegados = 0;

            plegado.AlSalirElRaton(true, () => true, () => plegados++);
            reloj.Avanzar(TimeSpan.FromSeconds(3));
            plegado.CancelarPlegadoPorTiempo();
            plegado.AlSalirElRaton(true, () => true, () => plegados++);
            reloj.Avanzar(TimeSpan.FromSeconds(3));
            Assert.AreEqual(0, plegados);
            reloj.Avanzar(TimeSpan.FromSeconds(1));

            Assert.AreEqual(1, plegados);
        }

        [TestMethod]
        public void ConLaChincheta_NoSeProgramaNada()
        {
            var (plegado, reloj) = Crear(fija: "1");
            int plegados = 0;

            plegado.AlSalirElRaton(true, () => true, () => plegados++);
            reloj.Avanzar(TimeSpan.FromSeconds(10));

            Assert.IsFalse(reloj.Programado);
            Assert.AreEqual(0, plegados);
        }

        [TestMethod]
        public void PonerLaChincheta_CancelaLoPendiente()
        {
            var (plegado, reloj) = Crear();
            int plegados = 0;

            plegado.AlSalirElRaton(true, () => true, () => plegados++);
            plegado.Fija = true;
            reloj.Avanzar(TimeSpan.FromSeconds(10));

            Assert.AreEqual(0, plegados);
        }

        [TestMethod]
        public void Compacto_NoSeProgramaNada()
        {
            var (plegado, reloj) = Crear();

            plegado.AlSalirElRaton(false, () => true, () => Assert.Fail("Ya está compacto"));

            Assert.IsFalse(reloj.Programado);
        }

        [TestMethod]
        public void SiAlVencerNoToca_NoSePliega()
        {
            // p. ej. el ratón ha vuelto encima sin MouseEnter, se escribe en el buscador, Ctrl+K o el popup de informes abierto
            var (plegado, reloj) = Crear();
            int plegados = 0;

            plegado.AlSalirElRaton(true, () => false, () => plegados++);
            reloj.Avanzar(TimeSpan.FromSeconds(5));

            Assert.AreEqual(0, plegados);
        }

        [TestMethod]
        public void SinTemporizador_NoHaceNada()
        {
            var plegado = new PlegadoMenuLateral(() => "0");

            plegado.AlSalirElRaton(true, () => true, () => Assert.Fail("Sin temporizador no hay plegado por tiempo"));
            plegado.CancelarPlegadoPorTiempo();

            Assert.IsFalse(plegado.PlegadoPorTiempoPendiente);
        }

        [TestMethod]
        public void SiPlegarFalla_SeRegistraYNoLanza()
        {
            var errores = new List<string>();
            var (plegado, reloj) = Crear(errores: errores);

            plegado.AlSalirElRaton(true, () => true, () => throw new InvalidOperationException("roto"));
            reloj.Avanzar(TimeSpan.FromSeconds(4));

            Assert.AreEqual(1, errores.Count);
            StringAssert.Contains(errores[0], "por tiempo");
        }
    }
}
