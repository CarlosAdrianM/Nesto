using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System.Linq;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.2): ParametrosDialogo sustituye a DialogParameters de Prism en IServicioDialogos
    /// y tiene que leerse igual: clave que falta o valor null → default, y conversión si el tipo no casa.
    /// </summary>
    [TestClass]
    public class ParametrosDialogoTests
    {
        private enum Color { Rojo, Verde }

        [TestMethod]
        public void InicializadorDeColeccion_GuardaLasClavesEnOrden()
        {
            var parametros = new ParametrosDialogo { { "title", "T" }, { "message", "M" } };

            Assert.AreEqual(2, parametros.Count);
            CollectionAssert.AreEqual(new[] { "title", "message" }, parametros.Keys.ToArray());
            Assert.IsTrue(parametros.ContainsKey("message"));
            Assert.IsFalse(parametros.ContainsKey("otra"));
        }

        [TestMethod]
        public void GetValue_ClaveQueFalta_DevuelveDefault()
        {
            var parametros = new ParametrosDialogo();

            Assert.IsNull(parametros.GetValue<string>("text"));
            Assert.AreEqual(0M, parametros.GetValue<decimal>("amount"));
            Assert.IsFalse(parametros.TryGetValue("amount", out decimal _));
        }

        [TestMethod]
        public void GetValue_ValorNull_DevuelveDefault()
        {
            var parametros = new ParametrosDialogo { { "amount", null } };

            Assert.AreEqual(0M, parametros.GetValue<decimal>("amount"));
            Assert.IsNull(parametros.GetValue<decimal?>("amount"));
        }

        [TestMethod]
        public void GetValue_TipoDistinto_LoConvierte()
        {
            var parametros = new ParametrosDialogo { { "amount", 5 }, { "color", "Verde" }, { "otroColor", 0 } };

            Assert.AreEqual(5M, parametros.GetValue<decimal>("amount"));
            Assert.AreEqual(5M, parametros.GetValue<decimal?>("amount"));
            Assert.AreEqual(Color.Verde, parametros.GetValue<Color>("color"));
            Assert.AreEqual(Color.Rojo, parametros.GetValue<Color>("otroColor"));
        }

        [TestMethod]
        public void ResultadoDialogo_SinParametros_TieneColeccionVacia()
        {
            var resultado = new ResultadoDialogo(ResultadoBoton.Cancel);

            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
            Assert.AreEqual(0, resultado.Parameters.Count);
        }
    }
}
