using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Cinta;
using Nesto.Infrastructure.Composicion;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using Nesto.Infrastructure.Shared;
using Nesto.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Threading;

namespace Nesto.Tests
{
    /// <summary>
    /// Nesto#490 (4C.5, tramo 4): la prueba de arranque sombra. Hace el mismo arranque que la aplicación salvo la red y el
    /// <c>Show()</c> (contenedor, shell, <c>AlCrearShell</c>, los 13 módulos) y compara lo que sale con lo que salía con
    /// Prism: la cinta (pestañas, grupos, botones y el DataContext de cada uno), el orden en que los módulos ponen su
    /// menú, <c>MainRegion</c>, el DataContext de cada menú de la cinta y el ViewModel de cada vista y diálogo que se
    /// pide por su nombre.
    /// <para>
    /// Las listas de referencia se sacaron ejecutando esta misma prueba con el arranque de Prism (PrismApplication:
    /// Unity, el catálogo de módulos y el ModuleManager), antes de quitarlo. Si MS.DI elige otro constructor sin avisar
    /// (p. ej. <c>PedidoVentaMenuBar</c> con <c>New()</c> y <c>New(viewModel)</c>), el DataContext no coincide y falla.
    /// Comprobado a mano: con el menú de pedidos de venta creado por <c>New()</c> (ValidateOnBuild no protesta) fallan la
    /// de los menús (DataContext null) y la de la cinta (el botón «Pedidos» se queda con el ViewModel de la plantilla).
    /// </para>
    /// <para>
    /// Desde el tramo 4, el arranque es el de la aplicación (<see cref="ArranqueNesto"/>) sobre MS.DI con ValidateOnBuild.
    /// </para>
    /// <para>
    /// Se arranca una sola vez para toda la clase, en un hilo STA con una <see cref="System.Windows.Application"/> (la de
    /// Nesto, con sus recursos: sin ellos las vistas no encuentran sus estilos), y cada prueba mira una parte de lo que
    /// salió. Al terminar se deja el proceso sin Application, como lo esperan las demás pruebas.
    /// </para>
    /// </summary>
    [TestClass]
    public class ArranqueSombraTests
    {
        private static Resultado _resultado;
        private static Exception _errorAlArrancar;

        private sealed class Resultado
        {
            public List<string> Menus { get; } = new List<string>();
            public List<string> Cinta { get; } = new List<string>();
            public List<string> Vistas { get; } = new List<string>();
            public List<string> Dialogos { get; } = new List<string>();
            public bool MainRegionRegistrada { get; set; }
            public bool MainRegionEsLaDelShell { get; set; }
        }

        private sealed class Arranque
        {
            public IResolutorNesto Resolutor { get; set; }
            public Window Shell { get; set; }
            public RegionesDelShell Regiones { get; set; }
            public List<UIElement> MenusEnLaCinta { get; } = new List<UIElement>();
            public List<string> Vistas { get; } = new List<string>();
            public List<string> Dialogos { get; } = new List<string>();
        }

        /// <summary>La cinta de verdad (la de MainWindow), apuntando además qué menú llega y en qué orden.</summary>
        private sealed class CintaQueApunta : IServicioCinta
        {
            private readonly IServicioCinta _real;
            private readonly List<UIElement> _menus;

            public CintaQueApunta(IServicioCinta real, List<UIElement> menus)
            {
                _real = real;
                _menus = menus;
            }

            public void AnadirMenu(UIElement menu)
            {
                _menus.Add(menu);
                _real.AnadirMenu(menu);
            }
        }

        private sealed class ApiQueNoContesta : System.Net.Http.HttpMessageHandler
        {
            protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(
                System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
                => new System.Threading.Tasks.TaskCompletionSource<System.Net.Http.HttpResponseMessage>().Task;
        }

        /// <summary>Solo apunta los nombres de las vistas y de los diálogos (para saber qué hay que resolver).</summary>
        private sealed class RegistroQueApunta : RegistroNestoBase
        {
            private readonly Arranque _arranque;

