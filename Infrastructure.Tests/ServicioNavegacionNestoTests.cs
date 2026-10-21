using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Controls;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4, tramo 4): lo que fijaba <c>ServicioNavegacionPrismTests</c> sobre el IRegionManager de Prism y no
    /// cubren ya <see cref="ShellRegionPrincipalTests"/> ni <see cref="RegionNestoTests"/>, ahora contra la única
    /// implementación, <see cref="ServicioNavegacionNesto"/>, con regiones de verdad.
    /// </summary>
    [TestClass]
    public class ServicioNavegacionNestoTests
    {
        private sealed class Receptor : IReceptorNavegacion
        {
            public List<ParametrosNavegacion> Llegadas { get; } = new List<ParametrosNavegacion>();
            public void AlLlegar(ParametrosNavegacion parametros) => Llegadas.Add(parametros);
        }

        // Como PedidoVentaView y ListaPedidosVentaViewModel: reciben la navegación de su ámbito. Se apunta cuál tenían al
        // activarse, para comprobar que les llega antes.
        private class ConAmbito : IConAmbitoNavegacion, IConActivacion
        {
            private bool _activa;
            public IServicioNavegacion NavegacionAmbito { get; set; }
            public IServicioNavegacion AlActivarse { get; private set; }
            public bool IsActive
            {
                get => _activa;
                set
                {
                    if (value && !_activa)
                    {
                        AlActivarse = NavegacionAmbito;
                    }
                    _activa = value;
                }
            }
        }

        private sealed class VistaConAmbito : Border, IConAmbitoNavegacion, IConActivacion
        {
            private readonly ConAmbito _apunte = new ConAmbito();
            public IServicioNavegacion NavegacionAmbito { get => _apunte.NavegacionAmbito; set => _apunte.NavegacionAmbito = value; }
            public IServicioNavegacion AlActivarse => _apunte.AlActivarse;
            public bool IsActive { get => _apunte.IsActive; set => _apunte.IsActive = value; }
        }

        private sealed class VistaPrueba : Border { }

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

        private static (ServicioNavegacionNesto navegacion, GestorRegiones gestor, TabControl pestanas) Crear(Func<string, object> crearVista = null)
        {
            var gestor = new GestorRegiones();
            var pestanas = new TabControl();
            _ = gestor.Registrar("MainRegion", pestanas);
            return (new ServicioNavegacionNesto(gestor, crearVista ?? (_ => new VistaPrueba())), gestor, pestanas);
        }

        [TestMethod]
        public void Constructor_SinGestorOSinCrearVista_Lanza()
        {
            _ = Assert.ThrowsException<ArgumentNullException>(() => new ServicioNavegacionNesto(null, _ => null));
            _ = Assert.ThrowsException<ArgumentNullException>(() => new ServicioNavegacionNesto(new GestorRegiones(), null));
        }

        [TestMethod]
        public void RequestNavigate_SinParametros_LleganUnosParametrosVacios()
        {
            EnSta(() =>
            {
                var receptor = new Receptor();
                var (navegacion, _, _) = Crear(_ => new VistaPrueba { DataContext = receptor });

                navegacion.RequestNavigate("MainRegion", nameof(VistaPrueba));
                navegacion.RequestNavigate("MainRegion", nameof(VistaPrueba), null);

                Assert.AreEqual(2, receptor.Llegadas.Count);
                Assert.IsTrue(receptor.Llegadas.All(p => p != null && p.Count == 0));
            });
        }

        [TestMethod]
        public void RequestNavigate_ConParametros_LleganLasMismasClavesYValores()
        {
            EnSta(() =>
            {
                var receptor = new Receptor();
                var (navegacion, _, _) = Crear(_ => new VistaPrueba { DataContext = receptor });
                var cliente = new object();

                navegacion.RequestNavigate("MainRegion", nameof(VistaPrueba),
                    new ParametrosNavegacion { { "clienteParameter", cliente }, { "empresa", "1" } });

                ParametrosNavegacion recibidos = receptor.Llegadas.Single();
                Assert.AreEqual(2, recibidos.Count);
                Assert.AreSame(cliente, recibidos.GetValue<object>("clienteParameter"));
                Assert.AreEqual("1", recibidos.GetValue<object>("empresa"));
            });
        }

        [TestMethod]
        public void VistaActiva_SinVistasOEnUnaRegionQueNoExiste_DevuelveNull()
        {
            EnSta(() =>
            {
                var (navegacion, _, _) = Crear();

                Assert.IsNull(navegacion.VistaActiva("MainRegion"));
                Assert.IsNull(navegacion.VistaActiva("RegionQueNoExiste"));
            });
        }

        [TestMethod]
        public void CerrarVistaActiva_SinVistaActiva_NoQuitaNada()
        {
            EnSta(() =>
            {
                var (navegacion, gestor, _) = Crear();

                navegacion.CerrarVistaActiva("MainRegion");

                Assert.AreEqual(0, gestor.Region("MainRegion").Vistas.Count);
            });
        }

        [TestMethod]
        public void CerrarVista_SiNoEstaEnLaRegion_NoQuitaNada()
        {
            EnSta(() =>
            {
                var (navegacion, gestor, _) = Crear();
                navegacion.RequestNavigate("MainRegion", nameof(VistaPrueba));

                navegacion.CerrarVista("MainRegion", new VistaPrueba());
                navegacion.CerrarVista("MainRegion", null);

                Assert.AreEqual(1, gestor.Region("MainRegion").Vistas.Count);
            });
        }

        [TestMethod]
        public void QuitarVistas_QuitaTodasLasVistasDeLaRegionActivasONo()
        {
            EnSta(() =>
            {
                var (navegacion, gestor, pestanas) = Crear();
                navegacion.AbrirVistaNueva("MainRegion", new VistaPrueba(), "Uno");
                navegacion.AbrirVistaNueva("MainRegion", new VistaPrueba(), "Dos");

                navegacion.QuitarVistas("MainRegion");

                Assert.AreEqual(0, gestor.Region("MainRegion").Vistas.Count);
                Assert.AreEqual(0, pestanas.Items.Count);
            });
        }

        [TestMethod]
        public void AbrirVistaConAmbito_EntregaElAmbitoALaVistaYASuDataContextAntesDeActivarla()
        {
            EnSta(() =>
            {
                var (navegacion, gestor, pestanas) = Crear();
                var contexto = new ConAmbito();
                var vista = new VistaConAmbito { DataContext = contexto };

                IServicioNavegacion ambito = navegacion.AbrirVistaConAmbito("MainRegion", vista);

                Assert.IsNotNull(ambito);
                Assert.AreNotSame(navegacion, ambito);
                Assert.AreSame(ambito, vista.AlActivarse, "La vista tenía su ámbito al activarse");
                Assert.AreSame(ambito, contexto.AlActivarse, "Y su DataContext también");
                Assert.AreSame(vista, pestanas.SelectedItem);
                GestorRegiones gestorAmbito = ((ServicioNavegacionNesto)ambito).Gestor;
                Assert.AreNotSame(gestor, gestorAmbito, "Con un gestor de regiones propio");
                Assert.AreSame(gestorAmbito, GestorRegiones.GetGestor(vista), "Colgado de la vista");
            });
        }

        [TestMethod]
        public void AbrirVistaNueva_EntregaALaVistaYASuDataContextLaNavegacionDeLaRegionAntesDeActivarla()
        {
            // La lista que la vista maestra pone en su región de lista navega al detalle de esa misma pestaña
            EnSta(() =>
            {
                var (navegacion, _, _) = Crear();
                var contexto = new ConAmbito();
                var vista = new VistaConAmbito { DataContext = contexto };

                navegacion.AbrirVistaNueva("MainRegion", vista, "ListaPedidosCompraRegion");

                Assert.AreSame(navegacion, vista.AlActivarse);
                Assert.AreSame(navegacion, contexto.AlActivarse);
            });
        }
    }
}
