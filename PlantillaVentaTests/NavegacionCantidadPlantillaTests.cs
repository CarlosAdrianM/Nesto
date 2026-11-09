using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PlantillaVenta;
using System.Windows.Input;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#633 (sugerencia 1060, Paloma): Intro en la cantidad confirma y devuelve el cursor al buscador.
    /// Tab sigue como hoy. El cambio de foco va al SOLTAR Intro: si fuera al pulsarlo, el KeyUp de ese
    /// mismo Intro llegaría al buscador y fijaría su texto como filtro.
    /// </summary>
    [TestClass]
    public class NavegacionCantidadPlantillaTests
    {
        [TestMethod]
        public void Intro_SePulsaYSeSuelta_VuelveAlBuscador()
        {
            var sut = new NavegacionCantidadPlantilla();

            bool manejarPulsacion = sut.AlPulsar(Key.Enter, ModifierKeys.None, yaManejada: false);
            bool volver = sut.AlSoltar(Key.Enter);

            Assert.IsTrue(manejarPulsacion, "La pulsación de Intro se marca como manejada (que no avance de fila ni pulse otro botón)");
            Assert.IsTrue(volver);
        }

        [TestMethod]
        public void Intro_AlPulsar_NoVuelveTodaviaAlBuscador()
        {
            var sut = new NavegacionCantidadPlantilla();

            sut.AlPulsar(Key.Enter, ModifierKeys.None, yaManejada: false);

            Assert.IsTrue(sut.VolverAlBuscadorPendiente);
        }

        [TestMethod]
        public void Tab_NoHaceNada()
        {
            var sut = new NavegacionCantidadPlantilla();

            Assert.IsFalse(sut.AlPulsar(Key.Tab, ModifierKeys.None, yaManejada: false));
            Assert.IsFalse(sut.AlSoltar(Key.Tab));
        }

        [TestMethod]
        public void IntroConModificador_NoHaceNada()
        {
            var sut = new NavegacionCantidadPlantilla();

            Assert.IsFalse(sut.AlPulsar(Key.Enter, ModifierKeys.Control, yaManejada: false));
            Assert.IsFalse(sut.AlSoltar(Key.Enter));
        }

        [TestMethod]
        public void IntroYaManejado_CantidadNoValida_NoVuelveAlBuscador()
        {
            // El ShortUpDown marca Intro como manejado cuando no puede confirmar lo escrito:
            // el cursor se queda en la cantidad para corregirla.
            var sut = new NavegacionCantidadPlantilla();

            Assert.IsFalse(sut.AlPulsar(Key.Enter, ModifierKeys.None, yaManejada: true));
            Assert.IsFalse(sut.AlSoltar(Key.Enter));
        }

        [TestMethod]
        public void SoltarIntroSinHaberloPulsadoAqui_NoVuelveAlBuscador()
        {
            var sut = new NavegacionCantidadPlantilla();

            Assert.IsFalse(sut.AlSoltar(Key.Enter));
        }

        [TestMethod]
        public void TrasVolver_ElSiguienteSoltarNoRepite()
        {
            var sut = new NavegacionCantidadPlantilla();
            sut.AlPulsar(Key.Enter, ModifierKeys.None, yaManejada: false);
            sut.AlSoltar(Key.Enter);

            Assert.IsFalse(sut.AlSoltar(Key.Enter));
        }

        [TestMethod]
        public void PerderElFoco_CancelaLoPendiente()
        {
            var sut = new NavegacionCantidadPlantilla();
            sut.AlPulsar(Key.Enter, ModifierKeys.None, yaManejada: false);

            sut.Cancelar();

            Assert.IsFalse(sut.AlSoltar(Key.Enter));
        }
    }
}
