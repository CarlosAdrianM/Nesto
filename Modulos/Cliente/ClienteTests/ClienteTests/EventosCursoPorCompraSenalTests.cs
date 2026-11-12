using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// Novedades 1233 (12/11/26): un curso por compra (con productos que comprar) se creó desde Nesto con la señal por defecto
    /// de la pantalla (100 € / 60 €) y las inscripciones nacieron de pago. Ahora, al añadir el primer producto a un evento nuevo
    /// con la señal por defecto se propone dejarlo sin coste; al guardar con productos y señal se pregunta («No» por defecto);
    /// con productos las dos señales van siempre explícitas (la API pone 100/60 si no vienen).
    /// </summary>
    [TestClass]
    public class EventosCursoPorCompraSenalTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();
        private readonly List<EventoModel> guardados = new();

        public EventosCursoPorCompraSenalTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel>());
            A.CallTo(() => eventos.LeerInscripciones(A<int>.Ignored)).Returns(new List<InscripcionEventoModel>());
            A.CallTo(() => eventos.BuscarProductoRequerido("45363")).Returns(new List<ProductoCursoModel> { new() { Producto = "45363", Nombre = "LIFTING COREANO" } });
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).ReturnsLazily((EventoModel e) =>
            {
                guardados.Add(e);
                return e;
            });
        }

        private async Task<MantenimientoEventosViewModel> Nuevo()
        {
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.NuevoCommand.Execute(null);
            vm.TituloEdicion = "Formación por compra Lifting Coreano";
            vm.FechaEdicion = DateTime.Today.AddDays(20);
            return vm;
        }

        private static async Task AnadirProducto(MantenimientoEventosViewModel vm)
        {
            vm.ProductoRequeridoNuevo = "45363";
            await vm.AnadirProductoRequeridoAsync();
        }

        private void ContestarPropuestaSinCoste(bool respuesta) =>
            A.CallTo(() => dialogos.ShowConfirmationAnswer(MantenimientoEventosViewModel.TITULO_CURSO_POR_COMPRA, A<string>.That.Contains("sin coste")))
                .Returns(respuesta);

        private void ContestarGuardarConSenal(bool respuesta) =>
            A.CallTo(() => dialogos.ShowConfirmationAnswer(MantenimientoEventosViewModel.TITULO_CURSO_POR_COMPRA, A<string>.That.Contains("Es correcto"), true))
                .Returns(respuesta);

        [TestMethod]
        public async Task Nuevo_SinProductos_EnsenaLaSenalPorDefecto()
        {
            var vm = await Nuevo();

            Assert.IsFalse(vm.EventoSinCoste);
            StringAssert.StartsWith(vm.TextoCosteEvento, "Con señal de");
            Assert.IsFalse(vm.CursoPorCompraConSenal);
        }

        [TestMethod]
        public async Task CursoPorCompraNuevo_AlAnadirElPrimerProducto_ProponeSinCoste_YMandaCeroYCeroExplicitos()
        {
            ContestarPropuestaSinCoste(true);
            var vm = await Nuevo();

            await AnadirProducto(vm);

            Assert.AreEqual(0, vm.ImporteSenalEdicion);
            Assert.AreEqual(0, vm.ImporteSenalNoReembolsableEdicion);
            Assert.IsTrue(vm.EventoSinCoste);
            StringAssert.StartsWith(vm.TextoCosteEvento, "Sin coste");
            await vm.GuardarAsync();

            EventoModel enviado = guardados.Single();
            Assert.AreEqual(0, enviado.ImporteSenal);
            Assert.AreEqual(0m, enviado.ImporteSenalNoReembolsable);
            string json = JsonConvert.SerializeObject(enviado);
            StringAssert.Contains(json, "\"ImporteSenal\":0");
            StringAssert.Contains(json, "\"ImporteSenalNoReembolsable\":0", "Con productos, la no reembolsable va explícita: si no, la API pone 60 €");
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.That.Contains("Es correcto"), A<bool>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task CursoPorCompraNuevo_ConLaNoReembolsableVacia_LaMandaACero()
        {
            ContestarPropuestaSinCoste(false);
            var vm = await Nuevo();
            vm.ImporteSenalEdicion = 0;
            vm.ImporteSenalNoReembolsableEdicion = null;

            await AnadirProducto(vm);
            await vm.GuardarAsync();

            Assert.IsTrue(vm.EventoSinCoste);
            EventoModel enviado = guardados.Single();
            Assert.AreEqual(0m, enviado.ImporteSenalNoReembolsable);
            StringAssert.Contains(JsonConvert.SerializeObject(enviado), "\"ImporteSenalNoReembolsable\":0");
        }

        [TestMethod]
        public async Task CursoPorCompra_ConSenal_PideConfirmacion_YConNoNoLlamaALaApi()
        {
            ContestarPropuestaSinCoste(false);
            ContestarGuardarConSenal(false);
            var vm = await Nuevo();
            await AnadirProducto(vm);

            Assert.IsTrue(vm.CursoPorCompraConSenal);
            await vm.GuardarAsync();

            A.CallTo(() => dialogos.ShowConfirmationAnswer(MantenimientoEventosViewModel.TITULO_CURSO_POR_COMPRA,
                A<string>.That.StartsWith("Este curso se gana comprando productos, pero además tiene señal de"), true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task CursoPorCompra_ConSenal_ConSiGuardaLaSenal()
        {
            ContestarPropuestaSinCoste(false);
            ContestarGuardarConSenal(true);
            var vm = await Nuevo();
            await AnadirProducto(vm);

            await vm.GuardarAsync();

            EventoModel enviado = guardados.Single();
            Assert.AreEqual(MantenimientoEventosViewModel.SENAL_REEMBOLSABLE_DEFECTO, enviado.ImporteSenal);
            Assert.AreEqual(MantenimientoEventosViewModel.SENAL_NO_REEMBOLSABLE_DEFECTO, enviado.ImporteSenalNoReembolsable);
        }

        [TestMethod]
        public async Task SenalCambiadaAMano_AlAnadirProductos_NoProponeNada()
        {
            var vm = await Nuevo();
            vm.ImporteSenalEdicion = 50;

            await AnadirProducto(vm);

            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.That.Contains("sin coste"))).MustNotHaveHappened();
            Assert.AreEqual(50, vm.ImporteSenalEdicion);
        }

        [TestMethod]
        public async Task EventoDePago_SinProductos_NoPregunta_YSeGuardaComoSiempre()
        {
            var vm = await Nuevo();

            await vm.GuardarAsync();

            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
            EventoModel enviado = guardados.Single();
            Assert.AreEqual(MantenimientoEventosViewModel.SENAL_REEMBOLSABLE_DEFECTO, enviado.ImporteSenal);
            Assert.AreEqual(MantenimientoEventosViewModel.SENAL_NO_REEMBOLSABLE_DEFECTO, enviado.ImporteSenalNoReembolsable);
            Assert.IsNull(enviado.ProductosRequeridos);
        }

        [TestMethod]
        public async Task EventoDePago_SinProductos_ConLaNoReembolsableVacia_SigueSinMandarla()
        {
            var vm = await Nuevo();
            vm.ImporteSenalNoReembolsableEdicion = null;

            await vm.GuardarAsync();

            Assert.IsNull(guardados.Single().ImporteSenalNoReembolsable);
            Assert.IsFalse(JsonConvert.SerializeObject(guardados.Single()).Contains("ImporteSenalNoReembolsable"));
        }

        [TestMethod]
        public async Task EventoGratuito_SinProductos_NoPregunta_YMandaCeroYCero()
        {
            var vm = await Nuevo();
            vm.ImporteSenalEdicion = 0;
            vm.ImporteSenalNoReembolsableEdicion = 0;

            Assert.IsTrue(vm.EventoSinCoste);
            await vm.GuardarAsync();

            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored)).MustNotHaveHappened();
            EventoModel enviado = guardados.Single();
            Assert.AreEqual(0, enviado.ImporteSenal);
            Assert.AreEqual(0m, enviado.ImporteSenalNoReembolsable);
            Assert.IsNull(enviado.ProductosRequeridos);
        }

        [TestMethod]
        public async Task EventoExistenteDePagoConProductos_AlGuardar_PideConfirmacion()
        {
            var existente = new EventoModel
            {
                Id = 9, Empresa = "1", Titulo = "Curso Lifting", Fecha = DateTime.Today.AddDays(20), Almacen = "ALC", Activo = true,
                ImporteSenal = 100, ImporteSenalNoReembolsable = 60,
                ProductosRequeridos = new List<ProductoRequeridoEventoModel> { new() { Producto = "45363", Nombre = "LIFTING COREANO" } }
            };
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { existente });
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();

            Assert.IsTrue(vm.CursoPorCompraConSenal);
            await vm.GuardarAsync();

            A.CallTo(() => dialogos.ShowConfirmationAnswer(MantenimientoEventosViewModel.TITULO_CURSO_POR_COMPRA, A<string>.That.Contains("Es correcto"), true))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).MustNotHaveHappened();
        }
    }
}
