using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using Prism.Regions;
using System;

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
    }
}
