using Microsoft.VisualStudio.TestTools.UnitTesting;
using FakeItEasy;
using Nesto.Contratos;
using System.Collections.ObjectModel;
using Prism.Events;
using Nesto.Modulos.PedidoVenta;
using static Nesto.Modulos.PedidoVenta.PedidoVentaModel;
using Prism.Ioc;

namespace PedidoVentaTests
{
    [TestClass]
    public class ListaPedidosVentaViewModelTests
    {
        [TestMethod]
        public void ListaPedidosVentaViewModel_OnCargarListaPedidos_listaPedidosTieneLosPedidosDelServicio()
        {
            var configuracion = A.Fake<IConfiguracion>();
            var servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            var pedido = A.Fake<ResumenPedido>();
            A.CallTo(() => servicio.cargarListaPedidos("", false, false)).Returns(new ObservableCollection<ResumenPedido> { pedido });
            var vm = new ListaPedidosVentaViewModel(configuracion, servicio, eventAggregator);

            vm.cmdCargarListaPedidos.Execute(null);


            A.CallTo(() => configuracion.leerParametro("1", "Vendedor")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, vm.listaPedidosOriginal.Count);
        }

        [TestMethod]
        public void ListaPedidoVentaViewModel_cargarPedidoPorDefecto_devuelveUnResumenPedidoConLosDatos()
        {
            var configuracion = A.Fake<IConfiguracion>();
            var servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            A.CallTo(() => configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1");
            A.CallTo(() => configuracion.leerParametro("1", "UltNumPedidoVta")).Returns("123456");
            var vm = new ListaPedidosVentaViewModel(configuracion, servicio, eventAggregator);

            ResumenPedido resumen = vm.cargarPedidoPorDefecto().Result;
            ResumenPedido esperado = new ResumenPedido { empresa = "1", numero = 123456 };
            Assert.AreEqual(esperado.empresa, resumen.empresa);
            Assert.AreEqual(esperado.numero, resumen.numero);
        }
    }
}
