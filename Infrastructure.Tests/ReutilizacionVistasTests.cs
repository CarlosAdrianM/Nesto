using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using Nesto.Infrastructure.Shared;
using System;
using System.Linq;
using System.Threading;
using System.Windows.Controls;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4): la navegación reutiliza la vista abierta como Prism, salvo la de un
    /// <see cref="IReceptorNavegacionPestanaNueva"/>, que abre otra en cada navegación (lo que hacía
    /// <c>IsNavigationTarget = false</c>). Eran las pruebas del <c>CargadorVistasNavegacion</c> de Prism; desde el tramo 4
    /// son de la región propia (<see cref="ServicioNavegacionNesto"/>), con las mismas comprobaciones.
    /// </summary>
    [TestClass]
    public class ReutilizacionVistasTests
    {
        private sealed class ViewModelReutiliza : IReceptorNavegacion
        {
            public void AlLlegar(ParametrosNavegacion parametros) { }
        }

        private sealed class ViewModelPestanaNueva : IReceptorNavegacionPestanaNueva
        {
            public void AlLlegar(ParametrosNavegacion parametros) { }
        }

        // Vista de mentira: la navegación la busca en la región por el nombre de su tipo
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
        private static (RegionNesto region, object abierta, object cargada) Navegar(object dataContextAbierta)
        {
            var gestor = new GestorRegiones();
            RegionNesto region = gestor.Registrar("MainRegion", new TabControl());
            var abierta = new VistaPrueba { DataContext = dataContextAbierta };
            region.Anadir(abierta);
            var navegacion = new ServicioNavegacionNesto(gestor, nombre => nombre == nameof(VistaPrueba) ? new VistaPrueba() : null);

            navegacion.RequestNavigate("MainRegion", nameof(VistaPrueba));
            return (region, abierta, navegacion.VistaActiva("MainRegion"));
        }

        [TestMethod]
        public void Navegar_ConUnReceptorQueReutiliza_ActivaLaVistaAbierta()
        {
            EnSta(() =>
            {
                var (region, abierta, cargada) = Navegar(new ViewModelReutiliza());

                Assert.AreSame(abierta, cargada);
                Assert.AreEqual(1, region.Vistas.Count);
            });
        }

        [TestMethod]
        public void Navegar_ConUnReceptorPestanaNueva_AbreOtraVista()
        {
            EnSta(() =>
            {
                var (region, abierta, cargada) = Navegar(new ViewModelPestanaNueva());

                Assert.IsNotNull(cargada);
                Assert.AreNotSame(abierta, cargada);
                Assert.AreEqual(2, region.Vistas.Count);
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
            Assert.IsFalse(typeof(ViewModelBase).GetInterfaces().Any(i => i.Name == "INavigationAware"));
            Assert.IsTrue(typeof(IReceptorNavegacionPestanaNueva).IsAssignableFrom(typeof(ViewModelBase)));
            Assert.IsFalse(typeof(IReceptorNavegacion).IsAssignableFrom(typeof(ViewModelBasico)));
        }

        [TestMethod]
        public void Navegar_ConUnaSubclaseDeViewModelBase_AbreOtraVista()
        {
            EnSta(() =>
            {
                var (region, abierta, cargada) = Navegar(new ViewModelHeredaDeLaBase());

                Assert.IsNotNull(cargada);
                Assert.AreNotSame(abierta, cargada);
                Assert.AreEqual(2, region.Vistas.Count);
            });
        }

        [TestMethod]
        public void Navegar_ConUnViewModelBasicoQueRecibeLaNavegacion_ReutilizaLaVista()
        {
            EnSta(() =>
            {
                var (region, abierta, cargada) = Navegar(new ViewModelBasicoReutiliza());

                Assert.AreSame(abierta, cargada);
                Assert.AreEqual(1, region.Vistas.Count);
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
