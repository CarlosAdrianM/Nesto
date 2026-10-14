using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prism.Ioc;
using Prism.Regions;
using Prism.RibbonRegionAdapter;
using System;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4, shell): fijan cómo se monta HOY la cinta de Nesto antes de quitarle Prism. Cada módulo
    /// (<c>IModule.OnInitialized</c>) hace lo mismo: resuelve su menú (un UserControl cuyo contenido es una
    /// <see cref="Ribbon"/>), y con el <see cref="RibbonRegionAdapter"/> (único, registrado como singleton) hace
    /// <c>Initialize(mainWindow.mainRibbon, "NombreDelModulo")</c> y <c>region.Add(menu, "MenuBar")</c>. El adaptador
    /// FUSIONA las pestañas: las del mismo título (Clientes, Proveedores, Contabilidad…) se juntan en una sola y cada
    /// grupo conserva el DataContext de su módulo, que es el que da los comandos de los botones.
    /// </summary>
    [TestClass]
    public class ShellCintaPrincipalTests
    {
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

        // Lo que hace cada IModule en OnInitialized
        private static void AnadirMenuDeModulo(RibbonRegionAdapter adaptador, Ribbon cintaPrincipal, string modulo, object menu)
        {
            IRegion region = adaptador.Initialize(cintaPrincipal, modulo);
            region.Add(menu, "MenuBar");
        }

        private static RibbonRegionAdapter CrearAdaptador()
            => new RibbonRegionAdapter(new RegionBehaviorFactory(A.Fake<IContainerExtension>()));

        private static RibbonTab Pestana(Ribbon cinta, string titulo)
            => cinta.Items.OfType<RibbonTab>().Single(t => (string)t.Header == titulo);

        private static string[] Grupos(RibbonTab pestana)
            => pestana.Items.OfType<RibbonGroup>().Select(g => (string)g.Header).ToArray();

        [TestMethod]
        public void UnModulo_SusPestanasPasanALaCintaPrincipalConElDataContextDelModulo()
        {
            EnSta(() =>
            {
                var cintaPrincipal = new Ribbon();
                var viewModelCajas = new object();
                var menu = MenuDeModulo(viewModelCajas, ("Contabilidad", "Cajas"), ("Contabilidad", "Verifactu"));

                AnadirMenuDeModulo(CrearAdaptador(), cintaPrincipal, "Cajas", menu);

                RibbonTab contabilidad = Pestana(cintaPrincipal, "Contabilidad");
                CollectionAssert.AreEqual(new[] { "Cajas", "Verifactu" }, Grupos(contabilidad));
                Assert.AreSame(viewModelCajas, contabilidad.DataContext);
                // Ya no está en la cinta del módulo: se ha movido a la principal
                Assert.AreEqual(0, ((Ribbon)menu.Content).Items.Count);
            });
        }

        [TestMethod]
        public void DosModulosConUnaPestanaDelMismoTitulo_SeFusionanEnUnaSolaYCadaGrupoConservaSuDataContext()
        {
            EnSta(() =>
            {
                var cintaPrincipal = new Ribbon();
                var adaptador = CrearAdaptador();
                var viewModelCliente = new object();
                var viewModelRapports = new object();

                AnadirMenuDeModulo(adaptador, cintaPrincipal, "Cliente", MenuDeModulo(viewModelCliente, ("Clientes", "Fichas")));
                AnadirMenuDeModulo(adaptador, cintaPrincipal, "Rapports", MenuDeModulo(viewModelRapports, ("Clientes", "Rapports")));

                Assert.AreEqual(1, cintaPrincipal.Items.OfType<RibbonTab>().Count(t => (string)t.Header == "Clientes"));
                RibbonTab clientes = Pestana(cintaPrincipal, "Clientes");
                CollectionAssert.AreEqual(new[] { "Fichas", "Rapports" }, Grupos(clientes));
                RibbonGroup fichas = clientes.Items.OfType<RibbonGroup>().First();
                RibbonGroup rapports = clientes.Items.OfType<RibbonGroup>().Last();
                Assert.AreSame(viewModelCliente, fichas.DataContext);
                Assert.AreSame(viewModelRapports, rapports.DataContext);
            });
        }

        [TestMethod]
        public void VariosModulos_LasPestanasNuevasQuedanEnElOrdenEnQueSeCarganLosModulos()
        {
            EnSta(() =>
            {
                var cintaPrincipal = new Ribbon();
                var adaptador = CrearAdaptador();

                AnadirMenuDeModulo(adaptador, cintaPrincipal, "NewMainMenu", MenuDeModulo(new object(), ("Clientes", "Fichas"), ("Herramientas", "Parámetros")));
                AnadirMenuDeModulo(adaptador, cintaPrincipal, "PedidoCompra", MenuDeModulo(new object(), ("Proveedores", "Pedidos")));
                AnadirMenuDeModulo(adaptador, cintaPrincipal, "Producto", MenuDeModulo(new object(), ("Productos", "Ficha"), ("Proveedores", "Reposición")));

                CollectionAssert.AreEqual(new[] { "Clientes", "Herramientas", "Proveedores", "Productos" },
                    cintaPrincipal.Items.OfType<RibbonTab>().Select(t => (string)t.Header).ToArray());
                CollectionAssert.AreEqual(new[] { "Pedidos", "Reposición" }, Grupos(Pestana(cintaPrincipal, "Proveedores")));
            });
        }

        [TestMethod]
        public void ElMenuDeAplicacionDelModuloPasaAlDeLaCintaPrincipal()
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

                AnadirMenuDeModulo(CrearAdaptador(), cintaPrincipal, "NewMainMenu", menu);

                Assert.IsNotNull(cintaPrincipal.ApplicationMenu);
                CollectionAssert.AreEqual(new[] { "Parámetros", "Novedades" },
                    cintaPrincipal.ApplicationMenu.Items.OfType<RibbonApplicationMenuItem>().Select(i => (string)i.Header).ToArray());
                Assert.AreSame(viewModel, cintaPrincipal.ApplicationMenu.Items.OfType<RibbonApplicationMenuItem>().First().DataContext);
            });
        }
    }
}
