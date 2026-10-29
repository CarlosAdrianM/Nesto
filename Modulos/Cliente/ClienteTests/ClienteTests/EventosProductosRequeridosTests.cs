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
    /// Nesto#567 (NestoAPI#732): en el mantenimiento del evento, la lista de productos que hay que haber comprado o pedido
    /// y si basta con uno o hacen falta todos. Se manda en el PUT del evento solo si se ha tocado (lista completa; [] quita
    /// el requisito); con la API de antes, que no los manda, no se toca nada.
    /// </summary>
    [TestClass]
    public class EventosProductosRequeridosTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();
        private readonly List<EventoModel> guardados = new();

        public EventosProductosRequeridosTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).ReturnsLazily((EventoModel e) =>
            {
                guardados.Add(e);
                return e;
            });
        }

        private async Task<MantenimientoEventosViewModel> Mantenimiento(EventoModel evento)
        {
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { evento });
            A.CallTo(() => eventos.LeerInscripciones(A<int>.Ignored)).Returns(new List<InscripcionEventoModel>());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            return vm;
        }

        private static EventoModel Evento(List<ProductoRequeridoEventoModel> productos, string modo = null) => new()
        {
            Id = 4, Empresa = "1", Titulo = "Formación por compra Lifting Coreano", Fecha = DateTime.Today.AddDays(20), Almacen = "ALC",
            Activo = true, Gratuito = true, ImporteSenal = 0, ImporteSenalNoReembolsable = 0,
            ProductosRequeridos = productos, ProductosRequeridosModo = modo
        };

        [TestMethod]
        public async Task CargarEvento_EnsenaLosProductosYElModo()
        {
            var vm = await Mantenimiento(Evento(new List<ProductoRequeridoEventoModel> { new() { Producto = "45363", Nombre = "LIFTING COREANO" } }, "Todos"));

            Assert.AreEqual("45363 - LIFTING COREANO", vm.ProductosRequeridosEdicion.Single().Texto);
            Assert.AreEqual(EventoModel.MODO_PRODUCTOS_TODOS, vm.ModoProductosEdicion.Valor);
        }

        [TestMethod]
        public async Task ApiDeAntes_SinModo_EsAlguno_YGuardarNoMandaNiLaListaNiElModo()
        {
            var vm = await Mantenimiento(Evento(null));

            Assert.AreEqual(EventoModel.MODO_PRODUCTOS_ALGUNO, vm.ModoProductosEdicion.Valor);
            await vm.GuardarAsync();

            EventoModel enviado = guardados.Single();
            Assert.IsNull(enviado.ProductosRequeridos);
            Assert.IsNull(enviado.ProductosRequeridosModo);
            string json = JsonConvert.SerializeObject(enviado);
            Assert.IsFalse(json.Contains("ProductosRequeridos"), "Sin tocarla no se manda: la API de antes (o sin el script) no la conoce");
        }

        [TestMethod]
        public async Task AnadirProducto_CompruebaQueExisteYGuardaLaListaCompleta()
        {
            A.CallTo(() => eventos.LeerProductoCurso("45364")).Returns(new ProductoCursoModel { Producto = "45364", Nombre = "SERUM COREANO" });
            var vm = await Mantenimiento(Evento(new List<ProductoRequeridoEventoModel> { new() { Producto = "45363", Nombre = "LIFTING COREANO" } }));

            vm.ProductoRequeridoNuevo = " 45364 ";
            await vm.AnadirProductoRequeridoAsync();
            await vm.GuardarAsync();

            CollectionAssert.AreEqual(new[] { "45363", "45364" }, guardados.Single().ProductosRequeridos.Select(p => p.Producto).ToArray());
            Assert.IsNull(guardados.Single().ProductosRequeridosModo, "El modo no ha cambiado");
            Assert.AreEqual("45364 - SERUM COREANO", vm.ProductosRequeridosEdicion.Last().Texto);
        }

        [TestMethod]
        public async Task AnadirProducto_QueNoExiste_LoDiceYNoLoAnade()
        {
            A.CallTo(() => eventos.LeerProductoCurso("99999")).Returns((ProductoCursoModel)null);
            var vm = await Mantenimiento(Evento(new List<ProductoRequeridoEventoModel>()));

            vm.ProductoRequeridoNuevo = "99999";
            await vm.AnadirProductoRequeridoAsync();

            Assert.AreEqual(0, vm.ProductosRequeridosEdicion.Count);
            StringAssert.Contains(errores.Single(), "No existe el producto 99999");
        }

        [TestMethod]
        public async Task QuitarTodos_MandaListaVaciaParaDejarDeExigirCompra()
        {
            var vm = await Mantenimiento(Evento(new List<ProductoRequeridoEventoModel> { new() { Producto = "45363" } }));

            vm.ProductoRequeridoSeleccionado = vm.ProductosRequeridosEdicion.Single();
            vm.QuitarProductoRequeridoCommand.Execute(null);
            await vm.GuardarAsync();

            Assert.IsNotNull(guardados.Single().ProductosRequeridos);
            Assert.AreEqual(0, guardados.Single().ProductosRequeridos.Count);
            StringAssert.Contains(JsonConvert.SerializeObject(guardados.Single()), "\"ProductosRequeridos\":[]");
        }

        [TestMethod]
        public async Task CambiarAHacenFaltaTodos_MandaElModoYLaLista()
        {
            var vm = await Mantenimiento(Evento(new List<ProductoRequeridoEventoModel> { new() { Producto = "45363" }, new() { Producto = "45364" } }, "Alguno"));

            vm.ModoProductosEdicion = vm.ModosProductosRequeridos.Single(m => m.Valor == EventoModel.MODO_PRODUCTOS_TODOS);
            await vm.GuardarAsync();

            Assert.AreEqual("Todos", guardados.Single().ProductosRequeridosModo);
            Assert.AreEqual(2, guardados.Single().ProductosRequeridos.Count);
        }

        [TestMethod]
        public async Task GuardarConTodos_SiLaApiLoRechaza_EnsenaSuMensaje()
        {
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored))
                .ThrowsAsync(new Exception("No se pudo guardar el evento: Falta lanzar el script de los productos requeridos."));
            var vm = await Mantenimiento(Evento(new List<ProductoRequeridoEventoModel> { new() { Producto = "45363" } }));

            vm.ModoProductosEdicion = vm.ModosProductosRequeridos.Single(m => m.Valor == EventoModel.MODO_PRODUCTOS_TODOS);
            await vm.GuardarAsync();

            Assert.AreEqual("No se pudo guardar el evento: Falta lanzar el script de los productos requeridos.", errores.Single());
        }

        [TestMethod]
        public async Task Inscribir_SinLaCompra_EnsenaElMensajeDeLaApi()
        {
            const string mensaje = "No se pudo inscribir al cliente: Para apuntarse a «Formación por compra Lifting Coreano» hay que haber comprado o pedido 45363 (LIFTING COREANO …), desde el 21/08/2026. No hemos encontrado ningún pedido del cliente con ese producto.";
            A.CallTo(() => eventos.Inscribir(A<int>.Ignored, A<InscribirEventoModel>.Ignored)).ThrowsAsync(new Exception(mensaje));
            var vm = new InscribirEventoDialogViewModel(eventos) { EsperaCargaPersonasMs = 0 };
            vm.Evento = Evento(null);
            vm.Cliente = "15191";

            await vm.InscribirAsync();

            Assert.AreEqual(mensaje, vm.Mensaje);
            Assert.IsNull(vm.Inscripcion);
        }

        [TestMethod]
        public void ExtraerMensaje_DelCuatrocientosDeCompra_DevuelveElMensajeTalCual()
        {
            string body = "{\"error\":{\"code\":\"EVENTO_REQUIERE_COMPRA\",\"message\":\"Para apuntarse hay que haber comprado 45363.\",\"details\":{\"productos\":[{\"Producto\":\"45363\"}]}}}";

            Assert.AreEqual("Para apuntarse hay que haber comprado 45363.", EventosService.ExtraerMensaje(body));
        }
    }
}
