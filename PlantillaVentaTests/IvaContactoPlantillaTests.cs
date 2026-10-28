using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Pedido 929498 (MariaJose, 28/10/26): la plantilla mandaba el IVA del contacto de la página de cliente (0, G21) aunque la
    /// entrega fuera al contacto 1, de recargo (E52): el pedido salió sin recargo. El IVA del pedido es el del contacto de entrega.
    /// </summary>
    [TestClass]
    public class IvaContactoPlantillaTests
    {
        private static PlantillaVentaViewModel CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "29382", contacto = "0", iva = "G21", cifNif = "76038311N" };
            vm.Estado.Cliente = "29382";
            vm.Estado.IvaCliente = "G21";
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "0", iva = "G21", ruta = "FW" };
            return vm;
        }

        [TestMethod]
        public void CambiarAlContactoDeRecargo_CambiaElIvaDelPedido()
        {
            PlantillaVentaViewModel vm = CrearViewModel();

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "1", iva = "E52 ", ruta = "FW" };

            Assert.AreEqual("E52", vm.Estado.IvaCliente);
            Assert.AreEqual("E52", vm.Estado.ToPedidoVentaDTO("ALG", () => 0, () => "NV").iva, "es lo que viaja a la API");
        }

        [TestMethod]
        public void SinIvaPuestoAMano_CambiarDeContacto_LoRespeta()
        {
            PlantillaVentaViewModel vm = CrearViewModel();
            vm.Estado.IvaCliente = null; // botón «Cambiar IVA»: pedido sin IVA

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "1", iva = "E52", ruta = "FW" };

            Assert.IsNull(vm.Estado.IvaCliente);
        }

        [TestMethod]
        public void MismoContacto_OContactoSinIvaEnFicha_NoTocaElIva()
        {
            PlantillaVentaViewModel vm = CrearViewModel();

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "0", iva = "E52", ruta = "FW" }; // Nesto#525: misma dirección reempujada
            Assert.AreEqual("G21", vm.Estado.IvaCliente, "el mismo contacto no reinicia nada");

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "2", iva = null, ruta = "FW" };
            Assert.AreEqual("G21", vm.Estado.IvaCliente, "sin IVA en la ficha del contacto se deja el que había");
        }
    }
}
