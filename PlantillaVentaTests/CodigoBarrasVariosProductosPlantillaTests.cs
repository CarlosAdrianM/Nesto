using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#647 (Carlos 26/11/26): en el buscador de la plantilla, un código de barras de varios productos pregunta cuál es (el
    /// selector común) y se busca la referencia elegida; la búsqueda de la API (PlantillaVentas/BuscarProducto) solo mira el código
    /// principal de la ficha, así que los candidatos salen de Productos/PorCodigoBarras, como en el núcleo.
    /// </summary>
    [TestClass]
    public class CodigoBarrasVariosProductosPlantillaTests
    {
        private const string EAN = "8436538394380";

        private static (PlantillaVentaViewModel vm, IServicioDialogos dialogos, List<string> buscados) CrearViewModel(params string[] candidatos)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), dialogos, A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            var buscados = new List<string>();
            vm.BuscarCandidatosCodigoBarras = codigo =>
            {
                buscados.Add(codigo);
                return Task.FromResult(candidatos.Select(p => new ProductoCodigoBarrasDuplicado { Producto = p, Nombre = "PACK " + p }).ToList());
            };
            return (vm, dialogos, buscados);
        }

        [TestMethod]
        public async Task CodigoDeVarios_PreguntaYBuscaElElegido()
        {
            var (vm, dialogos, _) = CrearViewModel("44967", "45931");
            A.CallTo(() => dialogos.ShowDialogAsync(ElectorProductoCodigoBarras.NOMBRE_DIALOGO, A<ParametrosDialogo>._))
                .Returns(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "producto", "45931" } }));

            Assert.AreEqual("45931", await vm.FiltroParaBuscarAsync(EAN));
        }

        [TestMethod]
        public async Task CodigoDeVarios_Cancela_NoSeBuscaNada()
        {
            var (vm, dialogos, _) = CrearViewModel("44967", "45931");
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).Returns(new ResultadoDialogo(ResultadoBoton.Cancel));

            Assert.IsNull(await vm.FiltroParaBuscarAsync(EAN));
        }

        [TestMethod]
        public async Task CodigoDeUno_EsaReferenciaSinPreguntar()
        {
            var (vm, dialogos, _) = CrearViewModel("44967");

            Assert.AreEqual("44967", await vm.FiltroParaBuscarAsync(EAN));
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task TextoNormal_SeBuscaTalCualSinMirarCodigos()
        {
            var (vm, _, buscados) = CrearViewModel("44967", "45931");

            Assert.AreEqual("esmalte", await vm.FiltroParaBuscarAsync("esmalte"));
            Assert.AreEqual("17404", await vm.FiltroParaBuscarAsync("17404"));
            Assert.AreEqual(0, buscados.Count);
        }

        // Nesto#647 (cola): la búsqueda contextual (PlantillaVentas/Buscar) solo cambia si el código es de varios productos
        [TestMethod]
        public async Task Contextual_CodigoDeVarios_PreguntaYBuscaElElegido_OCancela()
        {
            var (vm, dialogos, _) = CrearViewModel("44967", "45931");
            A.CallTo(() => dialogos.ShowDialogAsync(ElectorProductoCodigoBarras.NOMBRE_DIALOGO, A<ParametrosDialogo>._))
                .Returns(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "producto", "45931" } })).Once()
                .Then.Returns(new ResultadoDialogo(ResultadoBoton.Cancel));

            Assert.AreEqual("45931", await vm.FiltroParaBuscarContextualAsync(EAN));
            Assert.IsNull(await vm.FiltroParaBuscarContextualAsync(EAN), "cancelar = no se busca nada");
        }

        [TestMethod]
        public async Task Contextual_CodigoDeUnoONinguno_LoTecleadoTalCual()
        {
            var (deUno, dialogos, _) = CrearViewModel("44967");
            var (deNinguno, _, _) = CrearViewModel();

            Assert.AreEqual(EAN, await deUno.FiltroParaBuscarContextualAsync(EAN), "como siempre: no cambia la búsqueda");
            Assert.AreEqual(EAN, await deNinguno.FiltroParaBuscarContextualAsync(EAN));
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Contextual_TextoNormal_SinMirarCodigos()
        {
            var (vm, _, buscados) = CrearViewModel("44967", "45931");

            Assert.AreEqual("champú sin sal", await vm.FiltroParaBuscarContextualAsync("champú sin sal"));
            Assert.AreEqual(0, buscados.Count);
        }

        [TestMethod]
        public void BuscadoPorCodigo_SoloLaLineaDelElegido()
        {
            var lineas = new ObservableCollection<LineaPlantillaVenta>
            {
                new LineaPlantillaVenta { producto = "45931" },
                new LineaPlantillaVenta { producto = "459310" }
            };

            ObservableCollection<LineaPlantillaVenta> solo = PlantillaVentaViewModel.SoloElElegido(lineas, "45931");

            Assert.AreEqual("45931", solo.Single().producto);
            Assert.AreSame(lineas, PlantillaVentaViewModel.SoloElElegido(lineas, "99999"), "si no viene, la búsqueda tal cual");
        }
    }
}
