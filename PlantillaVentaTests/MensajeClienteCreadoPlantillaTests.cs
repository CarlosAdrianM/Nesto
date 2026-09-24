using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Shared;
using Nesto.Models.Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using Prism.Regions;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#490 (4C.1): la plantilla se entera de los clientes creados o modificados por el
    /// IMessenger (antes ClienteCreadoEvent de Prism) y refresca la fila del cliente en su lista.
    /// </summary>
    [TestClass]
    public class MensajeClienteCreadoPlantillaTests
    {
        [TestMethod]
        public void ClienteCreado_LlegaALaPlantilla_YActualizaElClienteDeLaLista()
        {
            IMessenger messenger = new WeakReferenceMessenger();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IUnityContainer>(), A.Fake<IRegionManager>(), configuracion,
                A.Fake<IPlantillaVentaService>(), messenger, A.Fake<IDialogService>(), A.Fake<IPedidoVentaService>(),
                A.Fake<IBorradorPlantillaVentaService>(), A.Fake<IServicioAutenticacion>());
            var enLista = new ClienteJson { empresa = "1", cliente = "15191", contacto = "0", nombre = "NOMBRE VIEJO" };
            vm.listaClientes = new ObservableCollection<ClienteJson> { enLista };

            messenger.Send(new ClienteCreadoMensaje(new Clientes
            {
                Empresa = "1  ", Nº_Cliente = "15191     ", Contacto = "0  ",
                Nombre = "NOMBRE NUEVO", Dirección = "CALLE MAYOR, 1", Población = "MADRID", Estado = 0
            }));

            Assert.AreEqual("NOMBRE NUEVO", enLista.nombre);
            Assert.AreEqual("CALLE MAYOR, 1", enLista.direccion);
            Assert.AreEqual("MADRID", enLista.poblacion);
        }
    }
}
