using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Modulos.Rapports;
using Prism.Regions;
using System.Collections.ObjectModel;
using Unity;
using idDescripcion = Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO.idDescripcion;

namespace RapportsTests
{
    /// <summary>
    /// Nesto#490 (4C.1): la lista de rapports escucha RapportGuardado y RapportNoGuardado por el
    /// IMessenger (antes IEventAggregator de Prism) solo mientras está activa (IsActive).
    /// </summary>
    [TestClass]
    public class MensajesRapportsTests
    {
        private IMessenger _messenger;
        private IRapportService _servicio;

        [TestInitialize]
        public void Initialize()
        {
            _messenger = new WeakReferenceMessenger();
            _servicio = A.Fake<IRapportService>();
        }

        private ListaRapportsViewModel CrearLista()
        {
            return new ListaRapportsViewModel(A.Fake<IRegionManager>(), A.Fake<IConfiguracion>(), _servicio,
                A.Fake<IUnityContainer>(), A.Fake<IServicioDialogos>(), _messenger);
        }

        private static (ListaRapportsViewModel, SeguimientoClienteDTO) ConRapportNuevo(ListaRapportsViewModel vm)
        {
            var nuevo = new SeguimientoClienteDTO { Id = 0, Cliente = "15191" };
            vm.listaRapports = new ObservableCollection<SeguimientoClienteDTO> { new SeguimientoClienteDTO { Id = 7 }, nuevo };
            return (vm, nuevo);
        }

        [TestMethod]
        public void RapportNoGuardado_ConLaListaActiva_QuitaLaFila()
        {
            var (vm, nuevo) = ConRapportNuevo(CrearLista());
            vm.IsActive = true;

            _messenger.Send(new RapportNoGuardadoMensaje(nuevo));

            Assert.AreEqual(1, vm.listaRapports.Count);
            Assert.IsFalse(vm.listaRapports.Contains(nuevo));
        }

        [TestMethod]
        public void RapportNoGuardado_ConLaListaInactiva_NoLlega()
        {
            var (vm, nuevo) = ConRapportNuevo(CrearLista());
            vm.IsActive = true;
            vm.IsActive = false;

            _messenger.Send(new RapportNoGuardadoMensaje(nuevo));

            Assert.AreEqual(2, vm.listaRapports.Count);
        }

        [TestMethod]
        public void IsActive_ActivarDesactivarYReactivar_NoLanzaYSigueEscuchando()
        {
            // El Messenger lanza si se registra dos veces el mismo receptor; Prism no.
            var (vm, nuevo) = ConRapportNuevo(CrearLista());
            vm.IsActive = true;
            vm.IsActive = false;
            vm.IsActive = true;

            _messenger.Send(new RapportNoGuardadoMensaje(nuevo));

            Assert.AreEqual(1, vm.listaRapports.Count);
        }

        [TestMethod]
        public void RapportGuardado_ConLaListaActiva_RecargaLosClientesProbabilidad()
        {
            var vm = CrearLista();
            vm.TipoRapportSeleccionado = new idDescripcion("V", "Visita");
            vm.IsActive = true;
            Fake.ClearRecordedCalls(_servicio);

            _messenger.Send(new RapportGuardadoMensaje(0));

            // Como con Prism: el 0 del aviso llega como grupoSubgrupo "0" (conversión implícita de VB).
            A.CallTo(() => _servicio.CargarClientesProbabilidad(A<string>._, "Visita", "0")).MustHaveHappenedOnceExactly();
        }
    }
}
