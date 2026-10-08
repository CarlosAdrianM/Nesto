using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Models;
using Nesto.Modulos.PedidoCompra.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PedidoCompraTests
{
    /// <summary>
    /// Nesto#490 (4C.4): el detalle del pedido de compra recibe la navegación por <see cref="IReceptorNavegacionPestanaNueva"/>
    /// en vez de INavigationAware: cada pedido elegido abre un detalle nuevo en DetallePedidoCompraRegion (antes
    /// IsNavigationTarget = false) y <c>AlLlegar</c> hace lo de <c>OnNavigatedTo</c>, con las mismas claves.
    /// </summary>
    [TestClass]
    public class ReceptoresNavegacionPedidoCompraTests
    {
        private IPedidoCompraService _servicio;
        private DetallePedidoCompraViewModel _vm;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IPedidoCompraService>();
            _vm = new DetallePedidoCompraViewModel(_servicio, A.Fake<IServicioDialogos>(), A.Fake<IServicioNavegacion>(), null,
                A.Fake<IConfiguracion>(), new WeakReferenceMessenger(), A.Fake<IServicioAutenticacion>());
        }

        [TestMethod]
        public void DetallePedidoCompra_NoDependeDeLaNavegacionDePrismYAbreVistaNueva()
        {
            Assert.IsFalse(typeof(Prism.Regions.INavigationAware).IsAssignableFrom(typeof(DetallePedidoCompraViewModel)));
            Assert.IsTrue(typeof(IReceptorNavegacionPestanaNueva).IsAssignableFrom(typeof(DetallePedidoCompraViewModel)));
        }

        [TestMethod]
        public void PedidoCompra_NoDependeDeLaNavegacionDePrism()
        {
            // Nesto#490 (4C.4, 6.º tramo): implementaba INavigationAware vacío y nadie navega a PedidoCompraView
            Assert.IsFalse(typeof(Prism.Regions.INavigationAware).IsAssignableFrom(typeof(PedidoCompraViewModel)));
            Assert.IsFalse(typeof(IReceptorNavegacion).IsAssignableFrom(typeof(PedidoCompraViewModel)));
        }

        [TestMethod]
        public void DetallePedidoCompra_AlLlegarConElPedidoDeLaLista_LoCargaDeLaApi()
        {
            var cargado = new PedidoCompraDTO { Empresa = "1", Id = 4321, Lineas = new List<LineaPedidoCompraDTO>() };
            A.CallTo(() => _servicio.CargarPedido("1", 4321)).Returns(Task.FromResult(cargado));

            _vm.AlLlegar(new ParametrosNavegacion { { "PedidoLookupParameter", new PedidoCompraLookup { Empresa = "1", Pedido = 4321 } } });

            A.CallTo(() => _servicio.CargarPedido("1", 4321)).MustHaveHappenedOnceExactly();
            Assert.AreSame(cargado, _vm.Pedido.Model);
        }

        [TestMethod]
        public void DetallePedidoCompra_AlLlegarConUnPedidoSinCrear_LoEnseñaSinIrALaApi()
        {
            var nuevo = new PedidoCompraDTO { Empresa = "1", Lineas = new List<LineaPedidoCompraDTO>() };

            _vm.AlLlegar(new ParametrosNavegacion { { "PedidoParameter", nuevo } });

            A.CallTo(() => _servicio.CargarPedido(A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.AreSame(nuevo, _vm.Pedido.Model);
        }
    }
}
