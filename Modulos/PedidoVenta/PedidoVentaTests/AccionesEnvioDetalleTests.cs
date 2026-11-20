using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Carlos 20/11/26: en la pestaña «Entrega» del pedido, «Añadir nota» a un envío (el mismo paso que Agencias › Tramitados).
    /// </summary>
    [TestClass]
    public class AccionesEnvioDetalleTests
    {
        private static (DetallePedidoViewModel vm, IServicioNotasEnvio notas, IServicioDialogos dialogos) Vm()
        {
            var dialogos = A.Fake<IServicioDialogos>();
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(), new WeakReferenceMessenger(),
                dialogos, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            var notas = A.Fake<IServicioNotasEnvio>();
            vm.ServicioDeNotasEnvio = notas;
            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", numero = 930553, cliente = "15191", contacto = "0" });
            return (vm, notas, dialogos);
        }

        [TestMethod]
        public async Task AnadirNota_ConTexto_LaGuardaYLoDice()
        {
            var (vm, notas, dialogos) = Vm();
            A.CallTo(() => dialogos.GetText(AnotadorEnvios.TITULO, A<string>.That.Contains("930553"))).Returns(" Avisar al cliente ");

            await vm.AnadirNotaEnvioAsync(new PedidoVentaModel.EnvioAgenciaDTO { Numero = 251104, Estado = 1 });

            A.CallTo(() => notas.Anadir(251104, "Avisar al cliente")).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowNotification(AnotadorEnvios.TITULO, A<string>.That.Contains("251104"))).MustHaveHappened();
        }

        private static (DetallePedidoViewModel vm, IServicioRecogidasReparto recogidas, IServicioDialogos dialogos, IPedidoVentaService servicio) VmNotaRecogida(bool almacen = true)
        {
            var dialogos = A.Fake<IServicioDialogos>();
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(Nesto.Infrastructure.Shared.Constantes.GruposSeguridad.ALMACEN)).Returns(almacen);
            var servicio = A.Fake<IPedidoVentaService>();
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), configuracion, servicio, new WeakReferenceMessenger(),
                dialogos, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            var recogidas = A.Fake<IServicioRecogidasReparto>();
            vm.ServicioDeRecogidasReparto = recogidas;
            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO
            {
                empresa = "1", numero = 930553, cliente = "15191", contacto = "0", notaEntrega = true, tipoNotaEntrega = "Recogida"
            });
            vm.ListaEnlacesSeguimiento = new System.Collections.Generic.List<PedidoVentaModel.EnvioAgenciaDTO>
            {
                new PedidoVentaModel.EnvioAgenciaDTO { Numero = 251104, AgenciaId = 14, Estado = 1 }
            };
            return (vm, recogidas, dialogos, servicio);
        }

        [TestMethod]
        public async Task AnularRecogida_NotaDeRecogida_ConMotivo_AnulaYReleeLosEnvios()
        {
            var (vm, recogidas, dialogos, servicio) = VmNotaRecogida();
            Assert.IsTrue(vm.PuedeAnularRecogidaNota);
            A.CallTo(() => dialogos.GetText(AnuladorRecogidas.TITULO, A<string>.That.Contains("NR930553 (parada 251104)"))).Returns("Ya no hace falta");
            A.CallTo(() => dialogos.ShowConfirmationAnswer(AnuladorRecogidas.TITULO, A<string>._, true)).Returns(true);

            await vm.AnularRecogidaNotaAsync();

            A.CallTo(() => recogidas.Anular(930553, "Ya no hace falta")).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.CargarEnlacesSeguimiento("1", 930553)).MustHaveHappened();
        }

        [TestMethod]
        public void AnularRecogida_SinGrupoOSinSerRecogida_NoSeOfrece()
        {
            var (sinGrupo, _, _, _) = VmNotaRecogida(almacen: false);
            Assert.IsFalse(sinGrupo.PuedeAnularRecogidaNota);

            var (vm, _, _) = Vm();
            Assert.IsFalse(vm.PuedeAnularRecogidaNota, "un pedido normal");
        }

        [TestMethod]
        public async Task AnadirNota_SinEnvio_NoPregunta()
        {
            var (vm, notas, dialogos) = Vm();

            await vm.AnadirNotaEnvioAsync(null);

            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => notas.Anadir(A<int>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task AnadirNota_ServidorSinActualizar_LoDiceClaro()
        {
            var (vm, notas, dialogos) = Vm();
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("nota");
            A.CallTo(() => notas.Anadir(A<int>._, A<string>._)).ThrowsAsync(ErroresApiReparto.Excepcion(503, null, null));

            await vm.AnadirNotaEnvioAsync(new PedidoVentaModel.EnvioAgenciaDTO { Numero = 251104 });

            A.CallTo(() => dialogos.ShowError(ErroresApiReparto.MENSAJE_SERVIDOR_SIN_ACTUALIZAR)).MustHaveHappenedOnceExactly();
        }
    }
}
