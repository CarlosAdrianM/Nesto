using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Rapports;
using System.Threading.Tasks;

namespace RapportsTests
{
    /// <summary>
    /// Nesto#490 (4C.4): la vista de un rapport recibe la navegación por <see cref="IReceptorNavegacionPestanaNueva"/>
    /// en vez de INavigationAware: cada rapport elegido abre una vista nueva en RapportDetailRegion (antes
    /// IsNavigationTarget = False) y <c>AlLlegar</c> hace lo de <c>OnNavigatedTo</c>, con la misma clave.
    /// </summary>
    [TestClass]
    public class ReceptoresNavegacionRapportsTests
    {
        [TestMethod]
        public void Rapport_NoDependeDeLaNavegacionDePrismYAbreVistaNueva()
        {
            Assert.IsFalse(typeof(Prism.Regions.INavigationAware).IsAssignableFrom(typeof(RapportViewModel)));
            Assert.IsTrue(typeof(IReceptorNavegacionPestanaNueva).IsAssignableFrom(typeof(RapportViewModel)));
        }

        [TestMethod]
        public void ListaRapports_NoDependeDeLaNavegacionDePrismYReutilizaSuPestana()
        {
            // Nesto#490 (4C.4, 6.º tramo): antes IsNavigationTarget = True. Hereda de ViewModelBasico, no de ViewModelBase
            // (que abre pestaña nueva), e implementa IReceptorNavegacion: Prism reutiliza la pestaña abierta.
            Assert.IsFalse(typeof(Prism.Regions.INavigationAware).IsAssignableFrom(typeof(ListaRapportsViewModel)));
            Assert.IsTrue(typeof(IReceptorNavegacion).IsAssignableFrom(typeof(ListaRapportsViewModel)));
            Assert.IsFalse(typeof(IReceptorNavegacionPestanaNueva).IsAssignableFrom(typeof(ListaRapportsViewModel)));
        }

        [TestMethod]
        public void Rapport_AlLlegar_CargaElRapportYElVendedorDelUsuario()
        {
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro("1", "Vendedor")).Returns(Task.FromResult("NV"));
            var vm = new RapportViewModel(configuracion, A.Fake<IRapportService>(), A.Fake<IServicioDialogos>(), new WeakReferenceMessenger());
            var rapport = new SeguimientoClienteDTO { Id = 7, Cliente = "15191" };

            vm.AlLlegar(new ParametrosNavegacion { { "rapportParameter", rapport } });

            Assert.AreSame(rapport, vm.rapport);
            Assert.AreEqual("NV", vm.VendedorUsuario);
        }

        [TestMethod]
        public void Rapport_AlLlegarConElVendedorYaLeido_NoLoVuelveALeer()
        {
            var configuracion = A.Fake<IConfiguracion>();
            var vm = new RapportViewModel(configuracion, A.Fake<IRapportService>(), A.Fake<IServicioDialogos>(), new WeakReferenceMessenger());
            vm.VendedorUsuario = "JE";
            Fake.ClearRecordedCalls(configuracion);

            vm.AlLlegar(new ParametrosNavegacion { { "rapportParameter", new SeguimientoClienteDTO() } });

            A.CallTo(() => configuracion.leerParametro("1", "Vendedor")).MustNotHaveHappened();
            Assert.AreEqual("JE", vm.VendedorUsuario);
        }
    }
}
