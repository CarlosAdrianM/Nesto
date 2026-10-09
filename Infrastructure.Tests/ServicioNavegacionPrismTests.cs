using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4, paso 1): ServicioNavegacionPrism tiene que navegar exactamente igual que
    /// llamar al IRegionManager de Prism: misma región, misma vista y los mismos parámetros.
    /// </summary>
    [TestClass]
    public class ServicioNavegacionPrismTests
    {
        private IRegionManager _regionManager;
        private ServicioNavegacionPrism _servicio;

        [TestInitialize]
        public void Inicializar()
        {
            _regionManager = A.Fake<IRegionManager>();
            _servicio = new ServicioNavegacionPrism(_regionManager);
        }

        [TestMethod]
        public void RequestNavigate_SinParametros_DelegaEnPrism()
        {
            _servicio.RequestNavigate("MainRegion", "CajasView");

            A.CallTo(() => _regionManager.RequestNavigate("MainRegion", "CajasView")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void RequestNavigate_ConParametros_PasaLasMismasClavesYValores()
        {
            NavigationParameters recibidos = null;
            A.CallTo(() => _regionManager.RequestNavigate("MainRegion", "ProductoView", A<NavigationParameters>._))
                .Invokes((string _, string _, NavigationParameters p) => recibidos = p);
            var cliente = new object();

            _servicio.RequestNavigate("MainRegion", "ProductoView",
                new ParametrosNavegacion { { "numeroProductoParameter", "12345" }, { "cliente", cliente }, { "nulo", null } });

            Assert.IsNotNull(recibidos);
            Assert.AreEqual(3, recibidos.Count);
            Assert.AreEqual("12345", recibidos["numeroProductoParameter"]);
            Assert.AreSame(cliente, recibidos["cliente"]);
            Assert.IsTrue(recibidos.ContainsKey("nulo"));
        }

        [TestMethod]
        public void RequestNavigate_ConParametrosNull_PasaUnosParametrosVacios()
        {
            NavigationParameters recibidos = null;
            A.CallTo(() => _regionManager.RequestNavigate("MainRegion", "ProductoView", A<NavigationParameters>._))
                .Invokes((string _, string _, NavigationParameters p) => recibidos = p);

            _servicio.RequestNavigate("MainRegion", "ProductoView", null);

            Assert.IsNotNull(recibidos);
            Assert.AreEqual(0, recibidos.Count);
        }

        [TestMethod]
        public void Constructor_SinRegionManager_Lanza()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new ServicioNavegacionPrism(null));
        }

        [TestMethod]
        public void VistaActiva_DevuelveLaPrimeraVistaActivaDeLaRegion()
        {
            var vista = new object();
            ConfigurarRegion("MainRegion", vista);

            Assert.AreSame(vista, _servicio.VistaActiva("MainRegion"));
        }

        [TestMethod]
        public void VistaActiva_SiLaRegionNoExiste_DevuelveNull()
        {
            A.CallTo(() => _regionManager.Regions.ContainsRegionWithName("MainRegion")).Returns(false);

            Assert.IsNull(_servicio.VistaActiva("MainRegion"));
        }

        [TestMethod]
        public void CerrarVistaActiva_DesactivaYQuitaLaVistaActiva()
        {
            var vista = new object();
            IRegion region = ConfigurarRegion("MainRegion", vista);

            _servicio.CerrarVistaActiva("MainRegion");

            A.CallTo(() => region.Deactivate(vista)).MustHaveHappenedOnceExactly()
                .Then(A.CallTo(() => region.Remove(vista)).MustHaveHappenedOnceExactly());
        }

        [TestMethod]
        public void CerrarVistaActiva_SinVistaActiva_NoQuitaNada()
        {
            IRegion region = ConfigurarRegion("MainRegion");

            _servicio.CerrarVistaActiva("MainRegion");

            A.CallTo(() => region.Remove(A<object>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void QuitarVistas_QuitaTodasLasVistasDeLaRegion()
        {
            // Como hacía a mano ListaRapports con RapportDetailRegion: no solo las activas, todas
            var primera = new object();
            var segunda = new object();
            IRegion region = ConfigurarRegion("RapportDetailRegion");
            var todas = A.Fake<IViewsCollection>();
            A.CallTo(() => todas.GetEnumerator()).ReturnsLazily(() => new List<object> { primera, segunda }.GetEnumerator());
            A.CallTo(() => region.Views).Returns(todas);

            _servicio.QuitarVistas("RapportDetailRegion");

            A.CallTo(() => region.Remove(primera)).MustHaveHappenedOnceExactly();
            A.CallTo(() => region.Remove(segunda)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void QuitarVistas_SiLaRegionNoExiste_NoHaceNada()
        {
            A.CallTo(() => _regionManager.Regions.ContainsRegionWithName("RapportDetailRegion")).Returns(false);

            _servicio.QuitarVistas("RapportDetailRegion");
        }

        [TestMethod]
        public void AbrirVistaNueva_LaAnadeConSuNombreYLaActiva()
        {
            IRegion region = ConfigurarRegion("MainRegion");
            var vista = new object();
            A.CallTo(() => region.GetView("Clientes")).Returns(null);

            _servicio.AbrirVistaNueva("MainRegion", vista, "Clientes");

            A.CallTo(() => region.Add(vista, "Clientes")).MustHaveHappenedOnceExactly();
            A.CallTo(() => region.Activate(vista)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AbrirVistaNueva_SiYaHayUnaConEseNombre_LeAnadeUnNumero()
        {
            // Como hacía el menú: Clientes, Clientes2, Clientes3…
            IRegion region = ConfigurarRegion("MainRegion");
            var vista = new object();
            A.CallTo(() => region.GetView("Clientes")).Returns(new object());
            A.CallTo(() => region.GetView("Clientes2")).Returns(new object());
            A.CallTo(() => region.GetView("Clientes3")).Returns(null);

            _servicio.AbrirVistaNueva("MainRegion", vista, "Clientes");

            A.CallTo(() => region.Add(vista, "Clientes3")).MustHaveHappenedOnceExactly();
        }

        // Nesto#490 (4C.4, 7.º tramo): los maestro-detalle (PedidoVenta, PedidoCompra)
        private sealed class ConAmbito : IConAmbitoNavegacion
        {
            public IServicioNavegacion NavegacionAmbito { get; set; }
        }

        private sealed class VistaConAmbito : Border, IConAmbitoNavegacion
        {
            public IServicioNavegacion NavegacionAmbito { get; set; }
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

        [TestMethod]
        public void AbrirVistaConAmbito_LaAnadeConUnAmbitoNuevoYLaActiva_YDevuelveLaNavegacionDeEseAmbito()
        {
            // Lo que hacía OnAbrirModulo de PedidoVenta/PedidoCompra: region.Add(vista, null, true) y Activate
            IRegion region = ConfigurarRegion("MainRegion");
            var ambito = A.Fake<IRegionManager>();
            var vista = new object();
            A.CallTo(() => region.Add(vista, null, true)).Returns(ambito);

            IServicioNavegacion navegacionAmbito = _servicio.AbrirVistaConAmbito("MainRegion", vista);

            A.CallTo(() => region.Add(vista, null, true)).MustHaveHappenedOnceExactly()
                .Then(A.CallTo(() => region.Activate(vista)).MustHaveHappenedOnceExactly());
            navegacionAmbito.RequestNavigate("DetallePedidoCompraRegion", "DetallePedidoCompraView");
            A.CallTo(() => ambito.RequestNavigate("DetallePedidoCompraRegion", "DetallePedidoCompraView")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _regionManager.RequestNavigate(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void AbrirVistaConAmbito_EntregaElAmbitoALaVistaYASuDataContextAntesDeActivarla()
        {
            EnSta(() =>
            {
                IRegion region = ConfigurarRegion("MainRegion");
                var contexto = new ConAmbito();
                var vista = new VistaConAmbito { DataContext = contexto };
                A.CallTo(() => region.Add(vista, null, true)).Returns(A.Fake<IRegionManager>());
                IServicioNavegacion alActivarVista = null, alActivarContexto = null;
                A.CallTo(() => region.Activate(vista)).Invokes(() =>
                {
                    alActivarVista = vista.NavegacionAmbito;
                    alActivarContexto = contexto.NavegacionAmbito;
                });

                IServicioNavegacion navegacionAmbito = _servicio.AbrirVistaConAmbito("MainRegion", vista);

                Assert.IsNotNull(navegacionAmbito);
                Assert.AreSame(navegacionAmbito, alActivarVista);
                Assert.AreSame(navegacionAmbito, alActivarContexto);
                Assert.AreNotSame(_servicio, navegacionAmbito);
            });
        }

        [TestMethod]
        public void AbrirVistaNueva_EntregaALaVistaYASuDataContextLaNavegacionDelAmbitoDeLaRegion()
        {
            // La lista que la vista maestra pone en su región de lista navega al detalle de esa misma pestaña
            EnSta(() =>
            {
                IRegion region = ConfigurarRegion("ListaPedidosCompraRegion");
                A.CallTo(() => region.GetView(A<string>._)).Returns(null);
                var contexto = new ConAmbito();
                var vista = new VistaConAmbito { DataContext = contexto };
                IServicioNavegacion alActivar = null;
                A.CallTo(() => region.Activate(vista)).Invokes(() => alActivar = contexto.NavegacionAmbito);

                _servicio.AbrirVistaNueva("ListaPedidosCompraRegion", vista, "ListaPedidosCompraRegion");

                Assert.AreSame(_servicio, alActivar);
                Assert.AreSame(_servicio, vista.NavegacionAmbito);
            });
        }

        private IRegion ConfigurarRegion(string nombre, params object[] activas)
        {
            var region = A.Fake<IRegion>();
            var vistas = A.Fake<IViewsCollection>();
            A.CallTo(() => vistas.GetEnumerator()).ReturnsLazily(() => new List<object>(activas).GetEnumerator());
            A.CallTo(() => region.ActiveViews).Returns(vistas);
            A.CallTo(() => _regionManager.Regions.ContainsRegionWithName(nombre)).Returns(true);
            A.CallTo(() => _regionManager.Regions[nombre]).Returns(region);
            return region;
        }
    }
}
