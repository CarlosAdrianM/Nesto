using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using static Nesto.Modulos.PedidoVenta.PedidoVentaModel;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#490 (4C.4, 7.º tramo): el maestro-detalle de PedidoVenta. Cada apertura del módulo (o CargarPedido, desde
    /// cualquier pantalla) es una pestaña nueva con su propio ámbito de regiones; la lista va en ListaPedidosRegion de ese
    /// ámbito y, al elegir un pedido, se vacía DetallePedidoRegion y se navega a un detalle nuevo. «Modificar con
    /// plantilla» abre la plantilla en MainRegion. Escritas contra el IRegionManager de Prism ANTES de migrar (06a5ea04).
    /// Desde el tramo 4 de 4C.4 las comprobaciones son las mismas, pero con la región propia de verdad (un
    /// <see cref="GestorRegiones"/> con MainRegion y <see cref="ServicioNavegacionNesto"/>, como en Nesto) en lugar de fakes
    /// de Prism, y la vista real declara sus regiones en XAML con <c>nav:Regiones.Nombre</c>.
    /// </summary>
    [TestClass]
    public class MaestroDetallePedidoVentaTests
    {
        // Las vistas de destino de la navegación, con el nombre con que se registran (se buscan por el nombre del tipo)
        private sealed class DetallePedidoView : Border, IReceptorNavegacion
        {
            public ParametrosNavegacion Recibidos { get; private set; }
            public void AlLlegar(ParametrosNavegacion parametros) => Recibidos = parametros;
        }

        private sealed class PlantillaVentaView : Border, IReceptorNavegacion
        {
            public ParametrosNavegacion Recibidos { get; private set; }
            public void AlLlegar(ParametrosNavegacion parametros) => Recibidos = parametros;
        }

        private sealed class Shell
        {
            public TabControl Pestanas { get; } = new TabControl();
            public GestorRegiones Gestor { get; } = new GestorRegiones();
            public ServicioNavegacionNesto Navegacion { get; set; }
        }

        private static object CrearVista(string nombre) => nombre switch
        {
            nameof(DetallePedidoView) => new DetallePedidoView(),
            nameof(PlantillaVentaView) => new PlantillaVentaView(),
            _ => throw new InvalidOperationException($"{nombre} no está registrada")
        };

        // Como RegionesDelShell en Nesto: MainRegion (el TabControl) en el gestor del shell y la navegación sobre él
        private static Shell CrearShell()
        {
            var shell = new Shell();
            _ = shell.Gestor.Registrar("MainRegion", shell.Pestanas);
            shell.Navegacion = new ServicioNavegacionNesto(shell.Gestor, CrearVista,
                (region, destino, error) => throw new AssertFailedException($"Falló la navegación a {destino} en {region}", error));
            return shell;
        }

        private static void EnSta(Action accion)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try { accion(); } catch (Exception ex) { error = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null)
            {
                throw new AssertFailedException(error.ToString(), error);
            }
        }

        private static ListaPedidosVentaViewModel CrearLista(IServicioNavegacion navegacion)
        {
            return new ListaPedidosVentaViewModel(A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(), new WeakReferenceMessenger(),
                A.Fake<IServicioDialogos>(), navegacion);
        }

        [TestMethod]
        public void Lista_AlElegirUnPedido_QuitaLosDetallesAnterioresYNavegaAUnoNuevoConElResumen()
        {
            EnSta(() =>
            {
                var ambito = new GestorRegiones();
                RegionNesto detalle = ambito.Registrar("DetallePedidoRegion", new ContentControl());
                var anterior = new DetallePedidoView { DataContext = new object() };
                detalle.Anadir(anterior);
                var vm = CrearLista(A.Fake<IServicioNavegacion>());
                vm.NavegacionAmbito = new ServicioNavegacionNesto(ambito, CrearVista);
                var resumen = new ResumenPedido { empresa = "1", numero = 12345, cliente = "15191" };

                vm.ListaPedidos.ElementoSeleccionado = resumen;

                var nuevo = (DetallePedidoView)detalle.Vistas.Single();
                Assert.AreNotSame(anterior, nuevo, "El detalle anterior sale de la región (no se acumula)");
                Assert.IsNull(anterior.DataContext, "Y se limpia su DataContext (fugas de memoria)");
                Assert.IsTrue(detalle.EsActiva(nuevo));
                Assert.IsNotNull(nuevo.Recibidos);
                Assert.AreEqual(1, nuevo.Recibidos.Count);
                Assert.AreSame(resumen, nuevo.Recibidos.GetValue<object>("resumenPedidoParameter"));
            });
        }

        [TestMethod]
        public void Lista_ModificarConPlantilla_AbreLaPlantillaEnMainRegionConElPedido()
        {
            EnSta(() =>
            {
                Shell shell = CrearShell();
                var vm = CrearLista(shell.Navegacion);

                vm.ModificarConPlantillaCommand.Execute(new ResumenPedido { empresa = "3", numero = 98765 });

                var plantilla = (PlantillaVentaView)shell.Pestanas.SelectedItem;
                Assert.IsNotNull(plantilla.Recibidos);
                Assert.AreEqual(2, plantilla.Recibidos.Count);
                Assert.AreEqual(98765, plantilla.Recibidos.GetValue<object>("pedidoAModificar"));
                Assert.AreEqual("3", plantilla.Recibidos.GetValue<object>("empresaPedido"));
            });
        }

        [TestMethod]
        public void Lista_ModificarConPlantilla_SinPedidoONuevo_NoNavega()
        {
            EnSta(() =>
            {
                Shell shell = CrearShell();
                var vm = CrearLista(shell.Navegacion);

                vm.ModificarConPlantillaCommand.Execute(null);
                vm.ModificarConPlantillaCommand.Execute(new ResumenPedido { empresa = "1", numero = 0 });
                vm.ModificarConPlantillaCommand.Execute(new ResumenPedido { empresa = "1", numero = 5, esNuevo = true });

                Assert.AreEqual(0, shell.Pestanas.Items.Count);
            });
        }

        [TestMethod]
        public void AbrirModulo_AnadeUnaVistaNuevaConSuAmbitoAMainRegionYLaActiva()
        {
            EnSta(() =>
            {
                Shell shell = CrearShell();
                var contenedor = A.Fake<IResolutorNesto>();
                var vm = new PedidoVentaViewModel(shell.Navegacion, A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(), contenedor);
                var vistas = new List<PedidoVentaView>();
                A.CallTo(() => contenedor.Resolver<PedidoVentaView>()).ReturnsLazily(() =>
                {
                    var nueva = new PedidoVentaView(contenedor, vm);
                    vistas.Add(nueva);
                    return nueva;
                });

                vm.cmdAbrirModulo.Execute(null);
                vm.cmdAbrirModulo.Execute(null);

                CollectionAssert.AreEqual(vistas, shell.Pestanas.Items.Cast<object>().ToList(), "Cada apertura es una pestaña nueva");
                Assert.AreSame(vistas[1], shell.Pestanas.SelectedItem);
                ComprobarQueNavegaEnSuAmbito(shell, vistas[0]);
                ComprobarQueNavegaEnSuAmbito(shell, vistas[1]);
                Assert.AreNotSame(((ServicioNavegacionNesto)vistas[0].NavegacionAmbito).Gestor,
                    ((ServicioNavegacionNesto)vistas[1].NavegacionAmbito).Gestor, "Cada pestaña con sus regiones");
            });
        }

        [TestMethod]
        public void CargarPedido_AbreUnaVistaNuevaConSuAmbitoYElPedidoInicial()
        {
            EnSta(() =>
            {
                Shell shell = CrearShell();
                var contenedor = A.Fake<IResolutorNesto>();
                var vm = new PedidoVentaViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(), contenedor);
                var vista = new PedidoVentaView(contenedor, vm);
                // Nesto#490 (4C.5, tramo 3a): CargarPedido saca la vista y la navegación del resolutor propio, no del IUnityContainer
                var resolutor = A.Fake<IResolutorNesto>();
                A.CallTo(() => resolutor.Resolver<PedidoVentaView>()).Returns(vista);
                A.CallTo(() => resolutor.Resolver<IServicioNavegacion>()).Returns(shell.Navegacion);

                PedidoVentaViewModel.CargarPedido("3", 98765, resolutor);

                Assert.AreSame(vista, shell.Pestanas.SelectedItem);
                ComprobarQueNavegaEnSuAmbito(shell, vista);
                Assert.AreEqual("3", vm.empresaInicial);
                Assert.AreEqual(98765, vm.pedidoInicial);
            });
        }

        [TestMethod]
        public void VistaPedidoVenta_AlCargarConPedidoInicial_PoneLaListaEnSuRegionUnaSolaVezYEligeEsePedido()
        {
            EnSta(() =>
            {
                Shell shell = CrearShell();
                var contenedor = A.Fake<IResolutorNesto>();
                var listaVm = CrearLista(A.Fake<IServicioNavegacion>());
                var lista = new ListaPedidosVenta(listaVm);
                A.CallTo(() => contenedor.Resolver<ListaPedidosVenta>()).Returns(lista);
                var vm = new PedidoVentaViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(), contenedor)
                {
                    empresaInicial = "3",
                    pedidoInicial = 98765
                };
                var vista = new PedidoVentaView(contenedor, vm);
                var ambito = (ServicioNavegacionNesto)shell.Navegacion.AbrirVistaConAmbito("MainRegion", vista);

                // Las regiones del XAML (ContentControl con nav:Regiones.Nombre) todavía no han recibido su Loaded: se
                // registran al buscarlas, como con Prism
                vista.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, vista));
                vista.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, vista));

                RegionNesto regionLista = ambito.Gestor.Region("ListaPedidosRegion");
                Assert.IsNotNull(regionLista, "ListaPedidosRegion es del ámbito de la pestaña");
                CollectionAssert.AreEqual(new object[] { lista }, regionLista.Vistas.ToList(), "La lista entra una sola vez");
                Assert.IsTrue(regionLista.EsActiva(lista));
                Assert.IsFalse(shell.Gestor.Contiene("ListaPedidosRegion"), "Las regiones de la pestaña no se ven desde el shell");
                Assert.AreSame(ambito, listaVm.NavegacionAmbito);
                var elegido = (ResumenPedido)listaVm.ListaPedidos.ElementoSeleccionado;
                Assert.AreEqual("3", elegido.empresa);
                Assert.AreEqual(98765, elegido.numero);
                // Y el detalle de esta pestaña carga ese pedido
                var detalle = (DetallePedidoView)ambito.Gestor.Region("DetallePedidoRegion").Vistas.Single();
                Assert.IsNotNull(detalle.Recibidos);
                Assert.AreSame(elegido, detalle.Recibidos.GetValue<object>("resumenPedidoParameter"));
            });
        }

        // La navegación que recibe la vista es la de SU ámbito: un gestor de regiones propio, colgado de la vista, y distinto
        // del del shell. Lo que navega va a las regiones de esa pestaña.
        private static void ComprobarQueNavegaEnSuAmbito(Shell shell, PedidoVentaView vista)
        {
            var navegacion = vista.NavegacionAmbito as ServicioNavegacionNesto;
            Assert.IsNotNull(navegacion);
            Assert.AreNotSame(shell.Gestor, navegacion.Gestor);
            Assert.AreSame(navegacion.Gestor, GestorRegiones.GetGestor(vista));
            navegacion.RequestNavigate("DetallePedidoRegion", nameof(DetallePedidoView));
            Assert.IsInstanceOfType(navegacion.Gestor.Region("DetallePedidoRegion").Vistas.Single(), typeof(DetallePedidoView));
            Assert.IsNull(shell.Gestor.Region("DetallePedidoRegion"));
        }

        [TestMethod]
        public void MaestroDetalle_NoUsaLasRegionesDePrism()
        {
            // Nesto#490 (4C.4, 7.º tramo): ni el ViewModel del módulo, ni la lista, ni las dos vistas tienen ya nada de Prism.Regions
            foreach (var tipo in new[] { typeof(PedidoVentaViewModel), typeof(ListaPedidosVentaViewModel), typeof(PedidoVentaView), typeof(ListaPedidosVenta) })
            {
                var conPrism = tipo.GetProperties().Select(p => p.PropertyType)
                    .Concat(tipo.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType))
                    .Concat(tipo.GetMethods().Where(m => m.DeclaringType == tipo).SelectMany(m => m.GetParameters()).Select(p => p.ParameterType))
                    .Concat(tipo.GetInterfaces())
                    .Where(t => t.Namespace?.StartsWith("Prism.Regions") ?? false);
                Assert.IsFalse(conPrism.Any(), $"{tipo.Name} usa {string.Join(", ", conPrism.Select(t => t.Name))}");
            }
            Assert.IsTrue(typeof(IConAmbitoNavegacion).IsAssignableFrom(typeof(PedidoVentaView)));
            Assert.IsTrue(typeof(IConAmbitoNavegacion).IsAssignableFrom(typeof(ListaPedidosVentaViewModel)));
        }
    }
}
