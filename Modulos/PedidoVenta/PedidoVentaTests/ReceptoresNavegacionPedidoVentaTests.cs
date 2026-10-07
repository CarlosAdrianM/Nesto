using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoVenta;
using System.Threading.Tasks;
using Unity;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#490 (4C.4): el detalle del pedido de venta recibe la navegación por <see cref="IReceptorNavegacionPestanaNueva"/>
    /// en vez de INavigationAware: cada pedido elegido abre un detalle nuevo en DetallePedidoRegion (antes
    /// IsNavigationTarget devolvía False) y <c>AlLlegar</c> hace lo de <c>OnNavigatedTo</c>, con la misma clave.
    /// </summary>
    [TestClass]
    public class ReceptoresNavegacionPedidoVentaTests
    {
        private IConfiguracion _configuracion;
        private IPedidoVentaService _servicio;
        private DetallePedidoViewModel _vm;

        [TestInitialize]
        public void Inicializar()
        {
            _configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => _configuracion.leerParametro(A<string>._, A<string>._)).Returns(Task.FromResult(string.Empty));
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns(Task.FromResult("ALG"));
            _servicio = A.Fake<IPedidoVentaService>();
            _vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), _configuracion, _servicio, new WeakReferenceMessenger(),
                A.Fake<IServicioDialogos>(), A.Fake<IUnityContainer>(), A.Fake<IServicioAutenticacion>());
        }

        [TestMethod]
        public void DetallePedido_NoDependeDeLaNavegacionDePrismYAbreVistaNueva()
        {
            Assert.IsFalse(typeof(Prism.Regions.INavigationAware).IsAssignableFrom(typeof(DetallePedidoViewModel)));
            Assert.IsTrue(typeof(IReceptorNavegacionPestanaNueva).IsAssignableFrom(typeof(DetallePedidoViewModel)));
        }

        [TestMethod]
        public void DetallePedido_AlLlegar_LeeLosParametrosDelUsuarioYCargaElPedidoDeLaLista()
        {
            _vm.AlLlegar(new ParametrosNavegacion { { "resumenPedidoParameter", new PedidoVentaModel.ResumenPedido { empresa = "1", numero = 901234 } } });

            Assert.AreEqual("ALG", _vm.AlmacenUsuario);
            A.CallTo(() => _servicio.cargarPedido("1", 901234)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void DetallePedido_AlLlegarSinPedido_NoCargaNada()
        {
            _vm.AlLlegar(new ParametrosNavegacion());

            A.CallTo(() => _servicio.cargarPedido(A<string>._, A<int>._)).MustNotHaveHappened();
        }
    }
}
