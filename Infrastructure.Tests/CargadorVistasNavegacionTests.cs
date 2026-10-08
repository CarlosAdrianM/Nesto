using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using Nesto.Infrastructure.Shared;
using Prism.Ioc;
using Prism.Regions;
using System;
using System.Linq;
using System.Threading;
using System.Windows.Controls;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4): <see cref="CargadorVistasNavegacion"/> reutiliza la vista abierta como Prism, salvo la de un
    /// <see cref="IReceptorNavegacionPestanaNueva"/>, que abre otra en cada navegación (lo que hacía
    /// <c>IsNavigationTarget = false</c>).
    /// </summary>
    [TestClass]
    public class CargadorVistasNavegacionTests
    {
        private sealed class ViewModelReutiliza : IReceptorNavegacion
        {
            public void AlLlegar(ParametrosNavegacion parametros) { }
        }

        private sealed class ViewModelPestanaNueva : IReceptorNavegacionPestanaNueva
        {
            public void AlLlegar(ParametrosNavegacion parametros) { }
        }

        // Vista de mentira: Prism la busca en la región por el nombre de su tipo
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
                throw new AssertFailedException(error.Message, error);
            }
        }

        // Navega a «VistaPrueba» en una región con una vista ya abierta cuyo DataContext es el indicado
        private static (Region region, object abierta, object cargada) Navegar(object dataContextAbierta)
        {
            var contenedor = A.Fake<IContainerExtension>();
            A.CallTo(() => contenedor.Resolve(typeof(object), nameof(VistaPrueba))).ReturnsLazily(() => new VistaPrueba());
            var region = new Region { Name = "MainRegion" };
            var abierta = new VistaPrueba { DataContext = dataContextAbierta };
            region.Add(abierta);
            var cargador = new CargadorVistasNavegacion(contenedor);

            object cargada = cargador.LoadContent(region, new NavigationContext(null, new Uri(nameof(VistaPrueba), UriKind.Relative)));
            return (region, abierta, cargada);
        }

        [TestMethod]
        public void LoadContent_ConUnReceptorQueReutiliza_DevuelveLaVistaAbierta()
        {
            EnSta(() =>
            {
                var (region, abierta, cargada) = Navegar(new ViewModelReutiliza());

                Assert.AreSame(abierta, cargada);
                Assert.AreEqual(1, region.Views.Count());
            });
        }

        [TestMethod]
        public void LoadContent_ConUnReceptorPestanaNueva_AbreOtraVista()
        {
            EnSta(() =>
            {
                var (region, abierta, cargada) = Navegar(new ViewModelPestanaNueva());

                Assert.IsNotNull(cargada);
                Assert.AreNotSame(abierta, cargada);
                Assert.AreEqual(2, region.Views.Count());
            });
        }

        // Nesto#490 (4C.4, 6.º tramo): las subclases de ViewModelBase siguen abriendo una pestaña nueva en cada
        // navegación (antes INavigationAware con IsNavigationTarget = false), y las de ViewModelBasico que reciben la
        // navegación reutilizan la abierta (la lista de rapports).
        private sealed class ViewModelHeredaDeLaBase : ViewModelBase { }

        private sealed class ViewModelBasicoReutiliza : ViewModelBasico, IReceptorNavegacion
        {
            public void AlLlegar(ParametrosNavegacion parametros) { }
        }

        [TestMethod]
        public void ViewModelBase_NoDependeDeLaNavegacionDePrismYPidePestanaNueva()
        {
            Assert.IsFalse(typeof(INavigationAware).IsAssignableFrom(typeof(ViewModelBase)));
            Assert.IsTrue(typeof(IReceptorNavegacionPestanaNueva).IsAssignableFrom(typeof(ViewModelBase)));
            Assert.IsFalse(typeof(IReceptorNavegacion).IsAssignableFrom(typeof(ViewModelBasico)));
        }

        [TestMethod]
        public void LoadContent_ConUnaSubclaseDeViewModelBase_AbreOtraVista()
        {
            EnSta(() =>
            {
                var (region, abierta, cargada) = Navegar(new ViewModelHeredaDeLaBase());

                Assert.IsNotNull(cargada);
                Assert.AreNotSame(abierta, cargada);
                Assert.AreEqual(2, region.Views.Count());
            });
        }

        [TestMethod]
        public void LoadContent_ConUnViewModelBasicoQueRecibeLaNavegacion_ReutilizaLaVista()
        {
            EnSta(() =>
            {
                var (region, abierta, cargada) = Navegar(new ViewModelBasicoReutiliza());

                Assert.AreSame(abierta, cargada);
                Assert.AreEqual(1, region.Views.Count());
            });
        }

        [TestMethod]
        public void Entregar_AUnaSubclaseDeViewModelBase_LlamaASuAlLlegarConLosParametros()
        {
            EnSta(() =>
            {
                var viewModel = A.Fake<ViewModelBase>();
                var vista = new VistaPrueba { DataContext = viewModel };
                var parametros = new ParametrosNavegacion { { "clave", 1 } };

                IReceptorNavegacion receptor = ReceptorNavegacion.Entregar(new[] { vista }, new[] { vista }, nameof(VistaPrueba), parametros);

                Assert.AreSame(viewModel, receptor);
                A.CallTo(() => viewModel.AlLlegar(parametros)).MustHaveHappenedOnceExactly();
            });
        }

        [TestMethod]
        public void QuierePestanaNueva_MiraLaVistaYSuDataContext()
        {
            EnSta(() =>
            {
                Assert.IsTrue(ReceptorNavegacion.QuierePestanaNueva(new ViewModelPestanaNueva()));
                Assert.IsTrue(ReceptorNavegacion.QuierePestanaNueva(new VistaPrueba { DataContext = new ViewModelPestanaNueva() }));
                Assert.IsFalse(ReceptorNavegacion.QuierePestanaNueva(new VistaPrueba { DataContext = new ViewModelReutiliza() }));
                Assert.IsFalse(ReceptorNavegacion.QuierePestanaNueva(new VistaPrueba()));
                Assert.IsFalse(ReceptorNavegacion.QuierePestanaNueva(null));
            });
        }
    }
}
