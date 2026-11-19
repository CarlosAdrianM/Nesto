using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System.Linq;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// «¿Dónde está lo que falta?» (pedido 930942, 19/11/26): la sección plegable del detalle del pedido, junto a la fecha de entrega a la
    /// agencia. Se pide a la API al abrirla, no al abrir el pedido.
    /// </summary>
    [TestClass]
    public class DondeEstaLoQueFaltaDetalleTests
    {
        private static (DetallePedidoViewModel vm, IServicioDondeEstaLoQueFalta dondeEsta) Vm(int numero = 930942)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(), new WeakReferenceMessenger(),
                A.Fake<IServicioDialogos>(), A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            var dondeEsta = A.Fake<IServicioDondeEstaLoQueFalta>();
            A.CallTo(() => dondeEsta.Leer("1", 930942)).Returns(new DondeEstaLoQueFaltaDTO
            {
                Mensaje = "Falta 1 producto en Algete.",
                Productos = { new ProductoQueFaltaDTO { Producto = "43818", Texto = "43818 MULTIVIT GLOW CREMA RICA (1 ud.): viene en la reposición 81463 de Alcobendas." } }
            });
            vm.ServicioDondeEstaLoQueFalta = dondeEsta;
            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", numero = numero, cliente = "15191", contacto = "0" });
            return (vm, dondeEsta);
        }

        [TestMethod]
        public async Task AlAbrirElPedido_NoSePide()
        {
            var (vm, dondeEsta) = Vm();

            await vm.CargarFechaEntregaAgenciaAsync();

            A.CallTo(() => dondeEsta.Leer(A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.DondeEstaAbierto);
        }

        [TestMethod]
        public async Task AlAbrirLaSeccion_SePideYSeVeUnaLineaPorProducto()
        {
            var (vm, dondeEsta) = Vm();

            vm.DondeEstaAbierto = true;
            await vm.PedirDondeEstaLoQueFaltaAsync();

            A.CallTo(() => dondeEsta.Leer("1", 930942)).MustHaveHappened();
            Assert.AreEqual("43818 MULTIVIT GLOW CREMA RICA (1 ud.): viene en la reposición 81463 de Alcobendas.", vm.DondeEstaLoQueFalta.Lineas.Single());
            Assert.AreEqual("Falta 1 producto en Algete.", vm.DondeEstaLoQueFalta.Mensaje);
        }

        [TestMethod]
        public async Task AlCambiarDePedido_SeCierra_YNoSePideElNuevo()
        {
            var (vm, dondeEsta) = Vm();
            vm.DondeEstaAbierto = true;
            await vm.PedirDondeEstaLoQueFaltaAsync();
            Fake.ClearRecordedCalls(dondeEsta);

            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", numero = 931000, cliente = "15191", contacto = "0" });
            vm.RefrescarDondeEstaLoQueFalta();

            Assert.IsFalse(vm.DondeEstaAbierto);
            Assert.IsFalse(vm.DondeEstaLoQueFalta.HayLineas);
            A.CallTo(() => dondeEsta.Leer(A<string>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task AlGuardarElMismoPedidoConLaSeccionAbierta_SeVuelveAPedir()
        {
            var (vm, dondeEsta) = Vm();
            vm.DondeEstaAbierto = true;
            await vm.PedirDondeEstaLoQueFaltaAsync();
            Fake.ClearRecordedCalls(dondeEsta);

            vm.RefrescarDondeEstaLoQueFalta();

            Assert.IsTrue(vm.DondeEstaAbierto);
            A.CallTo(() => dondeEsta.Leer("1", 930942)).MustHaveHappenedOnceExactly();
        }
    }
}
