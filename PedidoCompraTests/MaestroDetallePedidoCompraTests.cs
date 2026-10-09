using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Models;
using Nesto.Modulos.PedidoCompra.ViewModels;
using Nesto.Modulos.PedidoCompra.Views;
using Prism.Ioc;
using Prism.Regions;
using Prism.Regions.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;

namespace PedidoCompraTests
{
    /// <summary>
    /// Nesto#490 (4C.4, 7.º tramo): el maestro-detalle de PedidoCompra. Cada apertura del módulo es una pestaña nueva
    /// con su propio ámbito de regiones; la lista va en ListaPedidosCompraRegion de ese ámbito y, al elegir un pedido,
    /// se vacía DetallePedidoCompraRegion y se navega a un detalle nuevo con el pedido. Escritas contra el
    /// IRegionManager de Prism ANTES de migrar (78220dc8). Al pasar a IServicioNavegacion e IConAmbitoNavegacion las
    /// comprobaciones son las mismas, sobre los mismos fakes de Prism: solo cambia que a los ViewModels y a la vista se
    /// les da un ServicioNavegacionPrism sobre ellos en lugar del IRegionManager.
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
            vm.NavegacionAmbito = new ServicioNavegacionPrism(ambito);
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
            vm.NavegacionAmbito = new ServicioNavegacionPrism(ambito);
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
                var vm = new PedidoCompraViewModel(new ServicioNavegacionPrism(regionManager), A.Fake<IConfiguracion>(), contenedor);
                var vista = new PedidoCompraView(contenedor, vm);
                A.CallTo(() => contenedor.Resolve(typeof(PedidoCompraView))).Returns(vista);
                A.CallTo(() => principal.Add(vista, null, true)).Returns(ambito);

                vm.AbrirModuloCommand.Execute(null);

                A.CallTo(() => principal.Add(vista, null, true)).MustHaveHappenedOnceExactly()
                    .Then(A.CallTo(() => principal.Activate(vista)).MustHaveHappenedOnceExactly());
                // La vista recibe la navegación de SU ámbito: lo que navega va al IRegionManager con ámbito
                Assert.IsNotNull(vista.NavegacionAmbito);
                vista.NavegacionAmbito.RequestNavigate("DetallePedidoCompraRegion", "DetallePedidoCompraView");
                A.CallTo(() => ambito.RequestNavigate("DetallePedidoCompraRegion", "DetallePedidoCompraView")).MustHaveHappenedOnceExactly();
            });
        }

        [TestMethod]
        public void VistaPedidoCompra_AlCargar_PoneLaListaEnSuRegionDelAmbitoUnaSolaVez()
        {
            EnSta(() =>
            {
                var ambito = A.Fake<IRegionManager>();
                IRegion regionLista = ConfigurarRegion(ambito, "ListaPedidosCompraRegion");
                A.CallTo(() => regionLista.GetView(A<string>._)).Returns(null);
                var contenedor = A.Fake<IContainerProvider>();
                var listaVm = CrearLista();
                var lista = new ListaPedidosCompraView(listaVm);
                A.CallTo(() => contenedor.Resolve(typeof(ListaPedidosCompraView))).Returns(lista);
                var vista = new PedidoCompraView(contenedor, new PedidoCompraViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), contenedor));
                vista.NavegacionAmbito = new ServicioNavegacionPrism(ambito);

                vista.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, vista));
                vista.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, vista));

                A.CallTo(() => regionLista.Add(lista, "ListaPedidosCompraRegion")).MustHaveHappenedOnceExactly()
                    .Then(A.CallTo(() => regionLista.Activate(lista)).MustHaveHappenedOnceExactly());
                Assert.IsNotNull(listaVm.NavegacionAmbito);
                listaVm.NavegacionAmbito.RequestNavigate("DetallePedidoCompraRegion", "DetallePedidoCompraView");
                A.CallTo(() => ambito.RequestNavigate("DetallePedidoCompraRegion", "DetallePedidoCompraView")).MustHaveHappenedOnceExactly();
            });
        }

        [TestMethod]
        public void MaestroDetalle_NoUsaElRegionManagerDePrism()
        {
            // Nesto#490 (4C.4, 7.º tramo): ni el ViewModel del módulo, ni la lista, ni las dos vistas tienen ya nada de Prism.Regions
            foreach (var tipo in new[] { typeof(PedidoCompraViewModel), typeof(ListaPedidosCompraViewModel), typeof(PedidoCompraView), typeof(ListaPedidosCompraView) })
            {
                var conPrism = tipo.GetProperties().Select(p => p.PropertyType)
                    .Concat(tipo.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType))
                    .Concat(tipo.GetMethods().Where(m => m.DeclaringType == tipo).SelectMany(m => m.GetParameters()).Select(p => p.ParameterType))
                    .Where(t => t.Namespace == "Prism.Regions");
                Assert.IsFalse(conPrism.Any(), $"{tipo.Name} usa {string.Join(", ", conPrism.Select(t => t.Name))}");
            }
            Assert.IsTrue(typeof(IConAmbitoNavegacion).IsAssignableFrom(typeof(PedidoCompraView)));
            Assert.IsTrue(typeof(IConAmbitoNavegacion).IsAssignableFrom(typeof(ListaPedidosCompraViewModel)));
        }
    }
}
