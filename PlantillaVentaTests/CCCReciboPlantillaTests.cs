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
using System.Collections.Generic;
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#486: con recibo bancario el paso de finalizar enseña el SelectorCCC. La cuenta nace
    /// con la de la ficha del contacto (la misma que pondría la API al crear el pedido) y la que
    /// elija el usuario es la que se guarda en el Estado (de donde salen el DTO y el borrador).
    /// </summary>
    [TestClass]
    public class CCCReciboPlantillaTests
    {
        private static PlantillaVentaViewModel CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var clienteCreadoEvent = A.Fake<ClienteCreadoEvent>();
            A.CallTo(() => eventAggregator.GetEvent<ClienteCreadoEvent>()).Returns(clienteCreadoEvent);

            return new PlantillaVentaViewModel(A.Fake<IUnityContainer>(), A.Fake<IRegionManager>(), configuracion,
                A.Fake<IPlantillaVentaService>(), eventAggregator, A.Fake<IDialogService>(),
                A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
        }

        [TestMethod]
        public void CccSeleccionado_NaceConElDeLaFichaDelContacto()
        {
            var vm = CrearViewModel();
            var notificadas = new List<string>();
            vm.PropertyChanged += (s, e) => notificadas.Add(e.PropertyName);

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "0", ccc = "2" };

            Assert.AreEqual("2", vm.CccSeleccionado);
            CollectionAssert.Contains(notificadas, nameof(PlantillaVentaViewModel.CccSeleccionado),
                "El SelectorCCC tiene que enterarse de la cuenta de la ficha");
        }

        [TestMethod]
        public void CccSeleccionado_LaQueEligeElUsuarioLlegaAlEstado()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "0", ccc = "2" };

            vm.CccSeleccionado = "3";

            Assert.AreEqual("3", vm.Estado.Ccc);
        }

        [TestMethod]
        public void EsReciboBancario_SoloConRCB_YSeNotifica()
        {
            var vm = CrearViewModel();
            var notificadas = new List<string>();
            vm.PropertyChanged += (s, e) => notificadas.Add(e.PropertyName);

            vm.FormaPagoSeleccionada = new FormaPago { formaPago = "RCB", cccObligatorio = true };
            Assert.IsTrue(vm.EsReciboBancario);
            CollectionAssert.Contains(notificadas, nameof(PlantillaVentaViewModel.EsReciboBancario));

            vm.FormaPagoSeleccionada = new FormaPago { formaPago = "EFC" };
            Assert.IsFalse(vm.EsReciboBancario);
        }
    }
}
