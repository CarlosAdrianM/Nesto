using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos;
using Nesto.Modulos.CanalesExternos.Models.Cuadres;
using Nesto.Modulos.CanalesExternos.ViewModels;
using Prism.Services.Dialogs;
using System.Reflection;
using System.Threading.Tasks;

namespace CanalesExternosTests
{
    [TestClass]
    public class CanalesExternosCuadreFacturasViewModelTests
    {
        // El ViewModel construye su propio Factory con CanalExternoFacturasAmazon real, que depende
        // de IConfiguracion con la URL del API. Para testear lógica sin red, sustituimos el Factory
        // vía reflexión inyectando un fake de ICanalExternoFacturas.
        private static CanalesExternosCuadreFacturasViewModel CrearVm(ICanalExternoFacturas canalFake)
        {
            var configuracion = A.Fake<IConfiguracion>();
            var servicioAutenticacion = A.Fake<IServicioAutenticacion>();
            var dialogService = A.Fake<IDialogService>();
            var vm = new CanalesExternosCuadreFacturasViewModel(configuracion, servicioAutenticacion, dialogService);

            var factoryProp = vm.Factory;
            factoryProp["Amazon"] = canalFake;
            return vm;
        }

        [TestMethod]
        public async Task CalcularCuadre_PopulaCuadreFacturasDesdeElCanal()
        {
            var canal = A.Fake<ICanalExternoFacturas>();
            A.CallTo(() => canal.SoportaCuadreLiquidacion).Returns(true);
            var cuadreFacturasFake = new ResultadoCuadre<string> { Nombre = "test" };
            cuadreFacturasFake.Cuadrados.Add(new ElementoCuadre<string> { Clave = "A", ExisteEnNesto = true, ExisteEnAmazon = true });
            cuadreFacturasFake.SoloEnAmazon.Add(new ElementoCuadre<string> { Clave = "B", ExisteEnAmazon = true });
            A.CallTo(() => canal.CuadrarFacturasAsync(A<int>._, A<int>._)).Returns(Task.FromResult(cuadreFacturasFake));
            A.CallTo(() => canal.CuadrarConLiquidacionAsync(A<int>._, A<int>._))
                .Returns(Task.FromResult(new Nesto.Modulos.CanalesExternos.Models.CuadreLiquidacionCanalExterno()));

            var vm = CrearVm(canal);
            vm.Año = 2026;
            vm.Mes = 2;

            // Act — ejecutamos la tarea del comando directamente via reflexión
            // (el DelegateCommand envuelve la lambda async).
            var metodo = typeof(CanalesExternosCuadreFacturasViewModel).GetMethod(
                "OnCalcularCuadreAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            await (Task)metodo.Invoke(vm, null);

            // Assert
            Assert.IsNotNull(vm.CuadreFacturas);
            Assert.AreEqual(1, vm.FacturasCuadradas.Count);
            Assert.AreEqual("A", vm.FacturasCuadradas[0].Clave);
            Assert.AreEqual(1, vm.FacturasSoloEnAmazon.Count);
            Assert.AreEqual(0, vm.FacturasSoloEnNesto.Count);
        }

        [TestMethod]
        public async Task CalcularCuadre_CanalLanzaExcepcion_NoPropagaYMuestraError()
        {
            var canal = A.Fake<ICanalExternoFacturas>();
            A.CallTo(() => canal.SoportaCuadreLiquidacion).Returns(true);
            A.CallTo(() => canal.CuadrarConLiquidacionAsync(A<int>._, A<int>._))
                .Throws(new System.Exception("API caída"));

            var vm = CrearVm(canal);

            var metodo = typeof(CanalesExternosCuadreFacturasViewModel).GetMethod(
                "OnCalcularCuadreAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            await (Task)metodo.Invoke(vm, null);  // no debe propagar

            Assert.IsFalse(vm.EstaOcupado, "Debe liberar EstaOcupado incluso si hay excepción");
            Assert.IsNull(vm.CuadreFacturas);
        }

        [TestMethod]
        public void TituloViewModel_EsCuadreCanalesExternos()
        {
            var canal = A.Fake<ICanalExternoFacturas>();
            var vm = CrearVm(canal);

            Assert.AreEqual("Cuadre Canales Externos", vm.Titulo);
        }
    }
}