            public RegistroQueApunta(Arranque arranque)
            {
                _arranque = arranque;
            }

            public override void Transitorio(Type servicio, Type implementacion) { }
            public override void Singleton(Type servicio, Type implementacion) { }
            public override void Singleton(Type servicio, Func<IResolutorNesto, object> fabrica) { }
            public override void Instancia(Type servicio, object instancia) { }
            public override void Vista(string nombre, Type tipoVista) => _arranque.Vistas.Add(nombre);
            public override void Dialogo(string nombre, DialogoRegistrado dialogo) => _arranque.Dialogos.Add(nombre);
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            var hilo = new Thread(() =>
            {
                try
                {
                    // Como en la aplicación, lo que sigue a un await vuelve al hilo de la interfaz. Aquí ese dispatcher no
                    // se llega a ejecutar: las cargas que las vistas lanzan al crearse (contra la API falsa) se quedan a
                    // medias en lugar de tirar el proceso desde un async void.
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                    _resultado = ArrancarYMirar();
                }
                catch (Exception ex)
                {
                    _errorAlArrancar = ex;
                }
                finally
                {
                    QuitarApplication();
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            if (!hilo.Join(TimeSpan.FromMinutes(3)))
            {
                _errorAlArrancar = new TimeoutException("El arranque sombra no terminó en 3 minutos (¿una ventana modal o una espera síncrona?)");
            }
        }

        private static Resultado ArrancarYMirar()
        {
            // Sin esto, las imágenes de los XAML del exe con rutas relativas (images/cliente.png) se buscarían en el
            // ensamblado de vstest (la otra clase de pruebas lo fija igual; aquí se cambia por debajo si ya estaba fijado)
            typeof(System.Windows.Application)
                .GetField("_resourceAssembly", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, typeof(MenuBarView).Assembly);

            // La Application de Nesto, sin Run (no hay OnStartup): solo para tener sus recursos (Dictionary.xaml)
            var aplicacion = new Nesto.Views.Application();
            aplicacion.InitializeComponent();

            Arranque arranque = Arrancar();
            var resultado = new Resultado();
            foreach (FrameworkElement menu in arranque.MenusEnLaCinta.Cast<FrameworkElement>())
            {
                resultado.Menus.Add($"{menu.GetType().Name} | {NombreDelTipo(menu.DataContext)}");
            }
            resultado.Cinta.AddRange(VolcarCinta(((IMainWindow)arranque.Shell).mainRibbon));
            resultado.MainRegionRegistrada = arranque.Regiones.GestorShell.Contiene("MainRegion");
            resultado.MainRegionEsLaDelShell = ReferenceEquals(GestorRegiones.GetGestor(arranque.Shell), arranque.Regiones.GestorShell);
            using (Contenedor.Sustituir(arranque.Resolutor))
            {
                foreach (string nombre in arranque.Vistas)
                {
                    try
                    {
                        object vista = arranque.Resolutor.ResolverVista(nombre);
                        resultado.Vistas.Add($"{nombre} | {vista.GetType().Name} | {NombreDelTipo((vista as FrameworkElement)?.DataContext)}");
                    }
                    catch (Exception ex)
                    {
                        resultado.Vistas.Add($"{nombre} | ERROR {ex.GetType().Name}: {ex.Message}");
                    }
                }
                foreach (string nombre in arranque.Dialogos)
                {
                    try
                    {
                        FrameworkElement dialogo = ControlesUsuario.Dialogs.RegistroDialogos.CrearDialogo(arranque.Resolutor, nombre);
                        resultado.Dialogos.Add($"{nombre} | {dialogo.GetType().Name} | {NombreDelTipo(dialogo.DataContext)}");
                    }
                    catch (Exception ex)
                    {
                        resultado.Dialogos.Add($"{nombre} | ERROR {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            return resultado;
        }

        private static string NombreDelTipo(object objeto) => objeto?.GetType().Name ?? "null";

        // Las demás pruebas del proceso cuentan con que no hay Application (Application.Current es null)
        private static void QuitarApplication()
        {
            const BindingFlags estatico = BindingFlags.NonPublic | BindingFlags.Static;
            typeof(System.Windows.Application).GetField("_appInstance", estatico)?.SetValue(null, null);
            typeof(System.Windows.Application).GetField("_appCreatedInThisAppDomain", estatico)?.SetValue(null, false);
            if (System.Windows.Application.Current != null)
            {
                _errorAlArrancar ??= new InvalidOperationException("No se pudo quitar la Application de la prueba (¿cambió WPF por dentro?)");
            }
        }

        // Sin red: la configuración, el cliente de la API y la autenticación son fakes. La cinta, la de verdad.
        private static void RegistrarFakes(IRegistroNesto registro, Arranque arranque)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            _ = A.CallTo(() => configuracion.servidorAPI).Returns("http://localhost/NestoAPI-pruebas/");
            _ = A.CallTo(() => configuracion.usuario).Returns(@"NUEVAVISION\Pruebas");
            _ = A.CallTo(() => configuracion.UsuarioSinDominio).Returns("Pruebas");
            _ = A.CallTo(() => configuracion.LeerParametroSync(A<string>._, Parametros.Claves.AlmacenRuta)).Returns(Constantes.Sedes.ListaSedes.First().Codigo);
            registro.Instancia(configuracion);
            // La API no contesta nunca: lo que las vistas cargan al crearse se queda esperando, sin pasar por los avisos
            // de error (que abrirían ventanas modales y dejarían la prueba colgada)
            IClienteApiFactory api = A.Fake<IClienteApiFactory>();
            _ = A.CallTo(() => api.Crear()).ReturnsLazily(() => new System.Net.Http.HttpClient(new ApiQueNoContesta())
            {
                BaseAddress = new Uri("http://localhost/NestoAPI-pruebas/")
            });
            registro.Instancia(api);
            registro.Instancia(A.Fake<IServicioDialogos>());
            registro.Instancia(A.Fake<IServicioAutenticacion>());
            registro.Instancia(A.Fake<IServicioRegistroErrores>());
            registro.Singleton<IServicioCinta>(r => new CintaQueApunta(new ServicioCinta(r.Resolver<IMainWindow>().mainRibbon), arranque.MenusEnLaCinta));
        }

        // El arranque de la aplicación (ArranqueNesto, lo mismo que hace Application.OnStartup), con los fakes registrados
        // encima de todo y sin Show(). Hasta el tramo 4 aquí estaba el de PrismApplication.Initialize (Unity, CreateShell con
        // AlCrearShell y el ModuleManager de Prism), con el que se sacaron las listas de referencia.
        private static Arranque Arrancar()
        {
            var arranque = new Arranque();
            // Los nombres de las vistas y de los diálogos, para pedirlos todos después
            var apunta = new RegistroQueApunta(arranque);
            ComposicionNesto.Registrar(apunta);
            foreach (Type tipoModulo in ComposicionNesto.Modulos)
            {
                ((IModuloNesto)Activator.CreateInstance(ImplementacionDe(tipoModulo))).Registrar(apunta);
            }

            ArranqueNesto arranqueNesto = ArranqueNesto.Componer(registro => RegistrarFakes(registro, arranque));
            arranque.Resolutor = arranqueNesto.Resolutor;
            using (Contenedor.Sustituir(arranqueNesto.Resolutor))
            {
                arranque.Shell = arranqueNesto.CrearShell();
                arranque.Regiones = arranqueNesto.Resolutor.Resolver<RegionesDelShell>();
                arranqueNesto.InicializarModulos();
            }
            return arranque;
        }

        // El tipo que se crea para cada módulo del catálogo: el registrado para él en ComposicionNesto
        private static Type ImplementacionDe(Type tipoModulo)
        {
            var contenedor = new ContenedorMicrosoft();
            ComposicionNesto.Registrar(contenedor);
            return contenedor.Servicios.LastOrDefault(r => !r.IsKeyedService && r.ServiceType == tipoModulo)?.ImplementationType ?? tipoModulo;
        }

        private static Resultado ResultadoDelArranque()
        {
            if (_errorAlArrancar != null)
            {
                throw new AssertFailedException("El arranque sombra falló: " + _errorAlArrancar, _errorAlArrancar);
            }
            return _resultado;
        }

        [TestMethod]
        public void LosModulos_PonenSuMenuEnLaCinta_EnElOrdenDePrism_ConSuViewModel()
        {
            // El orden importa: los grupos y botones que se fusionan en la misma pestaña salen en el orden en que llegan
            CompararConPrism(MenusConPrism, ResultadoDelArranque().Menus, "menús de la cinta (menú | DataContext)");
        }

        [TestMethod]
        public void LaCinta_TieneLasMismasPestanasGruposYBotonesQueConPrism()
        {
            CompararConPrism(CintaConPrism, ResultadoDelArranque().Cinta, "cinta (tipo «texto» [DataContext])");
        }

        [TestMethod]
        public void MainRegion_SeRegistraEnElGestorDelShell()
        {
            Resultado r = ResultadoDelArranque();
            Assert.IsTrue(r.MainRegionEsLaDelShell, "AlCrearShell no colgó el gestor de regiones del shell");
            Assert.IsTrue(r.MainRegionRegistrada, "MainRegion no está registrada en el gestor del shell");
        }

        [TestMethod]
        public void CadaVistaPorNombre_SeResuelveConSuViewModel()
        {
            CompararConPrism(VistasConPrism, ResultadoDelArranque().Vistas, "vistas por nombre (nombre | vista | DataContext)");
        }

        [TestMethod]
        public void CadaDialogo_SeCreaConSuViewModel()
        {
            CompararConPrism(DialogosConPrism, ResultadoDelArranque().Dialogos, "diálogos (nombre | vista | DataContext)");
        }

        private static void CompararConPrism(IReadOnlyList<string> conPrism, IReadOnlyList<string> ahora, string que)
        {
            var diferencias = new List<string>();
            int total = Math.Max(conPrism.Count, ahora.Count);
            for (int i = 0; i < total; i++)
            {
                string esperado = i < conPrism.Count ? conPrism[i] : "(nada)";
                string actual = i < ahora.Count ? ahora[i] : "(nada)";
                if (esperado != actual)
                {
                    diferencias.Add($"[{i}] con Prism: {esperado}\n     ahora:     {actual}");
                }
            }
            Assert.AreEqual(0, diferencias.Count,
                $"Las {que} no son las de Prism:\n{string.Join("\n", diferencias.Take(20))}\n\nTodo lo de ahora:\n{string.Join("\n", ahora)}");
        }

        // Las listas de referencia: las que salían con el arranque de Prism (23/10/26, antes de quitarlo)
        private static readonly string[] MenusConPrism =
        {
            "MenuBarView | MenuBarViewModel",
            "PlantillaVentaMenuBar | PlantillaVentaViewModel",
            "InventarioMenuBar | InventarioViewModel",
            "CarteraPagosMenuBar | CarteraPagosViewModel",
            "PedidoVentaMenuBar | PedidoVentaViewModel",
            "RapportsMenuBar | ListaRapportsViewModel",
            "CanalesExternosMenuBar | CanalesExternosMenuBarViewModel",
            "ProductoMenuBar | ProductoMenuBarViewModel",
            "ClienteMenuBar | CrearClienteViewModel",
            "PedidoCompraMenuBar | PedidoCompraViewModel",
            "CajasMenuBar | CajasMenuBarViewModel",
            "GanavisionesMenuBar | GanavisionesMenuBarViewModel",
            "OfertasCombinadasMenuBar | OfertasCombinadasMenuBarViewModel",
        };

        private static readonly string[] CintaConPrism =
        {
            "RibbonApplicationMenu «» [null]",
            "  RibbonApplicationMenuItem «_Parámetros» [MenuBarViewModel]",
            "  RibbonApplicationMenuItem «_Novedades» [MenuBarViewModel]",
            "RibbonTab «Clientes» [MenuBarViewModel]",
            "  RibbonGroup «Mantenimiento» [MenuBarViewModel]",
            "    RibbonButton «Ficha» [MenuBarViewModel]",
            "    RibbonButton «Alquileres» [MenuBarViewModel]",
            "    RibbonButton «Remesas» [MenuBarViewModel]",
            "    RibbonButton «Agencias» [MenuBarViewModel]",
            "    RibbonButton «Mant. agencias» [MenuBarViewModel]",
            "    RibbonButton «Mant. familias» [MenuBarViewModel]",
            "    RibbonButton «Crear Cliente» [CrearClienteViewModel]",
            "    RibbonButton «NIF incorrectos» [CrearClienteViewModel]",
            "    RibbonButton «Códigos Postales» [CrearClienteViewModel]",
            "  RibbonGroup «Ventas» [PlantillaVentaViewModel]",
            "    RibbonButton «Plantilla» [PlantillaVentaViewModel]",
            "    RibbonButton «Pedidos» [PedidoVentaViewModel]",
            "    RibbonButton «Pendientes de entregar» [PedidoVentaViewModel]", // Nesto#552, después de la referencia de Prism
            "  RibbonGroup «Rapports» [ListaRapportsViewModel]",
            "    RibbonButton «Rapports» [ListaRapportsViewModel]",
            "  RibbonGroup «Documentos» [CrearClienteViewModel]",
            "    RibbonButton «Modelo 347» [CrearClienteViewModel]",
            "  RibbonGroup «Cartera» [CrearClienteViewModel]",
            "    RibbonButton «Extracto Cliente» [CrearClienteViewModel]",
            "  RibbonGroup «Eventos» [CrearClienteViewModel]",
            "    RibbonButton «Eventos» [CrearClienteViewModel]",
            "    RibbonButton «Señales de eventos» [CrearClienteViewModel]",
            "RibbonTab «Vendedores» [MenuBarViewModel]",
            "  RibbonGroup «Clientes» [MenuBarViewModel]",
            "    RibbonButton «Ficha» [MenuBarViewModel]",
            "    RibbonButton «Planes de Ventajas» [MenuBarViewModel]",
            "  RibbonGroup «Comisiones» [MenuBarViewModel]",
            "    RibbonButton «Comisiones» [MenuBarViewModel]",
            "RibbonTab «Herramientas» [MenuBarViewModel]",
            "  RibbonGroup «Contenido» [MenuBarViewModel]",
            "    RibbonButton «Videos» [MenuBarViewModel]",
            "  RibbonGroup «Canales Externos» [CanalesExternosMenuBarViewModel]",
            "    RibbonButton «Pedidos» [CanalesExternosMenuBarViewModel]",
            "    RibbonButton «Pagos» [CanalesExternosMenuBarViewModel]",
            "    RibbonButton «Facturas» [CanalesExternosMenuBarViewModel]",
            "    RibbonButton «Cuadre facturas» [CanalesExternosMenuBarViewModel]",
            "    RibbonButton «Productos» [CanalesExternosMenuBarViewModel]",
            "    RibbonButton «Poison Pills» [CanalesExternosMenuBarViewModel]",
            "RibbonTab «Informes» [MenuBarViewModel]",
            "  RibbonGroup «Vendedores Teléfono» [MenuBarViewModel]",
            "    RibbonButton «Rapport» [MenuBarViewModel]",
            "  RibbonGroup «Grupo de Empresas» [MenuBarViewModel]",
            "    RibbonButton «Ventas» [MenuBarViewModel]",
            "  RibbonGroup «Informes de Control» [MenuBarViewModel]",
            "    RibbonButton «Pedidos» [MenuBarViewModel]",
            "  RibbonGroup «Inventario» [MenuBarViewModel]",
            "    RibbonButton «Inventario» [MenuBarViewModel]",
            "  RibbonGroup «Almacén» [MenuBarViewModel]",
            "    RibbonButton «Picking» [MenuBarViewModel]",
            "    RibbonButton «Packing» [MenuBarViewModel]",
            "  RibbonGroup «Ratios» [MenuBarViewModel]",
            "  RibbonGroup «Contabilidad» [MenuBarViewModel]",
            "    RibbonButton «Balance Pymes» [MenuBarViewModel]",
            "    RibbonButton «PyG Pymes» [MenuBarViewModel]",
            "    RibbonCheckBox «Incluir Global» [MenuBarViewModel]",
            "  RibbonGroup «Fechas» [MenuBarViewModel]",
            "    StackPanel «» [MenuBarViewModel]",
            "RibbonTab «Productos» [InventarioViewModel]",
            "  RibbonGroup «Inventarios» [InventarioViewModel]",
            "    RibbonButton «Tienda» [InventarioViewModel]",
            "  RibbonGroup «Mantenimiento» [ProductoMenuBarViewModel]",
            "    RibbonButton «Ficha» [ProductoMenuBarViewModel]",
            "  RibbonGroup «Reposición» [ProductoMenuBarViewModel]",
            "    RibbonButton «Diarios» [ProductoMenuBarViewModel]",
            "    RibbonButton «Recibir» [ProductoMenuBarViewModel]",
            "    RibbonButton «Enviar» [ProductoMenuBarViewModel]",
            "    RibbonButton «Calendario» [ProductoMenuBarViewModel]",
            "    RibbonButton «» [ProductoMenuBarViewModel]",
            "  RibbonGroup «Almacén» [ProductoMenuBarViewModel]",
            "    RibbonButton «Etiquetas de hueco» [ProductoMenuBarViewModel]",
            "  RibbonGroup «Compras» [ProductoMenuBarViewModel]",
            "    RibbonButton «Propuestas de stock» [ProductoMenuBarViewModel]",
            "RibbonTab «Proveedores» [CarteraPagosViewModel]",
            "  RibbonGroup «Cartera Pagos» [CarteraPagosViewModel]",
            "    RibbonButton «Remesa» [CarteraPagosViewModel]",
            "  RibbonGroup «Pedidos» [PedidoCompraViewModel]",
            "    RibbonButton «Pedidos» [PedidoCompraViewModel]",
            "  RibbonGroup «Mantenimientos» [GanavisionesMenuBarViewModel]",
            "    RibbonButton «Ganavisiones» [GanavisionesMenuBarViewModel]",
            "    RibbonButton «Ofertas y descuentos» [OfertasCombinadasMenuBarViewModel]",
            "RibbonTab «Contabilidad» [CajasMenuBarViewModel]",
            "  RibbonGroup «Cajas» [CajasMenuBarViewModel]",
            "    RibbonButton «Cajas» [CajasMenuBarViewModel]",
            "    RibbonButton «Bancos» [CajasMenuBarViewModel]",
            "    RibbonButton «Mayor Cuenta» [CajasMenuBarViewModel]",
            "  RibbonGroup «Verifactu» [CajasMenuBarViewModel]",
            "    RibbonButton «Facturas Verifactu» [CajasMenuBarViewModel]",
            "  RibbonGroup «Cobros» [CajasMenuBarViewModel]",
            "    RibbonButton «Enlaces de pago» [CajasMenuBarViewModel]",
        };

        private static readonly string[] VistasConPrism =
        {
            "PlantillaVentaView | PlantillaVentaView | PlantillaVentaViewModel",
            "InventarioView | InventarioView | InventarioViewModel",
            "CarteraPagosView | CarteraPagosView | CarteraPagosViewModel",
            "PedidoVentaView | PedidoVentaView | PedidoVentaViewModel",
            "DetallePedidoView | DetallePedidoView | DetallePedidoViewModel",
            "ListaRapportsView | ListaRapportsView | ListaRapportsViewModel",
            "RapportView | RapportView | RapportViewModel",
            "CanalesExternosPedidosView | CanalesExternosPedidosView | CanalesExternosPedidosViewModel",
            "CanalesExternosPagosView | CanalesExternosPagosView | CanalesExternosPagosViewModel",
            "CanalesExternosProductosView | CanalesExternosProductosView | CanalesExternosProductosViewModel",
            "CanalesExternosFacturasView | CanalesExternosFacturasView | CanalesExternosFacturasViewModel",
            "CanalesExternosCuadreFacturasView | CanalesExternosCuadreFacturasView | CanalesExternosCuadreFacturasViewModel",
            "PoisonPillsView | PoisonPillsView | PoisonPillsViewModel",
            "ProductoView | ProductoView | ProductoViewModel",
            "ReposicionView | ReposicionView | ReposicionViewModel",
            "VideosView | VideosView | VideosViewModel",
            "EtiquetasHuecoView | EtiquetasHuecoView | EtiquetasHuecoViewModel",
            "RecibirReposicionView | RecibirReposicionView | RecibirReposicionViewModel",
            "EnviarReposicionView | EnviarReposicionView | EnviarReposicionViewModel",
            "CalendarioReposicionesView | CalendarioReposicionesView | CalendarioReposicionesViewModel",
            "DiferenciasReposicionView | DiferenciasReposicionView | DiferenciasReposicionViewModel",
            "PropuestasStockView | PropuestasStockView | PropuestasStockViewModel",
            "CrearClienteView | CrearClienteView | CrearClienteViewModel",
            "Modelo347View | Modelo347View | Modelo347ViewModel",
            "ExtractoClienteView | ExtractoClienteView | ExtractoClienteViewModel",
            "ClientesNifIncorrectosView | ClientesNifIncorrectosView | ClientesNifIncorrectosViewModel",
            "MantenimientoCodigosPostalesView | MantenimientoCodigosPostalesView | MantenimientoCodigosPostalesViewModel",
            "MantenimientoEventosView | MantenimientoEventosView | MantenimientoEventosViewModel",
            "SenalesEventosView | SenalesEventosView | SenalesEventosViewModel",
            "PedidoCompraView | PedidoCompraView | PedidoCompraViewModel",
            "DetallePedidoCompraView | DetallePedidoCompraView | DetallePedidoCompraViewModel",
            "ListaPedidosCompraView | ListaPedidosCompraView | ListaPedidosCompraViewModel",
            "CajasView | CajasView | CajasViewModel",
            "BancosView | BancosView | BancosViewModel",
            "MayorCuentaView | MayorCuentaView | MayorCuentaViewModel",
            "FacturasPendientesVerifactuView | FacturasPendientesVerifactuView | FacturasPendientesVerifactuViewModel",
            "AuditoriaEnlacesPagoView | AuditoriaEnlacesPagoView | AuditoriaEnlacesPagoViewModel",
            "GanavisionesView | GanavisionesView | GanavisionesViewModel",
            "OfertasCombinadasView | OfertasCombinadasView | OfertasCombinadasViewModel",
        };

        private static readonly string[] DialogosConPrism =
        {
            "ConfirmationDialog | ConfirmationDialog | ConfirmationDialogViewModel",
            "NotificationDialog | NotificationDialog | NotificationDialogViewModel",
            "InputAmountDialog | InputAmountDialog | InputAmountDialogViewModel",
            "InputTextDialog | InputTextDialog | InputTextDialogViewModel",
            "InputDateDialog | InputDateDialog | InputDateDialogViewModel",
            "CorreccionVideoProductoView | CorreccionVideoProductoView | CorreccionVideoProductoViewModel",
            "ActualizarControlesStockPopupView | ActualizarControlesStockPopupView | ActualizarControlesStockPopupViewModel",
            "SelectorProductoDuplicadoDialog | SelectorProductoDuplicadoDialog | SelectorProductoDuplicadoDialogViewModel",
            "NovedadesDialog | NovedadesDialog | NovedadesDialogViewModel",
            "ParametrosUsuarioDialog | ParametrosUsuarioDialog | ParametrosUsuarioDialogViewModel",
            "RevisionConceptoDialog | RevisionConceptoDialog | RevisionConceptoDialogViewModel",
            "ElegirEventoCursoDialog | ElegirEventoCursoDialog | ElegirEventoCursoDialogViewModel",
            "ElegirPedidoPendienteDialog | ElegirPedidoPendienteDialog | ElegirPedidoPendienteDialogViewModel",
            "PickingPopupView | PickingPopupView | PickingPopupViewModel",
            "SugerenciasRutaPickingPopup | SugerenciasRutaPickingPopup | SugerenciasRutaPickingPopupViewModel",
            "FacturarRutasPopup | FacturarRutasPopup | FacturarRutasPopupViewModel",
            "ErroresFacturacionRutasPopup | ErroresFacturacionRutasPopup | ErroresFacturacionRutasPopupViewModel",
            "CopiarFacturaView | CopiarFacturaView | CopiarFacturaViewModel",
            "SelectorGrupoComisionDialog | SelectorGrupoComisionDialog | SelectorGrupoComisionDialogViewModel",
            "CopiarSeguimientosView | CopiarSeguimientosView | CopiarSeguimientosViewModel",
            "ElegirEventoDialog | ElegirEventoDialog | ElegirEventoDialogViewModel",
            "InscribirEventoDialog | InscribirEventoDialog | InscribirEventoDialogViewModel",
            "AnunciarEventoDialog | AnunciarEventoDialog | AnunciarEventoDialogViewModel",
            "NotificacionTelefonoView | NotificacionTelefonoView | NotificacionTelefonoViewModel",
        };

        // La cinta, de arriba abajo: cada elemento con su tipo, su texto y el tipo de su DataContext (el ViewModel del
        // módulo que lo puso, que FusionCinta conserva al moverlo).
        private static IEnumerable<string> VolcarCinta(Ribbon cinta)
        {
            var lineas = new List<string>();
            if (cinta.ApplicationMenu != null)
            {
                Volcar(cinta.ApplicationMenu, 0, lineas);
            }
            if (cinta.QuickAccessToolBar != null)
            {
                Volcar(cinta.QuickAccessToolBar, 0, lineas);
            }
            foreach (object grupo in cinta.ContextualTabGroups)
            {
                lineas.Add("Contextual «" + Texto(grupo) + "»");
            }
            foreach (object elemento in cinta.Items)
            {
                Volcar(elemento, 0, lineas);
            }
            return lineas;
        }

        private static void Volcar(object elemento, int nivel, List<string> lineas)
        {
            lineas.Add($"{new string(' ', nivel * 2)}{elemento.GetType().Name} «{Texto(elemento)}» [{NombreDelTipo((elemento as FrameworkElement)?.DataContext)}]");
            if (elemento is ItemsControl itemsControl && elemento is not RibbonGallery)
            {
                foreach (object hijo in itemsControl.Items)
                {
                    Volcar(hijo, nivel + 1, lineas);
                }
            }
        }

        private static string Texto(object elemento)
        {
            object texto = elemento switch
            {
                RibbonButton b => b.Label,
                RibbonToggleButton t => t.Label,
                RibbonComboBox cb => cb.Label,
                RibbonMenuButton m => m.Label,
                RibbonCheckBox c => c.Label,
                RibbonTextBox tb => tb.Label,
                HeaderedItemsControl h => h.Header,
                HeaderedContentControl hc => hc.Header,
                FrameworkElement f => f.Name,
                _ => elemento.ToString()
            };
            return texto as string ?? texto?.ToString() ?? string.Empty;
        }
    }
}
