using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using static Nesto.Modulos.PedidoVenta.PedidoVentaModel;

namespace PedidoVentaTests
{
    /// <summary>
    /// Novedades 1283 (Laura): «Borrar pedido» en el menú «Más» del detalle del pedido, como en Nesto viejo.
    /// </summary>
    [TestClass]
    public class BorrarPedidoDetalleTests
    {
        private IServicioDialogos dialogos;
        private IPedidoVentaService servicio;
        private IMessenger messenger;
        private DetallePedidoViewModel vm;

        [TestInitialize]
        public void Preparar()
        {
            dialogos = A.Fake<IServicioDialogos>();
            servicio = A.Fake<IPedidoVentaService>();
            messenger = new WeakReferenceMessenger();
            vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, messenger,
                dialogos, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", numero = 930001, cliente = "15191", contacto = "0" });
        }

        [TestMethod]
        public void SeOfreceConUnPedidoCargado_YNoSinPedido()
        {
            Assert.IsTrue(vm.PuedeBorrarPedido);
            Assert.IsTrue(vm.BorrarPedidoCommand.CanExecute(null));

            var nuevo = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, messenger,
                dialogos, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            Assert.IsFalse(nuevo.PuedeBorrarPedido, "Sin pedido no hay nada que borrar");
        }

        [TestMethod]
        public void LaPreguntaLlevaElNumeroYElCliente()
        {
            Assert.AreEqual("¿Desea borrar el pedido 930001 del cliente 15191/0 (EL EDÉN)? Se borra entero, con todas sus líneas, y no se puede deshacer.",
                DetallePedidoViewModel.PreguntaBorrarPedido(930001, "15191  ", "0  ", "EL EDÉN"));
        }

        [TestMethod]
        public async Task DiceQueNo_NoSeBorraNada()
        {
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).Returns(false);

            await vm.BorrarPedidoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAnswer(DetallePedidoViewModel.TITULO_BORRAR_PEDIDO, A<string>.That.Contains("930001"), true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.BorrarPedido(A<string>._, A<int>._, A<bool>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task DiceQueSi_LoBorraYAvisaALasListas()
        {
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, true)).Returns(true);
            PedidoBorradoMensaje recibido = null;
            messenger.Register<PedidoBorradoMensaje>(this, (r, m) => recibido = m);

            await vm.BorrarPedidoAsync();

            A.CallTo(() => servicio.BorrarPedido("1", 930001, false)).MustHaveHappenedOnceExactly();
            Assert.IsNotNull(recibido);
            Assert.AreEqual(930001, recibido.Numero);
            Assert.AreEqual("1", recibido.Empresa);
        }

        [TestMethod]
        public async Task LaApiLoRechaza_SeEnsenaSuMensajeTalCual_YNoSeAvisa()
        {
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, true)).Returns(true);
            A.CallTo(() => servicio.BorrarPedido(A<string>._, A<int>._, A<bool>._)).ThrowsAsync(new Exception("El pedido 930001 tiene picking: no se puede borrar."));
            bool avisado = false;
            messenger.Register<PedidoBorradoMensaje>(this, (r, m) => avisado = true);

            await vm.BorrarPedidoAsync();

            A.CallTo(() => dialogos.ShowError("El pedido 930001 tiene picking: no se puede borrar.")).MustHaveHappenedOnceExactly();
            Assert.IsFalse(avisado);
            Assert.IsTrue(vm.PuedeBorrarPedido, "Se puede volver a intentar");
        }

        [TestMethod]
        public async Task JustificaInscripciones_SePreguntaYSeRepiteConelParametro()
        {
            const string PREGUNTA = "El pedido 930001 justifica la inscripción de 15191/0 en «Lifting Coreano». ¿Quiere seguir?";
            A.CallTo(() => dialogos.ShowConfirmationAnswer(DetallePedidoViewModel.TITULO_BORRAR_PEDIDO, A<string>._, true)).Returns(true);
            A.CallTo(() => dialogos.ShowConfirmationAnswer(InscripcionesEventoAnularException.TITULO, PREGUNTA, true)).Returns(true);
            A.CallTo(() => servicio.BorrarPedido("1", 930001, false)).ThrowsAsync(new InscripcionesEventoAnularException(PREGUNTA));

            await vm.BorrarPedidoAsync();

            A.CallTo(() => servicio.BorrarPedido("1", 930001, true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task JustificaInscripciones_DiceQueNo_NoSeBorra()
        {
            A.CallTo(() => dialogos.ShowConfirmationAnswer(DetallePedidoViewModel.TITULO_BORRAR_PEDIDO, A<string>._, true)).Returns(true);
            A.CallTo(() => servicio.BorrarPedido("1", 930001, false)).ThrowsAsync(new InscripcionesEventoAnularException("¿Quiere seguir?"));

            await vm.BorrarPedidoAsync();

            A.CallTo(() => servicio.BorrarPedido("1", 930001, true)).MustNotHaveHappened();
        }

        [TestMethod]
        public void Ruta_YErrores()
        {
            Assert.AreEqual("PedidosVenta?empresa=1&numero=930001", PedidoVentaService.RutaBorrarPedido("1  ", 930001, false));
            Assert.AreEqual("PedidosVenta?empresa=1&numero=930001&anularInscripcionesEventos=true", PedidoVentaService.RutaBorrarPedido("1", 930001, true));
            StringAssert.Contains(PedidoVentaService.ErrorBorrarPedido(404, "", 930001).Message, "ya no existe");
            Assert.AreEqual("No se puede borrar: tiene albarán.", PedidoVentaService.ErrorBorrarPedido(400, "{\"Message\":\"No se puede borrar: tiene albarán.\"}", 930001).Message);
            Assert.IsInstanceOfType(PedidoVentaService.ErrorBorrarPedido(409,
                "{\"error\":{\"code\":\"PEDIDO_JUSTIFICA_INSCRIPCIONES_EVENTO\",\"message\":\"¿Quiere seguir?\"}}", 930001), typeof(InscripcionesEventoAnularException));
        }

        [TestMethod]
        public void LaLista_QuitaElPedidoBorrado_YDejaDeElegirlo()
        {
            var servicioLista = A.Fake<IPedidoVentaService>();
            var navegacion = A.Fake<IServicioNavegacion>();
            var lista = new ListaPedidosVentaViewModel(A.Fake<IConfiguracion>(), servicioLista, messenger, A.Fake<IServicioDialogos>(), A.Fake<IServicioNavegacion>());
            lista.NavegacionAmbito = navegacion;
            A.CallTo(() => servicioLista.cargarListaPedidos(A<string>._, A<bool>._, A<bool>._)).Returns(new ObservableCollection<ResumenPedido>
            {
                new ResumenPedido { empresa = "1", numero = 930001 },
                new ResumenPedido { empresa = "1", numero = 930002 }
            });
            lista.cmdCargarListaPedidos.Execute(null);
            lista.ListaPedidos.ElementoSeleccionado = lista.ListaPedidos.Lista.First();

            messenger.Send(new PedidoBorradoMensaje("1", 930001));

            CollectionAssert.AreEqual(new[] { 930002 }, lista.ListaPedidos.Lista.Cast<ResumenPedido>().Select(p => p.numero).ToArray());
            Assert.IsFalse(lista.ListaPedidos.ListaOriginal.Cast<ResumenPedido>().Any(p => p.numero == 930001));
            Assert.IsNull(lista.ListaPedidos.ElementoSeleccionado);
            A.CallTo(() => navegacion.QuitarVistas("DetallePedidoRegion")).MustHaveHappened();
        }
    }
}
