using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Carlos 26/11/26: sacar o quitar el picking cambia la versión del pedido, y la banda decía «Carlos ha modificado este pedido
    /// a las 10:58» cuando había sido Almacén. Con la misma HuellaContenido, la banda dice lo que ha pasado con el picking; con otra
    /// (o con la API antigua, sin huella), la de siempre.
    /// </summary>
    [TestClass]
    public class PedidoCambiadoPorPickingTests
    {
        private const int NUMERO = 929555;
        private const string HUELLA = "0123456789ABCDEF0123456789ABCDEF";
        private static readonly DateTime HOY = new DateTime(2026, 11, 26);
        private static readonly DateTime A_LAS_1058 = new DateTime(2026, 11, 26, 10, 58, 0);

        private static PedidoVentaDTO Pedido() => new PedidoVentaDTO
        {
            empresa = "1",
            numero = NUMERO,
            cliente = "15191",
            contacto = "0",
            Version = "v1",
            Lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { id = 1, Producto = "38093", Cantidad = 2, almacen = "ALG", tipoLinea = 1, fechaEntrega = HOY }
            }
        };

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IServicioDialogos dialogos = null)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogos ?? A.Fake<IServicioDialogos>(), A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.pedido = new PedidoVentaWrapper(Pedido());
            vm.MarcarComoGuardado();
            return vm;
        }

        private static IPedidoVentaService ServicioConHuella(string huella = HUELLA)
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.LeerVersionPedido("1", NUMERO)).Returns(new VersionPedidoModel
            {
                Version = "v1", Usuario = "Carlos", Fecha = A_LAS_1058, Pickings = new List<int>(), HuellaContenido = huella
            });
            return servicio;
        }

        private static async Task<DetallePedidoViewModel> VmConHuella(string huella = HUELLA)
        {
            DetallePedidoViewModel vm = Vm(ServicioConHuella(huella));
            vm.AlCambiarElPedidoEnPantalla(); // como al cargarlo
            await vm.RecordarHuellaEnCurso;
            return vm;
        }

        // ---------- El texto ----------

        [TestMethod]
        public void Texto_MismaHuellaYPickingSacado_DiceQuienHaSacadoElPicking()
        {
            string texto = DetallePedidoViewModel.TextoBandaSegunCambio(HUELLA, new int[0], "Almacén", A_LAS_1058, HUELLA, new[] { 100123 }, "PickingSacado", HOY);

            Assert.AreEqual("Almacén ha sacado el picking 100123 de este pedido. Recárguelo antes de guardar.", texto);
        }

        [TestMethod]
        public void Texto_SinUsuario_EsAlmacen()
        {
            string texto = DetallePedidoViewModel.TextoBandaSegunCambio(HUELLA, new int[0], null, null, HUELLA, new[] { 100123 }, "PickingSacado", HOY);

            Assert.AreEqual("Almacén ha sacado el picking 100123 de este pedido. Recárguelo antes de guardar.", texto);
        }

        [TestMethod]
        public void Texto_PickingQuitado_DiceQueLoHaQuitado()
        {
            string texto = DetallePedidoViewModel.TextoBandaSegunCambio(HUELLA, new[] { 100123 }, "Paloma", A_LAS_1058, HUELLA, new int[0], "PickingQuitado", HOY);

            Assert.AreEqual("Paloma ha quitado el picking 100123 de este pedido. Recárguelo antes de guardar.", texto);
        }

        [TestMethod]
        public void Texto_SinMotivo_LoDeduceDeLosPickings()
        {
            Assert.AreEqual("Almacén ha sacado los pickings 100124 y 100125 de este pedido. Recárguelo antes de guardar.",
                DetallePedidoViewModel.TextoBandaSegunCambio(HUELLA, new[] { 100123 }, null, null, HUELLA, new[] { 100123, 100124, 100125 }, null, HOY));
            Assert.AreEqual("Almacén ha quitado el picking 100123 de este pedido. Recárguelo antes de guardar.",
                DetallePedidoViewModel.TextoBandaSegunCambio(HUELLA, new[] { 100123 }, null, null, HUELLA, new int[0], null, HOY));
        }

        [TestMethod]
        public void Texto_OtraHuella_ElDeSiempre()
        {
            string texto = DetallePedidoViewModel.TextoBandaSegunCambio(HUELLA, new int[0], "Aida", A_LAS_1058, "OTRA", new[] { 100123 }, "PickingSacado", HOY);

            Assert.AreEqual("Aida ha modificado este pedido a las 10:58. Recárguelo antes de guardar.", texto);
        }

        [TestMethod]
        public void Texto_ApiAntiguaSinHuella_ElDeSiempre()
        {
            Assert.AreEqual("Aida ha modificado este pedido a las 10:58. Recárguelo antes de guardar.",
                DetallePedidoViewModel.TextoBandaSegunCambio(null, null, "Aida", A_LAS_1058, null, null, null, HOY));
            Assert.AreEqual("Aida ha modificado este pedido a las 10:58. Recárguelo antes de guardar.",
                DetallePedidoViewModel.TextoBandaSegunCambio(HUELLA, null, "Aida", A_LAS_1058, null, null, "PickingSacado", HOY));
        }

        [TestMethod]
        public void Texto_MismaHuellaYMismosPickingsSinMotivo_ElDeSiempre()
        {
            string texto = DetallePedidoViewModel.TextoBandaSegunCambio(HUELLA, new[] { 100123 }, "Aida", A_LAS_1058, HUELLA, new[] { 100123 }, null, HOY);

            Assert.AreEqual("Aida ha modificado este pedido a las 10:58. Recárguelo antes de guardar.", texto);
        }

        // ---------- En el detalle ----------

        [TestMethod]
        public async Task AlCargar_PideLaHuellaUnaVez()
        {
            DetallePedidoViewModel vm = await VmConHuella();

            Assert.AreEqual(HUELLA, vm.HuellaContenidoConocida);
            Assert.IsFalse(vm.PedidoCambiadoPorOtro);
        }

        [TestMethod]
        public async Task Aviso_PickingSacadoConLaMismaHuella_LaBandaLoDice()
        {
            DetallePedidoViewModel vm = await VmConHuella();

            vm.ProcesarPedidoCambiado(new PedidoCambiadoAviso
            {
                Empresa = "1", Pedido = NUMERO, Usuario = "Almacén", Fecha = A_LAS_1058, Version = "v2",
                Pickings = new List<int> { 100123 }, HuellaContenido = HUELLA, Motivo = "PickingSacado"
            });

            Assert.IsTrue(vm.PedidoCambiadoPorOtro);
            Assert.AreEqual("Almacén ha sacado el picking 100123 de este pedido. Recárguelo antes de guardar.", vm.TextoPedidoCambiadoPorOtro);
        }

        [TestMethod]
        public async Task Aviso_ConOtraHuella_LaBandaDeSiempre()
        {
            DetallePedidoViewModel vm = await VmConHuella();

            vm.ProcesarPedidoCambiado(new PedidoCambiadoAviso
            {
                Empresa = "1", Pedido = NUMERO, Usuario = "Aida", Fecha = DateTime.Today.AddHours(15).AddMinutes(14), Version = "v2",
                Pickings = new List<int>(), HuellaContenido = "OTRA"
            });

            Assert.AreEqual("Aida ha modificado este pedido a las 15:14. Recárguelo antes de guardar.", vm.TextoPedidoCambiadoPorOtro);
        }

        [TestMethod]
        public async Task ApiAntigua_SinHuella_LaBandaDeSiempre()
        {
            DetallePedidoViewModel vm = await VmConHuella(huella: null);

            vm.ProcesarPedidoCambiado(new PedidoCambiadoAviso { Empresa = "1", Pedido = NUMERO, Usuario = "Aida", Fecha = DateTime.Today.AddHours(15).AddMinutes(14), Version = "v2" });

            Assert.IsNull(vm.HuellaContenidoConocida);
            Assert.AreEqual("Aida ha modificado este pedido a las 15:14. Recárguelo antes de guardar.", vm.TextoPedidoCambiadoPorOtro);
        }

        [TestMethod]
        public async Task Guardar_409PorElPicking_ElMensajeDiceQueSeHaSacadoElPicking()
        {
            IPedidoVentaService servicio = ServicioConHuella();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ThrowsAsync(new PedidoCambiadoException(
                "El pedido 929555 lo ha modificado Carlos a las 10:58 desde que usted lo abrió.", null, A_LAS_1058, new List<int> { 100123 }, HUELLA));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            var mensajes = new List<string>();
            A.CallTo(() => dialogos.ShowDialog(ServicioDialogosBase.ConfirmationDialog, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string nombre, ParametrosDialogo parametros, Action<ResultadoDialogo> callback) =>
                {
                    mensajes.Add(parametros.GetValue<string>("message"));
                    callback?.Invoke(new ResultadoDialogo(ResultadoBoton.Cancel));
                });
            DetallePedidoViewModel vm = Vm(servicio, dialogos);
            vm.AlCambiarElPedidoEnPantalla();
            await vm.RecordarHuellaEnCurso;

            await vm.ModificarPedidoAsync();

            Assert.AreEqual(1, mensajes.Count);
            StringAssert.StartsWith(mensajes[0], "Almacén ha sacado el picking 100123 de este pedido.");
        }

        [TestMethod]
        public void InterpretarRespuestaError_409ConPickingsYHuella()
        {
            string json = "{\"error\":{\"code\":\"PEDIDO_CAMBIADO\",\"message\":\"M\",\"details\":{\"Version\":\"v2\",\"Usuario\":\"Carlos\",\"Fecha\":\"2026-11-26T10:58:00\",\"Pickings\":[100123,100124],\"HuellaContenido\":\"" + HUELLA + "\"}},\"Message\":\"M\"}";

            var ex = PedidoVentaService.InterpretarRespuestaError(json) as PedidoCambiadoException;

            Assert.IsNotNull(ex);
            CollectionAssert.AreEqual(new[] { 100123, 100124 }, ex.Pickings);
            Assert.AreEqual(HUELLA, ex.HuellaContenido);
        }

        [TestMethod]
        public void PickingsDeLineas_SinCerosOrdenadosYSinRepetir()
        {
            var lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { picking = 100124 }, new LineaPedidoVentaDTO { picking = 0 }, new LineaPedidoVentaDTO { picking = 100123 }, new LineaPedidoVentaDTO { picking = 100124 }
            };

            CollectionAssert.AreEqual(new[] { 100123, 100124 }, DetallePedidoViewModel.PickingsDeLineas(lineas));
        }
    }
}
