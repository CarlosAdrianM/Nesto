using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using Prism.Events;
using Prism.Regions;
using Prism.Services.Dialogs;
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// MariaJose 21/08/26: desmarcaba "Servir junto" en la plantilla, la pantalla lo mostraba
    /// desmarcado y sin ningun aviso, pero el JSON del borrador llevaba "ServirJunto": true y el
    /// pedido se creaba con servir junto marcado.
    ///
    /// Causa: los checkboxes "Servir junto" y "Mantener junto" bindean contra
    /// direccionEntregaSeleccionada, mientras que ToPedidoVentaDTO() y el borrador leen
    /// Estado.ServirJunto / Estado.MantenerJunto, que solo se rellenaban al SELECCIONAR la
    /// direccion. Lo que el usuario tocaba despues no llegaba al Estado.
    /// </summary>
    [TestClass]
    public class ServirJuntoPlantillaTests
    {
        private static PlantillaVentaViewModel CrearViewModel()
        {
            IUnityContainer container = A.Fake<IUnityContainer>();
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IPedidoVentaService pedidoVentaService = A.Fake<IPedidoVentaService>();
            IBorradorPlantillaVentaService servicioBorradores = A.Fake<IBorradorPlantillaVentaService>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var clienteCreadoEvent = A.Fake<ClienteCreadoEvent>();
            A.CallTo(() => eventAggregator.GetEvent<ClienteCreadoEvent>()).Returns(clienteCreadoEvent);

            return new PlantillaVentaViewModel(container, regionManager, configuracion, servicio,
                eventAggregator, dialogService, pedidoVentaService, servicioBorradores,
                A.Fake<IServicioAutenticacion>());
        }

        [TestMethod]
        public void SincronizarListasAlEstado_UsuarioDesmarcaServirJunto_LlegaAlEstado()
        {
            var vm = CrearViewModel();
            // Al seleccionar la direccion, servirJunto viene marcado de la ficha del cliente
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = true };
            Assert.IsTrue(vm.Estado.ServirJunto, "De partida el Estado lo coge de la direccion");

            // El usuario lo desmarca en la pantalla (el checkbox bindea contra la direccion)
            vm.direccionEntregaSeleccionada.servirJunto = false;
            vm.SincronizarListasAlEstado();

            Assert.IsFalse(vm.Estado.ServirJunto,
                "Sin esto, el pedido y el borrador salian con servir junto marcado aunque la pantalla lo mostrase desmarcado");
        }

        [TestMethod]
        public void SincronizarListasAlEstado_UsuarioMarcaMantenerJunto_LlegaAlEstado()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { mantenerJunto = false };

            vm.direccionEntregaSeleccionada.mantenerJunto = true;
            vm.SincronizarListasAlEstado();

            Assert.IsTrue(vm.Estado.MantenerJunto);
        }

        [TestMethod]
        public void SincronizarListasAlEstado_SinDireccion_NoRompe()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = null;

            vm.SincronizarListasAlEstado();
        }
    }
}
