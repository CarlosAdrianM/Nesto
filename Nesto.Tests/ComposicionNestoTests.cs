using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Composicion;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Nesto.Tests
{
    /// <summary>
    /// Nesto#490 (4C.5, tramo 1): la prueba sombra. Monta la composición de Nesto (<see cref="ComposicionNesto"/> y los 13
    /// módulos de <see cref="ComposicionNesto.Modulos"/>) sobre Microsoft.Extensions.DependencyInjection, con
    /// <c>ValidateOnBuild</c>, sin abrir ventanas ni ir a la red. Es la red del tramo 4 (quitar Prism/Unity): todo lo que
    /// Nesto pide al contenedor tiene que poder construirse igual que con Unity.
    /// <para>
    /// El riesgo principal: Unity usa el constructor más largo y construye clases concretas sin registrar; MS.DI usa el
    /// constructor más largo que <em>puede</em> resolver (y si no puede con ninguno, lanza), así que con un registro que
    /// falte saldría, sin avisar, por un constructor más corto (p. ej. un menú de la cinta con <c>New()</c> y
    /// <c>New(viewModel)</c> saldría sin DataContext). Por eso, además de <c>ValidateOnBuild</c>, se comprueba que el
    /// constructor más largo de cada tipo registrado se puede resolver entero.
    /// </para>
    /// </summary>
    [TestClass]
    public class ComposicionNestoTests
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            // Sin Application, las imágenes de los XAML del exe con rutas relativas (images/cliente.png) se buscarían en el
            // ensamblado de vstest
            try
            {
                System.Windows.Application.ResourceAssembly = typeof(MenuBarView).Assembly;
            }
            catch (InvalidOperationException)
            {
                // Ya lo había fijado otra prueba (o alguien lo leyó antes) en este proceso: se cambia por debajo
                typeof(System.Windows.Application)
                    .GetField("_resourceAssembly", BindingFlags.NonPublic | BindingFlags.Static)
                    ?.SetValue(null, typeof(MenuBarView).Assembly);
            }
        }

        private sealed class Composicion
        {
            public ContenedorMicrosoft Contenedor { get; } = new ContenedorMicrosoft();
            public List<IModuloNesto> Modulos { get; } = new List<IModuloNesto>();
            public List<UIElement> MenusEnLaCinta { get; } = new List<UIElement>();
            public ResolutorMicrosoft Resolutor { get; set; }
        }

        // Lo mismo que hace Application.RegisterTypes + el catálogo de módulos de Prism, sobre MS.DI. Necesita STA:
        // MenuBarView es a la vez el módulo de lo viejo y el menú de la cinta (un UserControl).
        private static Composicion Montar(bool construir = true)
        {
            var composicion = new Composicion();
            ContenedorMicrosoft contenedor = composicion.Contenedor;

            ComposicionNesto.Registrar(contenedor);
            foreach (Type tipoModulo in ComposicionNesto.Modulos)
            {
                Type implementacion = ImplementacionDe(contenedor.Servicios, tipoModulo) ?? tipoModulo;
                var modulo = Activator.CreateInstance(implementacion) as IModuloNesto;
                Assert.IsNotNull(modulo, $"El módulo {tipoModulo.Name} ({implementacion.Name}) no implementa IModuloNesto");
                modulo.Registrar(contenedor);
                composicion.Modulos.Add(modulo);
            }

            // Nesto#490 (4C.4, tramo 4): la navegación (la región propia) ya la registra ComposicionNesto; no queda ningún fake
            // de lo que se registraba fuera (ni el IRegionManager de Prism)
            // Al final, sin red: la configuración, el cliente de la API y la autenticación son fakes
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            _ = A.CallTo(() => configuracion.servidorAPI).Returns("http://localhost/NestoAPI-pruebas/");
            _ = A.CallTo(() => configuracion.usuario).Returns(@"NUEVAVISION\Pruebas");
            _ = A.CallTo(() => configuracion.UsuarioSinDominio).Returns("Pruebas");
            // PlantillaVentaViewModel busca su almacén de ruta entre las sedes
            _ = A.CallTo(() => configuracion.LeerParametroSync(A<string>._, Parametros.Claves.AlmacenRuta)).Returns(Constantes.Sedes.ListaSedes.First().Codigo);
            contenedor.Instancia(configuracion);
            contenedor.Instancia(A.Fake<IClienteApiFactory>());
            contenedor.Instancia(A.Fake<IServicioAutenticacion>());
            // La cinta de verdad es la de MainWindow (una ventana): aquí solo se apunta lo que le llega
            IServicioCinta cinta = A.Fake<IServicioCinta>();
            _ = A.CallTo(() => cinta.AnadirMenu(A<UIElement>._)).Invokes((UIElement menu) => composicion.MenusEnLaCinta.Add(menu));
            contenedor.Instancia(cinta);

            if (construir)
            {
                // Sin validar aquí: la validación tiene su propia prueba, y así cada una de las demás dice todo lo que falla
                composicion.Resolutor = contenedor.Construir(validar: false);
            }
            return composicion;
        }

        [TestMethod]
        public void LaComposicion_SeConstruyeConValidateOnBuild()
        {
            EjecutarEnSTA(() =>
            {
                Composicion composicion = Montar(construir: false);

                try
                {
                    _ = composicion.Contenedor.Construir();
                }
                catch (AggregateException ex)
                {
                    Assert.Fail("Registros que MS.DI no puede construir:\n" + string.Join("\n", ex.InnerExceptions.Select(e => e.Message)));
                }
                Assert.AreEqual(13, composicion.Modulos.Count);
            });
        }

        [TestMethod]
        public void CadaTipoRegistrado_SeConstruyePorElConstructorQueUsariaUnity()
        {
            EjecutarEnSTA(() =>
            {
                Composicion composicion = Montar();
                var esServicio = composicion.Resolutor.Proveedor.GetRequiredService<IServiceProviderIsService>();

                var fallos = new List<string>();
                foreach (ServiceDescriptor registro in composicion.Contenedor.Servicios)
                {
                    Type implementacion = registro.IsKeyedService ? registro.KeyedImplementationType : registro.ImplementationType;
                    if (implementacion != null)
                    {
                        string quien = registro.IsKeyedService
                            ? $"la vista «{registro.ServiceKey}» ({implementacion.Name})"
                            : $"{registro.ServiceType.Name} → {implementacion.Name}";
                        fallos.AddRange(DependenciasQueFaltan(implementacion, esServicio).Select(f => $"{quien}: {f}"));
                    }
                }

                Assert.AreEqual(0, fallos.Count, "Con MS.DI saldrían por otro constructor o no saldrían:\n" + string.Join("\n", fallos.Distinct()));
            });
        }

        [TestMethod]
        public void LosDialogos_TienenVistaYViewModelResolubles()
        {
            EjecutarEnSTA(() =>
            {
                Composicion composicion = Montar();
                IServiceProvider proveedor = composicion.Resolutor.Proveedor;
                var esServicio = proveedor.GetRequiredService<IServiceProviderIsService>();

                List<string> nombres = composicion.Contenedor.Servicios
                    .Where(r => r.IsKeyedService && r.ServiceType == typeof(DialogoRegistrado))
                    .Select(r => (string)r.ServiceKey)
                    .ToList();
                var fallos = new List<string>();
                foreach (string nombre in nombres)
                {
                    DialogoRegistrado dialogo = composicion.Resolutor.Dialogo(nombre);
                    Assert.IsNotNull(dialogo, nombre);
                    // ResolutorMicrosoft.Resolver construye la vista y el ViewModel aunque no estén registrados (como Unity),
                    // pero sus dependencias sí tienen que estarlo
                    fallos.AddRange(DependenciasQueFaltan(dialogo.TipoVista, esServicio).Select(f => $"diálogo «{nombre}», vista: {f}"));
                    fallos.AddRange(DependenciasQueFaltan(dialogo.TipoViewModel, esServicio).Select(f => $"diálogo «{nombre}», ViewModel: {f}"));
                }

                Assert.IsTrue(nombres.Count >= 20, $"Solo hay {nombres.Count} diálogos registrados");
                Assert.AreEqual(0, fallos.Count, string.Join("\n", fallos));
            });
        }

        [TestMethod]
        public void LoQueSePidePorSuTipoConcreto_EstaRegistrado()
        {
            // Lo que no se ve en los constructores: se pide al contenedor por su tipo en tiempo de ejecución
            // (MenuBarViewModel abre sus ventanas, CargarPedido, los maestro-detalle, los selectores, AutocompleteBehavior)
            var porSuTipo = new List<Type>(MenuBarView.VentanasPorNombre.Values)
            {
                typeof(Nesto.Modulos.PedidoVenta.PedidoVentaView),
                typeof(Nesto.Modulos.PedidoVenta.ListaPedidosVenta),
                typeof(Nesto.Modulos.PedidoCompra.Views.PedidoCompraView),
                typeof(Nesto.Modulos.PedidoCompra.Views.ListaPedidosCompraView),
                typeof(ControlesUsuario.ViewModels.SelectorClienteViewModel),
                typeof(ControlesUsuario.ViewModels.SelectorProveedorViewModel),
                typeof(ControlesUsuario.Services.ServicioBusquedaProductos),
                typeof(ControlesUsuario.Services.ServicioBusquedaCuentas),
                typeof(ControlesUsuario.Notificaciones.CampanaNotificacionesViewModel),
            };

            EjecutarEnSTA(() =>
            {
                Composicion composicion = Montar();
                var esServicio = composicion.Resolutor.Proveedor.GetRequiredService<IServiceProviderIsService>();

                List<string> sinRegistrar = porSuTipo.Where(t => !esServicio.IsService(t)).Select(t => t.FullName).ToList();

                Assert.AreEqual(0, sinRegistrar.Count, "Sin registrar: " + string.Join(", ", sinRegistrar));
            });
        }

        [TestMethod]
        public void LasClasesDelTramo3_NoRecibenElContenedorDePrismNiDeUnity()
        {
            // Nesto#490 (4C.5, tramo 3b): ni lo guardan sin usarlo, ni le piden servicios, ni crean vistas con él.
            // Tramo 3c: tampoco el menú de lo viejo (MenuBarView crea su MenuBarViewModel con New y ya no le pasa el
            // IUnityContainer, sino el resolutor propio, el abridor de novedades y el servicio de diálogos).
            var clases = new[]
            {
                typeof(Nesto.Modulos.PedidoVenta.FacturarRutasPopupViewModel),
                typeof(Nesto.Modulos.Rapports.ListaRapportsViewModel),
                typeof(Nesto.ViewModels.MainViewModel),
                typeof(Nesto.ViewModels.AlquileresViewModel),
                typeof(Nesto.ViewModels.PlanesVentajasViewModel),
                typeof(Nesto.ViewModels.RemesasViewModel),
                typeof(Nesto.Modulos.PedidoVenta.PedidoVentaViewModel),
                typeof(Nesto.Modulos.PedidoVenta.PedidoVentaView),
                typeof(Nesto.Modulos.PedidoCompra.ViewModels.PedidoCompraViewModel),
                typeof(Nesto.Modulos.PedidoCompra.Views.PedidoCompraView),
                typeof(Nesto.ViewModels.MenuBarViewModel),
                typeof(MenuBarView),
            };

            List<string> conContenedor = clases
                .SelectMany(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SelectMany(c => c.GetParameters())
                    .Where(p => EsContenedorDePrismOUnity(p.ParameterType))
                    .Select(p => $"{t.Name} ({p.Name})"))
                .ToList();

            Assert.AreEqual(0, conContenedor.Count, "Reciben el contenedor: " + string.Join(", ", conContenedor));

            // Nesto#490 (4C.5, tramo 4): sin Prism ni Unity referenciados, por el nombre (IUnityContainer, IContainerProvider,
            // IContainerExtension…); la guarda de que no queda ninguna referencia está en ArranqueSombraTests
            static bool EsContenedorDePrismOUnity(Type tipo)
                => tipo.FullName != null && (tipo.FullName.StartsWith("Unity.") || tipo.FullName.StartsWith("Prism.Ioc."));
        }

        [TestMethod]
        public void NingunTipoDeNesto_DependeDeLasRegionesDePrism()
        {
            // Nesto#490 (4C.4, tramo 4): ni vistas, ni ViewModels, ni la infraestructura heredan, implementan, guardan o
            // reciben nada de Prism.Regions (IRegionManager, IActiveAware no es de Regions pero también se fue). Es lo que
            // permite quitar Prism en el tramo 4 de 4C.5 sin tocar las regiones. Se miran el exe y todos los ensamblados de
            // Nesto (los que referencian Infrastructure).
            var ensamblados = new Dictionary<string, Assembly>();
            var pendientes = new Queue<Assembly>(new[] { typeof(MenuBarView).Assembly });
            while (pendientes.Count > 0)
            {
                Assembly actual = pendientes.Dequeue();
                if (!ensamblados.TryAdd(actual.GetName().Name, actual))
                {
                    continue;
                }
                foreach (AssemblyName referencia in actual.GetReferencedAssemblies())
                {
                    if (ensamblados.ContainsKey(referencia.Name))
                    {
                        continue;
                    }
                    Assembly cargado;
                    try { cargado = Assembly.Load(referencia); } catch { continue; }
                    bool esDeNesto = cargado.GetName().Name == "Infrastructure"
                        || cargado.GetReferencedAssemblies().Any(r => r.Name == "Infrastructure");
                    if (esDeNesto)
                    {
                        pendientes.Enqueue(cargado);
                    }
                }
            }
            Assert.IsTrue(ensamblados.ContainsKey("Infrastructure") && ensamblados.Count > 10,
                "Se tienen que mirar todos los ensamblados de Nesto: " + string.Join(", ", ensamblados.Keys));

            static bool EsDeRegiones(Type t) => t != null && ((t.Namespace?.StartsWith("Prism.Regions") ?? false) || t.FullName == "Prism.IActiveAware");
            const BindingFlags todo = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var conRegiones = new List<string>();
            foreach (Assembly ensamblado in ensamblados.Values)
            {
                Type[] tipos;
                try { tipos = ensamblado.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { tipos = ex.Types.Where(t => t != null).ToArray(); }
                foreach (Type tipo in tipos)
                {
                    IEnumerable<Type> usados = new[] { tipo.BaseType }
                        .Concat(tipo.GetInterfaces())
                        .Concat(tipo.GetFields(todo).Select(f => f.FieldType))
                        .Concat(tipo.GetProperties(todo).Select(p => p.PropertyType))
                        .Concat(tipo.GetConstructors(todo).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType))
                        .Concat(tipo.GetMethods(todo).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)));
                    conRegiones.AddRange(usados.Where(EsDeRegiones).Distinct().Select(t => $"{tipo.FullName} → {t.Name}"));
                }
            }

            Assert.AreEqual(0, conRegiones.Count, "Usan Prism.Regions: " + string.Join("; ", conRegiones));
        }

        [TestMethod]
        public void ElResolutorPropio_SeResuelveYEsElDelContenedor()
        {
            EjecutarEnSTA(() =>
            {
                Composicion composicion = Montar();

                IResolutorNesto resolutor = composicion.Resolutor.Resolver<IResolutorNesto>();

                Assert.IsNotNull(resolutor);
                // Lo que se pide con él sale del mismo contenedor (los singletons son los mismos)
                Assert.AreSame(composicion.Resolutor.Resolver<IServicioCinta>(), resolutor.Resolver<IServicioCinta>());
            });
        }

        [TestMethod]
        public void LaNavegacion_EsLaRegionPropiaSobreElGestorDelShell()
        {
            // Nesto#490 (4C.4, tramo 4): sin Prism.Regions ni el piloto; la registra ComposicionNesto y sirve igual sobre MS.DI
            EjecutarEnSTA(() =>
            {
                Composicion composicion = Montar();

                IServicioNavegacion navegacion = composicion.Resolutor.Resolver<IServicioNavegacion>();
                var regiones = composicion.Resolutor.Resolver<Nesto.Infrastructure.Navegacion.RegionesDelShell>();

                Assert.IsInstanceOfType(navegacion, typeof(Nesto.Infrastructure.Navegacion.ServicioNavegacionNesto));
                Assert.AreSame(regiones.GestorShell, ((Nesto.Infrastructure.Navegacion.ServicioNavegacionNesto)navegacion).Gestor);
                Assert.AreSame(navegacion, composicion.Resolutor.Resolver<IServicioNavegacion>(), "Una sola para toda la aplicación");
            });
        }

        [TestMethod]
        public void LosModulos_SeResuelvenPorElTipoDelCatalogo()
        {
            EjecutarEnSTA(() =>
            {
                Composicion composicion = Montar();

                foreach (Type tipoModulo in ComposicionNesto.Modulos)
                {
                    Assert.IsInstanceOfType(composicion.Resolutor.Resolver(tipoModulo), typeof(IModuloNesto), tipoModulo.Name);
                }
            });
        }

        [TestMethod]
        public void LosModulos_SeInicializan_YCadaMenuDeLaCintaLlevaSuViewModel()
        {
            EjecutarEnSTA(() =>
            {
                Composicion composicion = Montar();

                foreach (IModuloNesto modulo in composicion.Modulos)
                {
                    modulo.Inicializar(composicion.Resolutor);
                }

                Assert.AreEqual(13, composicion.MenusEnLaCinta.Count, "Cada módulo pone su menú en la cinta");
                foreach (FrameworkElement menu in composicion.MenusEnLaCinta.Cast<FrameworkElement>())
                {
                    Type tipoViewModel = menu is MenuBarView
                        ? typeof(Nesto.ViewModels.MenuBarViewModel)
                        : ConstructorDeUnity(menu.GetType()).GetParameters().First().ParameterType;
                    Assert.IsInstanceOfType(menu.DataContext, tipoViewModel, $"{menu.GetType().Name} sin el DataContext de su ViewModel");
                }
            });
        }

        [TestMethod]
        public void LosServicios_SeResuelven()
        {
            EjecutarEnSTA(() =>
            {
                Composicion composicion = Montar();
                var fallos = new List<string>();

                foreach (ServiceDescriptor registro in composicion.Contenedor.Servicios.Where(r => !r.IsKeyedService && r.ServiceType.IsInterface))
                {
                    if (registro.ImplementationType != null && typeof(FrameworkElement).IsAssignableFrom(registro.ImplementationType))
                    {
                        continue; // Ventanas y vistas: con la comprobación de los constructores basta
                    }
                    try
                    {
                        Assert.IsNotNull(composicion.Resolutor.Resolver(registro.ServiceType), registro.ServiceType.Name);
                    }
                    catch (Exception ex) when (ex is not AssertFailedException)
                    {
                        fallos.Add($"{registro.ServiceType.Name}: {ex.GetType().Name}: {ex.Message}");
                    }
                }

                Assert.AreEqual(0, fallos.Count, string.Join("\n", fallos));
            });
        }

        // El constructor que usa Unity: el público con más parámetros.
        private static ConstructorInfo ConstructorDeUnity(Type tipo)
            => tipo.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                   .OrderByDescending(c => c.GetParameters().Length)
                   .FirstOrDefault();

        // Lo que le falta al contenedor para construir el tipo por el constructor que usaría Unity.
        private static IEnumerable<string> DependenciasQueFaltan(Type tipo, IServiceProviderIsService esServicio)
        {
            ConstructorInfo constructor = ConstructorDeUnity(tipo);
            if (constructor == null)
            {
                yield return "no tiene constructor público";
                yield break;
            }
            foreach (ParameterInfo parametro in constructor.GetParameters())
            {
                if (!parametro.HasDefaultValue && !esServicio.IsService(parametro.ParameterType))
                {
                    yield return $"falta {parametro.ParameterType.FullName} (parámetro {parametro.Name})";
                }
            }
        }

        private static Type ImplementacionDe(IServiceCollection servicios, Type servicio)
            => servicios.LastOrDefault(r => !r.IsKeyedService && r.ServiceType == servicio)?.ImplementationType;

        private static void EjecutarEnSTA(Action accion)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    accion();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null)
            {
                throw error as AssertFailedException ?? new AssertFailedException(error.ToString(), error);
            }
        }
    }
}
