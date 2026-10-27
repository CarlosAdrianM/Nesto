using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Cinta;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Menu;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (panel lateral, tramo 1): el <see cref="LectorCinta"/> saca el menú lateral de la cinta ya fusionada. Cada
    /// elemento tiene el MISMO ICommand y la misma Fuente (DataContext) que el botón de la cinta, sus propiedades siguen
    /// enlazadas (si el ViewModel cambia, el elemento también) y sale en el orden de la cinta.
    /// </summary>
    [TestClass]
    public class LectorCintaTests
    {
        private sealed class MenuViewModel : ObservableObject
        {
            private string _texto = "Diferencias";
            private Visibility _visibilidad = Visibility.Visible;
            private Visibility _visibilidadGrupo = Visibility.Visible;
            private bool _puede = true;

            public MenuViewModel()
            {
                AbrirCommand = new RelayCommand(() => Ejecuciones++, () => _puede);
                OtroCommand = new RelayCommand(() => { });
            }

            public RelayCommand AbrirCommand { get; }
            public RelayCommand OtroCommand { get; }
            public int Ejecuciones { get; private set; }

            public string Texto { get => _texto; set => SetProperty(ref _texto, value); }
            public Visibility Visibilidad { get => _visibilidad; set => SetProperty(ref _visibilidad, value); }
            public Visibility VisibilidadGrupo { get => _visibilidadGrupo; set => SetProperty(ref _visibilidadGrupo, value); }
            public bool IncluirGlobal { get; set; }

            public void PermitirAbrir(bool puede)
            {
                _puede = puede;
                AbrirCommand.NotifyCanExecuteChanged();
            }
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

        private static RibbonButton Boton(string etiqueta, string comando)
        {
            var boton = new RibbonButton { Label = etiqueta };
            boton.SetBinding(System.Windows.Controls.Primitives.ButtonBase.CommandProperty, new Binding(comando));
            return boton;
        }

        // Como en Nesto: la cinta principal (con su menú de aplicación) y dos módulos que ponen su menú con ServicioCinta
        private static (Ribbon cinta, MenuViewModel vmA, MenuViewModel vmB, RibbonButton conTextoEnlazado, RibbonGroup grupoB) Montar()
        {
            var principal = new Ribbon();
            var vmA = new MenuViewModel();
            var vmB = new MenuViewModel();

            var cintaA = new Ribbon { ApplicationMenu = new RibbonApplicationMenu() };
            var parametros = new RibbonApplicationMenuItem { Header = "Parámetros" };
            parametros.SetBinding(MenuItem.CommandProperty, new Binding(nameof(MenuViewModel.OtroCommand)));
            cintaA.ApplicationMenu.Items.Add(parametros);
            var grupoA = new RibbonGroup { Header = "Ventas" };
            RibbonButton pedidos = Boton("Pedidos", nameof(MenuViewModel.AbrirCommand));
            pedidos.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(MenuViewModel.Visibilidad)));
            pedidos.ToolTip = "Abre los pedidos";
            grupoA.Items.Add(pedidos);
            grupoA.Items.Add(Boton("Plantilla", nameof(MenuViewModel.OtroCommand)));
            var vacio = new RibbonGroup { Header = "Ratios" };
            var pestanaA = new RibbonTab { Header = "Clientes" };
            pestanaA.Items.Add(grupoA);
            pestanaA.Items.Add(vacio);
            cintaA.Items.Add(pestanaA);

            var cintaB = new Ribbon();
            var grupoB = new RibbonGroup { Header = "Reposición" };
            grupoB.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(MenuViewModel.VisibilidadGrupo)));
            var conTextoEnlazado = new RibbonButton();
            conTextoEnlazado.SetBinding(RibbonButton.LabelProperty, new Binding(nameof(MenuViewModel.Texto)));
            conTextoEnlazado.SetBinding(System.Windows.Controls.Primitives.ButtonBase.CommandProperty, new Binding(nameof(MenuViewModel.AbrirCommand)));
            grupoB.Items.Add(conTextoEnlazado);
            var casilla = new RibbonCheckBox { Label = "Incluir Global" };
            casilla.SetBinding(RibbonCheckBox.IsCheckedProperty, new Binding(nameof(MenuViewModel.IncluirGlobal)));
            grupoB.Items.Add(casilla);
            var pestanaB = new RibbonTab { Header = "Productos" };
            pestanaB.Items.Add(grupoB);
            cintaB.Items.Add(pestanaB);
            // El mismo título que una pestaña del módulo A: se fusiona con ella
            var cintaB2 = new RibbonTab { Header = "Clientes" };
            var grupoB2 = new RibbonGroup { Header = "Cartera" };
            grupoB2.Items.Add(Boton("Extracto", nameof(MenuViewModel.OtroCommand)));
            cintaB2.Items.Add(grupoB2);
            cintaB.Items.Add(cintaB2);

            var servicio = new ServicioCinta(principal);
            servicio.AnadirMenu(new UserControl { DataContext = vmA, Content = cintaA });
            servicio.AnadirMenu(new UserControl { DataContext = vmB, Content = cintaB });
            // Sin bombear el dispatcher, como en Nesto al completar el catálogo (antes de Show): los Binding de la cinta aún
            // no están activos, y aun así el catálogo tiene que salir con sus comandos y textos
            return (principal, vmA, vmB, conTextoEnlazado, grupoB);
        }

        private static void Bombear()
        {
            var marco = new System.Windows.Threading.DispatcherFrame();
            _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => marco.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(marco);
        }

        [TestMethod]
        public void CadaElemento_TieneElMismoICommandYLaMismaFuenteQueLaCinta()
        {
            EnSta(() =>
            {
                var (cinta, vmA, vmB, _, _) = Montar();

                var elementos = LectorCinta.Leer(cinta);

                foreach (ElementoMenuBase elemento in elementos)
                {
                    var origen = (FrameworkElement)elemento.Origen;
                    Assert.AreSame(origen.DataContext, elemento.Fuente, elemento.Clave);
                    if (elemento is ElementoMenu boton)
                    {
                        Assert.IsNotNull(boton.Comando, elemento.Clave + ": sin comando sin bombear el dispatcher");
                    }
                }
                ElementoMenu pedidos = elementos.OfType<ElementoMenu>().First();
                Assert.AreSame(vmA, pedidos.Fuente);
                Assert.AreSame(vmA.AbrirCommand, pedidos.Comando, "La misma instancia: no se resuelve nada del contenedor");
                // Y cuando la cinta activa sus Binding, el botón tiene ese mismo ICommand
                Bombear();
                Assert.AreSame(((RibbonButton)pedidos.Origen).Command, pedidos.Comando);
                Assert.AreSame(vmB, elementos.Single(e => e.Clave.StartsWith("Productos")
                    && e is ElementoMenu).Fuente);
            });
        }

        [TestMethod]
        public void LosElementos_SalenEnElOrdenDeLaCinta_ConSuClave_YSinLosGruposVacios()
        {
            EnSta(() =>
            {
                var (cinta, _, _, _, _) = Montar();

                var claves = LectorCinta.Leer(cinta).Select(e => e.Clave).ToArray();

                CollectionAssert.AreEqual(new[]
                {
                    "Clientes › Ventas › Pedidos",
                    "Clientes › Ventas › Plantilla",
                    "Clientes › Cartera › Extracto",           // fusionada en la pestaña Clientes, detrás de lo del módulo A
                    "Productos › Reposición › [AbrirCommand]", // texto enlazado: por el comando
                    "Productos › Reposición › Incluir Global",
                }, claves);
            });
        }

        [TestMethod]
        public void LoQueNoEsUnBoton_EsUnContenido()
        {
            EnSta(() =>
            {
                var (cinta, _, _, _, _) = Montar();

                ElementoMenuBase casilla = LectorCinta.Leer(cinta).Single(e => e.Clave.EndsWith("Incluir Global"));

                Assert.IsInstanceOfType(casilla, typeof(ContenidoMenu));
                Assert.AreEqual("Incluir Global", casilla.Titulo);
            });
        }

        [TestMethod]
        public void LosEnlaces_SiguenVivos_TextoVisibilidadYPermiso()
        {
            EnSta(() =>
            {
                var (cinta, vmA, vmB, _, _) = Montar();
                var elementos = LectorCinta.Leer(cinta);
                ElementoMenu pedidos = elementos.OfType<ElementoMenu>().First();
                ElementoMenu diferencias = elementos.OfType<ElementoMenu>().Single(e => e.Clave.EndsWith("[AbrirCommand]"));
                Assert.AreEqual("Abre los pedidos", pedidos.Ayuda);
                Assert.AreEqual("Diferencias", diferencias.Titulo);
                Assert.IsTrue(pedidos.Mostrar);

                vmB.Texto = "Diferencias (3)";
                Assert.AreEqual("Diferencias (3)", diferencias.Titulo, "El texto del ViewModel llega al elemento");

                vmA.Visibilidad = Visibility.Collapsed;
                Assert.IsFalse(pedidos.Mostrar, "Oculto en la cinta, oculto en el panel");
                vmA.Visibilidad = Visibility.Visible;
                Assert.IsTrue(pedidos.Mostrar);

                vmB.VisibilidadGrupo = Visibility.Hidden;
                Assert.IsFalse(diferencias.Mostrar, "El grupo oculto oculta sus elementos");
                vmB.VisibilidadGrupo = Visibility.Visible;

                vmA.PermitirAbrir(false);
                Assert.IsFalse(pedidos.Mostrar, "Decisión 3: sin permiso (CanExecute) no se ve");
                vmA.PermitirAbrir(true);
                Assert.IsTrue(pedidos.Mostrar);
            });
        }

        [TestMethod]
        public void ElMenuDeAplicacion_EsElPieDelPanel()
        {
            EnSta(() =>
            {
                var (cinta, vmA, _, _, _) = Montar();

                CatalogoMenu catalogo = LectorCinta.LeerCatalogo(cinta);

                ElementoMenu parametros = catalogo.MenuAplicacion.Single();
                Assert.AreEqual("Parámetros", parametros.Titulo);
                Assert.AreSame(vmA.OtroCommand, parametros.Comando);
                Assert.AreSame(vmA, parametros.Fuente);
            });
        }

        [TestMethod]
        public void ElCatalogo_LlevaLosTitulosDelMapa_YLoQueNoEstaEnElMapaVaAOtros()
        {
            EnSta(() =>
            {
                var (cinta, _, _, _, _) = Montar();

                CatalogoMenu catalogo = LectorCinta.LeerCatalogo(cinta);

                ElementoMenuBase pedidos = catalogo.Elementos.First();
                Assert.AreEqual(MapaMenuPorRol.VENTAS, pedidos.Seccion);
                Assert.AreEqual("Pedidos de venta", pedidos.Titulo, "El título del mapa manda");
                Assert.IsFalse(BindingOperations.IsDataBound(pedidos, ElementoMenuBase.TituloProperty));
                // «Clientes › Cartera › Extracto» y lo de «Productos › Reposición» de esta cinta de prueba no están en el mapa
                Assert.IsTrue(catalogo.SinMapear.Contains("Clientes › Cartera › Extracto"));
                Assert.AreEqual(MapaMenuPorRol.OTROS, catalogo.Secciones.Last().Titulo);
            });
        }
    }
}
