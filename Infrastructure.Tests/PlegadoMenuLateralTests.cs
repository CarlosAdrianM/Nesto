using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Menu;
using System;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#655 (paso 0): el menú lateral se pliega solo al trabajar fuera de él, salvo con el ratón encima, escribiendo en su
    /// buscador o con la chincheta (que se recuerda por usuario en NavegacionLateralFija).
    /// </summary>
    [TestClass]
    public class PlegadoMenuLateralTests
    {
        [DataTestMethod]
        [DataRow("1", true)]
        [DataRow(" 1 ", true)]
        [DataRow("0", false)]
        [DataRow("", false)]
        [DataRow(null, false)]
        public void Fija_SoloConUno(string valor, bool esperado)
        {
            Assert.AreEqual(esperado, new PlegadoMenuLateral(() => valor).Fija);
        }

        [TestMethod]
        public void Fija_SinParametroLegible_SePliegaSoloYLoRegistra()
        {
            var errores = new List<string>();
            var plegado = new PlegadoMenuLateral(() => throw new InvalidOperationException("sin fila"), null, (ex, contexto) => errores.Add(contexto));

            Assert.IsFalse(plegado.Fija);
            Assert.AreEqual(1, errores.Count);
            StringAssert.Contains(errores[0], "NavegacionLateralFija");
        }

        [TestMethod]
        public void Fija_SeLeeUnaSolaVez()
        {
            int lecturas = 0;
            var plegado = new PlegadoMenuLateral(() => { lecturas++; return "0"; });

            _ = plegado.Fija;
            _ = plegado.Fija;

            Assert.AreEqual(1, lecturas);
        }

        [TestMethod]
        public void Fija_AlCambiar_SeGuardaSoloSiCambiaYAvisa()
        {
            var guardados = new List<string>();
            var cambios = new List<string>();
            var plegado = new PlegadoMenuLateral(() => "0", guardados.Add);
            plegado.PropertyChanged += (s, e) => cambios.Add(e.PropertyName);

            plegado.Fija = false;
            plegado.Fija = true;
            plegado.Fija = true;
            plegado.Fija = false;

            CollectionAssert.AreEqual(new[] { "1", "0" }, guardados);
            CollectionAssert.Contains(cambios, nameof(PlegadoMenuLateral.Fija));
            CollectionAssert.Contains(cambios, nameof(PlegadoMenuLateral.TextoChincheta));
        }

        [TestMethod]
        public void Fija_SiNoSePuedeGuardar_NoRompeYSeQuedaPuesta()
        {
            var errores = new List<string>();
            var plegado = new PlegadoMenuLateral(() => "0", _ => throw new InvalidOperationException("sin red"), (ex, contexto) => errores.Add(contexto));

            plegado.Fija = true;

            Assert.IsTrue(plegado.Fija);
            Assert.AreEqual(1, errores.Count);
        }

        [TestMethod]
        public void TextoChincheta_DeUstedSegunEsteFija()
        {
            var plegado = new PlegadoMenuLateral(() => "0");
            Assert.AreEqual(PlegadoMenuLateral.TEXTO_FIJAR, plegado.TextoChincheta);

            plegado.Fija = true;
            Assert.AreEqual(PlegadoMenuLateral.TEXTO_SOLTAR, plegado.TextoChincheta);
        }

        [DataTestMethod]
        [DataRow(true, false, false, false, true, DisplayName = "abierto, sin chincheta, ratón fuera, sin escribir: se pliega")]
        [DataRow(false, false, false, false, false, DisplayName = "ya compacto: nada")]
        [DataRow(true, true, false, false, false, DisplayName = "con la chincheta: no")]
        [DataRow(true, false, true, false, false, DisplayName = "con el ratón encima: no")]
        [DataRow(true, false, false, true, false, DisplayName = "escribiendo en su buscador: no")]
        public void DebePlegarse(bool abierto, bool fija, bool ratonEncima, bool escribiendo, bool esperado)
        {
            var plegado = new PlegadoMenuLateral(() => fija ? "1" : "0");

            Assert.AreEqual(esperado, plegado.DebePlegarse(abierto, ratonEncima, escribiendo));
        }

        [TestMethod]
        public void Conmutador_LlevaElPlegadoConSuParametro()
        {
            var guardados = new List<string>();
            var conmutador = new ConmutadorNavegacionLateral(() => "1", leerFija: () => "1", guardarFija: guardados.Add);

            Assert.IsTrue(conmutador.Plegado.Fija);
            conmutador.Plegado.Fija = false;
            CollectionAssert.AreEqual(new[] { "0" }, guardados);
        }

        [TestMethod]
        public void Conmutador_SinParametroDeLaChincheta_SePliegaSolo()
        {
            Assert.IsFalse(new ConmutadorNavegacionLateral(() => "1").Plegado.Fija);
        }
    }
}
