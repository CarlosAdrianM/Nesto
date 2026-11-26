using ControlesUsuario.Models;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>Nesto#647: el núcleo común para preguntar de qué producto es un código de barras compartido.</summary>
    [TestClass]
    public class ElectorProductoCodigoBarrasTests
    {
        private static List<ProductoCodigoBarrasDuplicado> Dos() => new List<ProductoCodigoBarrasDuplicado>
        {
            new ProductoCodigoBarrasDuplicado { Producto = "41000", Nombre = "GUANTES M" },
            new ProductoCodigoBarrasDuplicado { Producto = "41001 ", Nombre = "GUANTES P" }
        };

        [TestMethod]
        public void LeerCandidatos_ListaDeGetProductos()
        {
            var candidatos = ElectorProductoCodigoBarras.LeerCandidatos("[{\"Producto\":\"41000\",\"Nombre\":\"A\"},{\"Producto\":\"41001\",\"Nombre\":\"B\"}]");

            CollectionAssert.AreEqual(new[] { "41000", "41001" }, candidatos.Select(c => c.Producto).ToArray());
        }

        [TestMethod]
        public void LeerCandidatos_ObjetoConMessageYProductos()
        {
            var candidatos = ElectorProductoCodigoBarras.LeerCandidatos(
                "{\"Message\":\"Lo llevan varios\",\"Productos\":[{\"Producto\":\"41000\",\"Cantidad\":1},{\"Producto\":\"41001\",\"Cantidad\":1},{\"Producto\":\"41001\"}]}");

            Assert.AreEqual(2, candidatos.Count, "sin repetir referencia");
        }

        [TestMethod]
        public void LeerCandidatos_OtroCuerpo_ListaVacia()
        {
            Assert.AreEqual(0, ElectorProductoCodigoBarras.LeerCandidatos("{\"Message\":\"Otro 409\",\"Codigo\":\"FilasDuplicadas\"}").Count);
            Assert.AreEqual(0, ElectorProductoCodigoBarras.LeerCandidatos("no es json").Count);
            Assert.AreEqual(0, ElectorProductoCodigoBarras.LeerCandidatos(null).Count);
        }

        [TestMethod]
        public async Task Elegir_Varios_PreguntaYDevuelveElElegido()
        {
            var dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowDialogAsync(ElectorProductoCodigoBarras.NOMBRE_DIALOGO, A<ParametrosDialogo>._))
                .Returns(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "producto", "41001" } }));

            string elegido = await ElectorProductoCodigoBarras.ElegirAsync(dialogos, Dos(), "8437");

            Assert.AreEqual("41001", elegido);
            A.CallTo(() => dialogos.ShowDialogAsync(ElectorProductoCodigoBarras.NOMBRE_DIALOGO,
                A<ParametrosDialogo>.That.Matches(p => p.GetValue<string>("title") == "El código de barras 8437 es de varios productos"
                    && p.GetValue<IEnumerable<ProductoCodigoBarrasDuplicado>>("candidatos").Count() == 2)))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Elegir_Cancela_Null()
        {
            var dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).Returns(new ResultadoDialogo(ResultadoBoton.Cancel));

            Assert.IsNull(await ElectorProductoCodigoBarras.ElegirAsync(dialogos, Dos(), "8437"));
        }

        [TestMethod]
        public async Task Elegir_UnoSolo_EsEseSinPreguntar()
        {
            var dialogos = A.Fake<IServicioDialogos>();

            string elegido = await ElectorProductoCodigoBarras.ElegirAsync(dialogos, Dos().Take(1), "8437");

            Assert.AreEqual("41000", elegido);
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
        }
    }
}
