using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using CommunityToolkit.Mvvm.Messaging;
using Prism.Regions;
using Prism.Services.Dialogs;
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#476: el selector de modo de servicio de la plantilla. Se apoya en
    /// direccionEntregaSeleccionada.servirJunto (lo que leen portes, bonificables y la validación)
    /// y guarda el modo parcial en el Estado, de donde salen el DTO y el borrador.
    /// </summary>
    [TestClass]
    public class ModoServicioPlantillaTests
    {
        private static PlantillaVentaViewModel CrearViewModel()
        {
            IUnityContainer container = A.Fake<IUnityContainer>();
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
            IMessenger messenger = new WeakReferenceMessenger();
            IDialogService dialogService = A.Fake<IDialogService>();
            IPedidoVentaService pedidoVentaService = A.Fake<IPedidoVentaService>();
            IBorradorPlantillaVentaService servicioBorradores = A.Fake<IBorradorPlantillaVentaService>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");

            return new PlantillaVentaViewModel(container, regionManager, configuracion, servicio,
                messenger, dialogService, pedidoVentaService, servicioBorradores,
                A.Fake<IServicioAutenticacion>());
        }

        [TestMethod]
        public void ModoServicio_ElPedidoNaceEnElPorDefecto_SinArrastrarElServirJuntoDeLaFicha()
        {
            // Carlos, 16/09/26: una referencia agotada o anulada dejaba pedidos «todo junto» sin
            // servir nunca. Por defecto 3, aunque la ficha del cliente tenga servir junto marcado.
            var vm = CrearViewModel();

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = true };

            Assert.AreEqual(ModosServicio.TRAS_REPONER_DE_TIENDAS, vm.ModoServicio);
            Assert.IsFalse(vm.direccionEntregaSeleccionada.servirJunto);
            Assert.AreEqual((byte)3, vm.Estado.ModoServicio);
            Assert.IsFalse(vm.Estado.ServirJunto);
        }

        [TestMethod]
        public void ModoServicio_ElParametroDelUsuarioCambiaElPorDefecto()
        {
            var vm = CrearViewModel();
            vm.ModoServicioPorDefecto = ModosServicio.TODO_JUNTO;

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.ModoServicio);
            Assert.IsTrue(vm.direccionEntregaSeleccionada.servirJunto);
        }

        [TestMethod]
        public void ElegirModoParcial_DesmarcaServirJuntoYLlegaAlEstadoYAlDto()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };

            vm.ModoServicio = ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ;
            vm.SincronizarListasAlEstado();

            Assert.IsFalse(vm.direccionEntregaSeleccionada.servirJunto);
            Assert.IsFalse(vm.Estado.ServirJunto);
            Assert.AreEqual((byte)4, vm.Estado.ModoServicio);
        }

        [TestMethod]
        public void VolverATodoJunto_MarcaServirJunto()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };
            vm.ModoServicio = ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ;

            vm.ModoServicio = ModosServicio.TODO_JUNTO;
            vm.SincronizarListasAlEstado();

            Assert.IsTrue(vm.direccionEntregaSeleccionada.servirJunto);
            Assert.IsTrue(vm.Estado.ServirJunto);
            Assert.AreEqual((byte)1, vm.Estado.ModoServicio);
        }

        [TestMethod]
        public void CambiarDeDireccion_ElModoVuelveAlPorDefecto()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };
            vm.ModoServicio = ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ;

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = true };

            Assert.AreEqual(ModosServicio.TRAS_REPONER_DE_TIENDAS, vm.ModoServicio);
            Assert.AreEqual((byte)3, vm.Estado.ModoServicio);
        }

        [TestMethod]
        public void SincronizarListasAlEstado_SinTocarElSelector_ElEstadoLlevaElModoPorDefecto()
        {
            // Un pedido que nunca tocó el selector también viaja con modo: el por defecto.
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = true };

            vm.SincronizarListasAlEstado();

            Assert.AreEqual((byte)3, vm.Estado.ModoServicio);
            Assert.IsFalse(vm.Estado.ServirJunto);
        }

        [TestMethod]
        public void CrearBorradorDesdePedido_ConservaElModoDeServicio()
        {
            var servicio = new BorradorPlantillaVentaService(A.Fake<IConfiguracion>());
            var pedido = new PedidoParaPlantillaModel
            {
                Empresa = "1", Cliente = "15191", Contacto = "0", NumeroPedido = 921838,
                ServirJunto = false, ModoServicio = 4, Almacen = "ALG"
            };

            var borrador = servicio.CrearBorradorDesdePedido(pedido);

            Assert.AreEqual((byte)4, borrador.ModoServicio);
            Assert.IsFalse(borrador.ServirJunto);
        }
    }
}
