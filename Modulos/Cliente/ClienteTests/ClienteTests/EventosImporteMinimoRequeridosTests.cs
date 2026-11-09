using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// NestoAPI#843 (Carlos, 09/11/26): importe mínimo de compra de los productos requeridos de un evento «gratuito por compra»
    /// (evento 6 «Rentabiliza cada ML de tus viales»: 400 € en viales). Se edita junto a «Hay que comprar» y va en
    /// EventoDTO.ImporteMinimoProductosRequeridos; vacío = sin mínimo. Solo se manda si cambia (0 lo quita).
    /// </summary>
    [TestClass]
    public class EventosImporteMinimoRequeridosTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();
        private readonly List<EventoModel> guardados = new();

        public EventosImporteMinimoRequeridosTests()
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

        private static EventoModel Viales(decimal? importeMinimo) => new()
        {
            Id = 6, Empresa = "1", Titulo = "Rentabiliza cada ML de tus viales", Fecha = DateTime.Today.AddDays(20), Almacen = "REI",
            Activo = true, ImporteSenal = 0, ImporteSenalNoReembolsable = 0, PedidoMinimoCupon = 0,
            ProductosRequeridos = new List<ProductoRequeridoEventoModel>
            {
                new() { Producto = "EVA01", Nombre = "VIAL EVA VISNÚ 1" },
                new() { Producto = "EVA02", Nombre = "VIAL EVA VISNÚ 2" }
            },
            ImporteMinimoProductosRequeridos = importeMinimo
        };

        [TestMethod]
        public async Task CargarEvento_EnsenaSuImporteMinimo()
        {
            var vm = await Mantenimiento(Viales(400));

            Assert.AreEqual(400M, vm.ImporteMinimoProductosRequeridosEdicion);
            Assert.IsTrue(vm.HayProductosRequeridos);
        }

        [TestMethod]
        public async Task ApiDeAntes_SinElCampo_VacioYNoSeManda()
        {
            var vm = await Mantenimiento(Viales(null));

            Assert.IsNull(vm.ImporteMinimoProductosRequeridosEdicion);
            await vm.GuardarAsync();

            Assert.AreEqual(0, errores.Count, string.Join(" | ", errores));
            Assert.IsNull(guardados.Single().ImporteMinimoProductosRequeridos);
            Assert.IsFalse(JsonConvert.SerializeObject(guardados.Single()).Contains("ImporteMinimoProductosRequeridos"));
        }

        [TestMethod]
        public async Task Guardar_MandaElImporteMinimo()
        {
            var vm = await Mantenimiento(Viales(null));
            vm.ImporteMinimoProductosRequeridosEdicion = 400;

            await vm.GuardarAsync();

            Assert.AreEqual(0, errores.Count, string.Join(" | ", errores));
            StringAssert.Contains(JsonConvert.SerializeObject(guardados.Single()), "\"ImporteMinimoProductosRequeridos\":400");
        }

        [TestMethod]
        public async Task SinCambiarlo_NoSeManda()
        {
            var vm = await Mantenimiento(Viales(400));

            await vm.GuardarAsync();

            Assert.IsNull(guardados.Single().ImporteMinimoProductosRequeridos, "No se toca");
        }

        [TestMethod]
        public async Task Vaciarlo_MandaCeroParaQuitarlo()
        {
            var vm = await Mantenimiento(Viales(400));
            vm.ImporteMinimoProductosRequeridosEdicion = null;

            await vm.GuardarAsync();

            Assert.AreEqual(0M, guardados.Single().ImporteMinimoProductosRequeridos);
        }

        [TestMethod]
        public async Task SinProductosRequeridos_SeQuitaElMinimo()
        {
            var vm = await Mantenimiento(Viales(400));
            var cambios = new List<string>();
            ((INotifyPropertyChanged)vm).PropertyChanged += (s, e) => cambios.Add(e.PropertyName);

            vm.ProductosRequeridosEdicion.Clear();
            await vm.GuardarAsync();

            Assert.IsFalse(vm.HayProductosRequeridos);
            CollectionAssert.Contains(cambios, nameof(MantenimientoEventosViewModel.HayProductosRequeridos));
            Assert.AreEqual(0, errores.Count, string.Join(" | ", errores));
            Assert.AreEqual(0M, guardados.Single().ImporteMinimoProductosRequeridos, "Sin productos que comprar, el mínimo no tiene sentido");
        }

        [TestMethod]
        public async Task Negativo_AvisaDeUstedYNoGuarda()
        {
            var vm = await Mantenimiento(Viales(null));
            vm.ImporteMinimoProductosRequeridosEdicion = -1;

            await vm.GuardarAsync();

            Assert.AreEqual(0, guardados.Count);
            StringAssert.Contains(errores.Single(), "no puede ser negativo");
        }

        [TestMethod]
        public async Task Nuevo_SinMinimo()
        {
            var vm = await Mantenimiento(Viales(400));

            vm.NuevoCommand.Execute(null);

            Assert.IsNull(vm.ImporteMinimoProductosRequeridosEdicion);
            Assert.IsFalse(vm.HayProductosRequeridos);
        }
    }
}
