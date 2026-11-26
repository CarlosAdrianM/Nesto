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

        // ---------- Lo tecleado en una casilla que no recibe 409 (pedido de compra, plantilla; Carlos 26/11/26) ----------

        [DataTestMethod]
        [DataRow("8436538394380", true)]
        [DataRow(" 2900000174047 ", true)]
        [DataRow("12345678", true)]
        [DataRow("17404", false)]
        [DataRow("1234567", false)]
        [DataRow("843653839438012", false)]
        [DataRow("ESMALTE", false)]
        [DataRow("8436A38394380", false)]
        [DataRow(null, false)]
        public void PareceCodigoBarras(string texto, bool esperado)
        {
            Assert.AreEqual(esperado, ElectorProductoCodigoBarras.PareceCodigoBarras(texto));
        }

        [TestMethod]
        public async Task LoTecleado_NoEsCodigo_TalCualSinPreguntarNiBuscar()
        {
            bool buscado = false;

            string r = await ElectorProductoCodigoBarras.ReferenciaDeLoTecleadoAsync(A.Fake<IServicioDialogos>(), "17404",
                _ => { buscado = true; return Task.FromResult(Dos()); });

            Assert.AreEqual("17404", r);
            Assert.IsFalse(buscado);
        }

        [TestMethod]
        public async Task LoTecleado_CodigoSinProductos_TalCual()
        {
            Assert.AreEqual("8436538394380", await ElectorProductoCodigoBarras.ReferenciaDeLoTecleadoAsync(A.Fake<IServicioDialogos>(),
                "8436538394380", _ => Task.FromResult(new List<ProductoCodigoBarrasDuplicado>())));
            Assert.AreEqual("8436538394380", await ElectorProductoCodigoBarras.ReferenciaDeLoTecleadoAsync(A.Fake<IServicioDialogos>(),
                "8436538394380", _ => Task.FromResult<List<ProductoCodigoBarrasDuplicado>>(null)), "la API no contesta: como si no fuera un código");
        }

        [TestMethod]
        public async Task LoTecleado_CodigoDeVarios_PreguntaYDaElElegido_OCancelaNull()
        {
            var dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowDialogAsync(ElectorProductoCodigoBarras.NOMBRE_DIALOGO, A<ParametrosDialogo>._))
                .Returns(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "producto", "41001" } })).Once()
                .Then.Returns(new ResultadoDialogo(ResultadoBoton.Cancel));

            Assert.AreEqual("41001", await ElectorProductoCodigoBarras.ReferenciaDeLoTecleadoAsync(dialogos, "8436538394380", _ => Task.FromResult(Dos())));
            Assert.IsNull(await ElectorProductoCodigoBarras.ReferenciaDeLoTecleadoAsync(dialogos, "8436538394380", _ => Task.FromResult(Dos())));
        }

        private sealed class Transporte : System.Net.Http.HttpMessageHandler
        {
            public System.Net.HttpStatusCode Estado { get; set; } = System.Net.HttpStatusCode.OK;
            public string Cuerpo { get; set; } = "[]";
            public System.Uri Pedida { get; private set; }

            protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                Pedida = request.RequestUri;
                return Task.FromResult(new System.Net.Http.HttpResponseMessage(Estado) { Content = new System.Net.Http.StringContent(Cuerpo) });
            }
        }

        [TestMethod]
        public async Task BuscarCandidatos_PorCodigoBarras_UnoPorProducto()
        {
            var transporte = new Transporte
            {
                Cuerpo = "[{\"Producto\":\"41000\",\"Nombre\":\"GUANTES M\",\"Cantidad\":1},{\"Producto\":\"41000\",\"Cantidad\":6},{\"Producto\":\"41001\",\"Nombre\":\"GUANTES P\"}]"
            };
            using var client = new System.Net.Http.HttpClient(transporte) { BaseAddress = new System.Uri("http://api.prueba/api/") };

            List<ProductoCodigoBarrasDuplicado> candidatos = await ElectorProductoCodigoBarras.BuscarCandidatosAsync(client, " 8436538394380 ", "1");

            Assert.AreEqual("/api/Productos/PorCodigoBarras?codigo=8436538394380&empresa=1", transporte.Pedida.PathAndQuery);
            CollectionAssert.AreEqual(new[] { "41000", "41001" }, candidatos.Select(c => c.Producto).ToArray());
        }

        [TestMethod]
        public async Task BuscarCandidatos_ErrorDeLaApi_Null()
        {
            using var client = new System.Net.Http.HttpClient(new Transporte { Estado = System.Net.HttpStatusCode.InternalServerError })
            {
                BaseAddress = new System.Uri("http://api.prueba/api/")
            };

            Assert.IsNull(await ElectorProductoCodigoBarras.BuscarCandidatosAsync(client, "8436538394380", "1"));
        }

        // Nesto#647 (cola): la ruta y la lectura comunes, para los servicios con su propio tratamiento de errores
        [TestMethod]
        public void RutaYLectura_Comunes()
        {
            Assert.AreEqual("Productos/PorCodigoBarras?codigo=8436538394380&empresa=1",
                ElectorProductoCodigoBarras.RutaPorCodigoBarras(" 8436538394380 ", " 1 "));
            List<ProductoCodigoBarrasDuplicado> candidatos = ElectorProductoCodigoBarras.LeerCandidatosPorCodigoBarras(
                "[{\"Producto\":\"41000 \",\"Nombre\":\"GUANTES M \"},{\"Producto\":\"41000\",\"Cantidad\":6},{\"Producto\":\" \"},{\"Producto\":\"41001\"}]");
            CollectionAssert.AreEqual(new[] { "41000", "41001" }, candidatos.Select(c => c.Producto).ToArray());
            Assert.AreEqual("GUANTES M", candidatos[0].Nombre);
            Assert.AreEqual(0, ElectorProductoCodigoBarras.LeerCandidatosPorCodigoBarras("").Count, "un 404 tratado como vacío");
        }

        [TestMethod]
        public async Task SiEsDeVarios_SoloCambiaConVarios()
        {
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowDialogAsync(ElectorProductoCodigoBarras.NOMBRE_DIALOGO, A<ParametrosDialogo>._))
                .Returns(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "producto", "41001" } }));

            Assert.AreEqual("41001", await ElectorProductoCodigoBarras.ReferenciaSiEsDeVariosAsync(dialogos, "8436538394380", _ => Task.FromResult(Dos())));
            Assert.AreEqual("8436538394380", await ElectorProductoCodigoBarras.ReferenciaSiEsDeVariosAsync(dialogos, "8436538394380",
                _ => Task.FromResult(Dos().Take(1).ToList())), "de uno: tal cual");
            Assert.AreEqual("8436538394380", await ElectorProductoCodigoBarras.ReferenciaSiEsDeVariosAsync(dialogos, "8436538394380",
                _ => Task.FromResult<List<ProductoCodigoBarrasDuplicado>>(null)), "la API no contesta: tal cual");
            Assert.AreEqual("guantes", await ElectorProductoCodigoBarras.ReferenciaSiEsDeVariosAsync(dialogos, "guantes", _ => Task.FromResult(Dos())));
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustHaveHappenedOnceExactly();
        }
    }
}
