using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Rapports;
using Prism.Events;
using Prism.Regions;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using Unity;

namespace RapportsTests
{
    /// <summary>
    /// Nesto#206: el rapport nuevo se mete en la lista antes de guardarse; si el guardado falla,
    /// la fila no puede quedarse como si hubiera ido bien.
    /// </summary>
    [TestClass]
    public class ListaRapportsViewModelTests
    {
        private static ListaRapportsViewModel CrearViewModel()
        {
            return new ListaRapportsViewModel(A.Fake<IRegionManager>(), A.Fake<IConfiguracion>(), A.Fake<IRapportService>(),
                A.Fake<IUnityContainer>(), A.Fake<IDialogService>(), A.Fake<IEventAggregator>());
        }

        [TestMethod]
        public void QuitarRapportNoGuardado_ElNuevoQueFallo_DesapareceDeLaListaYDejaDeEstarSeleccionado()
        {
            var vm = CrearViewModel();
            var nuevo = new SeguimientoClienteDTO { Id = 0, Cliente = "15191" };
            vm.listaRapports = new ObservableCollection<SeguimientoClienteDTO> { new SeguimientoClienteDTO { Id = 7 }, nuevo };
            vm.rapportSeleccionado = nuevo;

            vm.QuitarRapportNoGuardado(nuevo);

            Assert.AreEqual(1, vm.listaRapports.Count);
            Assert.IsNull(vm.rapportSeleccionado);
        }

        [TestMethod]
        public void QuitarRapportNoGuardado_UnoYaGuardadoQueFallaAlModificar_SeQueda()
        {
            // En la base de datos sigue existiendo: quitarlo de la lista sería mentir al revés.
            var vm = CrearViewModel();
            var existente = new SeguimientoClienteDTO { Id = 7 };
            vm.listaRapports = new ObservableCollection<SeguimientoClienteDTO> { existente };

            vm.QuitarRapportNoGuardado(existente);

            Assert.AreEqual(1, vm.listaRapports.Count);
        }

        [TestMethod]
        public void QuitarRapportNoGuardado_SinListaOConOtraCosa_NoRevienta()
        {
            var vm = CrearViewModel();

            vm.QuitarRapportNoGuardado(null);
            vm.QuitarRapportNoGuardado("no soy un rapport");
            vm.QuitarRapportNoGuardado(new SeguimientoClienteDTO { Id = 0 });
        }
    }
}
