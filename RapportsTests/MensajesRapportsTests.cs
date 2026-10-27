using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Modulos.Rapports;
using System.Collections.ObjectModel;
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
            return new ListaRapportsViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), _servicio,
                A.Fake<IServicioDialogos>(), _messenger, A.Fake<Nesto.Infrastructure.Services.IServicioSugerenciasProductos>());
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
        public void ConLaRegionPropia_IConActivacion_DaDeAltaYDeBajaEnLosMensajes()
        {
            // Nesto#490 (4C.4): la región propia avisa por IConActivacion (desde el tramo 4, ya sin el IActiveAware de Prism)
            var (vm, nuevo) = ConRapportNuevo(CrearLista());
            IConActivacion activable = vm;

            activable.IsActive = true;
            Assert.IsTrue(vm.IsActive);
            activable.IsActive = false;
            _messenger.Send(new RapportNoGuardadoMensaje(nuevo));
            Assert.AreEqual(2, vm.listaRapports.Count, "Inactiva: no le llega");

            activable.IsActive = true;
            _messenger.Send(new RapportNoGuardadoMensaje(nuevo));
            Assert.AreEqual(1, vm.listaRapports.Count, "Activa: le llega");
        }

        [TestMethod]
        public void ListaRapports_NoImplementaNingunaInterfazDePrism()
        {
            // Nesto#490 (4C.4, tramo 4): fuera el IActiveAware; la activación le llega solo por IConActivacion
            var dePrism = System.Linq.Enumerable.Where(typeof(ListaRapportsViewModel).GetInterfaces(),
                i => i.Namespace?.StartsWith("Prism") ?? false);
            Assert.IsFalse(System.Linq.Enumerable.Any(dePrism), string.Join(", ", dePrism));
            Assert.IsTrue(typeof(IConActivacion).IsAssignableFrom(typeof(ListaRapportsViewModel)));
        }

        [TestMethod]
        public void RapportGuardado_ConLaListaActiva_RecargaConElGrupoSeleccionado()
        {
            var vm = CrearLista();
            vm.TipoRapportSeleccionado = new idDescripcion("V", "Visita");
            vm.GrupoSubgrupoSeleccionado = "PEL/PEL";
            vm.IsActive = true;
            Fake.ClearRecordedCalls(_servicio);

            _messenger.Send(new RapportGuardadoMensaje(0));

            // NestoAPI#603 c2: se recarga con el grupo seleccionado, no con el "0" del aviso.
            A.CallTo(() => _servicio.CargarSugerenciasContacto(A<string>._, "Visita", "PEL/PEL")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void RapportGuardado_SinGrupoSeleccionado_RecargaSinGrupo()
        {
            var vm = CrearLista();
            vm.TipoRapportSeleccionado = new idDescripcion("V", "Visita");
            vm.IsActive = true;
            Fake.ClearRecordedCalls(_servicio);

            _messenger.Send(new RapportGuardadoMensaje(0));

            A.CallTo(() => _servicio.CargarSugerenciasContacto(A<string>._, "Visita", string.Empty)).MustHaveHappenedOnceExactly();
        }
    }
}
