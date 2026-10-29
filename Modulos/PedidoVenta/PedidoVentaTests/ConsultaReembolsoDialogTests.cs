using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#568 (NestoAPI#734): Administración contesta desde la campana si un pedido retenido por el almacén sale con o sin
    /// reembolso. Las reglas (no tarjeta ni prepago sin pagar, forma que sigue siendo contra reembolso…) las dice la API.
    /// </summary>
    [TestClass]
    public class ConsultaReembolsoDialogTests
    {
        private readonly IConsultaReembolsoService servicio = A.Fake<IConsultaReembolsoService>();

        private static ConsultaReembolsoListadoModel Abierta() => new ConsultaReembolsoListadoModel
        {
            Id = 3, Empresa = "1", Pedido = 929300, ImporteEnlaces = 252.94M, NumeroEnlaces = 1, Usuario = "Alfredo",
            Fecha = new DateTime(2026, 10, 29, 10, 15, 0), Cliente = "40026", Contacto = "0", NombreCliente = "CLIENTA", Abierta = true,
            EnlacesPendientes = new List<EnlacePagoPendienteModel>
            {
                new EnlacePagoPendienteModel { Id = 900, Importe = 252.94M, FechaCreacion = new DateTime(2026, 10, 20), Descripcion = "Pago pedido 929300 de Nueva Visión" }
            }
        };

        private async Task<ConsultaReembolsoDialogViewModel> Dialogo(ConsultaReembolsoListadoModel abierta = null)
        {
            A.CallTo(() => servicio.LeerConsultas("1", true)).Returns(new List<ConsultaReembolsoListadoModel> { abierta ?? Abierta() });
            var vm = new ConsultaReembolsoDialogViewModel(servicio, A.Fake<IConfiguracion>());
            vm.Empresa = "1";
            vm.Pedido = 929300;
            await vm.CargarAsync();
            return vm;
        }

        [TestMethod]
        public async Task Abrir_EnsenaQuienPreguntoElClienteYLosEnlaces()
        {
            var vm = await Dialogo();

            StringAssert.Contains(vm.TextoConsulta, "Alfredo retuvo el pedido 929300 del cliente 40026/0 (CLIENTA)");
            Assert.AreEqual("40026", vm.Cliente);
            StringAssert.Contains(vm.Enlaces[0].Texto, "Pago pedido 929300 de Nueva Visión");
            Assert.IsTrue(vm.HayConsultaAbierta);
            Assert.IsTrue(vm.SaleConReembolso, "Por defecto, con reembolso (lo de siempre)");
            Assert.IsTrue(vm.ResponderCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task ConReembolso_YAnularElEnlace_LoMandaYEnsenaElResultado()
        {
            A.CallTo(() => servicio.Responder("1", 929300, A<ResponderConsultaReembolsoModel>.Ignored)).Returns(new RespuestaConsultaReembolsoModel
            {
                Consulta = new ConsultaReembolsoModel { ConReembolso = true }, EnlacesAnulados = new List<int> { 900 }, Liberado = true
            });
            var vm = await Dialogo();

            vm.AnularEnlaces = true;
            vm.Comentario = " Lo pactó Paloma ";
            await vm.ResponderAsync();

            A.CallTo(() => servicio.Responder("1", 929300, A<ResponderConsultaReembolsoModel>.That.Matches(r =>
                r.ConReembolso == true && r.AnularEnlaces == true && r.FormaPago == null && r.Comentario == "Lo pactó Paloma"))).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.Contestada);
            StringAssert.Contains(vm.TextoResultado, "sale con reembolso");
            StringAssert.Contains(vm.TextoResultado, "Se ha anulado el enlace");
            StringAssert.Contains(vm.TextoResultado, "Ya no está retenido");
            Assert.IsFalse(vm.ResponderCommand.CanExecute(null), "Contestada, no se contesta otra vez");
        }

        [TestMethod]
        public async Task SinReembolso_SinFormaDePago_NoLlamaYLoPide()
        {
            var vm = await Dialogo();

            vm.SaleSinReembolso = true;
            await vm.ResponderAsync();

            StringAssert.Contains(vm.Mensaje, "indique la forma de pago pactada");
            A.CallTo(() => servicio.Responder(A<string>.Ignored, A<int>.Ignored, A<ResponderConsultaReembolsoModel>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task SinReembolso_ConFormaYPlazos_LosMandaSinAnularEnlaces()
        {
            A.CallTo(() => servicio.Responder("1", 929300, A<ResponderConsultaReembolsoModel>.Ignored)).Returns(new RespuestaConsultaReembolsoModel
            {
                Consulta = new ConsultaReembolsoModel { ConReembolso = false, FormaPago = "RCB", PlazosPago = "1/30" }, Liberado = true,
                Avisos = new List<string> { "Ya hay un envío en reparto con reembolso: cámbielo en Agencias." }
            });
            var vm = await Dialogo();

            vm.AnularEnlaces = true; // marcada con «con reembolso» y luego cambia de opinión: no cuenta
            vm.SaleSinReembolso = true;
            vm.FormaPago = "RCB";
            vm.PlazosPago = "1/30";
            await vm.ResponderAsync();

            A.CallTo(() => servicio.Responder("1", 929300, A<ResponderConsultaReembolsoModel>.That.Matches(r =>
                r.ConReembolso == false && r.AnularEnlaces == null && r.FormaPago == "RCB" && r.PlazosPago == "1/30"))).MustHaveHappenedOnceExactly();
            StringAssert.Contains(vm.TextoResultado, "sale sin reembolso (RCB, 1/30)");
            StringAssert.Contains(vm.TextoResultado, "cámbielo en Agencias");
        }

        [TestMethod]
        public async Task SinReembolso_ConTarjetaSinPagar_EnsenaEl400DeLaApiTalCual()
        {
            const string error = "El pedido no está pagado: no puede salir como tarjeta o prepago.";
            A.CallTo(() => servicio.Responder(A<string>.Ignored, A<int>.Ignored, A<ResponderConsultaReembolsoModel>.Ignored)).ThrowsAsync(new Exception(error));
            var vm = await Dialogo();

            vm.SaleSinReembolso = true;
            vm.FormaPago = "TAR";
            await vm.ResponderAsync();

            Assert.AreEqual(error, vm.Mensaje);
            Assert.IsFalse(vm.Contestada);
            Assert.IsTrue(vm.ResponderCommand.CanExecute(null), "Puede elegir otra forma y volver a contestar");
        }

        [TestMethod]
        public async Task YaContestada_LoDiceYNoDejaContestar()
        {
            A.CallTo(() => servicio.LeerConsultas("1", true)).Returns(new List<ConsultaReembolsoListadoModel>());
            A.CallTo(() => servicio.LeerConsulta("1", 929300)).Returns(new ConsultaReembolsoModel
            {
                Pedido = 929300, Respondida = new DateTime(2026, 10, 29), UsuarioRespuesta = "Paloma", ConReembolso = true
            });
            var vm = new ConsultaReembolsoDialogViewModel(servicio, A.Fake<IConfiguracion>()) { Empresa = "1", Pedido = 929300 };

            await vm.CargarAsync();

            StringAssert.Contains(vm.TextoResultado, "Administración (Paloma) contestó el 29/10/2026: sale con reembolso.");
            Assert.IsFalse(vm.ResponderCommand.CanExecute(null));
        }

        [TestMethod]
        public void ExtraerMensaje_DelCuerpoDeWebApi()
        {
            Assert.AreEqual("Solo Administración o Dirección pueden contestar.", ConsultaReembolso.ExtraerMensaje("{\"Message\":\"Solo Administración o Dirección pueden contestar.\"}", "x"));
            Assert.AreEqual("La consulta ya está contestada.", ConsultaReembolso.ExtraerMensaje("{\"error\":{\"code\":\"X\",\"message\":\"La consulta ya está contestada.\"}}", "x"));
            Assert.AreEqual("x", ConsultaReembolso.ExtraerMensaje("", "x"));
        }
    }
}
