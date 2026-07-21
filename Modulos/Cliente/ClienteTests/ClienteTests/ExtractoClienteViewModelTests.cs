using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#419 v1: ventana de Extracto de Cliente — consultar pendientes y liquidar dos
    /// movimientos (NestoAPI#333). La lógica de negocio vive en la API; el VM pinta y pide.
    /// </summary>
    [TestClass]
    public class ExtractoClienteViewModelTests
    {
        private readonly IExtractoClienteService servicio;
        private readonly IDialogService dialogService;
        private bool respuestaConfirmacion = true;
        private readonly List<string> mensajesDialogo = new List<string>();

        public ExtractoClienteViewModelTests()
        {
            servicio = A.Fake<IExtractoClienteService>();
            dialogService = A.Fake<IDialogService>();
            // ShowConfirmationAnswer/ShowError/ShowNotification son extensiones sobre
            // ShowDialog: se interceptan aquí (patrón DetallePedidoViewModelConfirmacionTests).
            A.CallTo(() => dialogService.ShowDialog(
                    A<string>.Ignored, A<IDialogParameters>.Ignored, A<Action<IDialogResult>>.Ignored))
                .Invokes((string nombre, IDialogParameters parametros, Action<IDialogResult> callback) =>
                {
                    if (parametros != null && parametros.ContainsKey("message"))
                    {
                        mensajesDialogo.Add(parametros.GetValue<string>("message"));
                    }
                    if (callback == null)
                    {
                        return;
                    }
                    IDialogResult resultado = A.Fake<IDialogResult>();
                    A.CallTo(() => resultado.Result)
                        .Returns(respuestaConfirmacion ? ButtonResult.OK : ButtonResult.Cancel);
                    callback(resultado);
                });
        }

        private ExtractoClienteViewModel CrearViewModel() => new ExtractoClienteViewModel(servicio, dialogService);

        private static ExtractoClienteModel Movimiento(int id, decimal pendiente, string empresa = "1",
            bool seleccionado = false)
        {
            return new ExtractoClienteModel
            {
                Id = id,
                Empresa = empresa,
                Cliente = "15191",
                ImportePendiente = pendiente,
                Seleccionado = seleccionado
            };
        }

        [TestMethod]
        public async Task Cargar_PoblaLosMovimientosPendientesDelCliente()
        {
            A.CallTo(() => servicio.LeerExtractoPendiente("15191")).Returns(new List<ExtractoClienteModel>
            {
                Movimiento(1, 500m),
                Movimiento(2, -200m)
            });
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = "15191";

            await vm.CargarAsync();

            Assert.AreEqual(2, vm.Movimientos.Count);
            Assert.AreEqual(300m, vm.TotalPendiente);
        }

        [TestMethod]
        public async Task Cargar_SiElServicioFalla_GridVacioYAviso()
        {
            A.CallTo(() => servicio.LeerExtractoPendiente(A<string>.Ignored)).Throws(new Exception("API caída"));
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = "15191";

            await vm.CargarAsync();

            Assert.AreEqual(0, vm.Movimientos.Count);
            Assert.IsTrue(mensajesDialogo.Any(m => m.Contains("API caída")));
        }

        [TestMethod]
        public async Task Liquidar_SoloSePuedeConExactamenteDosSeleccionados()
        {
            A.CallTo(() => servicio.LeerExtractoPendiente("15191")).Returns(new List<ExtractoClienteModel>
            {
                Movimiento(1, 500m),
                Movimiento(2, -200m),
                Movimiento(3, 100m)
            });
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = "15191";
            await vm.CargarAsync();

            Assert.IsFalse(vm.LiquidarCommand.CanExecute());

            vm.Movimientos[0].Seleccionado = true;
            Assert.IsFalse(vm.LiquidarCommand.CanExecute(), "Con uno solo no se puede liquidar");

            vm.Movimientos[1].Seleccionado = true;
            Assert.IsTrue(vm.LiquidarCommand.CanExecute(), "Con dos sí (el CanExecute se refresca al marcar)");

            vm.Movimientos[2].Seleccionado = true;
            Assert.IsFalse(vm.LiquidarCommand.CanExecute(), "Con tres no");
        }

        [TestMethod]
        public async Task Liquidar_ConfirmadoPorElUsuario_LlamaAlServicioYRecarga()
        {
            A.CallTo(() => servicio.LeerExtractoPendiente("15191")).Returns(new List<ExtractoClienteModel>
            {
                Movimiento(111, 500m, seleccionado: true),
                Movimiento(222, -200m, seleccionado: true)
            });
            _ = A.CallTo(() => servicio.LiquidarEfectos("1", 111, 222))
                .Returns(new ResultadoLiquidacionModel { Exito = true, ImportePdteOrigen = 300m, ImportePdteDestino = 0m });
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = "15191";
            await vm.CargarAsync();

            await vm.LiquidarAsync();

            A.CallTo(() => servicio.LiquidarEfectos("1", 111, 222)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.LeerExtractoPendiente("15191")).MustHaveHappenedTwiceExactly(); // carga + recarga
        }

        [TestMethod]
        public async Task Liquidar_SiElUsuarioCancela_NoLlamaAlServicio()
        {
            respuestaConfirmacion = false;
            A.CallTo(() => servicio.LeerExtractoPendiente("15191")).Returns(new List<ExtractoClienteModel>
            {
                Movimiento(111, 500m, seleccionado: true),
                Movimiento(222, -200m, seleccionado: true)
            });
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = "15191";
            await vm.CargarAsync();

            await vm.LiquidarAsync();

            A.CallTo(() => servicio.LiquidarEfectos(A<string>.Ignored, A<int>.Ignored, A<int>.Ignored))
                .MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Liquidar_MovimientosDeDistintaEmpresa_AvisaYNoLlama()
        {
            A.CallTo(() => servicio.LeerExtractoPendiente("15191")).Returns(new List<ExtractoClienteModel>
            {
                Movimiento(111, 500m, empresa: "1", seleccionado: true),
                Movimiento(222, -200m, empresa: "3", seleccionado: true)
            });
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = "15191";
            await vm.CargarAsync();

            await vm.LiquidarAsync();

            Assert.IsTrue(mensajesDialogo.Any(m => m.Contains("misma empresa")));
            A.CallTo(() => servicio.LiquidarEfectos(A<string>.Ignored, A<int>.Ignored, A<int>.Ignored))
                .MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Liquidar_SiLaApiRechaza_MuestraElMotivoYNoRevienta()
        {
            A.CallTo(() => servicio.LeerExtractoPendiente("15191")).Returns(new List<ExtractoClienteModel>
            {
                Movimiento(111, 500m, seleccionado: true),
                Movimiento(222, -200m, seleccionado: true)
            });
            A.CallTo(() => servicio.LiquidarEfectos(A<string>.Ignored, A<int>.Ignored, A<int>.Ignored))
                .Throws(new Exception("Ambos movimientos están ya remesados"));
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = "15191";
            await vm.CargarAsync();

            await vm.LiquidarAsync();

            Assert.IsTrue(mensajesDialogo.Any(m => m.Contains("remesados")));
        }
    }
}
