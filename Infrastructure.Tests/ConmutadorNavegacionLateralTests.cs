using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Menu;
using System;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (panel lateral, tramo 1): el piloto del menú lateral. "1" = menú lateral; cualquier otra cosa, sin fila o
    /// si no se puede leer = la cinta. Se lee una sola vez. El panel sale compacto salvo que el usuario lo dejara abierto.
    /// </summary>
    [TestClass]
    public class ConmutadorNavegacionLateralTests
    {
        [DataTestMethod]
        [DataRow("1", true)]
        [DataRow(" 1 ", true)]
        [DataRow("0", false)]
        [DataRow("", false)]
        [DataRow(null, false)]
        [DataRow("SI", false)]
        public void Activa_SoloConUno(string valor, bool esperado)
        {
            var conmutador = new ConmutadorNavegacionLateral(() => valor);

            Assert.AreEqual(esperado, conmutador.Activa);
        }

        [TestMethod]
        public void Activa_SiNoSePuedeLeerElParametro_LaCinta()
        {
            var conmutador = new ConmutadorNavegacionLateral(() => throw new InvalidOperationException("sin fila"));

            Assert.IsFalse(conmutador.Activa);
        }

        [TestMethod]
        public void Activa_SeLeeUnaSolaVez()
        {
            int lecturas = 0;
            string valor = "1";
            var conmutador = new ConmutadorNavegacionLateral(() => { lecturas++; return valor; });

            Assert.IsTrue(conmutador.Activa);
            valor = "0"; // cambiarlo desde Parámetros no se nota hasta reiniciar
            Assert.IsTrue(conmutador.Activa);
            Assert.AreEqual(1, lecturas);
        }

        [DataTestMethod]
        [DataRow(null, true)]
        [DataRow("", true)]
        [DataRow("1", true)]
        [DataRow("0", false)]
        public void CompactaAlArrancar_PorDefectoSi_SalvoQueLoDejaraAbierto(string valor, bool esperado)
        {
            var conmutador = new ConmutadorNavegacionLateral(() => "1", () => valor);

            Assert.AreEqual(esperado, conmutador.CompactaAlArrancar);
        }

        [TestMethod]
        public void CompactaAlArrancar_SinLectorOSiFalla_Compacta()
        {
            Assert.IsTrue(new ConmutadorNavegacionLateral(() => "1").CompactaAlArrancar);
            Assert.IsTrue(new ConmutadorNavegacionLateral(() => "1", () => throw new Exception("sin red")).CompactaAlArrancar);
        }

        [TestMethod]
        public void RecordarCompacta_GuardaSoloCuandoCambia()
        {
            var guardados = new List<string>();
            var conmutador = new ConmutadorNavegacionLateral(() => "1", () => null, guardados.Add);

            conmutador.RecordarCompacta(true);  // ya estaba compacto: nada
            conmutador.RecordarCompacta(false); // lo abre
            conmutador.RecordarCompacta(false); // sigue abierto: nada
            conmutador.RecordarCompacta(true);  // lo cierra

            CollectionAssert.AreEqual(new[] { "0", "1" }, guardados);
        }

        [TestMethod]
        public void RecordarCompacta_SiNoSePuedeGuardar_NoRompeYLoRegistra()
        {
            var errores = new List<string>();
            var conmutador = new ConmutadorNavegacionLateral(() => "1", () => null,
                _ => throw new InvalidOperationException("sin red"), (ex, contexto) => errores.Add(contexto));

            conmutador.RecordarCompacta(false);

            Assert.AreEqual(1, errores.Count);
            StringAssert.Contains(errores[0], "NavegacionLateralCompacta");
        }
    }
}
