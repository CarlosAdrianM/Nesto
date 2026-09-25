using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Events;
using Nesto.Modulos.PedidoCompra.Models;
using Nesto.Modulos.PedidoCompra.ViewModels;
using Prism.Regions;
using System.Linq;

namespace PedidoCompraTests
{
    /// <summary>
    /// Nesto#490 (4C.1): al guardar un pedido de compra el detalle avisa a la lista por el
    /// IMessenger (antes PedidoCompraModificadoEvent de Prism) para que cambie la fila del pedido
    /// sin crear por la del pedido ya creado.
    /// </summary>
    [TestClass]
    public class MensajesPedidoCompraTests
    {
        [TestMethod]
        public void PedidoCompraModificado_LlegaALaLista_YSustituyeLaFilaDelPedidoSinCrear()
        {
            IMessenger messenger = new WeakReferenceMessenger();
            var vm = new ListaPedidosCompraViewModel(A.Fake<IPedidoCompraService>(), A.Fake<IServicioDialogos>(), messenger);
            vm.ScopedRegionManager = A.Fake<IRegionManager>(); // al cambiar la fila se navega al detalle
            var sinCrear = new PedidoCompraLookup { Empresa = "1", Proveedor = "123" };
            vm.ListaPedidos.ListaOriginal.Add(sinCrear);

            messenger.Send(new PedidoCompraModificadoMensaje(new PedidoCompraDTO { Empresa = "1", Id = 4567, Proveedor = "123" }));

            Assert.IsFalse(vm.ListaPedidos.ListaOriginal.Contains(sinCrear));
            var nueva = vm.ListaPedidos.ListaOriginal.Cast<PedidoCompraLookup>().Single();
            Assert.AreEqual(4567, nueva.Pedido);
            Assert.AreSame(nueva, vm.ListaPedidos.ElementoSeleccionado);
        }
    }
}
