using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PedidoVenta.Models.Facturas;
using Nesto.Modulos.PedidoVenta.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// NestoAPI#727: la ventana «Facturar fin de mes» llama a POST api/FacturacionRutas/FinDeMes (sustituye al Nesto viejo):
    /// confirmación previa, fecha de corte por defecto el último día del mes anterior, resultado por cliente en la ventana
    /// y solo para Administración, Dirección e Informática.
    /// </summary>
    [TestClass]
    public class FacturarFinDeMesDialogViewModelTests
    {
        private IConfiguracion configuracion;
        private IServicioDialogos dialogos;
        private IServicioFacturacionRutas servicio;

        [TestInitialize]
        public void Inicializar()
        {
            configuracion = A.Fake<IConfiguracion>();
            dialogos = A.Fake<IServicioDialogos>();
            servicio = A.Fake<IServicioFacturacionRutas>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)).Returns(true);
        }

        private FacturarFinDeMesDialogViewModel Crear() => new(configuracion, dialogos, servicio);

        private void Confirma(bool respuesta)
            => A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).Returns(respuesta);

        [TestMethod]
        public void Abre_ConLaFechaDeCorteDeHoy_EntranTodosLosAlbaranes()
        {
            var vm = Crear();

            // Carlos 30/10/26: no hay fecha de corte que elegir (no puede haber albaranes con fecha futura): entran todos, hasta hoy
            Assert.AreEqual(DateTime.Today, vm.FechaCorte);
            Assert.AreEqual(new DateTime(2026, 10, 30), FacturarFinDeMesDialogViewModel.FechaCortePorDefecto(new DateTime(2026, 10, 30, 13, 45, 0)));
            StringAssert.Contains(vm.TextoConfirmacion(), "todos los albaranes de fin de mes de todos los clientes");
            Assert.AreEqual("Facturar fin de mes", vm.Title);
        }

        [TestMethod]
        public async Task ConfirmacionCancelada_NoLlamaALaApi()
        {
            Confirma(false);
            var vm = Crear();

            await vm.FacturarFinDeMesCommand.ExecuteAsync(null);

            A.CallTo(() => servicio.FacturarFinDeMes(A<FacturarFinDeMesRequestDTO>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowConfirmationAnswer("Facturar fin de mes", A<string>._, true)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Confirmacion_DiceClientesYQueLosPOnoEntran()
        {
            var vm = Crear();

            Assert.AreEqual("Se van a reunir y facturar todos los albaranes de fin de mes de todos los clientes. " +
                "Los pedidos con P.O. no entran: se facturan por P.O. cuando están completos. ¿Continúa?", vm.TextoConfirmacion());

            vm.Cliente = " 15191 ";
            StringAssert.Contains(vm.TextoConfirmacion(), "todos los albaranes de fin de mes del cliente 15191.");
        }

        [TestMethod]
        public async Task ConfirmacionAceptada_LlamaConLaFechaYElCliente()
        {
            Confirma(true);
            FacturarFinDeMesRequestDTO enviado = null;
            A.CallTo(() => servicio.FacturarFinDeMes(A<FacturarFinDeMesRequestDTO>._))
                .Invokes((FacturarFinDeMesRequestDTO r) => enviado = r)
                .Returns(Task.FromResult(new ResultadoFacturacionFinDeMesDTO()));
            var vm = Crear();
            vm.FechaCorte = new DateTime(2026, 9, 30);
            vm.Cliente = " 15191 ";
            vm.Contacto = "0";

            await vm.FacturarFinDeMesCommand.ExecuteAsync(null);

            Assert.IsNotNull(enviado);
            Assert.AreEqual("1", enviado.Empresa);
            Assert.AreEqual("2026-09-30", enviado.FechaCorte);
            Assert.AreEqual("15191", enviado.Cliente);
            Assert.AreEqual("0", enviado.Contacto);
        }

        [TestMethod]
        public async Task SinCliente_NoMandaClienteNiContacto()
        {
            Confirma(true);
            FacturarFinDeMesRequestDTO enviado = null;
            A.CallTo(() => servicio.FacturarFinDeMes(A<FacturarFinDeMesRequestDTO>._))
                .Invokes((FacturarFinDeMesRequestDTO r) => enviado = r)
                .Returns(Task.FromResult(new ResultadoFacturacionFinDeMesDTO()));
            var vm = Crear();
            vm.Cliente = "15191";
            vm.Contacto = "2";
            vm.Cliente = "";

            await vm.FacturarFinDeMesCommand.ExecuteAsync(null);

            Assert.IsNull(enviado.Cliente);
            Assert.IsNull(enviado.Contacto);
            Assert.IsFalse(vm.HayCliente);
        }

        [TestMethod]
        public async Task Resultado_SeVuelcaALasListasYSeNotificaElResumen()
        {
            Confirma(true);
            var resultado = new ResultadoFacturacionFinDeMesDTO
            {
                Facturas = new List<FacturaFinDeMesDTO>
                {
                    new() { Cliente = "15191", PedidoDestino = 927473, NumeroFactura = "NV26/012345", PedidosReunidos = new List<int> { 1, 2 } },
                    new() { Cliente = "1000", PedidoDestino = 927474, NumeroFactura = "NV26/012346" }
                },
                Saltados = new List<ClienteFinDeMesSaltadoDTO> { new() { Cliente = "2000", Motivo = "Tiene P.O. obligatorio" } },
                Errores = new List<ErrorFinDeMesDTO> { new() { Cliente = "3000", PedidoDestino = 927475, Mensaje = "Descuadre" } }
            };
            A.CallTo(() => servicio.FacturarFinDeMes(A<FacturarFinDeMesRequestDTO>._)).Returns(Task.FromResult(resultado));
            var vm = Crear();

            await vm.FacturarFinDeMesCommand.ExecuteAsync(null);

            Assert.AreEqual(2, vm.Facturas.Count);
            Assert.AreEqual("NV26/012345", vm.Facturas[0].NumeroFactura);
            Assert.AreEqual("1, 2", vm.Facturas[0].TextoPedidosReunidos);
            Assert.AreEqual(1, vm.Saltados.Count);
            Assert.AreEqual("Tiene P.O. obligatorio", vm.Saltados[0].Motivo);
            Assert.AreEqual(1, vm.Errores.Count);
            Assert.AreEqual(927475, vm.Errores[0].PedidoDestino);
            Assert.AreEqual("2 facturas, 1 cliente saltado, 1 error", vm.Resumen);
            Assert.IsFalse(vm.EstaProcesando);
            Assert.IsFalse(vm.HayMensajeError);
            A.CallTo(() => dialogos.ShowNotification("Facturar fin de mes", "2 facturas, 1 cliente saltado, 1 error")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ErrorDeLaApi_SeEnseniaTalCual()
        {
            Confirma(true);
            const string texto = "La facturación del fin de mes por la API está apagada.";
            A.CallTo(() => servicio.FacturarFinDeMes(A<FacturarFinDeMesRequestDTO>._)).ThrowsAsync(new InvalidOperationException(texto));
            var vm = Crear();

            await vm.FacturarFinDeMesCommand.ExecuteAsync(null);

            Assert.AreEqual(texto, vm.MensajeError);
            Assert.IsTrue(vm.HayMensajeError);
            Assert.IsNull(vm.Resumen);
            Assert.IsFalse(vm.EstaProcesando);
            A.CallTo(() => dialogos.ShowError(texto)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowNotification(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void MensajeError_400ConMessage_DevuelveElTexto_Y403Vacio_DiceQuienPuede()
        {
            Assert.AreEqual("La fecha de corte (01/12/2026) no puede ser posterior a hoy.",
                ServicioFacturacionRutas.MensajeErrorFinDeMes(HttpStatusCode.BadRequest, "{\"Message\":\"La fecha de corte (01/12/2026) no puede ser posterior a hoy.\"}"));
            StringAssert.Contains(ServicioFacturacionRutas.MensajeErrorFinDeMes(HttpStatusCode.Forbidden, ""), "Administración, Dirección e Informática");
        }

        [TestMethod]
        public void SinPermiso_ElBotonNoSeVeNiSePuedeUsar()
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(A<string>._)).Returns(false);
            var vm = Crear();

            Assert.IsFalse(vm.PuedeFacturarFinDeMes);
            Assert.IsFalse(vm.FacturarFinDeMesCommand.CanExecute(null));
        }

        [DataTestMethod]
        [DataRow(Constantes.GruposSeguridad.ADMINISTRACION)]
        [DataRow(Constantes.GruposSeguridad.DIRECCION)]
        [DataRow(Constantes.GruposSeguridad.INFORMATICA)]
        public void ConPermiso_ElBotonSeVe(string grupo)
        {
            var conf = A.Fake<IConfiguracion>();
            A.CallTo(() => conf.UsuarioEnGrupo(grupo)).Returns(true);

            var vm = new FacturarFinDeMesDialogViewModel(conf, dialogos, servicio);

            Assert.IsTrue(vm.PuedeFacturarFinDeMes);
            Assert.IsTrue(vm.FacturarFinDeMesCommand.CanExecute(null));
        }

        [TestMethod]
        public void Almacen_NoPuedeFacturarElFinDeMes()
        {
            var conf = A.Fake<IConfiguracion>();
            A.CallTo(() => conf.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)).Returns(true);

            Assert.IsFalse(FacturarFinDeMesDialogViewModel.TienePermiso(conf));
        }

        [TestMethod]
        public void MientrasFactura_NoSePuedeCerrarYCerrarDevuelveCancel()
        {
            var vm = Crear();
            IDialogoNesto dialogo = vm;
            ResultadoDialogo resultado = null;
            dialogo.RequestClose += r => resultado = r;

            vm.EstaProcesando = true;
            Assert.IsFalse(dialogo.CanCloseDialog());
            Assert.IsFalse(vm.FacturarFinDeMesCommand.CanExecute(null));

            vm.EstaProcesando = false;
            vm.CerrarCommand.Execute(null);
            Assert.IsTrue(dialogo.CanCloseDialog());
            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
        }
    }
}
