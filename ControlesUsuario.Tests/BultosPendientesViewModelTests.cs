using ControlesUsuario.BultosPendientes;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// NestoAPI#880 (corte 2): los bultos que faltaron en una entrega incompleta, con «Cerrar…» (elige el cierre y pide el motivo). En
    /// Agencias y como ventana, todos; en la pestaña «Entrega» del pedido, los suyos y solo si hay alguno.
    /// </summary>
    [TestClass]
    public class BultosPendientesViewModelTests
    {
        private readonly IServicioBultosPendientes servicio = A.Fake<IServicioBultosPendientes>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();

        public BultosPendientesViewModelTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>._)).Invokes((string m) => errores.Add(m));
        }

        private static BultoPendienteModel Bulto(int id, string estado = "Pendiente") => new()
        {
            Id = id, Empresa = "1", EnvioId = 251099, Pedido = 930942, Codigo = "RP251099-5", Bulto = 5, Total = 5,
            Estado = estado, Texto = "RP251099-5 del pedido 930942"
        };

        private BultosPendientesViewModel Vm() => new(servicio, dialogos);

        [TestMethod]
        public async Task AlLlegar_LeeTodosLosPendientes()
        {
            A.CallTo(() => servicio.Listar(true, null, null, null)).Returns(new List<BultoPendienteModel> { Bulto(1) });
            BultosPendientesViewModel vm = Vm();

            await vm.CargarAsync(null, null);

            Assert.AreEqual(1, vm.Bultos.Count);
            Assert.IsTrue(vm.SeVe);
        }

        [TestMethod]
        public async Task DelPedido_TodosLosSuyos_YSinNingunoNoSeVe()
        {
            A.CallTo(() => servicio.Listar(false, "1", 930942, null)).Returns(new List<BultoPendienteModel>());
            BultosPendientesViewModel vm = Vm();

            await vm.CargarAsync("1", 930942);

            A.CallTo(() => servicio.Listar(false, "1", 930942, null)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.SeVe);
        }

        [TestMethod]
        public async Task PedidoNuevo_NoLeeNadaNiSeVe()
        {
            BultosPendientesViewModel vm = Vm();

            await vm.CargarAsync("1", 0);

            A.CallTo(() => servicio.Listar(A<bool>._, A<string>._, A<int?>._, A<int?>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.SeVe);
        }

        [TestMethod]
        public async Task Sin403O503_EnElPedidoNoSePinta()
        {
            A.CallTo(() => servicio.Listar(A<bool>._, A<string>._, A<int?>._, A<int?>._))
                .Throws(new BultosPendientesApiException(503, ServicioBultosPendientes.MENSAJE_SIN_TABLA));
            BultosPendientesViewModel vm = Vm();

            await vm.CargarAsync("1", 930942);

            Assert.IsFalse(vm.Disponible);
            Assert.IsFalse(vm.SeVe);
            Assert.AreEqual(0, errores.Count, "Sin avisos");
        }

        [TestMethod]
        public async Task Cerrar_ConCierreYMotivo_LlamaALaApiYRelee()
        {
            A.CallTo(() => servicio.Listar(A<bool>._, A<string>._, A<int?>._, A<int?>._)).Returns(new List<BultoPendienteModel> { Bulto(17) });
            BultosPendientesViewModel vm = Vm();
            await vm.CargarAsync(null, null);
            vm.Seleccionado = vm.Bultos.Single();

            Assert.IsTrue(vm.AbrirCierreCommand.CanExecute(null));
            vm.AbrirCierreCommand.Execute(null);
            Assert.IsFalse(vm.CerrarBultoCommand.CanExecute(null), "Sin cierre ni motivo, no");
            vm.CierreElegido = ReglasBultosPendientes.CIERRE_ENTREGADO_EN_MANO;
            vm.MotivoCierre = "Se lo llevó Alfredo el jueves";
            Assert.IsTrue(vm.CerrarBultoCommand.CanExecute(null));

            await vm.CerrarBultoAsync();

            A.CallTo(() => servicio.Cerrar(17, "EntregadoEnMano", "Se lo llevó Alfredo el jueves")).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.Listar(A<bool>._, A<string>._, A<int?>._, A<int?>._)).MustHaveHappenedTwiceExactly();
            Assert.IsFalse(vm.MostrandoCierre);
        }

        [TestMethod]
        public async Task Cerrar_403_SeEnsenaSuMensaje()
        {
            A.CallTo(() => servicio.Listar(A<bool>._, A<string>._, A<int?>._, A<int?>._)).Returns(new List<BultoPendienteModel> { Bulto(17) });
            A.CallTo(() => servicio.Cerrar(A<int>._, A<string>._, A<string>._))
                .Throws(new BultosPendientesApiException(403, "Solo Almacén, Dirección e Informática pueden cerrarlo."));
            BultosPendientesViewModel vm = Vm();
            await vm.CargarAsync(null, null);
            vm.Seleccionado = vm.Bultos.Single();
            vm.AbrirCierreCommand.Execute(null);
            vm.CierreElegido = ReglasBultosPendientes.CIERRE_PERDIDO;
            vm.MotivoCierre = "No aparece";

            await vm.CerrarBultoAsync();

            Assert.AreEqual("Solo Almacén, Dirección e Informática pueden cerrarlo.", errores.Single());
            Assert.IsTrue(vm.MostrandoCierre, "Sigue abierto para corregir");
        }

        [TestMethod]
        public async Task UnoCerrado_NoSePuedeVolverACerrar()
        {
            A.CallTo(() => servicio.Listar(A<bool>._, A<string>._, A<int?>._, A<int?>._)).Returns(new List<BultoPendienteModel> { Bulto(17, "Cerrado") });
            BultosPendientesViewModel vm = Vm();
            await vm.CargarAsync(null, null);
            vm.Seleccionado = vm.Bultos.Single();

            Assert.IsFalse(vm.AbrirCierreCommand.CanExecute(null));
        }

        [TestMethod]
        public void Servicio_Url_Y_503()
        {
            Assert.AreEqual("RepartoPropio/BultosPendientes?soloPendientes=false&empresa=1&pedido=930942",
                ServicioBultosPendientes.UrlListar(false, "1", 930942, null));
            var ex = Assert.ThrowsException<BultosPendientesApiException>(() => ServicioBultosPendientes.Comprobar(HttpStatusCode.ServiceUnavailable, "{\"Message\":\"x\"}"));
            Assert.AreEqual(ServicioBultosPendientes.MENSAJE_SIN_TABLA, ex.Message);
            var ex403 = Assert.ThrowsException<BultosPendientesApiException>(() => ServicioBultosPendientes.Comprobar(HttpStatusCode.Forbidden, "{\"Message\":\"De usted\"}"));
            Assert.AreEqual("De usted", ex403.Message);
        }

        [TestMethod]
        public void TextoEstado_ConCierreYMotivo()
        {
            var b = Bulto(1, "Cerrado");
            b.Cierre = "EntregadoEnMano";
            b.MotivoCierre = "Se lo llevó Alfredo";

            Assert.AreEqual("Entregado en mano: Se lo llevó Alfredo", b.TextoEstado);
            Assert.AreEqual("5 de 5", b.TextoBulto);
        }
    }
}
