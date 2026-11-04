using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#618 (NestoAPI#786): diálogo «Nueva nota de cobro». Los efectos se cobran en el orden en que se marcan, el importe se
    /// propone como lo pendiente de lo marcado (editable, o solo importe sin efectos) y los errores de la API se enseñan tal cual.
    /// </summary>
    [TestClass]
    public class NotaCobroDialogViewModelTests
    {
        private readonly IExtractoClienteService extractos = A.Fake<IExtractoClienteService>();
        private readonly INotaCobroService servicio = A.Fake<INotaCobroService>();
        private CrearNotaCobroModel enviada;

        public NotaCobroDialogViewModelTests()
        {
            A.CallTo(() => extractos.LeerExtractoPendiente("15191")).Returns(new List<ExtractoClienteModel>
            {
                Movimiento(101, "1", 134.16M, new DateTime(2026, 10, 30)),
                Movimiento(102, "3", 50M, new DateTime(2026, 9, 15)),
                Movimiento(103, "1", 20.5M, new DateTime(2026, 11, 2))
            });
            A.CallTo(() => servicio.CrearNotaCobro(A<CrearNotaCobroModel>._)).ReturnsLazily((CrearNotaCobroModel n) =>
            {
                enviada = n;
                return Task.FromResult(new NotaCobroCreadaModel
                {
                    Empresa = "1",
                    Pedido = 929700,
                    Cliente = n.Cliente,
                    Contacto = n.Contacto ?? "0",
                    CodigoDocumento = "NC929700",
                    Importe = n.Importe ?? 0,
                    Fecha = new DateTime(2026, 11, 6),
                    Texto = "Cobro de 134,16 €"
                });
            });
        }

        private static ExtractoClienteModel Movimiento(int id, string empresa, decimal pendiente, DateTime vencimiento) => new()
        {
            Id = id,
            Empresa = empresa,
            Cliente = "15191",
            Contacto = "0",
            Fecha = vencimiento.AddDays(-30),
            Tipo = "2",
            Documento = $"NV26/{id}",
            Efecto = "1",
            Importe = pendiente,
            ImportePendiente = pendiente,
            Vencimiento = vencimiento
        };

        private async Task<NotaCobroDialogViewModel> Abierto()
        {
            var vm = new NotaCobroDialogViewModel(extractos, servicio) { EsperaCargaMs = 0 };
            await vm.AbrirAsync("15191  ", "0", "PELUQUERÍA LA BONITA");
            return vm;
        }

        private static EfectoACobrarModel Efecto(NotaCobroDialogViewModel vm, int id) => vm.Efectos.Single(e => e.Id == id);

        [TestMethod]
        public async Task Abrir_PoneElClienteYCargaLoPendienteDeTodasLasEmpresasPorVencimiento()
        {
            NotaCobroDialogViewModel vm = await Abierto();

            Assert.AreEqual("15191", vm.Cliente);
            Assert.AreEqual("0", vm.Contacto);
            Assert.AreEqual("PELUQUERÍA LA BONITA", vm.NombreCliente);
            CollectionAssert.AreEqual(new[] { 102, 101, 103 }, vm.Efectos.Select(e => e.Id).ToList());
            CollectionAssert.AreEqual(new[] { "3", "1", "1" }, vm.Efectos.Select(e => e.Empresa).ToList());
            Assert.AreEqual(DateTime.Today, vm.Fecha, "Por defecto, hoy");
            Assert.IsNull(vm.Importe);
            Assert.IsFalse(vm.CrearCommand.CanExecute(null), "Sin efectos ni importe no se puede crear");
        }

        [TestMethod]
        public async Task MarcarEfectos_ProponeLaSumaDeLoPendienteDeLoMarcado()
        {
            NotaCobroDialogViewModel vm = await Abierto();

            Efecto(vm, 101).Marcado = true;
            Assert.AreEqual(134.16M, vm.Importe);

            Efecto(vm, 103).Marcado = true;
            Assert.AreEqual(154.66M, vm.Importe);
            Assert.AreEqual(154.66M, vm.ImporteMarcado);

            Efecto(vm, 101).Marcado = false;
            Assert.AreEqual(20.5M, vm.Importe);

            Efecto(vm, 103).Marcado = false;
            Assert.IsNull(vm.Importe, "Sin nada marcado, el importe se queda vacío para teclearlo");
        }

        [TestMethod]
        public async Task ElImporteSePuedeCambiarALoQueSeVaACobrar()
        {
            NotaCobroDialogViewModel vm = await Abierto();
            Efecto(vm, 101).Marcado = true;

            vm.Importe = 200M;
            await vm.CrearAsync();

            CollectionAssert.AreEqual(new[] { 101 }, enviada.Efectos);
            Assert.AreEqual(200M, enviada.Importe);
        }

        [TestMethod]
        public async Task LosEfectosVanEnElOrdenEnQueSeMarcan()
        {
            NotaCobroDialogViewModel vm = await Abierto();

            Efecto(vm, 103).Marcado = true;
            Efecto(vm, 101).Marcado = true;
            Efecto(vm, 102).Marcado = true;

            Assert.AreEqual(1, Efecto(vm, 103).Orden);
            Assert.AreEqual(2, Efecto(vm, 101).Orden);
            Assert.AreEqual(3, Efecto(vm, 102).Orden);

            Efecto(vm, 101).Marcado = false;
            Assert.AreEqual(0, Efecto(vm, 101).Orden);
            Assert.AreEqual("", Efecto(vm, 101).TextoOrden);
            Assert.AreEqual(2, Efecto(vm, 102).Orden, "Al desmarcar uno, los de detrás suben");

            Efecto(vm, 101).Marcado = true;
            Assert.AreEqual(3, Efecto(vm, 101).Orden, "Al volver a marcarlo, va el último");

            await vm.CrearAsync();

            CollectionAssert.AreEqual(new[] { 103, 102, 101 }, enviada.Efectos);
            CollectionAssert.AreEqual(new[] { 103, 102, 101 }, vm.EfectosMarcados.Select(e => e.Id).ToList());
        }

        [TestMethod]
        public async Task SoloImporteSinEfectos_SeCreaConElImporteYSinEfectos()
        {
            NotaCobroDialogViewModel vm = await Abierto();

            vm.Importe = 75M;
            vm.Comentario = "  Pasar por la tarde  ";
            vm.Fecha = DateTime.Today.AddDays(2);
            Assert.IsTrue(vm.CrearCommand.CanExecute(null));
            await vm.CrearAsync();

            Assert.IsNotNull(enviada);
            Assert.AreEqual(Constantes.Empresas.EMPRESA_DEFECTO, enviada.Empresa);
            Assert.AreEqual("15191", enviada.Cliente);
            Assert.AreEqual("0", enviada.Contacto);
            Assert.AreEqual(0, enviada.Efectos.Count);
            Assert.AreEqual(75M, enviada.Importe);
            Assert.AreEqual(DateTime.Today.AddDays(2), enviada.Fecha);
            Assert.AreEqual("Pasar por la tarde", enviada.Comentario);
            Assert.IsTrue(vm.Creada);
            Assert.IsNull(vm.Mensaje);
        }

        [TestMethod]
        public async Task ImporteCeroSinEfectos_NoSePuedeCrear()
        {
            NotaCobroDialogViewModel vm = await Abierto();

            vm.Importe = 0M;

            Assert.IsFalse(vm.CrearCommand.CanExecute(null));
            Assert.AreEqual("Marque los efectos a cobrar o escriba el importe.", vm.MotivoNoCrear);
            await vm.CrearAsync();
            A.CallTo(() => servicio.CrearNotaCobro(A<CrearNotaCobroModel>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Crear_EnseñaElResultadoConCodigoFechaEImporte()
        {
            NotaCobroDialogViewModel vm = await Abierto();
            Efecto(vm, 101).Marcado = true;

            await vm.CrearAsync();

            Assert.IsTrue(vm.Creada);
            Assert.AreEqual("Nota de cobro NC929700 creada: parada en el reparto del 06/11/26 por 134,16 €", vm.TextoCreada);
            Assert.IsFalse(vm.CrearCommand.CanExecute(null), "Ya creada, no se crea otra");
        }

        [TestMethod]
        public async Task Crear_ElErrorDeLaApiSeEnseñaTalCual()
        {
            const string mensaje = "El cliente 15191/0 ya tiene la nota de cobro NC929650 en el reparto. Cámbiela o anúlela antes de crear otra.";
            A.CallTo(() => servicio.CrearNotaCobro(A<CrearNotaCobroModel>._)).ThrowsAsync(new Exception(mensaje));
            NotaCobroDialogViewModel vm = await Abierto();
            Efecto(vm, 101).Marcado = true;

            await vm.CrearAsync();

            Assert.AreEqual(mensaje, vm.Mensaje);
            Assert.IsFalse(vm.Creada);
            Assert.IsTrue(vm.CrearCommand.CanExecute(null), "Se puede corregir y volver a intentarlo");
        }

        [TestMethod]
        public async Task Cerrar_TrasCrearDevuelveLaNota()
        {
            NotaCobroDialogViewModel vm = await Abierto();
            ResultadoDialogo resultado = null;
            ((IDialogoNesto)vm).RequestClose += r => resultado = r;
            Efecto(vm, 101).Marcado = true;
            await vm.CrearAsync();

            vm.CerrarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
            Assert.AreEqual(929700, resultado.Parameters.GetValue<NotaCobroCreadaModel>("nota").Pedido);
        }

        [TestMethod]
        public async Task CambiarDeCliente_RecargaElExtractoYQuitaLoMarcado()
        {
            NotaCobroDialogViewModel vm = await Abierto();
            Efecto(vm, 101).Marcado = true;
            A.CallTo(() => extractos.LeerExtractoPendiente("8897")).Returns(new List<ExtractoClienteModel>());

            vm.Cliente = "8897";
            await vm.CargarExtractoAsync();

            Assert.AreEqual(0, vm.Efectos.Count);
            Assert.AreEqual(0, vm.EfectosMarcados.Count);
        }

        [TestMethod]
        public void ExtraerMensaje_LeeElMessageDeLaApi()
        {
            Assert.AreEqual("No existe el cliente 99999.", NotaCobroService.ExtraerMensaje("{\"Message\":\"No existe el cliente 99999.\"}"));
            Assert.AreEqual("Ya tiene otra.", NotaCobroService.ExtraerMensaje("{\"error\":{\"code\":\"X\",\"message\":\"Ya tiene otra.\"}}"));
            Assert.AreEqual("texto plano", NotaCobroService.ExtraerMensaje("texto plano"));
        }
    }
}
