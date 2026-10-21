using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4): la propiedad adjunta <see cref="Regiones.NombreProperty"/>, que registra las regiones del XAML de
    /// forma diferida, como Prism (al cargarse el control o cuando alguien busca una región que todavía no está), y
    /// <see cref="RegionesDelShell"/>, que monta las del shell y la navegación. Eran las pruebas del piloto (tramo 3,
    /// <c>RegionesPilotoTests</c>); en el tramo 4 se quitan el piloto y Prism, y quedan las del piloto encendido.
    /// </summary>
    [TestClass]
    public class RegionesTests
    {
        // Las vistas se buscan por el nombre de su tipo, como en Nesto
        private sealed class VistaMaestra : Grid { }
        private sealed class VistaLista : Border { }
        private sealed class VistaRapports : Grid { }
        private sealed class VistaDetalle : Border { }

        [TestCleanup]
        public void Limpiar()
        {
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

        #region Regiones del shell

        [TestMethod]
        public void RegionesDelShell_LaNavegacionEsLaPropiaSobreElGestorDelShell()
        {
            var regiones = new RegionesDelShell();

            IServicioNavegacion navegacion = regiones.CrearNavegacion(_ => null);

            Assert.IsInstanceOfType(navegacion, typeof(ServicioNavegacionNesto));
            Assert.AreSame(regiones.GestorShell, ((ServicioNavegacionNesto)navegacion).Gestor);
        }

        [TestMethod]
        public void RegionesDelShell_AlCrearElShell_CuelgaElGestorDeLaVentanaYMainRegionSeRegistraEnEl()
        {
            EnSta(() =>
            {
                var regiones = new RegionesDelShell();
                var ventana = new Grid();
                var pestanas = new TabControl();
                Regiones.SetNombre(pestanas, "MainRegion");
                _ = ventana.Children.Add(pestanas);

                regiones.AlCrearShell(ventana);

                Assert.AreSame(regiones.GestorShell, GestorRegiones.GetGestor(ventana));
                Assert.IsInstanceOfType(regiones.GestorShell.Region("MainRegion"), typeof(RegionPestanas));
            });
        }

        [TestMethod]
        public void RegionesDelShell_UnaNavegacionFallida_VaAlRegistroDeErrores()
        {
            EnSta(() =>
            {
                var errores = new List<(Exception error, string contexto)>();
                var regiones = new RegionesDelShell((ex, contexto) => errores.Add((ex, contexto)));
                regiones.GestorShell.Registrar("MainRegion", new TabControl());
                IServicioNavegacion navegacion = regiones.CrearNavegacion(
                    nombre => throw new InvalidOperationException($"{nombre} no está registrada"));

                navegacion.RequestNavigate("MainRegion", "VistaQueNoExiste");

                Assert.AreEqual(1, errores.Count);
                Assert.IsInstanceOfType(errores[0].error, typeof(InvalidOperationException));
                StringAssert.Contains(errores[0].contexto, "VistaQueNoExiste");
                StringAssert.Contains(errores[0].contexto, "MainRegion");
            });
        }

        [TestMethod]
        public void RegionesDelShell_SiElRegistroDeErroresFalla_LaNavegacionNoLanza()
        {
            EnSta(() =>
            {
                var regiones = new RegionesDelShell((ex, contexto) => throw new Exception("Sin servidor"));
                regiones.GestorShell.Registrar("MainRegion", new TabControl());
                IServicioNavegacion navegacion = regiones.CrearNavegacion(nombre => throw new InvalidOperationException());

                navegacion.RequestNavigate("MainRegion", "VistaQueNoExiste");
            });
        }

        [TestMethod]
        public void RegionesDelShell_UnRegistroDeRegionQueFalla_VaAlRegistroDeErrores()
        {
            EnSta(() =>
            {
                var errores = new List<string>();
                var regiones = new RegionesDelShell((ex, contexto) => errores.Add(contexto));
                var ventana = new Grid();
                regiones.AlCrearShell(ventana);
                // Dos regiones con el mismo nombre en el mismo gestor: la segunda no se puede registrar
                var uno = new ContentControl();
                var otro = new ContentControl();
                Regiones.SetNombre(uno, "RegionRepetida");
                Regiones.SetNombre(otro, "RegionRepetida");
                _ = ventana.Children.Add(uno);
                _ = ventana.Children.Add(otro);

                _ = regiones.GestorShell.Region("RegionRepetida");

                CollectionAssert.AreEqual(new[] { "Regiones: registro de la región RegionRepetida" }, errores);
            });
        }

        #endregion

        #region Propiedad adjunta

        [TestMethod]
        public void LaAdjunta_DejaElControlPendienteHastaEncontrarSuGestor()
        {
            EnSta(() =>
            {
                var control = new ContentControl();

                Regiones.SetNombre(control, "DetalleRegion");

                Assert.AreEqual(1, Regiones.Pendientes(c => ReferenceEquals(c, control)));
            });
        }

        [TestMethod]
        public void LaAdjunta_SinGestorEnElArbol_SeQuedaPendienteHastaQueLoEncuentraAlCargarse()
        {
            EnSta(() =>
            {
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
        public void LaAdjunta_AlCargarseElControl_SeRegistraEnElGestorDeArriba()
        {
            EnSta(() =>
            {
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
        public void LaAdjunta_MaestroDetalle_LaRegionDeDentroSeEncuentraAntesDeSuLoaded()
        {
            EnSta(() =>
            {
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
        public void LaAdjunta_RegionDeUnaVistaSinAmbito_EsDelShellYAlCerrarlaDejaSitioALaNueva()
        {
            EnSta(() =>
            {
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
        public void LaAdjunta_UnRegistroQueFalla_AvisaYNoRompeLaBusqueda()
        {
            EnSta(() =>
            {
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
