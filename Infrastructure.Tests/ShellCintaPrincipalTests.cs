using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Cinta;
using Prism.Ioc;
using Prism.Regions;
using Prism.RibbonRegionAdapter;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4, shell): fijan cómo se monta la cinta de Nesto. Cada módulo (<c>IModule.OnInitialized</c>) hace
    /// lo mismo: resuelve su menú (un UserControl cuyo contenido es una <see cref="Ribbon"/>) y lo añade a la cinta
    /// principal (<c>mainWindow.mainRibbon</c>). Las pestañas se FUSIONAN: las del mismo título (Clientes, Proveedores,
    /// Contabilidad…) se juntan en una sola y cada grupo conserva el DataContext de su módulo, que es el que da los
    /// comandos de los botones.
    /// <para>
    /// Cada prueba se ejecuta con las dos implementaciones: la de antes (<c>RibbonRegionAdapter</c> de Prism, único,
    /// con <c>Initialize(cinta, "NombreDelModulo")</c> + <c>region.Add(menu, "MenuBar")</c>) y la nueva
    /// (<see cref="ServicioCinta"/>). Las comprobaciones son las mismas: así se ve que hacen lo mismo.
    /// </para>
    /// </summary>
    [TestClass]
    public class ShellCintaPrincipalTests
    {
        private const string PRISM = "Prism";
        private const string NESTO = "Nesto";

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

        // Lo que hace cada IModule en OnInitialized, con una u otra implementación. Como en Nesto, una sola instancia
        // (el adaptador o el servicio) para todos los módulos.
        private static Action<string, UIElement> CrearCinta(string implementacion, Ribbon cintaPrincipal)
        {
            if (implementacion == PRISM)
            {
                var adaptador = new RibbonRegionAdapter(new RegionBehaviorFactory(A.Fake<IContainerExtension>()));
                return (modulo, menu) =>
                {
                    IRegion region = adaptador.Initialize(cintaPrincipal, modulo);
                    region.Add(menu, "MenuBar");
                };
            }
            var servicio = new ServicioCinta(cintaPrincipal);
            return (modulo, menu) => servicio.AnadirMenu(menu);
        }

        // El menú de un módulo, como CajasMenuBar.xaml: un UserControl con una Ribbon de pestañas con grupos
        private static UserControl MenuDeModulo(object viewModel, params (string pestana, string grupo)[] grupos)
        {
            var cinta = new Ribbon();
            foreach (var porPestana in grupos.GroupBy(g => g.pestana))
            {
                var pestana = new RibbonTab { Header = porPestana.Key };
                foreach (var (_, grupo) in porPestana)
                {
                    pestana.Items.Add(new RibbonGroup { Header = grupo });
                }
                cinta.Items.Add(pestana);
            }
            return new UserControl { DataContext = viewModel, Content = cinta };
        }

        private static RibbonTab Pestana(Ribbon cinta, string titulo)
            => cinta.Items.OfType<RibbonTab>().Single(t => (string)t.Header == titulo);

        private static string[] Pestanas(Ribbon cinta)
            => cinta.Items.OfType<RibbonTab>().Select(t => (string)t.Header).ToArray();

        private static string[] Grupos(RibbonTab pestana)
            => pestana.Items.OfType<RibbonGroup>().Select(g => (string)g.Header).ToArray();

        [DataTestMethod]
        [DataRow(PRISM)]
        [DataRow(NESTO)]
        public void UnModulo_SusPestanasPasanALaCintaPrincipalConElDataContextDelModulo(string implementacion)
        {
            EnSta(() =>
            {
                var cintaPrincipal = new Ribbon();
                var viewModelCajas = new object();
                var menu = MenuDeModulo(viewModelCajas, ("Contabilidad", "Cajas"), ("Contabilidad", "Verifactu"));

                CrearCinta(implementacion, cintaPrincipal)("Cajas", menu);

                RibbonTab contabilidad = Pestana(cintaPrincipal, "Contabilidad");
                CollectionAssert.AreEqual(new[] { "Cajas", "Verifactu" }, Grupos(contabilidad));
                Assert.AreSame(viewModelCajas, contabilidad.DataContext);
                // Ya no está en la cinta del módulo: se ha movido a la principal
                Assert.AreEqual(0, ((Ribbon)menu.Content).Items.Count);
            });
        }

        [DataTestMethod]
        [DataRow(PRISM)]
        [DataRow(NESTO)]
        public void DosModulosConUnaPestanaDelMismoTitulo_SeFusionanEnUnaSolaYCadaGrupoConservaSuDataContext(string implementacion)
        {
            EnSta(() =>
            {
                var cintaPrincipal = new Ribbon();
                var anadir = CrearCinta(implementacion, cintaPrincipal);
                var viewModelCliente = new object();
                var viewModelRapports = new object();

                anadir("Cliente", MenuDeModulo(viewModelCliente, ("Clientes", "Fichas")));
                anadir("Rapports", MenuDeModulo(viewModelRapports, ("Clientes", "Rapports")));

                Assert.AreEqual(1, cintaPrincipal.Items.OfType<RibbonTab>().Count(t => (string)t.Header == "Clientes"));
                RibbonTab clientes = Pestana(cintaPrincipal, "Clientes");
                CollectionAssert.AreEqual(new[] { "Fichas", "Rapports" }, Grupos(clientes));
                RibbonGroup fichas = clientes.Items.OfType<RibbonGroup>().First();
                RibbonGroup rapports = clientes.Items.OfType<RibbonGroup>().Last();
                Assert.AreSame(viewModelCliente, fichas.DataContext);
                Assert.AreSame(viewModelRapports, rapports.DataContext);
            });
        }

        [DataTestMethod]
        [DataRow(PRISM)]
        [DataRow(NESTO)]
        public void VariosModulos_LasPestanasNuevasQuedanEnElOrdenEnQueSeCarganLosModulos(string implementacion)
        {
            EnSta(() =>
            {
                var cintaPrincipal = new Ribbon();
                var anadir = CrearCinta(implementacion, cintaPrincipal);

                anadir("NewMainMenu", MenuDeModulo(new object(), ("Clientes", "Fichas"), ("Herramientas", "Parámetros")));
                anadir("PedidoCompra", MenuDeModulo(new object(), ("Proveedores", "Pedidos")));
                anadir("Producto", MenuDeModulo(new object(), ("Productos", "Ficha"), ("Proveedores", "Reposición")));

                CollectionAssert.AreEqual(new[] { "Clientes", "Herramientas", "Proveedores", "Productos" }, Pestanas(cintaPrincipal));
                CollectionAssert.AreEqual(new[] { "Pedidos", "Reposición" }, Grupos(Pestana(cintaPrincipal, "Proveedores")));
            });
        }

        [DataTestMethod]
        [DataRow(PRISM)]
        [DataRow(NESTO)]
        public void ElMenuDeAplicacionDelModuloPasaAlDeLaCintaPrincipal(string implementacion)
        {
            // MenuBarView (el de lo viejo) trae el menú de aplicación: Parámetros, Novedades…
            EnSta(() =>
            {
                var cintaPrincipal = new Ribbon();
                var viewModel = new object();
                var cintaModulo = new Ribbon { ApplicationMenu = new RibbonApplicationMenu() };
                cintaModulo.ApplicationMenu.Items.Add(new RibbonApplicationMenuItem { Header = "Parámetros" });
                cintaModulo.ApplicationMenu.Items.Add(new RibbonApplicationMenuItem { Header = "Novedades" });
                var menu = new UserControl { DataContext = viewModel, Content = cintaModulo };

                CrearCinta(implementacion, cintaPrincipal)("NewMainMenu", menu);

                Assert.IsNotNull(cintaPrincipal.ApplicationMenu);
                CollectionAssert.AreEqual(new[] { "Parámetros", "Novedades" },
                    cintaPrincipal.ApplicationMenu.Items.OfType<RibbonApplicationMenuItem>().Select(i => (string)i.Header).ToArray());
                Assert.AreSame(viewModel, cintaPrincipal.ApplicationMenu.Items.OfType<RibbonApplicationMenuItem>().First().DataContext);
            });
        }

        [DataTestMethod]
        [DataRow(PRISM)]
        [DataRow(NESTO)]
        public void DosModulosConUnGrupoDelMismoTituloEnLaMismaPestana_SeJuntanLosBotonesYCadaUnoConservaSuDataContext(string implementacion)
        {
            EnSta(() =>
            {
                var cintaPrincipal = new Ribbon();
                var anadir = CrearCinta(implementacion, cintaPrincipal);
                var viewModelA = new object();
                var viewModelB = new object();
                UserControl MenuConBoton(object viewModel, string boton)
                {
                    var grupo = new RibbonGroup { Header = "Informes" };
                    grupo.Items.Add(new RibbonButton { Label = boton });
                    var pestana = new RibbonTab { Header = "Ventas" };
                    pestana.Items.Add(grupo);
                    var cinta = new Ribbon();
                    cinta.Items.Add(pestana);
                    return new UserControl { DataContext = viewModel, Content = cinta };
                }

                anadir("A", MenuConBoton(viewModelA, "Uno"));
                anadir("B", MenuConBoton(viewModelB, "Dos"));

                RibbonTab ventas = Pestana(cintaPrincipal, "Ventas");
                RibbonGroup informes = ventas.Items.OfType<RibbonGroup>().Single();
                RibbonButton[] botones = informes.Items.OfType<RibbonButton>().ToArray();
                CollectionAssert.AreEqual(new[] { "Uno", "Dos" }, botones.Select(b => b.Label).ToArray());
                Assert.AreSame(viewModelA, informes.DataContext);
                Assert.AreSame(viewModelA, botones[0].DataContext);
                Assert.AreSame(viewModelB, botones[1].DataContext);
            });
        }

        [DataTestMethod]
        [DataRow(PRISM)]
        [DataRow(NESTO)]
        public void UnaPestanaSinTitulo_SeFusionaPorSuNombre(string implementacion)
        {
            EnSta(() =>
            {
                var cintaPrincipal = new Ribbon();
                var anadir = CrearCinta(implementacion, cintaPrincipal);
                UserControl MenuConPestanaConNombre(string grupo)
                {
                    var pestana = new RibbonTab { Name = "PestanaSinTitulo" };
                    pestana.Items.Add(new RibbonGroup { Header = grupo });
                    var cinta = new Ribbon();
                    cinta.Items.Add(pestana);
                    return new UserControl { DataContext = new object(), Content = cinta };
                }

                anadir("A", MenuConPestanaConNombre("Uno"));
                anadir("B", MenuConPestanaConNombre("Dos"));

                RibbonTab pestana = cintaPrincipal.Items.OfType<RibbonTab>().Single();
                CollectionAssert.AreEqual(new[] { "Uno", "Dos" }, Grupos(pestana));
            });
        }

        [DataTestMethod]
        [DataRow(PRISM)]
        [DataRow(NESTO)]
        public void LaBarraDeAccesoRapidoDelModuloPasaALaDeLaCintaPrincipal(string implementacion)
        {
            EnSta(() =>
            {
                var cintaPrincipal = new Ribbon();
                var viewModel = new object();
                var cintaModulo = new Ribbon { QuickAccessToolBar = new RibbonQuickAccessToolBar() };
                cintaModulo.QuickAccessToolBar.Items.Add(new RibbonButton { Label = "Guardar" });
                var menu = new UserControl { DataContext = viewModel, Content = cintaModulo };

                CrearCinta(implementacion, cintaPrincipal)("A", menu);

                Assert.IsNotNull(cintaPrincipal.QuickAccessToolBar);
                RibbonButton guardar = cintaPrincipal.QuickAccessToolBar.Items.OfType<RibbonButton>().Single();
                Assert.AreEqual("Guardar", guardar.Label);
                Assert.AreSame(viewModel, guardar.DataContext);
            });
        }

        [DataTestMethod]
        [DataRow(PRISM)]
        [DataRow(NESTO)]
        public void UnMenuQueNoEsUnaCinta_NoSePuedeAnadir(string implementacion)
        {
            EnSta(() =>
            {
                var cintaPrincipal = new Ribbon();
                var anadir = CrearCinta(implementacion, cintaPrincipal);

                Assert.ThrowsException<NotSupportedException>(() => anadir("A", new UserControl { Content = new Grid() }));
                Assert.ThrowsException<NotSupportedException>(() => anadir("B", new Border()));
            });
        }

        [TestMethod]
        public void ServicioCinta_LaClaveDeFusionMandaSobreElTitulo()
        {
            EnSta(() =>
            {
                var cintaPrincipal = new Ribbon();
                var servicio = new ServicioCinta(cintaPrincipal);
                var menuA = MenuDeModulo(new object(), ("Ventas", "Uno"));
                var menuB = MenuDeModulo(new object(), ("Ventas", "Dos"));
                FusionCinta.SetClaveFusion(((Ribbon)menuB.Content).Items.OfType<RibbonTab>().Single(), "OtraClave");

                servicio.AnadirMenu(menuA);
                servicio.AnadirMenu(menuB);

                Assert.AreEqual(2, cintaPrincipal.Items.OfType<RibbonTab>().Count(t => (string)t.Header == "Ventas"));
            });
        }

        [TestMethod]
        public void ServicioCinta_ElOrdenDeFusionColocaLaPestanaDelanteDeLasQueTienenMas()
        {
            EnSta(() =>
            {
                var cintaPrincipal = new Ribbon();
                var servicio = new ServicioCinta(cintaPrincipal);
                var menuA = MenuDeModulo(new object(), ("Clientes", "Uno"), ("Productos", "Dos"));
                var menuB = MenuDeModulo(new object(), ("Inicio", "Tres"));
                FusionCinta.SetOrdenFusion(((Ribbon)menuB.Content).Items.OfType<RibbonTab>().Single(), 1);

                servicio.AnadirMenu(menuA);
                servicio.AnadirMenu(menuB);

                CollectionAssert.AreEqual(new[] { "Inicio", "Clientes", "Productos" }, Pestanas(cintaPrincipal));
            });
        }

        [TestMethod]
        public void ServicioCinta_SinCintaPrincipalOSinMenu_FallaConArgumentNull()
        {
            EnSta(() =>
            {
                Assert.ThrowsException<ArgumentNullException>(() => new ServicioCinta(null));
                Assert.ThrowsException<ArgumentNullException>(() => new ServicioCinta(new Ribbon()).AnadirMenu(null));
            });
        }
    }
}
