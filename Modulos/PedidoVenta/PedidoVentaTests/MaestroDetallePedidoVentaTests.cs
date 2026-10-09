using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoVenta;
using Prism.Ioc;
using Prism.Regions;
using Prism.Regions.Behaviors;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using Unity;
using Unity.Resolution;
using static Nesto.Modulos.PedidoVenta.PedidoVentaModel;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#490 (4C.4, 7.º tramo): el maestro-detalle de PedidoVenta. Cada apertura del módulo (o CargarPedido, desde
    /// cualquier pantalla) es una pestaña nueva con su propio ámbito de regiones; la lista va en ListaPedidosRegion de ese
    /// ámbito y, al elegir un pedido, se vacía DetallePedidoRegion y se navega a un detalle nuevo. «Modificar con
    /// plantilla» abre la plantilla en MainRegion. Escritas contra el IRegionManager de Prism ANTES de migrar.
    /// </summary>
    [TestClass]
    public class MaestroDetallePedidoVentaTests
    {
        // Las vistas reales declaran sus regiones en XAML y Prism, al leerlo, pide al ContainerLocator el comportamiento
        // que las crea. Se pone uno de mentira solo durante la prueba (las regiones no llegan a crearse: se usan las de los fakes).
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
            A.CallTo(() => region.GetView(A<string>._)).Returns(null);
            A.CallTo(() => regionManager.Regions.ContainsRegionWithName(nombre)).Returns(true);
            A.CallTo(() => regionManager.Regions[nombre]).Returns(region);
            return region;
        }

        private static ListaPedidosVentaViewModel CrearLista(IRegionManager regionManager)
        {
            return new ListaPedidosVentaViewModel(A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(), new WeakReferenceMessenger(),
                A.Fake<IServicioDialogos>(), regionManager);
        }

        [TestMethod]
        public void Lista_AlElegirUnPedido_QuitaLosDetallesAnterioresYNavegaAUnoNuevoConElResumen()
        {
            var ambito = A.Fake<IRegionManager>();
            var anterior = new object();
            IRegion detalle = ConfigurarRegion(ambito, "DetallePedidoRegion", anterior);
            NavigationParameters recibidos = null;
            A.CallTo(() => ambito.RequestNavigate("DetallePedidoRegion", "DetallePedidoView", A<NavigationParameters>._))
                .Invokes((string _, string _, NavigationParameters p) => recibidos = p);
            var vm = CrearLista(A.Fake<IRegionManager>());
            vm.scopedRegionManager = ambito;
            var resumen = new ResumenPedido { empresa = "1", numero = 12345, cliente = "15191" };

            vm.ListaPedidos.ElementoSeleccionado = resumen;

            A.CallTo(() => detalle.Remove(anterior)).MustHaveHappenedOnceExactly()
                .Then(A.CallTo(() => ambito.RequestNavigate("DetallePedidoRegion", "DetallePedidoView", A<NavigationParameters>._)).MustHaveHappenedOnceExactly());
            Assert.IsNotNull(recibidos);
            Assert.AreEqual(1, recibidos.Count);
            Assert.AreSame(resumen, recibidos["resumenPedidoParameter"]);
        }

        [TestMethod]
        public void Lista_ModificarConPlantilla_AbreLaPlantillaEnMainRegionConElPedido()
        {
            var regionManager = A.Fake<IRegionManager>();
            NavigationParameters recibidos = null;
            A.CallTo(() => regionManager.RequestNavigate("MainRegion", "PlantillaVentaView", A<NavigationParameters>._))
                .Invokes((string _, string _, NavigationParameters p) => recibidos = p);
            var vm = CrearLista(regionManager);

            vm.ModificarConPlantillaCommand.Execute(new ResumenPedido { empresa = "3", numero = 98765 });

            Assert.IsNotNull(recibidos);
            Assert.AreEqual(2, recibidos.Count);
            Assert.AreEqual(98765, recibidos["pedidoAModificar"]);
            Assert.AreEqual("3", recibidos["empresaPedido"]);
        }

        [TestMethod]
        public void Lista_ModificarConPlantilla_SinPedidoONuevo_NoNavega()
        {
            var regionManager = A.Fake<IRegionManager>();
            var vm = CrearLista(regionManager);

            vm.ModificarConPlantillaCommand.Execute(null);
            vm.ModificarConPlantillaCommand.Execute(new ResumenPedido { empresa = "1", numero = 0 });
            vm.ModificarConPlantillaCommand.Execute(new ResumenPedido { empresa = "1", numero = 5, esNuevo = true });

            A.CallTo(regionManager).Where(c => c.Method.Name == "RequestNavigate").MustNotHaveHappened();
        }

        [TestMethod]
        public void AbrirModulo_AnadeUnaVistaNuevaConSuAmbitoAMainRegionYLaActiva()
        {
            EnSta(() =>
            {
                var regionManager = A.Fake<IRegionManager>();
                IRegion principal = ConfigurarRegion(regionManager, "MainRegion");
                var ambito = A.Fake<IRegionManager>();
                var contenedor = A.Fake<IUnityContainer>();
                var vm = new PedidoVentaViewModel(regionManager, A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(), contenedor);
                var vista = new PedidoVentaView(contenedor, vm);
                A.CallTo(() => contenedor.Resolve(typeof(PedidoVentaView), A<string>._, A<ResolverOverride[]>._)).Returns(vista);
                A.CallTo(() => principal.Add(vista, null, true)).Returns(ambito);

                vm.cmdAbrirModulo.Execute(null);

                A.CallTo(() => principal.Add(vista, null, true)).MustHaveHappenedOnceExactly()
                    .Then(A.CallTo(() => principal.Activate(vista)).MustHaveHappenedOnceExactly());
                Assert.AreSame(ambito, vista.scopedRegionManager);
            });
        }

        [TestMethod]
        public void CargarPedido_AbreUnaVistaNuevaConSuAmbitoYElPedidoInicial()
        {
            EnSta(() =>
            {
                var regionManager = A.Fake<IRegionManager>();
                IRegion principal = ConfigurarRegion(regionManager, "MainRegion");
                var ambito = A.Fake<IRegionManager>();
                var contenedor = A.Fake<IUnityContainer>();
                var vm = new PedidoVentaViewModel(A.Fake<IRegionManager>(), A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(), contenedor);
                var vista = new PedidoVentaView(contenedor, vm);
                A.CallTo(() => contenedor.Resolve(typeof(PedidoVentaView), A<string>._, A<ResolverOverride[]>._)).Returns(vista);
                A.CallTo(() => contenedor.Resolve(typeof(IRegionManager), A<string>._, A<ResolverOverride[]>._)).Returns(regionManager);
                A.CallTo(() => principal.Add(vista, null, true)).Returns(ambito);

                PedidoVentaViewModel.CargarPedido("3", 98765, contenedor);

                A.CallTo(() => principal.Add(vista, null, true)).MustHaveHappenedOnceExactly()
                    .Then(A.CallTo(() => principal.Activate(vista)).MustHaveHappenedOnceExactly());
                Assert.AreSame(ambito, vista.scopedRegionManager);
                Assert.AreEqual("3", vm.empresaInicial);
                Assert.AreEqual(98765, vm.pedidoInicial);
            });
        }

        [TestMethod]
        public void VistaPedidoVenta_AlCargarConPedidoInicial_PoneLaListaEnSuRegionUnaSolaVezYEligeEsePedido()
        {
            EnSta(() =>
            {
                var ambito = A.Fake<IRegionManager>();
                IRegion regionLista = ConfigurarRegion(ambito, "ListaPedidosRegion");
                ConfigurarRegion(ambito, "DetallePedidoRegion");
                NavigationParameters recibidos = null;
                A.CallTo(() => ambito.RequestNavigate("DetallePedidoRegion", "DetallePedidoView", A<NavigationParameters>._))
                    .Invokes((string _, string _, NavigationParameters p) => recibidos = p);
                var contenedor = A.Fake<IUnityContainer>();
                var listaVm = CrearLista(A.Fake<IRegionManager>());
                var lista = new ListaPedidosVenta(listaVm);
                A.CallTo(() => contenedor.Resolve(typeof(ListaPedidosVenta), A<string>._, A<ResolverOverride[]>._)).Returns(lista);
                var vm = new PedidoVentaViewModel(A.Fake<IRegionManager>(), A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(), contenedor)
                {
                    empresaInicial = "3",
                    pedidoInicial = 98765
                };
                var vista = new PedidoVentaView(contenedor, vm);
                vista.scopedRegionManager = ambito;

                vista.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, vista));
                vista.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, vista));

                A.CallTo(() => regionLista.Add(lista, "ListaPedidosVenta")).MustHaveHappenedOnceExactly();
                A.CallTo(() => regionLista.Activate(lista)).MustHaveHappenedOnceExactly();
                Assert.AreSame(ambito, listaVm.scopedRegionManager);
                var elegido = (ResumenPedido)listaVm.ListaPedidos.ElementoSeleccionado;
                Assert.AreEqual("3", elegido.empresa);
                Assert.AreEqual(98765, elegido.numero);
                // Y el detalle de esta pestaña carga ese pedido
                Assert.IsNotNull(recibidos);
                Assert.AreSame(elegido, recibidos["resumenPedidoParameter"]);
            });
        }
    }
}
