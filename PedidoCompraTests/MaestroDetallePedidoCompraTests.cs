using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Models;
using Nesto.Modulos.PedidoCompra.ViewModels;
using Nesto.Modulos.PedidoCompra.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace PedidoCompraTests
{
    /// <summary>
    /// Nesto#490 (4C.4, 7.º tramo): el maestro-detalle de PedidoCompra. Cada apertura del módulo es una pestaña nueva
    /// con su propio ámbito de regiones; la lista va en ListaPedidosCompraRegion de ese ámbito y, al elegir un pedido,
    /// se vacía DetallePedidoCompraRegion y se navega a un detalle nuevo con el pedido. Escritas contra el
    /// IRegionManager de Prism ANTES de migrar (78220dc8). Desde el tramo 4 de 4C.4 las comprobaciones son las mismas,
    /// pero con la región propia de verdad (un <see cref="GestorRegiones"/> con MainRegion y
    /// <see cref="ServicioNavegacionNesto"/>, como en Nesto) en lugar de fakes de Prism, y la vista real declara sus
    /// regiones en XAML con <c>nav:Regiones.Nombre</c>.
    /// </summary>
    [TestClass]
    public class MaestroDetallePedidoCompraTests
    {
        // La vista de destino del detalle, con el nombre con que se registra (se busca por el nombre del tipo)
        private sealed class DetallePedidoCompraView : Border, IReceptorNavegacion
        {
            public ParametrosNavegacion Recibidos { get; private set; }
            public void AlLlegar(ParametrosNavegacion parametros) => Recibidos = parametros;
        }

        private static object CrearVista(string nombre) => nombre == nameof(DetallePedidoCompraView)
            ? new DetallePedidoCompraView()
            : throw new InvalidOperationException($"{nombre} no está registrada");

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

        // Como RegionesDelShell en Nesto: MainRegion (el TabControl) en el gestor del shell y la navegación sobre él
        private static (TabControl pestanas, GestorRegiones gestor, ServicioNavegacionNesto navegacion) CrearShell()
        {
            var pestanas = new TabControl();
            var gestor = new GestorRegiones();
            _ = gestor.Registrar("MainRegion", pestanas);
            return (pestanas, gestor, new ServicioNavegacionNesto(gestor, CrearVista,
                (region, destino, error) => throw new AssertFailedException($"Falló la navegación a {destino} en {region}", error)));
        }

        private static ListaPedidosCompraViewModel CrearLista()
        {
            return new ListaPedidosCompraViewModel(A.Fake<IPedidoCompraService>(), A.Fake<IServicioDialogos>(), new WeakReferenceMessenger());
        }

        // El ámbito de una pestaña con su región de detalle (un ContentControl, como en PedidoCompraView)
        private static (RegionNesto detalle, ServicioNavegacionNesto navegacion) CrearAmbito()
        {
            var ambito = new GestorRegiones();
            RegionNesto detalle = ambito.Registrar("DetallePedidoCompraRegion", new ContentControl());
            return (detalle, new ServicioNavegacionNesto(ambito, CrearVista));
        }

        [TestMethod]
        public void Lista_AlElegirUnPedidoCreado_QuitaLosDetallesAnterioresYNavegaAUnoNuevoConElPedido()
        {
            EnSta(() =>
            {
                var (detalle, navegacion) = CrearAmbito();
                var anterior = new DetallePedidoCompraView { DataContext = new object() };
                detalle.Anadir(anterior);
                var vm = CrearLista();
                vm.NavegacionAmbito = navegacion;
                var lookup = new PedidoCompraLookup { Empresa = "1", Pedido = 4321, Proveedor = "123" };

                vm.ListaPedidos.ElementoSeleccionado = lookup;

                var nuevo = (DetallePedidoCompraView)detalle.Vistas.Single();
                Assert.AreNotSame(anterior, nuevo, "El detalle anterior sale de la región (no se acumula)");
                Assert.IsNull(anterior.DataContext, "Y se limpia su DataContext (fugas de memoria)");
                Assert.IsTrue(detalle.EsActiva(nuevo));
                Assert.IsNotNull(nuevo.Recibidos);
                Assert.AreEqual(1, nuevo.Recibidos.Count);
                Assert.AreSame(lookup, nuevo.Recibidos.GetValue<object>("PedidoLookupParameter"));
            });
        }

        [TestMethod]
        public void Lista_AlElegirUnPedidoSinCrear_NavegaConElPedidoSinCrearDeEseProveedor()
        {
            EnSta(() =>
            {
                var (detalle, navegacion) = CrearAmbito();
                var vm = CrearLista();
                vm.NavegacionAmbito = navegacion;
                var otro = new PedidoCompraDTO { Empresa = "1", Proveedor = "999" };
                var sinCrear = new PedidoCompraDTO { Empresa = "1", Proveedor = "123" };
                vm.ListaPedidosSinCrear = new List<PedidoCompraDTO> { otro, sinCrear };

                vm.ListaPedidos.ElementoSeleccionado = new PedidoCompraLookup { Empresa = "1", Pedido = 0, Proveedor = "123" };

                var nuevo = (DetallePedidoCompraView)detalle.Vistas.Single();
                Assert.IsNotNull(nuevo.Recibidos);
                Assert.AreEqual(1, nuevo.Recibidos.Count);
                Assert.AreSame(sinCrear, nuevo.Recibidos.GetValue<object>("PedidoParameter"));
            });
        }

        [TestMethod]
        public void AbrirModulo_AnadeUnaVistaNuevaConSuAmbitoAMainRegionYLaActiva()
        {
            EnSta(() =>
            {
                var (pestanas, gestorShell, navegacionShell) = CrearShell();
                var contenedor = A.Fake<IResolutorNesto>();
                var vm = new PedidoCompraViewModel(navegacionShell, A.Fake<IConfiguracion>(), contenedor);
                var vistas = new List<PedidoCompraView>();
                A.CallTo(() => contenedor.Resolver<PedidoCompraView>()).ReturnsLazily(() =>
                {
                    var nueva = new PedidoCompraView(contenedor, vm);
                    vistas.Add(nueva);
                    return nueva;
                });

                vm.AbrirModuloCommand.Execute(null);
                vm.AbrirModuloCommand.Execute(null);

                CollectionAssert.AreEqual(vistas, pestanas.Items.Cast<object>().ToList(), "Cada apertura es una pestaña nueva");
                Assert.AreSame(vistas[1], pestanas.SelectedItem);
                foreach (PedidoCompraView vista in vistas)
                {
                    // La vista recibe la navegación de SU ámbito: lo que navega va a las regiones de su pestaña
                    var navegacion = vista.NavegacionAmbito as ServicioNavegacionNesto;
                    Assert.IsNotNull(navegacion);
                    Assert.AreNotSame(gestorShell, navegacion.Gestor);
                    Assert.AreSame(navegacion.Gestor, GestorRegiones.GetGestor(vista));
                    navegacion.RequestNavigate("DetallePedidoCompraRegion", nameof(DetallePedidoCompraView));
                    Assert.IsInstanceOfType(navegacion.Gestor.Region("DetallePedidoCompraRegion").Vistas.Single(), typeof(DetallePedidoCompraView));
                }
                Assert.IsNull(gestorShell.Region("DetallePedidoCompraRegion"));
            });
        }

        [TestMethod]
        public void VistaPedidoCompra_AlCargar_PoneLaListaEnSuRegionDelAmbitoUnaSolaVez()
        {
            EnSta(() =>
            {
                var (_, gestorShell, navegacionShell) = CrearShell();
                var contenedor = A.Fake<IResolutorNesto>();
                var listaVm = CrearLista();
                var lista = new ListaPedidosCompraView(listaVm);
                A.CallTo(() => contenedor.Resolver<ListaPedidosCompraView>()).Returns(lista);
                var vista = new PedidoCompraView(contenedor, new PedidoCompraViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), contenedor));
                var ambito = (ServicioNavegacionNesto)navegacionShell.AbrirVistaConAmbito("MainRegion", vista);

                // Las regiones del XAML todavía no han recibido su Loaded: se registran al buscarlas, como con Prism
                vista.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, vista));
                vista.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, vista));

                RegionNesto regionLista = ambito.Gestor.Region("ListaPedidosCompraRegion");
                Assert.IsNotNull(regionLista, "ListaPedidosCompraRegion es del ámbito de la pestaña");
                CollectionAssert.AreEqual(new object[] { lista }, regionLista.Vistas.ToList(), "La lista entra una sola vez");
                Assert.IsTrue(regionLista.EsActiva(lista));
                Assert.IsFalse(gestorShell.Contiene("ListaPedidosCompraRegion"), "Las regiones de la pestaña no se ven desde el shell");
                Assert.AreSame(ambito, listaVm.NavegacionAmbito);
                listaVm.NavegacionAmbito.RequestNavigate("DetallePedidoCompraRegion", nameof(DetallePedidoCompraView));
                Assert.IsInstanceOfType(ambito.Gestor.Region("DetallePedidoCompraRegion").Vistas.Single(), typeof(DetallePedidoCompraView));
            });
        }

        [TestMethod]
        public void MaestroDetalle_NoUsaLasRegionesDePrism()
        {
            // Nesto#490 (4C.4, 7.º tramo): ni el ViewModel del módulo, ni la lista, ni las dos vistas tienen ya nada de Prism.Regions
            foreach (var tipo in new[] { typeof(PedidoCompraViewModel), typeof(ListaPedidosCompraViewModel), typeof(PedidoCompraView), typeof(ListaPedidosCompraView) })
            {
                var conPrism = tipo.GetProperties().Select(p => p.PropertyType)
                    .Concat(tipo.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType))
                    .Concat(tipo.GetMethods().Where(m => m.DeclaringType == tipo).SelectMany(m => m.GetParameters()).Select(p => p.ParameterType))
                    .Concat(tipo.GetInterfaces())
                    .Where(t => t.Namespace?.StartsWith("Prism.Regions") ?? false);
                Assert.IsFalse(conPrism.Any(), $"{tipo.Name} usa {string.Join(", ", conPrism.Select(t => t.Name))}");
            }
            Assert.IsTrue(typeof(IConAmbitoNavegacion).IsAssignableFrom(typeof(PedidoCompraView)));
            Assert.IsTrue(typeof(IConAmbitoNavegacion).IsAssignableFrom(typeof(ListaPedidosCompraViewModel)));
        }
    }
}
