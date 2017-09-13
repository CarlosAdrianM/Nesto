using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FakeItEasy;
using Nesto.Contratos;
using Nesto.Modulos.PedidoVenta;
using Microsoft.Practices.Unity;
using static Nesto.Modulos.PedidoVenta.PedidoVentaModel;
using System.Collections.ObjectModel;

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
            var container = A.Fake<IUnityContainer>();
            var pedido = A.Fake<ResumenPedido>();
            A.CallTo(() => servicio.cargarListaPedidos("", false)).Returns(new ObservableCollection<ResumenPedido> { pedido });
            var vm = new ListaPedidosVentaViewModel(configuracion, servicio, container);

            vm.cmdCargarListaPedidos.Execute(null);


            A.CallTo(() => configuracion.leerParametro("1", "Vendedor")).MustHaveHappened(Repeated.Exactly.Once);
            Assert.AreEqual(1, vm.listaPedidosOriginal.Count);
        }

        [TestMethod]
        public void ListaPedidoVentaViewModel_cargarPedidoPorDefecto_devuelveUnResumenPedidoConLosDatos()
        {
            var configuracion = A.Fake<IConfiguracion>();
            var servicio = A.Fake<IPedidoVentaService>();
            var container = A.Fake<IUnityContainer>();
            A.CallTo(() => configuracion.leerParametro("1", "EmpresaPorDefecto")).Returns("1");
            A.CallTo(() => configuracion.leerParametro("1", "UltNumPedidoVta")).Returns("123456");
            var vm = new ListaPedidosVentaViewModel(configuracion, servicio, container);

            ResumenPedido resumen = vm.cargarPedidoPorDefecto().Result;
            ResumenPedido esperado = new ResumenPedido { empresa = "1", numero = 123456 };
            Assert.AreEqual(esperado.empresa, resumen.empresa);
            Assert.AreEqual(esperado.numero, resumen.numero);
        }
    }
}
