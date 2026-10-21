using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4, shell): cómo se comporta la región principal del shell (el TabControl «MainRegion» de MainWindow).
    /// Se escribieron contra Prism antes de quitárselo (un <see cref="TabControl"/> con el <c>SelectorRegionAdapter</c>, el
    /// <c>CargadorVistasNavegacion</c>, los dos comportamientos globales y <c>ServicioNavegacionPrism</c>), y desde el tramo
    /// de la región propia se ejecutaron con las DOS implementaciones y las mismas comprobaciones. En el tramo 4 se quita la
    /// de Prism y queda la propia: el TabControl registrado en un <see cref="GestorRegiones"/> y manejado con
    /// <see cref="ServicioNavegacionNesto"/>, como en Nesto (<see cref="RegionesDelShell"/>). Incluyen los maestro-detalle
    /// con regiones de verdad (ContentControl dentro de la vista) y el aviso de activación (<see cref="IConActivacion"/>).
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

        // Como ListaRapportsViewModel: la región le dice si su pestaña es la activa (IConActivacion, antes IActiveAware de Prism)
        private sealed class ConActivacion : IConActivacion
        {
            private bool _isActive;
            public List<bool> Cambios { get; } = new List<bool>();
            public bool IsActive
            {
                get => _isActive;
                set
                {
                    if (_isActive != value)
                    {
                        _isActive = value;
                        Cambios.Add(value);
                    }
                }
            }
        }

        private sealed class VistaDetalle : Border { }
        private sealed class VistaLista : Border { }
        private sealed class VistaConActivacion : Border { }

        private sealed class Shell
        {
            public TabControl Pestanas { get; set; }
            public IServicioNavegacion Navegacion { get; set; }
            /// <summary>El gestor de regiones del shell.</summary>
            public GestorRegiones Gestor { get; set; }
            public Func<IEnumerable<object>> VistasActivas { get; set; }
            public Func<string, object> VistaPorNombre { get; set; }
            /// <summary>El gestor de regiones colgado de la vista (GestorRegiones.GetGestor).</summary>
            public Func<object, GestorRegiones> GestorDe { get; set; }
            /// <summary>Registra una región de ContentControl en el gestor colgado de la vista, como haría su XAML.</summary>
            public Action<object, string, ContentControl> RegistrarRegionEnVista { get; set; }
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

        // La región propia: el TabControl registrado en un GestorRegiones y las vistas creadas por su nombre
        private static Shell CrearShell(Dictionary<string, Func<object>> vistasRegistradas = null)
        {
            vistasRegistradas ??= new Dictionary<string, Func<object>>();
            var pestanas = new TabControl();
            var gestor = new GestorRegiones();
            RegionNesto principal = gestor.Registrar(MAIN_REGION, pestanas);
            return new Shell
            {
                Pestanas = pestanas,
                Navegacion = new ServicioNavegacionNesto(gestor, nombre => vistasRegistradas[nombre]()),
                Gestor = gestor,
                VistasActivas = () => principal.VistasActivas,
                VistaPorNombre = nombre => principal.Vista(nombre),
                GestorDe = vista => GestorRegiones.GetGestor((DependencyObject)vista),
                RegistrarRegionEnVista = (vista, nombre, control) => GestorRegiones.GetGestor((DependencyObject)vista).Registrar(nombre, control)
            };
        }

        // Lo que hace la X de la pestaña (CloseTabAction en el shell) con la vista de esa pestaña. Antes de este tramo,
        // estas pruebas copiaban lo que hacía la acción con Prism (RegionManager.GetObservableRegion del TabControl y
        // region.Remove); ahora la acción llama a CierrePestanas y las comprobaciones son las mismas.
        private static void CerrarPestana(Shell shell, object vista)
        {
            CierrePestanas.Cerrar(shell.Navegacion, CierrePestanas.REGION_PRINCIPAL, vista);
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
                Assert.IsTrue(shell.VistasActivas().Contains(primera));
                Assert.IsFalse(shell.VistasActivas().Contains(segunda));
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
                Assert.AreSame(primera, shell.VistaPorNombre("Clientes"));
                Assert.AreSame(segunda, shell.VistaPorNombre("Clientes2"));
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
                object gestorPrimera = shell.GestorDe(primera);
                object gestorSegunda = shell.GestorDe(segunda);
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

        // ---- Maestro-detalle con regiones de verdad (PedidoVentaView, PedidoCompraView y ListaRapportsView) ----

        // Lo que hace el XAML de una vista maestra: sus dos ContentControl con región, registrados en el gestor colgado de ella
        private static (ContentControl lista, ContentControl detalle) RegionesDeLaMaestra(Shell shell, object vista)
        {
            var lista = new ContentControl();
            var detalle = new ContentControl();
            shell.RegistrarRegionEnVista(vista, "ListaRegion", lista);
            shell.RegistrarRegionEnVista(vista, "DetalleRegion", detalle);
            return (lista, detalle);
        }

        [TestMethod]
        public void MaestroDetalle_DosPestanasConRegionesDelMismoNombre_CadaUnaNavegaASuDetalle()
        {
            EnSta(() =>
            {
                var shell = CrearShell(new Dictionary<string, Func<object>>
                {
                    { nameof(VistaDetalle), () => new VistaDetalle { DataContext = new ReceptorPestanaNueva() } }
                });
                var primera = new VistaMaestra { DataContext = new ConAmbito() };
                var segunda = new VistaMaestra { DataContext = new ConAmbito() };
                IServicioNavegacion primerAmbito = shell.Navegacion.AbrirVistaConAmbito(MAIN_REGION, primera);
                IServicioNavegacion segundoAmbito = shell.Navegacion.AbrirVistaConAmbito(MAIN_REGION, segunda);
                var (_, primerDetalle) = RegionesDeLaMaestra(shell, primera);
                var (_, segundoDetalle) = RegionesDeLaMaestra(shell, segunda);

                primerAmbito.RequestNavigate("DetalleRegion", nameof(VistaDetalle), new ParametrosNavegacion { { "pedido", 1 } });
                segundoAmbito.RequestNavigate("DetalleRegion", nameof(VistaDetalle), new ParametrosNavegacion { { "pedido", 2 } });

                var detalleUno = (VistaDetalle)primerDetalle.Content;
                var detalleDos = (VistaDetalle)segundoDetalle.Content;
                Assert.AreNotSame(detalleUno, detalleDos);
                Assert.AreEqual(1, ((ReceptorPestanaNueva)detalleUno.DataContext).Llegadas.Single().GetValue<int>("pedido"));
                Assert.AreEqual(2, ((ReceptorPestanaNueva)detalleDos.DataContext).Llegadas.Single().GetValue<int>("pedido"));
                Assert.AreSame(detalleUno, primerAmbito.VistaActiva("DetalleRegion"));
                Assert.AreSame(detalleDos, segundoAmbito.VistaActiva("DetalleRegion"));
                // Las regiones del ámbito no son del shell
                Assert.IsNull(shell.Navegacion.VistaActiva("DetalleRegion"));
                CollectionAssert.AreEqual(new object[] { primera, segunda }, Pestanas(shell));
            });
        }

        [TestMethod]
        public void MaestroDetalle_LaListaEntraEnSuRegionYRecibeLaNavegacionDelAmbito()
        {
            // Lo que hacen PedidoVentaView y PedidoCompraView al cargarse: AbrirVistaNueva de la lista en su región de lista
            EnSta(() =>
            {
                var shell = CrearShell(new Dictionary<string, Func<object>>
                {
                    { nameof(VistaDetalle), () => new VistaDetalle { DataContext = new ReceptorPestanaNueva() } }
                });
                var maestra = new VistaMaestra { DataContext = new ConAmbito() };
                IServicioNavegacion ambito = shell.Navegacion.AbrirVistaConAmbito(MAIN_REGION, maestra);
                var (regionLista, regionDetalle) = RegionesDeLaMaestra(shell, maestra);
                var contextoLista = new ConAmbito();
                var lista = new VistaLista { DataContext = contextoLista };

                ambito.AbrirVistaNueva("ListaRegion", lista, "ListaRegion");

                Assert.AreSame(lista, regionLista.Content);
                Assert.AreSame(lista, ambito.VistaActiva("ListaRegion"));
                Assert.AreSame(ambito, contextoLista.NavegacionAmbito);
                // La lista cuelga del gestor de la pestaña, no del shell
                Assert.AreSame(shell.GestorDe(maestra), shell.GestorDe(lista));
                // Y lo que navega la lista va al detalle de esa pestaña
                contextoLista.NavegacionAmbito.RequestNavigate("DetalleRegion", nameof(VistaDetalle));
                Assert.IsInstanceOfType(regionDetalle.Content, typeof(VistaDetalle));
            });
        }

        [TestMethod]
        public void MaestroDetalle_ElegirOtroElemento_QuitaElDetalleAnteriorYLimpiaSuDataContext()
        {
            // Lo que hacen las listas de pedidos y la de rapports: QuitarVistas del detalle y navegar a uno nuevo
            EnSta(() =>
            {
                var shell = CrearShell(new Dictionary<string, Func<object>>
                {
                    { nameof(VistaDetalle), () => new VistaDetalle { DataContext = new ReceptorPestanaNueva() } }
                });
                var maestra = new VistaMaestra { DataContext = new ConAmbito() };
                IServicioNavegacion ambito = shell.Navegacion.AbrirVistaConAmbito(MAIN_REGION, maestra);
                var (_, regionDetalle) = RegionesDeLaMaestra(shell, maestra);
                ambito.RequestNavigate("DetalleRegion", nameof(VistaDetalle), new ParametrosNavegacion { { "pedido", 1 } });
                var anterior = (VistaDetalle)regionDetalle.Content;
                var receptorAnterior = (ReceptorPestanaNueva)anterior.DataContext;

                ambito.QuitarVistas("DetalleRegion");
                ambito.RequestNavigate("DetalleRegion", nameof(VistaDetalle), new ParametrosNavegacion { { "pedido", 2 } });

                var nuevo = (VistaDetalle)regionDetalle.Content;
                Assert.AreNotSame(anterior, nuevo);
                Assert.IsNull(anterior.DataContext);
                Assert.AreEqual(1, receptorAnterior.Llegadas.Count);
                Assert.AreEqual(2, ((ReceptorPestanaNueva)nuevo.DataContext).Llegadas.Single().GetValue<int>("pedido"));
                Assert.AreSame(nuevo, ambito.VistaActiva("DetalleRegion"));
            });
        }

        [TestMethod]
        public void RegionDentroDeUnaVistaSinAmbitoPropio_EsDelShell()
        {
            // ListaRapportsView entra en MainRegion por navegación (sin ámbito): su RapportDetailRegion es del gestor del
            // shell, y ListaRapportsViewModel navega a ella con la navegación de siempre
            EnSta(() =>
            {
                var shell = CrearShell(new Dictionary<string, Func<object>>
                {
                    { nameof(VistaReutiliza), () => new VistaReutiliza { DataContext = new ReceptorQueReutiliza() } },
                    { nameof(VistaDetalle), () => new VistaDetalle { DataContext = new ReceptorPestanaNueva() } }
                });
                shell.Navegacion.RequestNavigate(MAIN_REGION, nameof(VistaReutiliza));
                object lista = Pestanas(shell).Single();
                Assert.AreSame(shell.Gestor, shell.GestorDe(lista));
                var regionDetalle = new ContentControl();
                shell.RegistrarRegionEnVista(lista, "RapportDetailRegion", regionDetalle);

                shell.Navegacion.QuitarVistas("RapportDetailRegion");
                shell.Navegacion.RequestNavigate("RapportDetailRegion", nameof(VistaDetalle), new ParametrosNavegacion { { "rapport", 7 } });

                var detalle = (VistaDetalle)regionDetalle.Content;
                Assert.AreEqual(7, ((ReceptorPestanaNueva)detalle.DataContext).Llegadas.Single().GetValue<int>("rapport"));
                Assert.AreSame(detalle, shell.Navegacion.VistaActiva("RapportDetailRegion"));
                // Sigue siendo la pestaña seleccionada del shell
                Assert.AreSame(lista, shell.Navegacion.VistaActiva(MAIN_REGION));
            });
        }

        [TestMethod]
        public void CambiarDePestana_AvisaAlViewModelDeSiSuPestanaEsLaActiva()
        {
            // ListaRapportsViewModel se da de alta en los mensajes de rapports solo mientras su pestaña está activa
            EnSta(() =>
            {
                var activable = new ConActivacion();
                var shell = CrearShell(new Dictionary<string, Func<object>>
                {
                    { nameof(VistaConActivacion), () => new VistaConActivacion { DataContext = activable } }
                });

                shell.Navegacion.RequestNavigate(MAIN_REGION, nameof(VistaConActivacion));
                object vista = Pestanas(shell).Single();
                Assert.IsTrue(activable.IsActive);

                shell.Navegacion.AbrirVistaNueva(MAIN_REGION, new Border(), "Otra");
                Assert.IsFalse(activable.IsActive);

                shell.Pestanas.SelectedItem = vista;
                Assert.IsTrue(activable.IsActive);

                shell.Navegacion.RequestNavigate(MAIN_REGION, nameof(VistaConActivacion));
                CollectionAssert.AreEqual(new[] { true, false, true }, activable.Cambios, "Volver a navegar a la que ya está activa no la reactiva");
            });
        }

        [TestMethod]
        public void LaXDeLaPestanaActiva_LaQuitaYLimpiaSuDataContext()
        {
            EnSta(() =>
            {
                var activable = new ConActivacion();
                var shell = CrearShell(new Dictionary<string, Func<object>>
                {
                    { nameof(VistaConActivacion), () => new VistaConActivacion { DataContext = activable } }
                });
                shell.Navegacion.RequestNavigate(MAIN_REGION, nameof(VistaConActivacion));
                var vista = (VistaConActivacion)Pestanas(shell).Single();

                CerrarPestana(shell, vista);

                Assert.AreEqual(0, Pestanas(shell).Count);
                Assert.IsNull(vista.DataContext);
                // La única diferencia buscada con Prism (tramo 2 de la región propia). Con Prism, al quitar la pestaña activa
                // su DataContext se limpiaba antes de que el RegionActiveAwareBehavior desactivara la vista, así que al
                // ViewModel no le llegaba IsActive = false (ListaRapportsViewModel se quedaba dado de alta en los mensajes de
                // rapports hasta que lo recogía el GC). La región propia la desactiva antes de limpiarla, como hacía Prism al
                // cerrar con CerrarVistaActiva.
                CollectionAssert.AreEqual(new[] { true, false }, activable.Cambios, string.Join(",", activable.Cambios));
            });
        }

        [TestMethod]
        public void NavegarAUnaVistaQueNoEstaRegistrada_NoAbreNadaNiLanza()
        {
            // Como en Prism: la navegación que falla no sube a quien navega
            EnSta(() =>
            {
                var shell = CrearShell();

                shell.Navegacion.RequestNavigate(MAIN_REGION, "VistaQueNoExiste");

                Assert.AreEqual(0, Pestanas(shell).Count);
                Assert.IsNull(shell.Navegacion.VistaActiva(MAIN_REGION));
            });
        }

        [TestMethod]
        public void AbrirOCerrarEnUnaRegionQueNoExiste_LanzaKeyNotFound()
        {
            EnSta(() =>
            {
                var shell = CrearShell();

                Assert.ThrowsException<KeyNotFoundException>(() => shell.Navegacion.AbrirVistaNueva("RegionQueNoExiste", new Border(), "Uno"));
                Assert.ThrowsException<KeyNotFoundException>(() => shell.Navegacion.AbrirVistaConAmbito("RegionQueNoExiste", new Border()));
                Assert.ThrowsException<KeyNotFoundException>(() => shell.Navegacion.CerrarVistaActiva("RegionQueNoExiste"));
                // Estas no lanzan
                shell.Navegacion.CerrarVista("RegionQueNoExiste", new Border());
                shell.Navegacion.QuitarVistas("RegionQueNoExiste");
            });
        }

        [TestMethod]
        public void NavegarConParametrosEnElDestino_LleganDelanteDeLosQueSePasan()
        {
            // Nadie navega hoy con «Vista?clave=valor», pero Prism lo admite y la región propia también
            EnSta(() =>
            {
                var receptor = new ReceptorQueReutiliza();
                var shell = CrearShell(new Dictionary<string, Func<object>>
                {
                    { nameof(VistaReutiliza), () => new VistaReutiliza { DataContext = receptor } }
                });

                shell.Navegacion.RequestNavigate(MAIN_REGION, nameof(VistaReutiliza) + "?cliente=15%20191&ruta=AT",
                    new ParametrosNavegacion { { "empresa", "1" } });

                Assert.AreEqual(1, Pestanas(shell).Count);
                ParametrosNavegacion recibidos = receptor.Llegadas.Single();
                CollectionAssert.AreEqual(new[] { "cliente", "ruta", "empresa" }, recibidos.Keys.ToArray(), string.Join(",", recibidos.Keys));
                Assert.AreEqual("15 191", recibidos.GetValue<string>("cliente"));
            });
        }
    }
}
