using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Shared;

namespace Infrastructure.Tests
{
    /// <summary>
    /// Nesto#372: el popup de novedades solo se muestra cuando la versión actual es POSTERIOR
    /// a la última vista. La primera vez (sin parámetro guardado) no se muestra: se hace un
    /// arranque silencioso para no enseñar todo el histórico de golpe.
    /// </summary>
    [TestClass]
    public class NovedadesHelperTests
    {
        [TestMethod]
        public void DebeMostrarNovedades_VersionActualPosterior_True()
        {
            Assert.IsTrue(NovedadesHelper.DebeMostrarNovedades("1.10.6.0", "1.10.5.3"));
        }

        [TestMethod]
        public void DebeMostrarNovedades_MismaVersion_False()
        {
            Assert.IsFalse(NovedadesHelper.DebeMostrarNovedades("1.10.5.3", "1.10.5.3"));
        }

        [TestMethod]
        public void DebeMostrarNovedades_VersionActualAnterior_False()
        {
            // Puede pasar si el usuario entra en una máquina con una versión antigua
            Assert.IsFalse(NovedadesHelper.DebeMostrarNovedades("1.10.4.0", "1.10.5.3"));
        }

        [TestMethod]
        public void DebeMostrarNovedades_ComparacionSemantica_NoAlfabetica()
        {
            Assert.IsTrue(NovedadesHelper.DebeMostrarNovedades("1.10.10.0", "1.10.5.3"));
        }

        [TestMethod]
        public void DebeMostrarNovedades_SinUltimaVersionVista_FalseBootstrapSilencioso()
        {
            Assert.IsFalse(NovedadesHelper.DebeMostrarNovedades("1.10.6.0", null));
            Assert.IsFalse(NovedadesHelper.DebeMostrarNovedades("1.10.6.0", ""));
        }

        [TestMethod]
        public void DebeMostrarNovedades_VersionActualInvalida_False()
        {
            Assert.IsFalse(NovedadesHelper.DebeMostrarNovedades(null, "1.10.5.3"));
            Assert.IsFalse(NovedadesHelper.DebeMostrarNovedades("dev", "1.10.5.3"));
        }
    }
}
