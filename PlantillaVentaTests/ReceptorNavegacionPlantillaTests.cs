using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#490 (4C.4, 6.º tramo): la plantilla recibe la navegación por <see cref="IReceptorNavegacionPestanaNueva"/>
    /// en vez de INavigationAware: cada navegación abre una plantilla nueva (antes IsNavigationTarget = False) y
    /// <c>AlLlegar</c> hace lo de <c>OnNavigatedTo</c>, con las mismas claves («Modificar con plantilla», Nesto#397).
    /// </summary>
    [TestClass]
    public class ReceptorNavegacionPlantillaTests
    {
        private IPlantillaVentaService _servicio;

        private PlantillaVentaViewModel CrearViewModel()
        {
            _servicio = A.Fake<IPlantillaVentaService>();
            A.CallTo(() => _servicio.CargarProductosBonificablesIds()).Returns(Task.FromResult(new HashSet<string>()));
            // Sin pedido: avisa y no sigue (así la prueba no depende de cargar el borrador)
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla(A<string>._, A<int>._)).Returns(Task.FromResult<PedidoParaPlantillaModel>(null));
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            return new PlantillaVentaViewModel(A.Fake<IUnityContainer>(), A.Fake<IServicioNavegacion>(), configuracion, _servicio,
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IPedidoVentaService>(),
                A.Fake<IBorradorPlantillaVentaService>(), A.Fake<IServicioAutenticacion>());
        }

        private async Task EsperarCargaDelPedido()
        {
            for (int i = 0; i < 100 && !Fake.GetCalls(_servicio).Any(c => c.Method.Name == nameof(IPlantillaVentaService.CargarPedidoParaPlantilla)); i++)
            {
                await Task.Delay(20);
            }
        }

        [TestMethod]
        public void Plantilla_NoDependeDeLaNavegacionDePrismYAbrePestanaNueva()
        {
            Assert.IsFalse(typeof(Prism.Regions.INavigationAware).IsAssignableFrom(typeof(PlantillaVentaViewModel)));
            Assert.IsTrue(typeof(IReceptorNavegacionPestanaNueva).IsAssignableFrom(typeof(PlantillaVentaViewModel)));
            Assert.IsTrue(typeof(ITabCloseConfirmation).IsAssignableFrom(typeof(PlantillaVentaViewModel)));
        }

        [TestMethod]
        public async Task AlLlegar_ConPedidoAModificar_CargaEsePedidoDeSuEmpresa()
        {
            var vm = CrearViewModel();

            vm.AlLlegar(new ParametrosNavegacion { { "pedidoAModificar", 912345 }, { "empresaPedido", "3" } });
            await EsperarCargaDelPedido();

            A.CallTo(() => _servicio.CargarPedidoParaPlantilla("3", 912345)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AlLlegar_ConPedidoAModificarSinEmpresa_LoCargaDeLaEmpresaPorDefecto()
        {
            var vm = CrearViewModel();

            vm.AlLlegar(new ParametrosNavegacion { { "pedidoAModificar", 912345 } });
            await EsperarCargaDelPedido();

            A.CallTo(() => _servicio.CargarPedidoParaPlantilla("1", 912345)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AlLlegar_SinParametros_CargaLosBonificablesYNoCargaNingunPedido()
        {
            var vm = CrearViewModel();

            vm.AlLlegar(new ParametrosNavegacion());
            await Task.Delay(100);

            A.CallTo(() => _servicio.CargarProductosBonificablesIds()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla(A<string>._, A<int>._)).MustNotHaveHappened();
        }
    }
}
