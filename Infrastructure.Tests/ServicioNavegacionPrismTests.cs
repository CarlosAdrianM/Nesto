using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using Prism.Regions;
using System;
using System.Collections.Generic;

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
