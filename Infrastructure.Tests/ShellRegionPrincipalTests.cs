using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using Nesto.Infrastructure.Shared;
using Prism.Ioc;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4, shell): fijan cómo se comporta HOY la región principal del shell (el TabControl «MainRegion» de
    /// MainWindow) antes de quitarle Prism. Se monta como en Nesto: un <see cref="TabControl"/> adaptado con el
    /// <see cref="SelectorRegionAdapter"/> de Prism, con el cargador de vistas <see cref="CargadorVistasNavegacion"/> y los
    /// dos comportamientos globales (<see cref="LimpiarVistaAlQuitarRegionBehavior"/> y
    /// <see cref="ReceptorNavegacionRegionBehavior"/>), y se maneja con <see cref="ServicioNavegacionPrism"/>, como los
    /// ViewModels. Cuando la región principal deje de ser de Prism, estas pruebas tienen que seguir pasando igual.
    /// </summary>
    [TestClass]
    public class ShellRegionPrincipalTests
    {
        private const string MAIN_REGION = "MainRegion";

        // Una vista que reutiliza su pestaña (IReceptorNavegacion), como el Extracto de cliente o el 347
        private sealed class ReceptorQueReutiliza : IReceptorNavegacion
        {
            public List<ParametrosNavegacion> Llegadas { get; } = new List<ParametrosNavegacion>();
            public string Titulo => "Reutiliza";
            public void AlLlegar(ParametrosNavegacion parametros) => Llegadas.Add(parametros);
        }

        // Una vista que abre pestaña nueva cada vez (IReceptorNavegacionPestanaNueva), como las de ViewModelBase
        private sealed class ReceptorPestanaNueva : IReceptorNavegacionPestanaNueva
        {
            public List<ParametrosNavegacion> Llegadas { get; } = new List<ParametrosNavegacion>();
            public void AlLlegar(ParametrosNavegacion parametros) => Llegadas.Add(parametros);
        }

        // Una vista que pregunta antes de cerrarse (ITabCloseConfirmation), como la plantilla de venta
        private sealed class ConConfirmacion : ITabCloseConfirmation
        {
            public bool Respuesta { get; set; }
            public int Preguntas { get; private set; }
            public bool ConfirmTabClose()
            {
                Preguntas++;
                return Respuesta;
            }
        }

        private sealed class ConAmbito : IConAmbitoNavegacion
        {
            public IServicioNavegacion NavegacionAmbito { get; set; }
        }

        // Prism busca la vista abierta por el nombre de su tipo: una clase por «vista registrada»
        private sealed class VistaReutiliza : Border { }
        private sealed class VistaPestanaNueva : Border { }
        private sealed class VistaMaestra : Border { }

        private sealed class Shell
        {
            public TabControl Pestanas { get; set; }
            public IRegion Region { get; set; }
            public IRegionManager Gestor { get; set; }
            public ServicioNavegacionPrism Navegacion { get; set; }
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

        // Lo mismo que monta Prism en Nesto para el TabControl MainRegion (Application.ConfigureDefaultRegionBehaviors y
        // el registro del cargador de vistas), con un contenedor de mentira que crea las vistas por su nombre.
        private static Shell CrearShell(Dictionary<string, Func<object>> vistasRegistradas = null)
        {
            vistasRegistradas ??= new Dictionary<string, Func<object>>();
            var contenedor = A.Fake<IContainerExtension>();
            A.CallTo(() => contenedor.Resolve(typeof(object), A<string>._))
                .ReturnsLazily((Type _, string nombre) => vistasRegistradas[nombre]());
            // Prism apunta cada navegación en el diario (Atrás/Adelante); Nesto no lo usa, pero sin esto la navegación falla
            A.CallTo(() => contenedor.Resolve(typeof(IRegionNavigationJournalEntry)))
                .ReturnsLazily(() => new RegionNavigationJournalEntry());

            var pestanas = new TabControl();
            var adaptador = new SelectorRegionAdapter(new RegionBehaviorFactory(contenedor));
            IRegion region = adaptador.Initialize(pestanas, MAIN_REGION);
            var navegacionPrism = new RegionNavigationService(contenedor, new CargadorVistasNavegacion(contenedor), new RegionNavigationJournal())
            {
                Region = region
            };
            region.NavigationService = navegacionPrism;
            region.Behaviors.Add(LimpiarVistaAlQuitarRegionBehavior.BehaviorKey, new LimpiarVistaAlQuitarRegionBehavior());
            region.Behaviors.Add(ReceptorNavegacionRegionBehavior.BehaviorKey, new ReceptorNavegacionRegionBehavior());

            var gestor = new RegionManager();
            gestor.Regions.Add(region);
            return new Shell { Pestanas = pestanas, Region = region, Gestor = gestor, Navegacion = new ServicioNavegacionPrism(gestor) };
        }

        // Lo que hace la X de la pestaña (CloseTabAction en el shell) con la vista de esa pestaña
        private static void CerrarPestana(Shell shell, object vista)
        {
            var confirmacion = (vista as FrameworkElement)?.DataContext as ITabCloseConfirmation;
            if (confirmacion != null && !confirmacion.ConfirmTabClose())
            {
                return;
            }
            IRegion region = RegionManager.GetObservableRegion(shell.Pestanas).Value;
            if (region == null)
            {
                return;
            }
            if (region.Views.Contains(vista))
            {
                region.Remove(vista);
            }
        }

        private static List<object> Pestanas(Shell shell) => shell.Pestanas.Items.Cast<object>().ToList();

        [TestMethod]
        public void Navegar_AUnaVistaRegistrada_AbreUnaPestanaYLaSelecciona()
        {
            EnSta(() =>
            {
                var shell = CrearShell(new Dictionary<string, Func<object>>
                {
                    { nameof(VistaReutiliza), () => new VistaReutiliza { DataContext = new ReceptorQueReutiliza() } }
                });

                shell.Navegacion.RequestNavigate(MAIN_REGION, nameof(VistaReutiliza));

                Assert.AreEqual(1, Pestanas(shell).Count);
                object vista = Pestanas(shell).Single();
                Assert.IsInstanceOfType(vista, typeof(VistaReutiliza));
                Assert.AreSame(vista, shell.Pestanas.SelectedItem);
                Assert.AreSame(vista, shell.Navegacion.VistaActiva(MAIN_REGION));
            });
        }

        [TestMethod]
        public void Navegar_DosVecesAUnReceptorQueReutiliza_UnaSolaPestanaQueRecibeLasDosNavegaciones()
        {
            EnSta(() =>
            {
                var receptor = new ReceptorQueReutiliza();
                var shell = CrearShell(new Dictionary<string, Func<object>>
                {
                    { nameof(VistaReutiliza), () => new VistaReutiliza { DataContext = receptor } }
                });

                shell.Navegacion.RequestNavigate(MAIN_REGION, nameof(VistaReutiliza), new ParametrosNavegacion { { "clienteParameter", "1" } });
                shell.Navegacion.RequestNavigate(MAIN_REGION, nameof(VistaReutiliza), new ParametrosNavegacion { { "clienteParameter", "2" } });

                Assert.AreEqual(1, Pestanas(shell).Count);
                Assert.AreEqual(2, receptor.Llegadas.Count);
                Assert.AreEqual("1", receptor.Llegadas[0].GetValue<string>("clienteParameter"));
                Assert.AreEqual("2", receptor.Llegadas[1].GetValue<string>("clienteParameter"));
            });
        }

        [TestMethod]
        public void Navegar_DosVecesAUnReceptorPestanaNueva_DosPestanasYLaSeleccionadaEsLaUltima()
        {
            EnSta(() =>
            {
                var receptores = new List<ReceptorPestanaNueva>();
                var shell = CrearShell(new Dictionary<string, Func<object>>
                {
                    { nameof(VistaPestanaNueva), () =>
                        {
                            var receptor = new ReceptorPestanaNueva();
                            receptores.Add(receptor);
                            return new VistaPestanaNueva { DataContext = receptor };
                        }
                    }
                });

                shell.Navegacion.RequestNavigate(MAIN_REGION, nameof(VistaPestanaNueva));
                shell.Navegacion.RequestNavigate(MAIN_REGION, nameof(VistaPestanaNueva));

                Assert.AreEqual(2, Pestanas(shell).Count);
                Assert.AreEqual(2, receptores.Count);
                Assert.AreSame(Pestanas(shell)[1], shell.Pestanas.SelectedItem);
                Assert.AreSame(Pestanas(shell)[1], shell.Navegacion.VistaActiva(MAIN_REGION));
                // Cada navegación llega a la vista que se acaba de abrir, y solo a ella
                Assert.AreEqual(1, receptores[0].Llegadas.Count);
                Assert.AreEqual(1, receptores[1].Llegadas.Count);
            });
        }

        [TestMethod]
        public void Navegar_AUnaVistaQueYaEstabaAbierta_LaVuelveASeleccionar()
        {
            EnSta(() =>
            {
                var shell = CrearShell(new Dictionary<string, Func<object>>
                {
                    { nameof(VistaReutiliza), () => new VistaReutiliza { DataContext = new ReceptorQueReutiliza() } },
                    { nameof(VistaPestanaNueva), () => new VistaPestanaNueva { DataContext = new ReceptorPestanaNueva() } }
                });
                shell.Navegacion.RequestNavigate(MAIN_REGION, nameof(VistaReutiliza));
                object reutiliza = Pestanas(shell)[0];
                shell.Navegacion.RequestNavigate(MAIN_REGION, nameof(VistaPestanaNueva));
                Assert.AreNotSame(reutiliza, shell.Pestanas.SelectedItem);

                shell.Navegacion.RequestNavigate(MAIN_REGION, nameof(VistaReutiliza));

                Assert.AreEqual(2, Pestanas(shell).Count);
                Assert.AreSame(reutiliza, shell.Pestanas.SelectedItem);
                Assert.AreSame(reutiliza, shell.Navegacion.VistaActiva(MAIN_REGION));
            });
        }

        [TestMethod]
        public void ElegirOtraPestana_LaConvierteEnLaVistaActiva()
        {
            EnSta(() =>
            {
                var shell = CrearShell();
                var primera = new Border();
                var segunda = new Border();
                shell.Navegacion.AbrirVistaNueva(MAIN_REGION, primera, "Clientes");
                shell.Navegacion.AbrirVistaNueva(MAIN_REGION, segunda, "Clientes");
                Assert.AreSame(segunda, shell.Navegacion.VistaActiva(MAIN_REGION));

                shell.Pestanas.SelectedItem = primera;

                Assert.AreSame(primera, shell.Navegacion.VistaActiva(MAIN_REGION));
                Assert.IsTrue(shell.Region.ActiveViews.Contains(primera));
                Assert.IsFalse(shell.Region.ActiveViews.Contains(segunda));
            });
        }

        [TestMethod]
        public void AbrirVistaNueva_DosVecesConElMismoNombre_DosPestanasNumeradasYSeleccionaLaUltima()
        {
            EnSta(() =>
            {
                var shell = CrearShell();
                var primera = new Border();
                var segunda = new Border();

                shell.Navegacion.AbrirVistaNueva(MAIN_REGION, primera, "Clientes");
                shell.Navegacion.AbrirVistaNueva(MAIN_REGION, segunda, "Clientes");

                CollectionAssert.AreEqual(new object[] { primera, segunda }, Pestanas(shell));
                Assert.AreSame(primera, shell.Region.GetView("Clientes"));
                Assert.AreSame(segunda, shell.Region.GetView("Clientes2"));
                Assert.AreSame(segunda, shell.Pestanas.SelectedItem);
            });
        }

        [TestMethod]
        public void CerrarVistaActiva_QuitaLaPestanaSeleccionadaYLimpiaSuDataContext()
        {
            EnSta(() =>
            {
                var shell = CrearShell();
                var primera = new Border { DataContext = new object() };
                var segunda = new Border { DataContext = new object() };
                shell.Navegacion.AbrirVistaNueva(MAIN_REGION, primera, "Uno");
                shell.Navegacion.AbrirVistaNueva(MAIN_REGION, segunda, "Dos");

                shell.Navegacion.CerrarVistaActiva(MAIN_REGION);

                CollectionAssert.AreEqual(new object[] { primera }, Pestanas(shell));
                Assert.IsNull(segunda.DataContext);
                Assert.IsNotNull(primera.DataContext);
            });
        }

        [TestMethod]
        public void LaXDeUnaPestana_LaQuitaYLimpiaSuDataContext()
        {
            EnSta(() =>
            {
                var shell = CrearShell();
                var primera = new Border { DataContext = new object() };
                var segunda = new Border { DataContext = new object() };
                shell.Navegacion.AbrirVistaNueva(MAIN_REGION, primera, "Uno");
                shell.Navegacion.AbrirVistaNueva(MAIN_REGION, segunda, "Dos");

                // La X de la pestaña que NO está seleccionada
                CerrarPestana(shell, primera);

                CollectionAssert.AreEqual(new object[] { segunda }, Pestanas(shell));
                Assert.IsNull(primera.DataContext);
                Assert.AreSame(segunda, shell.Pestanas.SelectedItem);
            });
        }

        [TestMethod]
        public void LaXDeUnaPestanaQuePideConfirmacion_SiDiceQueNo_NoLaQuita()
        {
            EnSta(() =>
            {
                var shell = CrearShell();
                var confirmacion = new ConConfirmacion { Respuesta = false };
                var vista = new Border { DataContext = confirmacion };
                shell.Navegacion.AbrirVistaNueva(MAIN_REGION, vista, "PlantillaVenta");

                CerrarPestana(shell, vista);

                Assert.AreEqual(1, confirmacion.Preguntas);
                CollectionAssert.AreEqual(new object[] { vista }, Pestanas(shell));
                Assert.AreSame(confirmacion, vista.DataContext);
            });
        }

        [TestMethod]
        public void LaXDeUnaPestanaQuePideConfirmacion_SiDiceQueSi_LaQuita()
        {
            EnSta(() =>
            {
                var shell = CrearShell();
                var confirmacion = new ConConfirmacion { Respuesta = true };
                var vista = new Border { DataContext = confirmacion };
                shell.Navegacion.AbrirVistaNueva(MAIN_REGION, vista, "PlantillaVenta");

                CerrarPestana(shell, vista);

                Assert.AreEqual(1, confirmacion.Preguntas);
                Assert.AreEqual(0, Pestanas(shell).Count);
                Assert.IsNull(vista.DataContext);
            });
        }

        [TestMethod]
        public void LaXDeUnaVistaQueYaNoEstaEnLaRegion_NoHaceNada()
        {
            EnSta(() =>
            {
                var shell = CrearShell();
                var abierta = new Border { DataContext = new object() };
                shell.Navegacion.AbrirVistaNueva(MAIN_REGION, abierta, "Uno");

                CerrarPestana(shell, new Border());

                CollectionAssert.AreEqual(new object[] { abierta }, Pestanas(shell));
                Assert.IsNotNull(abierta.DataContext);
            });
        }

        [TestMethod]
        public void AbrirVistaConAmbito_DosVeces_DosPestanasCadaUnaConSuPropiaNavegacion()
        {
            // Los maestro-detalle (PedidoVenta, PedidoCompra): cada pestaña con sus regiones, aunque se llamen igual
            EnSta(() =>
            {
                var shell = CrearShell();
                var primerContexto = new ConAmbito();
                var segundoContexto = new ConAmbito();
                var primera = new VistaMaestra { DataContext = primerContexto };
                var segunda = new VistaMaestra { DataContext = segundoContexto };

                IServicioNavegacion primerAmbito = shell.Navegacion.AbrirVistaConAmbito(MAIN_REGION, primera);
                IServicioNavegacion segundoAmbito = shell.Navegacion.AbrirVistaConAmbito(MAIN_REGION, segunda);

                CollectionAssert.AreEqual(new object[] { primera, segunda }, Pestanas(shell));
                Assert.AreSame(segunda, shell.Pestanas.SelectedItem);
                Assert.AreSame(primerAmbito, primerContexto.NavegacionAmbito);
                Assert.AreSame(segundoAmbito, segundoContexto.NavegacionAmbito);
                Assert.AreNotSame(primerAmbito, segundoAmbito);
                Assert.AreNotSame(shell.Navegacion, primerAmbito);
                // Cada pestaña lleva colgado su propio gestor de regiones, distinto del del shell
                IRegionManager gestorPrimera = RegionManager.GetRegionManager(primera);
                IRegionManager gestorSegunda = RegionManager.GetRegionManager(segunda);
                Assert.IsNotNull(gestorPrimera);
                Assert.IsNotNull(gestorSegunda);
                Assert.AreNotSame(gestorPrimera, gestorSegunda);
                Assert.AreNotSame(shell.Gestor, gestorPrimera);
            });
        }

        [TestMethod]
        public void NavegarAUnaRegionQueNoExiste_NoAbreNadaNiLanza()
        {
            EnSta(() =>
            {
                var shell = CrearShell(new Dictionary<string, Func<object>>
                {
                    { nameof(VistaReutiliza), () => new VistaReutiliza() }
                });

                shell.Navegacion.RequestNavigate("RegionQueNoExiste", nameof(VistaReutiliza));

                Assert.AreEqual(0, Pestanas(shell).Count);
                Assert.IsNull(shell.Navegacion.VistaActiva("RegionQueNoExiste"));
            });
        }
    }
}
