using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#562 (NestoAPI#723): en la plantilla, «Su pedido» se resalta si el cliente exige el P.O. Además, lo escrito en
    /// «Su pedido» tiene que llegar al pedido: la vista bindeaba SuPedido, pero el ViewModel no tenía la propiedad (solo
    /// Estado) y el P.O. se perdía; y al modificar un pedido con la plantilla (o retomar un borrador) tampoco viajaba.
    /// </summary>
    [TestClass]
    public class SuPedidoObligatorioPlantillaTests
    {
        private static (PlantillaVentaViewModel vm, IPedidoVentaService pedidos) CrearViewModel(bool exige)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            IPedidoVentaService pedidos = A.Fake<IPedidoVentaService>();
            A.CallTo(() => pedidos.LeerSuPedidoObligatorio(A<string>._, A<string>._)).Returns(Task.FromResult(exige));
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), pedidos, A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "15191", contacto = "0", iva = "G21", cifNif = "12345678A" };
            return (vm, pedidos);
        }

        [TestMethod]
        public async Task ClienteQueExigeElPO_SinPO_SeResaltaElCampo()
        {
            var (vm, pedidos) = CrearViewModel(exige: true);

            await vm.CargarSuPedidoObligatorioAsync();

            A.CallTo(() => pedidos.LeerSuPedidoObligatorio("1", "15191")).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.SuPedidoObligatorio);
            Assert.IsTrue(vm.FaltaSuPedido);
            StringAssert.Contains(vm.EtiquetaSuPedido, "*");
            vm.SuPedido = "4500123456";
            Assert.IsFalse(vm.FaltaSuPedido, "Con el P.O. escrito ya no falta");
        }

        [TestMethod]
        public async Task ClienteQueNoLoExige_NoSeResalta()
        {
            var (vm, _) = CrearViewModel(exige: false);

            await vm.CargarSuPedidoObligatorioAsync();

            Assert.IsFalse(vm.SuPedidoObligatorio);
            Assert.IsFalse(vm.FaltaSuPedido);
        }

        [TestMethod]
        public void SuPedidoEscritoEnLaPlantilla_LlegaAlPedido()
        {
            var (vm, _) = CrearViewModel(exige: false);

            vm.SuPedido = "4500123456";

            Assert.AreEqual("4500123456", vm.Estado.SuPedido);
            var pedido = vm.Estado.ToPedidoVentaDTO("DIR", () => 0, () => "NV");
            Assert.AreEqual("4500123456", pedido.suPedido);
        }

        [TestMethod]
        public void ModificarConLaPlantilla_ConservaElPO()
        {
            var servicio = new BorradorPlantillaVentaService(A.Fake<IConfiguracion>());
            var pedido = new PedidoParaPlantillaModel
            {
                Empresa = "1",
                Cliente = "15191",
                Contacto = "0",
                NumeroPedido = 929500,
                SuPedido = "4500123456",
                Lineas = new List<LineaParaPlantillaModel>()
            };

            var borrador = servicio.CrearBorradorDesdePedido(pedido);

            Assert.AreEqual("4500123456", borrador.SuPedido);
        }
    }
}
