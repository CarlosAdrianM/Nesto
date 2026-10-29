using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#518 (NestoAPI#613): la pestaña «Cupones» de la ficha del cliente. Lista todos (también usados, anulados y caducados);
    /// Dirección emite cupones manuales con motivo y Administración o Dirección anulan los que no se han usado. La API manda.
    /// </summary>
    [TestClass]
    public class CuponesClienteViewModelTests
    {
        private readonly IServicioCupones servicio = A.Fake<IServicioCupones>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly List<string> errores = new();

        public CuponesClienteViewModelTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
        }

        private void EnGrupo(string grupo) => A.CallTo(() => configuracion.UsuarioEnGrupo(grupo)).Returns(true);

        private CuponesClienteViewModel Vm() => new(servicio, dialogos, configuracion);

        private static CuponDTO Cupon(string codigo, string estado, DateTime emision, string origen = "Evento") => new()
        {
            Codigo = codigo,
            Empresa = "1",
            Cliente = "15191",
            Importe = 50M,
            PedidoMinimo = 100M,
            Origen = origen,
            Evento = origen == "Evento" ? "Masterclass Cloasma" : null,
            Motivo = origen == "Manual" ? "Compensación" : null,
            FechaEmision = emision,
            ValidoDesde = new DateTime(2026, 10, 30),
            Caducidad = new DateTime(2027, 4, 30),
            Estado = estado,
            SePuedeUsar = estado == "Activo"
        };

        private void Devuelve(params CuponDTO[] cupones) =>
            A.CallTo(() => servicio.LeerDelCliente(A<string>._, A<string>._, A<bool>._)).Returns(Task.FromResult(cupones.ToList()));

        [TestMethod]
        public async Task Cargar_PideTodos_YOrdenaPrimeroLosQueSePuedenUsar()
        {
            Devuelve(Cupon("EV-2609-AAAA", "Usado", new DateTime(2026, 9, 1)),
                     Cupon("EV-2610-BBBB", "Activo", new DateTime(2026, 10, 1)),
                     Cupon("MN-2610-CCCC", "Emitido", new DateTime(2026, 10, 20), "Manual"));
            var vm = Vm();

            await vm.CargarAsync("1", "15191 ", "0");

            A.CallTo(() => servicio.LeerDelCliente("1", "15191", true)).MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new[] { "EV-2610-BBBB", "MN-2610-CCCC", "EV-2609-AAAA" }, vm.Cupones.Select(c => c.Codigo).ToArray());
            Assert.AreEqual("Evento: Masterclass Cloasma", vm.Cupones[0].Origen);
            Assert.AreEqual("Manual: Compensación", vm.Cupones[1].Origen);
            Assert.IsFalse(vm.HayMensaje);
        }

        [TestMethod]
        public async Task SinCupones_ODeError_LoDice()
        {
            Devuelve();
            var vm = Vm();
            await vm.CargarAsync("1", "15191", "0");
            Assert.AreEqual("El cliente no tiene cupones.", vm.Mensaje);

            A.CallTo(() => servicio.LeerDelCliente(A<string>._, A<string>._, A<bool>._)).Returns(Task.FromResult<List<CuponDTO>>(null));
            await vm.RecargarAsync();
            Assert.AreEqual("No se han podido leer los cupones del cliente.", vm.Mensaje);
        }

        [TestMethod]
        public void Permisos_EmitirSoloDireccion_AnularAdministracionODireccion()
        {
            var vm = Vm();
            Assert.IsFalse(vm.PuedeEmitirManual);
            Assert.IsFalse(vm.PuedeAnular);

            EnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
            Assert.IsFalse(vm.PuedeEmitirManual, "Administración no emite cupones manuales");
            Assert.IsTrue(vm.PuedeAnular);

            EnGrupo(Constantes.GruposSeguridad.DIRECCION);
            Assert.IsTrue(vm.PuedeEmitirManual);
        }

        [TestMethod]
        public async Task EmitirManual_PideImporteMotivoYFecha_YLoManda()
        {
            EnGrupo(Constantes.GruposSeguridad.DIRECCION);
            Devuelve();
            A.CallTo(() => dialogos.GetAmount(A<string>._, A<string>._)).Returns(30M);
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns(" Compensación por el retraso ");
            A.CallTo(() => dialogos.GetDate(A<string>._, A<string>._, A<DateTime?>._)).Returns(new DateTime(2026, 11, 1));
            CrearCuponManualDTO enviado = null;
            A.CallTo(() => servicio.CrearManual(A<CrearCuponManualDTO>._))
                .Invokes((CrearCuponManualDTO p) => enviado = p)
                .Returns(Task.FromResult(new CuponDTO { Codigo = "MN-2611-AB2C" }));
            var vm = Vm();
            await vm.CargarAsync("1", "15191", "0");

            await vm.EmitirManualAsync();

            Assert.IsNotNull(enviado);
            Assert.AreEqual("15191", enviado.Cliente);
            Assert.AreEqual("0", enviado.Contacto);
            Assert.AreEqual(30M, enviado.Importe);
            Assert.AreEqual("Compensación por el retraso", enviado.Motivo);
            Assert.AreEqual(new DateTime(2026, 11, 1), enviado.ValidoDesde);
            A.CallTo(() => dialogos.ShowNotification("Cupón manual", A<string>.That.Contains("MN-2611-AB2C"))).MustHaveHappened();
            A.CallTo(() => servicio.LeerDelCliente(A<string>._, A<string>._, true)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task EmitirManual_SinMotivo_NoLlamaALaApi()
        {
            EnGrupo(Constantes.GruposSeguridad.DIRECCION);
            Devuelve();
            A.CallTo(() => dialogos.GetAmount(A<string>._, A<string>._)).Returns(30M);
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("   ");
            var vm = Vm();
            await vm.CargarAsync("1", "15191", "0");

            await vm.EmitirManualAsync();

            A.CallTo(() => servicio.CrearManual(A<CrearCuponManualDTO>._)).MustNotHaveHappened();
            Assert.AreEqual("El cupón manual necesita un motivo.", errores.Single());
        }

        [TestMethod]
        public async Task EmitirManual_SinSerDeDireccion_NoHaceNada()
        {
            Devuelve();
            var vm = Vm();
            await vm.CargarAsync("1", "15191", "0");

            Assert.IsFalse(vm.EmitirManualCommand.CanExecute(null));
            await vm.EmitirManualAsync();

            A.CallTo(() => dialogos.GetAmount(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => servicio.CrearManual(A<CrearCuponManualDTO>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task EmitirManual_ErrorDeLaApi_SeEnsenaSuMensaje()
        {
            EnGrupo(Constantes.GruposSeguridad.DIRECCION);
            Devuelve();
            A.CallTo(() => dialogos.GetAmount(A<string>._, A<string>._)).Returns(30M);
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("Motivo");
            A.CallTo(() => servicio.CrearManual(A<CrearCuponManualDTO>._)).ThrowsAsync(new InvalidOperationException("No se pudo emitir el cupón: Solo Dirección…"));
            var vm = Vm();
            await vm.CargarAsync("1", "15191", "0");

            await vm.EmitirManualAsync();

            Assert.AreEqual("No se pudo emitir el cupón: Solo Dirección…", errores.Single());
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Anular_SoloLosNoUsados_ConMotivo_YRecarga()
        {
            EnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
            Devuelve(Cupon("EV-2610-BBBB", "Activo", new DateTime(2026, 10, 1)), Cupon("EV-2609-AAAA", "Usado", new DateTime(2026, 9, 1)));
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._, A<string>._)).Returns("Devolución del curso.");
            var vm = Vm();
            await vm.CargarAsync("1", "15191", "0");

            vm.CuponSeleccionado = vm.Cupones.Single(c => c.Codigo == "EV-2609-AAAA");
            Assert.IsFalse(vm.AnularCommand.CanExecute(null), "Un cupón usado no se anula");

            vm.CuponSeleccionado = vm.Cupones.Single(c => c.Codigo == "EV-2610-BBBB");
            Assert.IsTrue(vm.AnularCommand.CanExecute(null));
            await vm.AnularAsync();

            A.CallTo(() => servicio.Anular("EV-2610-BBBB", "Devolución del curso.")).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.LeerDelCliente(A<string>._, A<string>._, true)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task Anular_SiCancelaElMotivo_NoAnula()
        {
            EnGrupo(Constantes.GruposSeguridad.DIRECCION);
            Devuelve(Cupon("EV-2610-BBBB", "Activo", new DateTime(2026, 10, 1)));
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._, A<string>._)).Returns(null);
            var vm = Vm();
            await vm.CargarAsync("1", "15191", "0");
            vm.CuponSeleccionado = vm.Cupones.Single();

            await vm.AnularAsync();

            A.CallTo(() => servicio.Anular(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task CambiarDeCliente_DescartaLaRespuestaDelAnterior()
        {
            var lenta = new TaskCompletionSource<List<CuponDTO>>();
            A.CallTo(() => servicio.LeerDelCliente("1", "11111", true)).Returns(lenta.Task);
            A.CallTo(() => servicio.LeerDelCliente("1", "15191", true))
                .Returns(Task.FromResult(new List<CuponDTO> { Cupon("EV-2610-BBBB", "Activo", new DateTime(2026, 10, 1)) }));
            var vm = Vm();

            Task primera = vm.CargarAsync("1", "11111", "0");
            await vm.CargarAsync("1", "15191", "0");
            lenta.SetResult(new List<CuponDTO> { Cupon("EV-2610-ZZZZ", "Activo", new DateTime(2026, 10, 1)) });
            await primera;

            Assert.AreEqual("EV-2610-BBBB", vm.Cupones.Single().Codigo);
        }

        // ---------------- Carlos 29/10/26: pedido mínimo del cupón manual (200 € por defecto) ----------------

        private CrearCuponManualDTO EmitirConPedidoMinimo(string escrito)
        {
            EnGrupo(Constantes.GruposSeguridad.DIRECCION);
            Devuelve();
            A.CallTo(() => dialogos.GetAmount(A<string>._, A<string>._)).Returns(30M);
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("Compensación");
            A.CallTo(() => dialogos.GetText("Cupón manual", A<string>.That.Contains("Pedido mínimo (€)"), "200")).Returns(escrito);
            CrearCuponManualDTO enviado = null;
            A.CallTo(() => servicio.CrearManual(A<CrearCuponManualDTO>._))
                .Invokes((CrearCuponManualDTO p) => enviado = p)
                .Returns(Task.FromResult(new CuponDTO { Codigo = "MN-2611-AB2C" }));
            var vm = Vm();
            vm.CargarAsync("1", "15191", "0").GetAwaiter().GetResult();
            vm.EmitirManualAsync().GetAwaiter().GetResult();
            return enviado;
        }

        [TestMethod]
        public void EmitirManual_PideElPedidoMinimoCon200PorDefecto_YLoManda()
        {
            CrearCuponManualDTO enviado = EmitirConPedidoMinimo("200");

            Assert.AreEqual(200M, enviado.PedidoMinimo);
            A.CallTo(() => dialogos.ShowConfirmationAnswer("Cupón manual", A<string>.That.Contains("al menos 200 €"))).MustHaveHappened();
        }

        [TestMethod]
        public void EmitirManual_ConOtroPedidoMinimo_MandaEse()
        {
            Assert.AreEqual(150.5M, EmitirConPedidoMinimo("150,50 €").PedidoMinimo);
        }

        [TestMethod]
        public void EmitirManual_PedidoMinimoQueNoEsUnNumero_NoLlamaALaApi()
        {
            Assert.IsNull(EmitirConPedidoMinimo("doscientos"));
            StringAssert.Contains(errores.Single(), "no es un importe válido");
        }

        [TestMethod]
        public void EmitirManual_CancelarElPedidoMinimo_NoLlamaALaApi()
        {
            Assert.IsNull(EmitirConPedidoMinimo(null));
            Assert.AreEqual(0, errores.Count);
        }

        [DataTestMethod]
        [DataRow("", "200")]
        [DataRow("  ", "200")]
        [DataRow("0", "0")]
        [DataRow("150", "150")]
        [DataRow("150,5", "150.5")]
        [DataRow("150.5", "150.5")]
        [DataRow("1.500,25", "1500.25")]
        [DataRow("90 €", "90")]
        public void LeerPedidoMinimo_EntiendeLoQueSeEscribe(string texto, string esperado)
        {
            Assert.AreEqual(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), CuponesClienteViewModel.LeerPedidoMinimo(texto));
        }

        [DataTestMethod]
        [DataRow("abc")]
        [DataRow("-5")]
        public void LeerPedidoMinimo_NoValido_EsNull(string texto)
        {
            Assert.IsNull(CuponesClienteViewModel.LeerPedidoMinimo(texto));
        }
    }
}
