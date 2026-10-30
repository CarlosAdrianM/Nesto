using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Events;
using Nesto.Modulos.PedidoCompra.Models;
using Nesto.Modulos.PedidoCompra.ViewModels;
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
            vm.NavegacionAmbito = A.Fake<IServicioNavegacion>(); // al cambiar la fila se navega al detalle
            var sinCrear = new PedidoCompraLookup { Empresa = "1", Proveedor = "123" };
            vm.ListaPedidos.ListaOriginal.Add(sinCrear);

            messenger.Send(new PedidoCompraModificadoMensaje(new PedidoCompraDTO { Empresa = "1", Id = 4567, Proveedor = "123" }));

            Assert.IsFalse(vm.ListaPedidos.ListaOriginal.Contains(sinCrear));
            var nueva = vm.ListaPedidos.ListaOriginal.Cast<PedidoCompraLookup>().Single();
            Assert.AreEqual(4567, nueva.Pedido);
            Assert.AreSame(nueva, vm.ListaPedidos.ElementoSeleccionado);
        }

        /// <summary>
        /// Nesto#585 (sugerencia 910, Manuel 30/10/26, STALEKS): el messenger es global, así que el aviso
        /// llega también a la lista de otra pestaña de Pedido Compra que no tiene la fila «sin crear» de ese
        /// proveedor. Antes su Single lanzaba «Sequence contains no matching element».
        /// </summary>
        [TestMethod]
        public void PedidoCompraModificado_ListaDeOtraPestanaSinEsaFila_NoLanzaYLaPropiaSeActualiza()
        {
            IMessenger messenger = new WeakReferenceMessenger();
            var otraPestana = new ListaPedidosCompraViewModel(A.Fake<IPedidoCompraService>(), A.Fake<IServicioDialogos>(), messenger);
            otraPestana.NavegacionAmbito = A.Fake<IServicioNavegacion>();
            var creadoEnOtra = new PedidoCompraLookup { Empresa = "1", Pedido = 220758, Proveedor = "1119" };
            otraPestana.ListaPedidos.ListaOriginal.Add(creadoEnOtra);
            var propia = new ListaPedidosCompraViewModel(A.Fake<IPedidoCompraService>(), A.Fake<IServicioDialogos>(), messenger);
            propia.NavegacionAmbito = A.Fake<IServicioNavegacion>();
            var sinCrear = new PedidoCompraLookup { Empresa = "1", Proveedor = "1119" };
            propia.ListaPedidos.ListaOriginal.Add(sinCrear);

            messenger.Send(new PedidoCompraModificadoMensaje(new PedidoCompraDTO { Empresa = "1", Id = 220762, Proveedor = "1119" }));

            Assert.AreSame(creadoEnOtra, otraPestana.ListaPedidos.ListaOriginal.Cast<PedidoCompraLookup>().Single(), "La otra pestaña no cambia");
            Assert.AreEqual(220762, propia.ListaPedidos.ListaOriginal.Cast<PedidoCompraLookup>().Single().Pedido);
            Assert.AreEqual(220762, (propia.ListaPedidos.ElementoSeleccionado as PedidoCompraLookup)?.Pedido);
            System.GC.KeepAlive(otraPestana);
            System.GC.KeepAlive(propia);
        }

        /// <summary>
        /// Nesto#585: al guardar un pedido sin crear con otra pestaña de Pedido Compra viva, no sale ningún
        /// error y el pedido guardado se puede enviar por correo.
        /// </summary>
        [TestMethod]
        public void GuardarPedido_ConOtraPestanaDePedidoCompraAbierta_NoDaErrorYSePuedeEnviar()
        {
            IMessenger messenger = new WeakReferenceMessenger();
            var servicio = A.Fake<IPedidoCompraService>();
            var dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
            A.CallTo(() => servicio.CrearPedido(A<PedidoCompraDTO>._)).Returns(System.Threading.Tasks.Task.FromResult(220762));

            var otraPestana = new ListaPedidosCompraViewModel(servicio, A.Fake<IServicioDialogos>(), messenger);
            otraPestana.NavegacionAmbito = A.Fake<IServicioNavegacion>();
            var detalle = new DetallePedidoCompraViewModel(servicio, dialogos, A.Fake<IServicioNavegacion>(), null,
                A.Fake<IConfiguracion>(), messenger, A.Fake<IServicioAutenticacion>());
            detalle.Pedido = new PedidoCompraWrapper(new PedidoCompraDTO { Empresa = "1", Proveedor = "1119" }, servicio);
            var enviarCambio = false;
            detalle.EnviarPedidoCommand.CanExecuteChanged += (s, e) => enviarCambio = true;

            detalle.GuardarPedidoCommand.Execute(null);

            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
            Assert.AreEqual(220762, detalle.Pedido.Id);
            Assert.IsTrue(enviarCambio, "Hay que avisar de que «Enviar pedido» ya se puede pulsar");
            Assert.IsTrue(detalle.EnviarPedidoCommand.CanExecute(detalle.Pedido));
            System.GC.KeepAlive(otraPestana);
        }
    }
}
