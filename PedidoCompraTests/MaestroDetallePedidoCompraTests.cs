using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Models;
using Nesto.Modulos.PedidoCompra.ViewModels;
using Nesto.Modulos.PedidoCompra.Views;
using Prism.Ioc;
using Prism.Regions;
using Prism.Regions.Behaviors;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;

namespace PedidoCompraTests
{
    /// <summary>
    /// Nesto#490 (4C.4, 7.º tramo): el maestro-detalle de PedidoCompra. Cada apertura del módulo es una pestaña nueva
    /// con su propio ámbito de regiones; la lista va en ListaPedidosCompraRegion de ese ámbito y, al elegir un pedido,
    /// se vacía DetallePedidoCompraRegion y se navega a un detalle nuevo con el pedido. Escritas contra el
    /// IRegionManager de Prism ANTES de migrar.
    /// </summary>
    [TestClass]
    public class MaestroDetallePedidoCompraTests
    {
        // Las vistas reales declaran sus regiones en XAML (prism:RegionManager.RegionName) y Prism, al leerlo, pide al
        // ContainerLocator el comportamiento que las crea; sin contenedor revienta. Se pone uno de mentira solo durante
        // la prueba: la región no llega a crearse (no hay Loaded de los ContentControl) y las pruebas usan las de los fakes.
        private static void EnSta(Action accion)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                var contenedor = A.Fake<IContainerExtension>();
                A.CallTo(() => contenedor.Resolve(typeof(DelayedRegionCreationBehavior)))
                    .ReturnsLazily(() => new DelayedRegionCreationBehavior(new RegionAdapterMappings()));
                ContainerLocator.SetContainerExtension(() => contenedor);
                try { accion(); } catch (Exception ex) { error = ex; }
                finally { ContainerLocator.ResetContainer(); }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null)
            {
                throw new AssertFailedException(error.ToString(), error);
            }
        }

        private static IRegion ConfigurarRegion(IRegionManager regionManager, string nombre, params object[] vistas)
        {
            var region = A.Fake<IRegion>();
            var coleccion = A.Fake<IViewsCollection>();
            A.CallTo(() => coleccion.GetEnumerator()).ReturnsLazily(() => new List<object>(vistas).GetEnumerator());
            A.CallTo(() => region.Views).Returns(coleccion);
            A.CallTo(() => regionManager.Regions.ContainsRegionWithName(nombre)).Returns(true);
            A.CallTo(() => regionManager.Regions[nombre]).Returns(region);
            return region;
        }

        private static ListaPedidosCompraViewModel CrearLista()
        {
            return new ListaPedidosCompraViewModel(A.Fake<IPedidoCompraService>(), A.Fake<IServicioDialogos>(), new WeakReferenceMessenger());
        }

        [TestMethod]
        public void Lista_AlElegirUnPedidoCreado_QuitaLosDetallesAnterioresYNavegaAUnoNuevoConElPedido()
        {
            var ambito = A.Fake<IRegionManager>();
            var anterior = new object();
            IRegion detalle = ConfigurarRegion(ambito, "DetallePedidoCompraRegion", anterior);
            NavigationParameters recibidos = null;
            A.CallTo(() => ambito.RequestNavigate("DetallePedidoCompraRegion", "DetallePedidoCompraView", A<NavigationParameters>._))
                .Invokes((string _, string _, NavigationParameters p) => recibidos = p);
            var vm = CrearLista();
            vm.ScopedRegionManager = ambito;
            var lookup = new PedidoCompraLookup { Empresa = "1", Pedido = 4321, Proveedor = "123" };

            vm.ListaPedidos.ElementoSeleccionado = lookup;

            A.CallTo(() => detalle.Remove(anterior)).MustHaveHappenedOnceExactly()
                .Then(A.CallTo(() => ambito.RequestNavigate("DetallePedidoCompraRegion", "DetallePedidoCompraView", A<NavigationParameters>._)).MustHaveHappenedOnceExactly());
            Assert.IsNotNull(recibidos);
            Assert.AreEqual(1, recibidos.Count);
            Assert.AreSame(lookup, recibidos["PedidoLookupParameter"]);
        }

        [TestMethod]
        public void Lista_AlElegirUnPedidoSinCrear_NavegaConElPedidoSinCrearDeEseProveedor()
        {
            var ambito = A.Fake<IRegionManager>();
            ConfigurarRegion(ambito, "DetallePedidoCompraRegion");
            NavigationParameters recibidos = null;
            A.CallTo(() => ambito.RequestNavigate("DetallePedidoCompraRegion", "DetallePedidoCompraView", A<NavigationParameters>._))
                .Invokes((string _, string _, NavigationParameters p) => recibidos = p);
            var vm = CrearLista();
            vm.ScopedRegionManager = ambito;
            var otro = new PedidoCompraDTO { Empresa = "1", Proveedor = "999" };
            var sinCrear = new PedidoCompraDTO { Empresa = "1", Proveedor = "123" };
            vm.ListaPedidosSinCrear = new List<PedidoCompraDTO> { otro, sinCrear };

            vm.ListaPedidos.ElementoSeleccionado = new PedidoCompraLookup { Empresa = "1", Pedido = 0, Proveedor = "123" };

            Assert.IsNotNull(recibidos);
            Assert.AreEqual(1, recibidos.Count);
            Assert.AreSame(sinCrear, recibidos["PedidoParameter"]);
        }

        [TestMethod]
        public void AbrirModulo_AnadeUnaVistaNuevaConSuAmbitoAMainRegionYLaActiva()
        {
            EnSta(() =>
            {
                var regionManager = A.Fake<IRegionManager>();
                IRegion principal = ConfigurarRegion(regionManager, "MainRegion");
                var ambito = A.Fake<IRegionManager>();
                var contenedor = A.Fake<IContainerProvider>();
                var vm = new PedidoCompraViewModel(regionManager, A.Fake<IConfiguracion>(), contenedor);
                var vista = new PedidoCompraView(contenedor, vm);
                A.CallTo(() => contenedor.Resolve(typeof(PedidoCompraView))).Returns(vista);
                A.CallTo(() => principal.Add(vista, null, true)).Returns(ambito);

                vm.AbrirModuloCommand.Execute(null);

                A.CallTo(() => principal.Add(vista, null, true)).MustHaveHappenedOnceExactly()
                    .Then(A.CallTo(() => principal.Activate(vista)).MustHaveHappenedOnceExactly());
                Assert.AreSame(ambito, vista.ScopedRegionManager);
            });
        }

        [TestMethod]
        public void VistaPedidoCompra_AlCargar_PoneLaListaEnSuRegionDelAmbitoUnaSolaVez()
        {
            EnSta(() =>
            {
                var ambito = A.Fake<IRegionManager>();
                IRegion regionLista = ConfigurarRegion(ambito, "ListaPedidosCompraRegion");
                var contenedor = A.Fake<IContainerProvider>();
                var listaVm = CrearLista();
                var lista = new ListaPedidosCompraView(listaVm);
                A.CallTo(() => contenedor.Resolve(typeof(ListaPedidosCompraView))).Returns(lista);
                var vista = new PedidoCompraView(contenedor, new PedidoCompraViewModel(A.Fake<IRegionManager>(), A.Fake<IConfiguracion>(), contenedor));
                vista.ScopedRegionManager = ambito;

                vista.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, vista));
                vista.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, vista));

                A.CallTo(() => regionLista.Add(lista, "ListaPedidosCompraRegion")).MustHaveHappenedOnceExactly()
                    .Then(A.CallTo(() => regionLista.Activate(lista)).MustHaveHappenedOnceExactly());
                Assert.AreSame(ambito, listaVm.ScopedRegionManager);
            });
        }
    }
}
