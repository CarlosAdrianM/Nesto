using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Models;
using Prism.Regions;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#489 / NestoAPI#533: al modificar un pedido desde la plantilla (Nesto#397), la API puede rechazar el cambio
    /// de modo porque el pedido ya tiene picking. Igual que en el detalle: vuelve el modo grabado y se ofrece pedírselo
    /// a almacén.
    /// </summary>
    [TestClass]
    public class ModoConPickingPlantillaTests
    {
        private static PlantillaVentaViewModel CrearViewModel(IServicioDialogos dialogService, IPedidoVentaService pedidoVentaService)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IUnityContainer>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), dialogService, pedidoVentaService, A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            return vm;
        }

        private static IServicioDialogos DialogoQueResponde(bool acepta)
        {
            IServicioDialogos dialogService = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogService.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(acepta));
            return dialogService;
        }

        [TestMethod]
        public async Task Plantilla_ConPicking_SiAcepta_PideElCambioYVuelveAlModoGrabado()
        {
            IPedidoVentaService pedidoVentaService = A.Fake<IPedidoVentaService>();
            A.CallTo(() => pedidoVentaService.SolicitarCambioModo(A<string>._, A<int>._, A<byte>._, A<string>._)).Returns("Se lo hemos pedido a almacén.");
            PlantillaVentaViewModel vm = CrearViewModel(DialogoQueResponde(true), pedidoVentaService);
            vm.ModoServicio = ModosServicio.TODO_JUNTO; // el cambio del usuario

            await vm.ResolverModoConPickingAsync(new ModoConPickingException("Ya tiene picking."), "1", 926879,
                ModosServicio.TODO_JUNTO, ModosServicio.SEGUN_VAYA_ENTRANDO);

            Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, vm.ModoServicio);
            A.CallTo(() => pedidoVentaService.SolicitarCambioModo("1", 926879, ModosServicio.TODO_JUNTO, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Plantilla_ConPicking_SiRechaza_NoLlamaAlEndpoint()
        {
            IPedidoVentaService pedidoVentaService = A.Fake<IPedidoVentaService>();
            PlantillaVentaViewModel vm = CrearViewModel(DialogoQueResponde(false), pedidoVentaService);
            vm.ModoServicio = ModosServicio.TODO_JUNTO;

            await vm.ResolverModoConPickingAsync(new ModoConPickingException("Ya tiene picking."), "1", 926879,
                ModosServicio.TODO_JUNTO, ModosServicio.SEGUN_VAYA_ENTRANDO);

            Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, vm.ModoServicio);
            A.CallTo(() => pedidoVentaService.SolicitarCambioModo(A<string>._, A<int>._, A<byte>._, A<string>._)).MustNotHaveHappened();
        }
    }
}
