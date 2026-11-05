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
    /// Nesto#626 (NestoAPI#829, sugerencia 1064 de Reina): en los eventos mixtos, cada inscrito dice si viene presencial u online;
    /// la lista se filtra y se ordena por eso y la cabecera cuenta cuántos de cada.
    /// </summary>
    [TestClass]
    public class EventosFormaAsistenciaTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();

        public EventosFormaAsistenciaTests()
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
        }

        private static InscripcionEventoModel Inscripcion(int id, string forma = null, string estado = InscripcionEventoModel.PAGADA) =>
            new()
            {
                Id = id, EventoId = 7, Evento = "Masterclass", FechaEvento = DateTime.Today.AddDays(5), Empresa = "1",
                Cliente = "1519" + id, Contacto = "0", Nombre = "CLIENTA " + id, Estado = estado,
                Modalidad = InscribirEventoModel.MODALIDAD_REEMBOLSABLE, FormaAsistencia = forma
            };

        private async Task<MantenimientoEventosViewModel> Mantenimiento(string formato, params InscripcionEventoModel[] inscritos)
        {
            var evento = new EventoModel { Id = 7, Empresa = "1", Titulo = "Masterclass", Fecha = DateTime.Today.AddDays(5), Almacen = "REI", Activo = true, Formato = formato };
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { evento });
            A.CallTo(() => eventos.LeerInscripciones(7)).ReturnsLazily(() => inscritos.Select(i => Inscripcion(i.Id, i.FormaAsistencia, i.Estado)).ToList());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            await vm.CargarInscripcionesAsync();
            return vm;
        }

        private static List<int> Ids(MantenimientoEventosViewModel vm) => vm.InscripcionesVisibles.Select(i => i.Id).ToList();

        [TestMethod]
        public void EventoModel_Mixto()
        {
            Assert.IsTrue(new EventoModel { Formato = "mixto " }.EsMixto);
            Assert.IsFalse(new EventoModel { Formato = "Mixto" }.EsOnline);
            Assert.IsFalse(new EventoModel { Formato = null }.EsMixto);
        }

        [TestMethod]
        public async Task Formatos_IncluyenMixto_YSeCargaYSeManda()
        {
            var vm = await Mantenimiento(EventoModel.FORMATO_MIXTO);

            Assert.AreEqual(EventoModel.FORMATO_MIXTO, vm.FormatoEdicion.Valor);
            Assert.AreEqual("Mixto (presencial y online)", vm.Formatos.Single(f => f.Valor == EventoModel.FORMATO_MIXTO).Texto);
            Assert.IsTrue(vm.EventoEsMixto);
        }

        [TestMethod]
        public async Task NoMixto_SinFiltroNiOrdenNiContador()
        {
            var vm = await Mantenimiento(null, Inscripcion(1, "Online"), Inscripcion(2));
            vm.FiltroFormaAsistencia = vm.FiltrosFormaAsistencia.Single(f => f.Valor == InscripcionEventoModel.FORMA_PRESENCIAL);
            vm.OrdenarPorFormaAsistencia = true;

            Assert.IsFalse(vm.EventoEsMixto);
            CollectionAssert.AreEqual(new List<int> { 1, 2 }, Ids(vm), "En un evento que no es mixto el filtro no se aplica");
            Assert.AreEqual(string.Empty, vm.ResumenFormaAsistencia);
        }

        [TestMethod]
        public async Task Filtro_PresencialOnlineSinIndicarYTodos()
        {
            var vm = await Mantenimiento(EventoModel.FORMATO_MIXTO,
                Inscripcion(1, "Online"), Inscripcion(2, "Presencial"), Inscripcion(3), Inscripcion(4, "online"));

            vm.FiltroFormaAsistencia = vm.FiltrosFormaAsistencia.Single(f => f.Texto == "Online");
            CollectionAssert.AreEqual(new List<int> { 1, 4 }, Ids(vm));

            vm.FiltroFormaAsistencia = vm.FiltrosFormaAsistencia.Single(f => f.Texto == "Presencial");
            CollectionAssert.AreEqual(new List<int> { 2 }, Ids(vm));

            vm.FiltroFormaAsistencia = vm.FiltrosFormaAsistencia.Single(f => f.Texto == "Sin indicar");
            CollectionAssert.AreEqual(new List<int> { 3 }, Ids(vm));

            vm.FiltroFormaAsistencia = vm.FiltrosFormaAsistencia.Single(f => f.Texto == "Todos");
            CollectionAssert.AreEqual(new List<int> { 1, 2, 3, 4 }, Ids(vm));
        }

        [TestMethod]
        public async Task Orden_PresencialOnlineYSinIndicar_Estable()
        {
            var vm = await Mantenimiento(EventoModel.FORMATO_MIXTO,
                Inscripcion(1, "Online"), Inscripcion(2), Inscripcion(3, "Presencial"), Inscripcion(4, "Online"), Inscripcion(5, "Presencial"));

            vm.OrdenarPorFormaAsistencia = true;
            CollectionAssert.AreEqual(new List<int> { 3, 5, 1, 4, 2 }, Ids(vm));

            vm.OrdenarPorFormaAsistencia = false;
            CollectionAssert.AreEqual(new List<int> { 1, 2, 3, 4, 5 }, Ids(vm), "Sin ordenar, como viene de la API");
        }

        [TestMethod]
        public async Task Contador_SoloVivas()
        {
            var vm = await Mantenimiento(EventoModel.FORMATO_MIXTO,
                Inscripcion(1, "Online"), Inscripcion(2, "Presencial"), Inscripcion(3, "Presencial"), Inscripcion(4),
                Inscripcion(5, "Online", InscripcionEventoModel.ANULADA));

            Assert.AreEqual("2 presencial · 1 online · 1 sin indicar", vm.ResumenFormaAsistencia);
        }

        [TestMethod]
        public async Task ElegirEnElCombo_SeGuardaYSeRehaceElFiltro()
        {
            var vm = await Mantenimiento(EventoModel.FORMATO_MIXTO, Inscripcion(1), Inscripcion(2, "Presencial"));
            A.CallTo(() => eventos.CambiarFormaAsistencia(1, "Online")).Returns(Inscripcion(1, "Online"));
            vm.FiltroFormaAsistencia = vm.FiltrosFormaAsistencia.Single(f => f.Texto == "Sin indicar");
            InscripcionEventoModel primera = vm.InscripcionesVisibles.Single();

            primera.FormaAsistenciaEdicion = InscripcionEventoModel.FORMA_ONLINE;
            await Task.Yield();

            A.CallTo(() => eventos.CambiarFormaAsistencia(1, "Online")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(InscripcionEventoModel.FORMA_ONLINE, primera.FormaAsistenciaGuardada);
            Assert.AreEqual(0, vm.InscripcionesVisibles.Count, "Ya no está «sin indicar»");
            Assert.AreEqual("1 presencial · 1 online", vm.ResumenFormaAsistencia);
        }

        [TestMethod]
        public async Task ElegirSinIndicar_MandaVacio()
        {
            var vm = await Mantenimiento(EventoModel.FORMATO_MIXTO, Inscripcion(1, "Presencial"));
            A.CallTo(() => eventos.CambiarFormaAsistencia(1, A<string>._)).Returns(Inscripcion(1));

            vm.Inscripciones[0].FormaAsistenciaEdicion = string.Empty;
            await Task.Yield();

            A.CallTo(() => eventos.CambiarFormaAsistencia(1, string.Empty)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(string.Empty, vm.Inscripciones[0].FormaAsistenciaGuardada);
        }

        [TestMethod]
        public async Task SiLaApiNoLoAcepta_MensajeYVuelveALoQueHabia()
        {
            var vm = await Mantenimiento(EventoModel.FORMATO_MIXTO, Inscripcion(1, "Presencial"));
            A.CallTo(() => eventos.CambiarFormaAsistencia(1, A<string>._))
                .Throws(new Exception("«Masterclass» no es un evento mixto"));

            vm.Inscripciones[0].FormaAsistenciaEdicion = InscripcionEventoModel.FORMA_ONLINE;
            await Task.Yield();

            A.CallTo(() => dialogos.ShowError("«Masterclass» no es un evento mixto")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(InscripcionEventoModel.FORMA_PRESENCIAL, vm.Inscripciones[0].FormaAsistenciaEdicion);
            A.CallTo(() => eventos.CambiarFormaAsistencia(1, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SinPermisos_NoLlamaALaApiYVuelve()
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(A<string>._)).Returns(false);
            var vm = await Mantenimiento(EventoModel.FORMATO_MIXTO, Inscripcion(1));

            vm.Inscripciones[0].FormaAsistenciaEdicion = InscripcionEventoModel.FORMA_ONLINE;
            await Task.Yield();

            A.CallTo(() => eventos.CambiarFormaAsistencia(A<int>._, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual(string.Empty, vm.Inscripciones[0].FormaAsistenciaEdicion);
        }

        [TestMethod]
        public void Combo_NullDelTeardownNoEsUnaEleccion()
        {
            InscripcionEventoModel inscripcion = Inscripcion(1, "Online");

            inscripcion.FormaAsistenciaEdicion = null;

            Assert.AreEqual(InscripcionEventoModel.FORMA_ONLINE, inscripcion.FormaAsistenciaEdicion);
        }

        [TestMethod]
        public void Model_LaFormaDeLaApiSeLeeEnElDto()
        {
            InscripcionEventoModel leida = Newtonsoft.Json.JsonConvert.DeserializeObject<InscripcionEventoModel>("{ \"Id\": 1, \"FormaAsistencia\": \"Online\" }");
            Assert.AreEqual(InscripcionEventoModel.FORMA_ONLINE, leida.FormaAsistenciaGuardada);
            Assert.AreEqual(InscripcionEventoModel.FORMA_ONLINE, leida.FormaAsistenciaEdicion);
            Assert.AreEqual(string.Empty, new InscripcionEventoModel().FormaAsistenciaGuardada);
        }
    }
}
