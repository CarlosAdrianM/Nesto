using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Shared;

namespace Infrastructure.Tests
{
    /// <summary>Inmovilizados (Carlos 25/11/26): «I/1823» es el código de la casa (I, barra, solo cifras).</summary>
    [TestClass]
    public class CodigoInmovilizadoTests
    {
        [DataTestMethod]
        [DataRow("I/1823", "1823")]
        [DataRow("i/1823", "1823")]
        [DataRow("  I/1823  ", "1823")]
        [DataRow("I/7", "7")]
        public void TryLeer_CodigoValido_DevuelveElNumero(string texto, string esperado)
        {
            Assert.IsTrue(CodigoInmovilizado.TryLeer(texto, out string numero));
            Assert.AreEqual(esperado, numero);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("I/")]
        [DataRow("1823")]
        [DataRow("I1823")]
        [DataRow("I/18a3")]
        [DataRow("I/ 1823")]
        [DataRow("I/-1823")]
        [DataRow("X/1823")]
        [DataRow("II/1823")]
        [DataRow("I\\1823")]
        public void TryLeer_NoEsCodigo_False(string texto)
        {
            Assert.IsFalse(CodigoInmovilizado.TryLeer(texto, out string numero));
            Assert.IsNull(numero);
            Assert.IsFalse(CodigoInmovilizado.EsCodigo(texto));
        }

        [TestMethod]
        public void Formatear_PoneElPrefijo()
        {
            Assert.AreEqual("I/1823", CodigoInmovilizado.Formatear(" 1823 "));
            Assert.AreEqual(string.Empty, CodigoInmovilizado.Formatear(null));
        }
    }
}
