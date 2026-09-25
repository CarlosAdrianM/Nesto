using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Rapports;
using CommunityToolkit.Mvvm.Messaging;
using Prism.Regions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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
                A.Fake<IUnityContainer>(), A.Fake<IServicioDialogos>(), new WeakReferenceMessenger());
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

        // ---- Nesto#488: la carga de la lista se espera y no sale del hilo de la UI ----

        private static ListaRapportsViewModel CrearViewModel(IRapportService servicio)
        {
            return new ListaRapportsViewModel(A.Fake<IRegionManager>(), A.Fake<IConfiguracion>(), servicio,
                A.Fake<IUnityContainer>(), A.Fake<IServicioDialogos>(), new WeakReferenceMessenger());
        }

        [TestMethod]
        public async Task CargarListaRapportsAsync_ConCliente_EsperaAlResumenDeVentasAntesDeTerminar()
        {
            // Antes: Task.Run(Sub() LlamarApiResumenVentasAsync()) con un Async Sub -> fire-and-forget en el pool.
            var servicio = A.Fake<IRapportService>();
            A.CallTo(() => servicio.cargarListaRapports(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new ObservableCollection<SeguimientoClienteDTO>()));
            var resumen = new TaskCompletionSource<ResumenVentasClienteResponse>();
            A.CallTo(() => servicio.CargarResumenVentasCliente(A<string>._, A<string>._, A<string>._)).Returns(resumen.Task);
            var vm = CrearViewModel(servicio);
            vm.clienteSeleccionado = "15191";
            bool avisado = false;
            vm.GenerarResumenCommand.CanExecuteChanged += (s, e) => avisado = true;

            Task carga = vm.CargarListaRapportsAsync();

            Assert.IsFalse(carga.IsCompleted, "La carga tiene que esperar al resumen de ventas");
            Assert.IsFalse(avisado);
            resumen.SetResult(new ResumenVentasClienteResponse { Datos = new List<VentaClienteResumenDTO>() });
            await carga;
            Assert.IsNotNull(vm.ResumenVentasCliente);
            Assert.IsTrue(avisado, "Al terminar se reevalúa «Generar resumen»");
        }

        [TestMethod]
        public void CrearRapport_DeUnCliente_LaListaCargadaNoPisaAlRapportNuevo()
        {
            // Antes: Task.Run(Sub() cmdCargarListaRapports.Execute(Nothing)) volvía antes de cargar, y la
            // lista llegaba después (en el pool) y sustituía a la que ya tenía el rapport nuevo.
            var servicio = A.Fake<IRapportService>();
            A.CallTo(() => servicio.cargarListaRapports(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new ObservableCollection<SeguimientoClienteDTO> { new SeguimientoClienteDTO { Id = 7, Cliente = "15191" } }));
            A.CallTo(() => servicio.CargarResumenVentasCliente(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new ResumenVentasClienteResponse { Datos = new List<VentaClienteResumenDTO>() }));
            var vm = CrearViewModel(servicio);

            vm.cmdCrearRapport.Execute(new ClienteProbabilidadVenta { cliente = "15191", contacto = "0" });

            Assert.AreEqual(2, vm.listaRapports.Count);
            Assert.AreEqual(7, vm.listaRapports[0].Id);
            Assert.AreEqual(0, vm.listaRapports[1].Id);
            Assert.AreEqual("15191", vm.listaRapports[1].Cliente);
        }
    }
}
