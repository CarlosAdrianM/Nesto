using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using Prism.Ioc;
using Prism.Regions;
using Prism.Regions.Behaviors;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4, tramo 3): el piloto de la región propia. El conmutador (parámetro <c>RegionesPropias</c>) y la
    /// propiedad adjunta <see cref="Regiones.NombreProperty"/>, que con el piloto apagado deja todo a Prism y con el piloto
    /// encendido registra las regiones del XAML de forma diferida, como Prism: al cargarse el control o cuando alguien busca
    /// una región que todavía no está.
    /// </summary>
    [TestClass]
    public class RegionesPilotoTests
    {
        // Las vistas se buscan por el nombre de su tipo, como en Nesto
        private sealed class VistaMaestra : Grid { }
        private sealed class VistaLista : Border { }
        private sealed class VistaRapports : Grid { }
        private sealed class VistaDetalle : Border { }

        [TestCleanup]
        public void Limpiar()
        {
            Regiones.Propias = false;
            Regiones.AlFallarRegistro = null;
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

        private static IServicioNavegacion NavegacionPrism() => A.Fake<IServicioNavegacion>();

        #region Conmutador

        [TestMethod]
        public void Conmutador_SinParametro_Prism()
        {
            IServicioNavegacion prism = NavegacionPrism();
            var conmutador = new ConmutadorRegiones(() => string.Empty);

            Assert.AreSame(prism, conmutador.CrearNavegacion(() => prism, _ => null));
            Assert.IsFalse(conmutador.RegionesPropias);
        }

        [TestMethod]
        public void Conmutador_ConOtroValor_Prism()
        {
            IServicioNavegacion prism = NavegacionPrism();

            Assert.AreSame(prism, new ConmutadorRegiones(() => "0").CrearNavegacion(() => prism, _ => null));
            Assert.AreSame(prism, new ConmutadorRegiones(() => null).CrearNavegacion(() => prism, _ => null));
        }

        [TestMethod]
        public void Conmutador_ConUno_RegionPropiaSobreElGestorDelShell()
        {
            int lecturas = 0;
            var conmutador = new ConmutadorRegiones(() => { lecturas++; return "1"; });
            bool prismCreado = false;

            IServicioNavegacion navegacion = conmutador.CrearNavegacion(() => { prismCreado = true; return NavegacionPrism(); }, _ => null);

            Assert.IsInstanceOfType(navegacion, typeof(ServicioNavegacionNesto));
            Assert.AreSame(conmutador.GestorShell, ((ServicioNavegacionNesto)navegacion).Gestor);
            Assert.IsFalse(prismCreado);
            Assert.IsTrue(conmutador.RegionesPropias);
            Assert.AreEqual(1, lecturas, "El parámetro se lee una sola vez");
        }

        [TestMethod]
        public void Conmutador_SiNoSePuedeLeerElParametro_Prism()
        {
            IServicioNavegacion prism = NavegacionPrism();
            var conmutador = new ConmutadorRegiones(() => throw new Exception("Sin conexión"));

            Assert.AreSame(prism, conmutador.CrearNavegacion(() => prism, _ => null));
        }

        [TestMethod]
        public void Conmutador_EnciendeOApagaLaPropiedadAdjuntaYCuelgaElGestorDelShellSoloConElPiloto()
        {
            EnSta(() =>
            {
                var encendido = new ConmutadorRegiones(() => "1");
                var ventanaPiloto = new Grid();
                encendido.AntesDeCrearShell();
                encendido.AlCrearShell(ventanaPiloto);
                Assert.IsTrue(Regiones.Propias);
                Assert.AreSame(encendido.GestorShell, GestorRegiones.GetGestor(ventanaPiloto));

                var apagado = new ConmutadorRegiones(() => throw new Exception("Sin conexión"));
                var ventana = new Grid();
                apagado.AntesDeCrearShell();
                apagado.AlCrearShell(ventana);
                Assert.IsFalse(Regiones.Propias);
                Assert.IsNull(GestorRegiones.GetGestor(ventana));
            });
        }

        [TestMethod]
        public void Conmutador_UnaNavegacionFallidaConLaRegionPropia_VaAlRegistroDeErrores()
        {
            EnSta(() =>
            {
                var errores = new List<(Exception error, string contexto)>();
                var conmutador = new ConmutadorRegiones(() => "1", (ex, contexto) => errores.Add((ex, contexto)));
                conmutador.GestorShell.Registrar("MainRegion", new TabControl());
                IServicioNavegacion navegacion = conmutador.CrearNavegacion(NavegacionPrism,
                    nombre => throw new InvalidOperationException($"{nombre} no está registrada"));

                navegacion.RequestNavigate("MainRegion", "VistaQueNoExiste");

                Assert.AreEqual(1, errores.Count);
                Assert.IsInstanceOfType(errores[0].error, typeof(InvalidOperationException));
                StringAssert.Contains(errores[0].contexto, "VistaQueNoExiste");
                StringAssert.Contains(errores[0].contexto, "MainRegion");
            });
        }

        [TestMethod]
        public void Conmutador_SiElRegistroDeErroresFalla_LaNavegacionNoLanza()
        {
            EnSta(() =>
            {
                var conmutador = new ConmutadorRegiones(() => "1", (ex, contexto) => throw new Exception("Sin servidor"));
                conmutador.GestorShell.Registrar("MainRegion", new TabControl());
                IServicioNavegacion navegacion = conmutador.CrearNavegacion(NavegacionPrism, nombre => throw new InvalidOperationException());

                navegacion.RequestNavigate("MainRegion", "VistaQueNoExiste");
            });
        }

        #endregion

        #region Propiedad adjunta

        [TestMethod]
        public void PilotoApagado_LaAdjuntaPasaElNombreAPrismYNoRegistraNada()
        {
            int creacionesPrism = 0;
            var contenedor = A.Fake<IContainerExtension>();
            A.CallTo(() => contenedor.Resolve(typeof(DelayedRegionCreationBehavior)))
                .ReturnsLazily(() =>
                {
                    creacionesPrism++;
                    // Como en Nesto: el ContentControl con el adaptador de Prism
                    var mapeos = new RegionAdapterMappings();
                    mapeos.RegisterMapping(typeof(ContentControl), new ContentControlRegionAdapter(new RegionBehaviorFactory(contenedor)));
                    return new DelayedRegionCreationBehavior(mapeos);
                });
            ContainerLocator.SetContainerExtension(() => contenedor);
            try
            {
                EnSta(() =>
                {
                    var gestor = new GestorRegiones();
                    var raiz = new Grid();
                    GestorRegiones.SetGestor(raiz, gestor);
                    var control = new ContentControl();
                    _ = raiz.Children.Add(control);

                    Regiones.SetNombre(control, "DetalleRegion");
                    control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

                    Assert.AreEqual("DetalleRegion", RegionManager.GetRegionName(control), "Lo mismo que prism:RegionManager.RegionName");
                    Assert.AreEqual(1, creacionesPrism, "Prism crea su región diferida, como siempre");
                    Assert.AreEqual("DetalleRegion", RegionManager.GetObservableRegion(control).Value?.Name, "Y al cargarse el control, la región es de Prism");
                    Assert.IsNull(gestor.Region("DetalleRegion"), "La región propia no se entera");
                    Assert.AreEqual(0, Regiones.Pendientes(c => ReferenceEquals(c, control)));
                    Assert.IsNull(control.Content);
                });
            }
            finally
            {
                ContainerLocator.ResetContainer();
            }
        }

        [TestMethod]
        public void PilotoEncendido_PrismNoSeEntera()
        {
            EnSta(() =>
            {
                Regiones.Propias = true;
                var control = new ContentControl();

                Regiones.SetNombre(control, "DetalleRegion");

                Assert.IsNull(RegionManager.GetRegionName(control));
                Assert.AreEqual(1, Regiones.Pendientes(c => ReferenceEquals(c, control)));
            });
        }

        [TestMethod]
        public void PilotoEncendido_SinGestorEnElArbol_SeQuedaPendienteHastaQueLoEncuentraAlCargarse()
        {
            EnSta(() =>
            {
                Regiones.Propias = true;
                var gestor = new GestorRegiones();
                var raiz = new Grid();
                var control = new ContentControl();
                Regiones.SetNombre(control, "DetalleRegion");
                _ = raiz.Children.Add(control);

                control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Assert.AreEqual(1, Regiones.Pendientes(c => ReferenceEquals(c, control)), "Sin gestor arriba, sigue pendiente");

                GestorRegiones.SetGestor(raiz, gestor);
                // El Loaded solo se escucha una vez (como Prism); a partir de ahí, cualquier búsqueda lo registra
                Assert.IsNotNull(gestor.Region("DetalleRegion"));
                Assert.AreEqual(0, Regiones.Pendientes(c => ReferenceEquals(c, control)));
            });
        }

        [TestMethod]
        public void PilotoEncendido_AlCargarseElControl_SeRegistraEnElGestorDeArriba()
        {
            EnSta(() =>
            {
                Regiones.Propias = true;
                var gestor = new GestorRegiones();
                var raiz = new Grid();
                GestorRegiones.SetGestor(raiz, gestor);
                var control = new ContentControl();
                Regiones.SetNombre(control, "DetalleRegion");
                _ = raiz.Children.Add(control);

                control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

                Assert.AreEqual(0, Regiones.Pendientes(c => ReferenceEquals(c, control)));
                Assert.IsInstanceOfType(gestor.Region("DetalleRegion"), typeof(RegionContenido));
            });
        }

        // PedidoVentaView/PedidoCompraView: la vista entra en MainRegion con su propio ámbito y, en su Loaded, mete la lista en
        // su región de dentro antes de que el ContentControl de esa región haya recibido el suyo. Dos pestañas iguales, cada
        // una con sus regiones del mismo nombre.
        [TestMethod]
        public void PilotoEncendido_MaestroDetalle_LaRegionDeDentroSeEncuentraAntesDeSuLoaded()
        {
            EnSta(() =>
            {
                Regiones.Propias = true;
                var gestorShell = new GestorRegiones();
                var ventana = new Grid();
                GestorRegiones.SetGestor(ventana, gestorShell);
                var pestanas = new TabControl();
                Regiones.SetNombre(pestanas, "MainRegion");
                _ = ventana.Children.Add(pestanas);
                var navegacion = new ServicioNavegacionNesto(gestorShell, _ => null);

                (VistaMaestra vista, ContentControl lista, ContentControl detalle) CrearMaestra()
                {
                    var maestra = new VistaMaestra();
                    var lista = new ContentControl();
                    var detalle = new ContentControl();
                    Regiones.SetNombre(lista, "ListaPedidosRegion");
                    Regiones.SetNombre(detalle, "DetallePedidoRegion");
                    _ = maestra.Children.Add(lista);
                    _ = maestra.Children.Add(detalle);
                    return (maestra, lista, detalle);
                }

                var primera = CrearMaestra();
                var segunda = CrearMaestra();
                // Sin un solo Loaded: la región principal se registra al buscarla, y las de dentro, al buscarlas en su ámbito
                IServicioNavegacion ambito1 = navegacion.AbrirVistaConAmbito("MainRegion", primera.vista);
                IServicioNavegacion ambito2 = navegacion.AbrirVistaConAmbito("MainRegion", segunda.vista);
                var lista1 = new VistaLista();
                var lista2 = new VistaLista();
                ambito1.AbrirVistaNueva("ListaPedidosRegion", lista1, "ListaPedidosVenta");
                ambito2.AbrirVistaNueva("ListaPedidosRegion", lista2, "ListaPedidosVenta");

                Assert.AreEqual(2, pestanas.Items.Count);
                Assert.AreSame(lista1, primera.lista.Content);
                Assert.AreSame(lista2, segunda.lista.Content);
                Assert.IsFalse(gestorShell.Contiene("ListaPedidosRegion"), "Las regiones de un ámbito no se ven desde el shell");
                Assert.IsTrue(((ServicioNavegacionNesto)ambito1).Gestor.Contiene("DetallePedidoRegion"));
                Assert.AreEqual(0, Regiones.Pendientes(c => c == primera.detalle || c == segunda.detalle));
            });
        }

        // ListaRapportsView: entra en MainRegion sin ámbito propio, y su RapportDetailRegion es del gestor del shell. Al cerrarla
        // y volver a abrir Rapports, la región es la de la pestaña nueva (como Prism, que la saca del gestor al no encontrarlo).
        [TestMethod]
        public void PilotoEncendido_RegionDeUnaVistaSinAmbito_EsDelShellYAlCerrarlaDejaSitioALaNueva()
        {
            EnSta(() =>
            {
                Regiones.Propias = true;
                var gestorShell = new GestorRegiones();
                var ventana = new Grid();
                GestorRegiones.SetGestor(ventana, gestorShell);
                var pestanas = new TabControl();
                Regiones.SetNombre(pestanas, "MainRegion");
                _ = ventana.Children.Add(pestanas);
                var detalles = new Dictionary<VistaRapports, ContentControl>();
                var vistas = new Dictionary<string, Func<object>>
                {
                    ["VistaRapports"] = () =>
                    {
                        var vista = new VistaRapports();
                        var detalle = new ContentControl();
                        Regiones.SetNombre(detalle, "RapportDetailRegion");
                        _ = vista.Children.Add(detalle);
                        detalles[vista] = detalle;
                        return vista;
                    },
                    ["VistaDetalle"] = () => new VistaDetalle()
                };
                var navegacion = new ServicioNavegacionNesto(gestorShell, nombre => vistas[nombre]());

                navegacion.RequestNavigate("MainRegion", "VistaRapports");
                var primera = (VistaRapports)navegacion.VistaActiva("MainRegion");
                navegacion.RequestNavigate("RapportDetailRegion", "VistaDetalle");
                Assert.IsInstanceOfType(detalles[primera].Content, typeof(VistaDetalle));

                navegacion.CerrarVista("MainRegion", primera);
                navegacion.RequestNavigate("MainRegion", "VistaRapports");
                var segunda = (VistaRapports)navegacion.VistaActiva("MainRegion");
                navegacion.RequestNavigate("RapportDetailRegion", "VistaDetalle");

                Assert.AreNotSame(primera, segunda);
                Assert.IsInstanceOfType(detalles[segunda].Content, typeof(VistaDetalle), "El detalle va a la pestaña nueva");
            });
        }

        [TestMethod]
        public void PilotoEncendido_UnRegistroQueFalla_AvisaYNoRompeLaBusqueda()
        {
            EnSta(() =>
            {
                Regiones.Propias = true;
                var avisos = new List<string>();
                Regiones.AlFallarRegistro = (nombre, error) => avisos.Add(nombre);
                var gestor = new GestorRegiones();
                var raiz = new Grid();
                GestorRegiones.SetGestor(raiz, gestor);
                // Dos controles con el mismo nombre en el mismo ámbito: Prism también lanzaría
                var uno = new ContentControl();
                var otro = new ContentControl();
                Regiones.SetNombre(uno, "DetalleRegion");
                Regiones.SetNombre(otro, "DetalleRegion");
                _ = raiz.Children.Add(uno);
                _ = raiz.Children.Add(otro);

                RegionNesto region = gestor.Region("DetalleRegion");

                Assert.IsNotNull(region);
                CollectionAssert.AreEqual(new[] { "DetalleRegion" }, avisos);
                Assert.AreEqual(0, Regiones.Pendientes(c => c == uno || c == otro), "No se reintenta");
            });
        }

        #endregion
    }
}
