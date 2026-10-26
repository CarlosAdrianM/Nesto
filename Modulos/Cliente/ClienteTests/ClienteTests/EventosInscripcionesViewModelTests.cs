using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#517 (NestoAPI#611/#612): campos nuevos del evento (tienda, referencia del curso, plazas, lugar, descripción),
    /// inscritos del evento (inscribir sin pedido, anular, marcar pagada, pasar lista, facturar el curso), el diálogo de
    /// inscribir y la pestaña «Eventos» de la ficha del cliente. Las reglas viven en la API; los VM piden y pintan.
    /// </summary>
    [TestClass]
    public class EventosInscripcionesViewModelTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();

        public EventosInscripcionesViewModelTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).Returns(true);
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored)).Returns(true);
        }

        private static EventoModel Evento(int id, DateTime fecha, string almacen = "REI") =>
            new() { Id = id, Empresa = "1", Titulo = "Masterclass Cloasma", Fecha = fecha, ImporteSenal = 50, Activo = true, Almacen = almacen };

        private static InscripcionEventoModel Inscripcion(int id, string estado, FacturacionCursoModel facturacion = null) =>
            new()
            {
                Id = id, EventoId = 7, Evento = "Masterclass Cloasma", Empresa = "1", Cliente = "15191", Contacto = "0",
                Nombre = "Peluquería Pepa", Estado = estado, EstadoTexto = estado, Facturacion = facturacion
            };

        private async Task<MantenimientoEventosViewModel> MantenimientoConEvento(EventoModel evento, params InscripcionEventoModel[] inscritos)
        {
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { evento });
            A.CallTo(() => eventos.LeerInscripciones(evento.Id)).Returns(inscritos.ToList());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            await vm.CargarInscripcionesAsync();
            return vm;
        }

        // ---------------- Ficha del evento ----------------

        [TestMethod]
        public async Task Guardar_MandaLaTiendaLaReferenciaDelCursoLasPlazasElLugarYLaDescripcion()
        {
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).ReturnsLazily((EventoModel e) => e);
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();

            vm.NuevoCommand.Execute(null);
            vm.TituloEdicion = "Masterclass Cloasma";
            vm.FechaEdicion = DateTime.Today.AddDays(10);
            vm.ImporteSenalEdicion = 50;
            vm.ProductoCursoEdicion = " CURSO01 ";
            vm.PlazasEdicion = 12;
            vm.LugarEdicion = " Aula de Reina ";
            vm.DescripcionLargaEdicion = "   ";
            await vm.GuardarAsync();

            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.That.Matches(e =>
                e.Almacen == "REI" && e.ProductoCurso == "CURSO01" && e.Plazas == 12 && e.Lugar == "Aula de Reina" && e.DescripcionLarga == null)))
                .MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task Nuevo_ElEventoNaceEnLaTiendaDelUsuario_YSinTiendaSiNoEsDeEventos()
        {
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.NuevoCommand.Execute(null);
            Assert.AreEqual("REI", vm.TiendaEdicion.Codigo);

            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("ALG");
            var otro = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await otro.CargarAsync();
            otro.NuevoCommand.Execute(null);
            Assert.IsNull(otro.TiendaEdicion, "Algete no hace eventos: hay que elegir la tienda");
        }

        [TestMethod]
        public async Task Guardar_SinTienda_NoDejaYLoExplica_TambienEnLosOnline()
        {
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("ALG");
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.NuevoCommand.Execute(null);
            vm.TituloEdicion = "Webinar Cloasma";
            vm.FormatoEdicion = vm.Formatos.Single(f => f.Valor == EventoModel.FORMATO_ONLINE);

            await vm.GuardarAsync();

            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).MustNotHaveHappened();
            StringAssert.StartsWith(errores.Single(), "Elija la tienda del evento (Reina o Alcobendas). También en los online");
        }

        [TestMethod]
        public async Task Formato_PorDefectoPresencial_SeCargaYSeManda()
        {
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).ReturnsLazily((EventoModel e) => e);
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.NuevoCommand.Execute(null);
            Assert.AreEqual(EventoModel.FORMATO_PRESENCIAL, vm.FormatoEdicion.Valor);
            Assert.AreEqual("Online (webinar)", vm.Formatos.Single(f => f.Valor == EventoModel.FORMATO_ONLINE).Texto);

            vm.TituloEdicion = "Webinar Cloasma";
            vm.FormatoEdicion = vm.Formatos.Single(f => f.Valor == EventoModel.FORMATO_ONLINE);
            await vm.GuardarAsync();
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.That.Matches(e => e.Formato == "Online" && e.Almacen == "REI")))
                .MustHaveHappenedOnceExactly();

            EventoModel sinFormato = Evento(8, DateTime.Today.AddDays(5));
            MantenimientoEventosViewModel otro = await MantenimientoConEvento(sinFormato);
            Assert.AreEqual(EventoModel.FORMATO_PRESENCIAL, otro.FormatoEdicion.Valor, "Null cuenta como Presencial");
        }

        [TestMethod]
        public async Task Seleccionar_CargaLaFichaYLosInscritos()
        {
            EventoModel evento = Evento(7, DateTime.Today.AddDays(5), "ALC");
            evento.ProductoCurso = "CURSO01";
            evento.Plazas = 10;
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(evento, Inscripcion(1, InscripcionEventoModel.RESERVADA), Inscripcion(2, InscripcionEventoModel.PAGADA));

            Assert.AreEqual("ALC", vm.TiendaEdicion.Codigo);
            Assert.AreEqual("CURSO01", vm.ProductoCursoEdicion);
            Assert.AreEqual(10, vm.PlazasEdicion);
            Assert.AreEqual(2, vm.Inscripciones.Count);
            StringAssert.StartsWith(vm.ResumenInscripciones, "2 inscritos");
        }

        [TestMethod]
        public void EventoSinTienda_NoMandaAlmacen_PeroSiLasPlazasVacias()
        {
            // La API exige REI o ALC si Almacen viene en el cuerpo, aunque sea null; las plazas null sí se mandan (sin límite).
            JObject json = JObject.Parse(JsonConvert.SerializeObject(new EventoModel { Titulo = "X", Almacen = null, Plazas = null }));

            Assert.IsFalse(json.ContainsKey("Almacen"));
            Assert.IsTrue(json.ContainsKey("Plazas"));
            Assert.IsTrue(json.ContainsKey("DescripcionLarga"));
            Assert.IsFalse(json.ContainsKey("NombreTienda"));
        }

        [TestMethod]
        public async Task ComprobarProducto_DiceSiNoExisteOSiNoEsExento()
        {
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            vm.NuevoCommand.Execute(null);
            A.CallTo(() => eventos.LeerProductoCurso("NOEXISTE")).Returns(Task.FromResult<ProductoCursoModel>(null));
            vm.ProductoCursoEdicion = "NOEXISTE";
            await vm.ComprobarProductoAsync();
            StringAssert.Contains(vm.NombreProductoCurso, "No existe el producto NOEXISTE");

            A.CallTo(() => eventos.LeerProductoCurso("CURSO21")).Returns(new ProductoCursoModel { Producto = "CURSO21", Nombre = "Curso peelings", Iva = "G21" });
            vm.ProductoCursoEdicion = "CURSO21";
            await vm.ComprobarProductoAsync();
            StringAssert.Contains(vm.NombreProductoCurso, "exentos (EX)");

            A.CallTo(() => eventos.LeerProductoCurso("CURSO01")).Returns(new ProductoCursoModel { Producto = "CURSO01", Nombre = "Masterclass", Iva = "EX" });
            vm.ProductoCursoEdicion = "CURSO01";
            await vm.ComprobarProductoAsync();
            Assert.AreEqual("Masterclass (exento)", vm.NombreProductoCurso);
        }

        [TestMethod]
        public async Task ComprobarProducto_TieneQueSerDelGrupoCUR()
        {
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            vm.NuevoCommand.Execute(null);
            A.CallTo(() => eventos.LeerProductoCurso("12345")).Returns(new ProductoCursoModel { Producto = "12345", Nombre = "Crema", Iva = "G21", Grupo = "COS" });
            vm.ProductoCursoEdicion = "12345";
            await vm.ComprobarProductoAsync();
            Assert.AreEqual("La referencia 12345 (Crema) es del grupo COS, no de cursos (CUR): escriba una referencia de curso o pulse «Crear referencia».", vm.NombreProductoCurso);

            A.CallTo(() => eventos.LeerProductoCurso("90077")).Returns(new ProductoCursoModel { Producto = "90077", Nombre = "Curso", Iva = "EX", Grupo = "CUR" });
            vm.ProductoCursoEdicion = "90077";
            await vm.ComprobarProductoAsync();
            Assert.AreEqual("Curso (exento)", vm.NombreProductoCurso);
        }

        // ---------------- Inscritos ----------------

        [TestMethod]
        public async Task Inscribir_AbreElDialogoConElEvento_YRecargaAlVolver()
        {
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today.AddDays(5)));
            A.CallTo(() => dialogos.ShowDialogAsync(InscribirEventoDialogViewModel.NOMBRE, A<ParametrosDialogo>.Ignored))
                .Returns(new ResultadoDialogo(ResultadoBoton.OK));

            await vm.InscribirAsync();

            A.CallTo(() => dialogos.ShowDialogAsync(InscribirEventoDialogViewModel.NOMBRE,
                A<ParametrosDialogo>.That.Matches(p => p.GetValue<int>("eventoId") == 7))).MustHaveHappenedOnceExactly();
            A.CallTo(() => eventos.LeerInscripciones(7)).MustHaveHappenedTwiceOrMore();
        }

        [TestMethod]
        public async Task Anular_SoloLasReservadas()
        {
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today.AddDays(5)),
                Inscripcion(1, InscripcionEventoModel.RESERVADA), Inscripcion(2, InscripcionEventoModel.PAGADA));
            A.CallTo(() => eventos.AnularInscripcion(1)).Returns(Inscripcion(1, InscripcionEventoModel.ANULADA));

            vm.InscripcionSeleccionada = vm.Inscripciones[1];
            Assert.IsFalse(vm.AnularInscripcionCommand.CanExecute(null), "Una pagada no se anula");
            vm.InscripcionSeleccionada = vm.Inscripciones[0];
            Assert.IsTrue(vm.AnularInscripcionCommand.CanExecute(null));
            await vm.AnularInscripcionAsync();

            A.CallTo(() => eventos.AnularInscripcion(1)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(InscripcionEventoModel.ANULADA, vm.Inscripciones.Single(i => i.Id == 1).Estado);
        }

        [TestMethod]
        public async Task MarcarPagada_EnEfectivo_MandaFormaEImporte_YHoyEsAhora()
        {
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today.AddDays(5)), Inscripcion(1, InscripcionEventoModel.RESERVADA));
            A.CallTo(() => eventos.MarcarPagada(1, A<MarcarPagoInscripcionModel>.Ignored)).Returns(Inscripcion(1, InscripcionEventoModel.PAGADA));
            vm.InscripcionSeleccionada = vm.Inscripciones.Single();
            Assert.AreEqual(50, vm.ImportePago, "Por defecto, la señal del evento");

            vm.FormaPagoPago = MarcarPagoInscripcionModel.EFECTIVO;
            await vm.MarcarPagadaAsync();

            A.CallTo(() => eventos.MarcarPagada(1, A<MarcarPagoInscripcionModel>.That.Matches(p =>
                p.FormaPago == "Efectivo" && p.Importe == 50 && p.Fecha == null))).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.That.Contains("caja de la tienda"))).MustHaveHappened();
            Assert.AreEqual(InscripcionEventoModel.PAGADA, vm.InscripcionSeleccionada.Estado);
        }

        [TestMethod]
        public async Task MarcarPagada_OtroDia_MandaEseDia_YSinConfirmarNoMarca()
        {
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today.AddDays(5)), Inscripcion(1, InscripcionEventoModel.CADUCADA));
            A.CallTo(() => eventos.MarcarPagada(1, A<MarcarPagoInscripcionModel>.Ignored)).Returns(Inscripcion(1, InscripcionEventoModel.CADUCADA));
            vm.InscripcionSeleccionada = vm.Inscripciones.Single();
            Assert.IsTrue(vm.MarcarPagadaCommand.CanExecute(null), "Una caducada que paga tarde se puede marcar");
            vm.FechaPagoEdicion = DateTime.Today.AddDays(-2);
            await vm.MarcarPagadaAsync();
            A.CallTo(() => eventos.MarcarPagada(1, A<MarcarPagoInscripcionModel>.That.Matches(p =>
                p.FormaPago == "Transferencia" && p.Fecha == DateTime.Today.AddDays(-2)))).MustHaveHappenedOnceExactly();

            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).Returns(false);
            await vm.MarcarPagadaAsync();
            A.CallTo(() => eventos.MarcarPagada(A<int>.Ignored, A<MarcarPagoInscripcionModel>.Ignored)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task MarcarPagada_FechaFutura_NoLlamaALaApi()
        {
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today.AddDays(5)), Inscripcion(1, InscripcionEventoModel.RESERVADA));
            vm.InscripcionSeleccionada = vm.Inscripciones.Single();
            vm.FechaPagoEdicion = DateTime.Today.AddDays(1);

            await vm.MarcarPagadaAsync();

            A.CallTo(() => eventos.MarcarPagada(A<int>.Ignored, A<MarcarPagoInscripcionModel>.Ignored)).MustNotHaveHappened();
            Assert.AreEqual("La fecha del pago no puede ser futura.", errores.Single());
        }

        [TestMethod]
        public async Task PasarLista_AntesDelEvento_NoSePuede()
        {
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today.AddDays(1)), Inscripcion(1, InscripcionEventoModel.PAGADA));

            Assert.IsFalse(vm.SePuedePasarLista);
            Assert.IsFalse(vm.GuardarAsistenciaCommand.CanExecute(null));
            StringAssert.Contains(vm.AvisoListaAsistencia, "desde el día del evento");
        }

        [TestMethod]
        public async Task PasarLista_ElDiaDelEvento_MandaSoloLosMarcadosDeLosQueEntranEnLaLista()
        {
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today),
                Inscripcion(1, InscripcionEventoModel.PAGADA), Inscripcion(2, InscripcionEventoModel.FACTURADA),
                Inscripcion(3, InscripcionEventoModel.ASISTIO), Inscripcion(4, InscripcionEventoModel.RESERVADA));
            Assert.IsTrue(vm.Inscripciones.Single(i => i.Id == 3).Asistio, "La que ya consta que vino empieza marcada");
            Assert.IsFalse(vm.Inscripciones.Single(i => i.Id == 4).EntraEnListaAsistencia, "Sin pagar no entra en la lista");

            vm.Inscripciones.Single(i => i.Id == 1).Asistio = true;
            vm.Inscripciones.Single(i => i.Id == 4).Asistio = true; // no cuenta: no está pagada
            Assert.IsTrue(vm.GuardarAsistenciaCommand.CanExecute(null));
            await vm.GuardarAsistenciaAsync();

            A.CallTo(() => eventos.PasarLista(7, A<IEnumerable<int>>.That.Matches(a => a.OrderBy(x => x).SequenceEqual(new[] { 1, 3 }))))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowConfirmationAnswer("Lista de asistencia",
                    "Las siguientes personas pagaron y no están marcadas: Peluquería Pepa. Quedarán como ‘No asistió’ y perderán su cupón. ¿Desea guardar la lista?",
                    true))
                .MustHaveHappenedOnceExactly();
        }

        private static InscripcionEventoModel Inscrita(int id, string estado, string nombre, string modalidad = InscribirEventoModel.MODALIDAD_REEMBOLSABLE)
        {
            InscripcionEventoModel inscripcion = Inscripcion(id, estado);
            inscripcion.Nombre = nombre;
            inscripcion.Modalidad = modalidad;
            return inscripcion;
        }

        [TestMethod]
        public async Task GuardarLista_PagadasSinMarcar_PreguntaConSusNombresConNoPorDefecto_YSiDiceNoNoGuarda()
        {
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today),
                Inscrita(1, InscripcionEventoModel.PAGADA, "Ana P. "), Inscrita(2, InscripcionEventoModel.FACTURADA, "Lucía R."),
                Inscrita(3, InscripcionEventoModel.PAGADA, "Marta S."), Inscrita(4, InscripcionEventoModel.RESERVADA, "Sin pagar"));
            vm.Inscripciones.Single(i => i.Id == 3).Asistio = true;
            string mensaje = null;
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored, true))
                .ReturnsLazily((string t, string m, bool n) => { mensaje = m; return false; });

            await vm.GuardarAsistenciaAsync();

            Assert.AreEqual("Las siguientes personas pagaron y no están marcadas: Ana P., Lucía R. Quedarán como ‘No asistió’ y perderán su cupón. ¿Desea guardar la lista?",
                mensaje);
            A.CallTo(() => eventos.PasarLista(A<int>.Ignored, A<IEnumerable<int>>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task GuardarLista_PagadasSinMarcar_SiDiceSi_Guarda()
        {
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today),
                Inscrita(1, InscripcionEventoModel.PAGADA, "Ana P."), Inscrita(2, InscripcionEventoModel.PAGADA, "Lucía R."));
            vm.Inscripciones.Single(i => i.Id == 2).Asistio = true;
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored, true)).Returns(true);

            await vm.GuardarAsistenciaAsync();

            A.CallTo(() => eventos.PasarLista(7, A<IEnumerable<int>>.That.Matches(a => a.SequenceEqual(new[] { 2 })))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task GuardarLista_TodasLasQuePagaronMarcadas_GuardaSinPreguntar()
        {
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today),
                Inscrita(1, InscripcionEventoModel.PAGADA, "Ana P."), Inscrita(2, InscripcionEventoModel.ASISTIO, "Lucía R."),
                // La gratuita sin marcar no pagó: no se pregunta por ella.
                Inscrita(3, InscripcionEventoModel.INSCRITA, "Gratis G.", InscribirEventoModel.MODALIDAD_GRATUITA));
            vm.Inscripciones.Single(i => i.Id == 1).Asistio = true;

            await vm.GuardarAsistenciaAsync();

            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => eventos.PasarLista(7, A<IEnumerable<int>>.That.Matches(a => a.OrderBy(x => x).SequenceEqual(new[] { 1, 2 }))))
                .MustHaveHappenedOnceExactly();
        }

        // ---------------- Crear la referencia del curso (POST api/Eventos/{id}/ProductoCurso) ----------------

        [TestMethod]
        public async Task CrearReferencia_ConLasDosSenales_CreaLasDosYPoneLaReembolsableEnElEvento()
        {
            EventoModel evento = Evento(7, DateTime.Today.AddDays(5));
            evento.ImporteSenalNoReembolsable = 60;
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(evento);
            A.CallTo(() => eventos.CrearProductoCurso(7, InscribirEventoModel.MODALIDAD_REEMBOLSABLE))
                .Returns(new ProductoCursoCreadoModel { Producto = "EVENTO00007", Nombre = "Masterclass Cloasma", Creado = true });
            A.CallTo(() => eventos.CrearProductoCurso(7, InscribirEventoModel.MODALIDAD_NO_REEMBOLSABLE))
                .Returns(new ProductoCursoCreadoModel { Producto = "EVENTO00007NR", Nombre = "Masterclass Cloasma (no reembolsable)", Creado = false });

            Assert.IsTrue(vm.CrearProductoCursoCommand.CanExecute(null));
            await vm.CrearProductoCursoAsync();

            Assert.AreEqual("EVENTO00007", vm.ProductoCursoEdicion);
            Assert.IsFalse(vm.TieneReferenciaForzada, "La propia del evento no es forzada");
            Assert.IsTrue(vm.CrearProductoCursoCommand.CanExecute(null), "Se puede volver a pulsar (la API devuelve las que ya existen)");
            StringAssert.Contains(vm.NombreProductoCurso, "EVENTO00007 (Masterclass Cloasma) creada.");
            StringAssert.Contains(vm.NombreProductoCurso, "EVENTO00007NR (Masterclass Cloasma (no reembolsable)) ya existía.");
            Assert.IsTrue(vm.ComprobarProductoCommand.CanExecute(null), "«Comprobar» sigue funcionando con la referencia nueva");
            A.CallTo(() => eventos.CrearProductoCurso(A<int>.Ignored, A<string>.Ignored)).MustHaveHappenedTwiceExactly();
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task CrearReferencia_SoloNoReembolsable_CreaSoloEsaYLaPoneEnElEvento()
        {
            EventoModel evento = Evento(7, DateTime.Today.AddDays(5));
            evento.ImporteSenal = 0;
            evento.ImporteSenalNoReembolsable = 60;
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(evento);
            A.CallTo(() => eventos.CrearProductoCurso(7, InscribirEventoModel.MODALIDAD_NO_REEMBOLSABLE))
                .Returns(new ProductoCursoCreadoModel { Producto = "EVENTO00007NR", Nombre = "Curso", Creado = true });

            await vm.CrearProductoCursoAsync();

            Assert.AreEqual("EVENTO00007NR", vm.ProductoCursoEdicion);
            A.CallTo(() => eventos.CrearProductoCurso(7, InscribirEventoModel.MODALIDAD_REEMBOLSABLE)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task CrearReferencia_ConUnaReferenciaForzada_NoCreaNada()
        {
            EventoModel evento = Evento(7, DateTime.Today.AddDays(5));
            evento.ImporteSenalNoReembolsable = 60;
            evento.ProductoCurso = "90077";
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(evento);

            Assert.IsTrue(vm.TieneReferenciaForzada);
            Assert.IsFalse(vm.CrearProductoCursoCommand.CanExecute(null));
            await vm.CrearProductoCursoAsync();

            A.CallTo(() => eventos.CrearProductoCurso(A<int>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
            Assert.AreEqual("90077", vm.ProductoCursoEdicion);

            vm.ProductoCursoEdicion = "";
            Assert.IsTrue(vm.CrearProductoCursoCommand.CanExecute(null), "Al borrarla se puede crear la propia");
        }

        [TestMethod]
        public async Task CrearReferencia_EventoSinGuardar_NoSePuede()
        {
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();

            vm.NuevoCommand.Execute(null);
            await vm.CrearProductoCursoAsync();

            Assert.IsFalse(vm.CrearProductoCursoCommand.CanExecute(null));
            A.CallTo(() => eventos.CrearProductoCurso(A<int>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task CrearReferencia_SiLaApiFalla_EnseñaElErrorYNoTocaLaReferencia()
        {
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today.AddDays(5)));
            A.CallTo(() => eventos.CrearProductoCurso(7, A<string>.Ignored))
                .ThrowsAsync(new Exception("No se pudo crear la referencia del curso: no tiene permiso para crear productos."));

            await vm.CrearProductoCursoAsync();

            Assert.IsNull(vm.ProductoCursoEdicion);
            Assert.AreEqual("No se pudo crear la referencia del curso: no tiene permiso para crear productos.", errores.Single());
        }

        [TestMethod]
        public async Task Facturar_SoloConErrorONoAutomatizable_YEnseñaElMotivo()
        {
            var conError = new FacturacionCursoModel { Estado = FacturacionCursoModel.ERROR, Mensaje = "Fecha no permitida" };
            var facturada = new FacturacionCursoModel { Estado = FacturacionCursoModel.FACTURADA, Factura = "CV2600700" };
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today.AddDays(5)),
                Inscripcion(1, InscripcionEventoModel.PAGADA, conError), Inscripcion(2, InscripcionEventoModel.FACTURADA, facturada));
            A.CallTo(() => eventos.FacturarCurso(1)).Returns(Inscripcion(1, InscripcionEventoModel.FACTURADA, facturada));

            vm.InscripcionSeleccionada = vm.Inscripciones.Single(i => i.Id == 2);
            Assert.IsFalse(vm.FacturarCommand.CanExecute(null));
            Assert.AreEqual("CV2600700 (Facturada)", vm.InscripcionSeleccionada.TextoFacturacion);
            vm.InscripcionSeleccionada = vm.Inscripciones.Single(i => i.Id == 1);
            Assert.AreEqual("Fecha no permitida", vm.MensajeFacturacion);
            Assert.IsTrue(vm.FacturarCommand.CanExecute(null));
            await vm.FacturarAsync();

            A.CallTo(() => eventos.FacturarCurso(1)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(InscripcionEventoModel.FACTURADA, vm.Inscripciones.Single(i => i.Id == 1).Estado);
        }

        [TestMethod]
        public async Task ErrorDeLaApi_SeEnseña()
        {
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today.AddDays(5)), Inscripcion(1, InscripcionEventoModel.RESERVADA));
            A.CallTo(() => eventos.AnularInscripcion(1)).Throws(new Exception("No se pudo anular la inscripción: sin permiso"));
            vm.InscripcionSeleccionada = vm.Inscripciones.Single();

            await vm.AnularInscripcionAsync();

            Assert.AreEqual("No se pudo anular la inscripción: sin permiso", errores.Single());
        }

        // ---------------- Diálogo de inscribir ----------------

        private InscribirEventoDialogViewModel Dialogo(params EventoModel[] activos)
        {
            A.CallTo(() => eventos.LeerEventos(true)).Returns(activos.ToList());
            return new InscribirEventoDialogViewModel(eventos);
        }

        [TestMethod]
        public void Dialogo_DesdeLaFicha_ConTarjeta_GeneraElEnlaceYLoMandaAlCorreo_YDevuelveLaInscripcion()
        {
            InscribirEventoDialogViewModel vm = Dialogo(Evento(7, DateTime.Today.AddDays(5)), Evento(8, DateTime.Today.AddDays(9)));
            InscripcionEventoModel creada = Inscripcion(11, InscripcionEventoModel.RESERVADA);
            creada.UrlPago = "https://api.nuevavision.es/pago/abc";
            A.CallTo(() => eventos.Inscribir(A<int>.Ignored, A<InscribirEventoModel>.Ignored)).Returns(creada);
            IDialogoNesto dialogo = vm;
            ResultadoDialogo resultado = null;
            dialogo.RequestClose += r => resultado = r;

            dialogo.OnDialogOpened(new ParametrosDialogo { { "empresa", "1" }, { "cliente", "15191 " }, { "contacto", "0" }, { "nombre", "Peluquería Pepa" } });
            Assert.IsTrue(vm.ClienteFijo);
            Assert.AreEqual(7, vm.Evento.Id, "Sin evento dado, el más próximo");
            vm.Evento = vm.Eventos[1];
            vm.Correo = " pepa@example.com ";
            vm.InscribirCommand.Execute(null);

            A.CallTo(() => eventos.Inscribir(8, A<InscribirEventoModel>.That.Matches(p =>
                p.Cliente == "15191" && p.Contacto == "0" && p.Origen == "Nesto" && p.FormaPago == "Tarjeta"
                && p.GenerarEnlacePago && p.Correo == "pepa@example.com"))).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.Inscrito);
            Assert.IsTrue(vm.TieneEnlace);
            Assert.IsFalse(vm.InscribirCommand.CanExecute(null), "No se inscribe dos veces");

            vm.CerrarCommand.Execute(null);
            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
            Assert.AreSame(creada, resultado.Parameters.GetValue<InscripcionEventoModel>("inscripcion"));
        }

        [TestMethod]
        public void Dialogo_EnEfectivoOPorTransferencia_SinEnlace()
        {
            InscribirEventoDialogViewModel vm = Dialogo(Evento(7, DateTime.Today.AddDays(5)));
            ((IDialogoNesto)vm).OnDialogOpened(new ParametrosDialogo { { "eventoId", 7 } });
            vm.Cliente = "15191";
            vm.FormaPago = vm.FormasPago.Single(f => f.Valor == MarcarPagoInscripcionModel.EFECTIVO);
            vm.Origen = vm.Origenes.Single(o => o.Valor == InscribirEventoModel.ORIGEN_TELEFONO);
            vm.Correo = "pepa@example.com";
            Assert.IsFalse(vm.EsTarjeta);
            StringAssert.Contains(vm.AvisoFormaPago, "caja de la tienda");

            vm.InscribirCommand.Execute(null);

            A.CallTo(() => eventos.Inscribir(7, A<InscribirEventoModel>.That.Matches(p =>
                p.FormaPago == "Efectivo" && !p.GenerarEnlacePago && p.Correo == null && p.Origen == "Telefono" && p.Contacto == null)))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Dialogo_ConElEventoDado_LoEligeYNoEnseñaLosPasados()
        {
            InscribirEventoDialogViewModel vm = Dialogo(Evento(6, DateTime.Today.AddDays(-3)), Evento(7, DateTime.Today.AddDays(5)), Evento(8, DateTime.Today.AddDays(9)));

            ((IDialogoNesto)vm).OnDialogOpened(new ParametrosDialogo { { "eventoId", 8 } });

            Assert.AreEqual(8, vm.Evento.Id);
            CollectionAssert.AreEqual(new[] { 7, 8 }, vm.Eventos.Select(e => e.Id).ToArray());
            Assert.IsFalse(vm.ClienteFijo);
            Assert.IsFalse(vm.InscribirCommand.CanExecute(null), "Falta el cliente");
        }

        [TestMethod]
        public void Dialogo_ErrorDeLaApi_SeQuedaAbiertoConElMensaje_YCerrarEsCancelar()
        {
            InscribirEventoDialogViewModel vm = Dialogo(Evento(7, DateTime.Today.AddDays(5)));
            A.CallTo(() => eventos.Inscribir(A<int>.Ignored, A<InscribirEventoModel>.Ignored))
                .Throws(new Exception("No se pudo inscribir al cliente: No quedan plazas en «Masterclass Cloasma» (12 ocupadas)."));
            IDialogoNesto dialogo = vm;
            ResultadoDialogo resultado = null;
            dialogo.RequestClose += r => resultado = r;
            dialogo.OnDialogOpened(new ParametrosDialogo { { "cliente", "15191" } });

            vm.InscribirCommand.Execute(null);
            StringAssert.Contains(vm.Mensaje, "No quedan plazas");
            Assert.IsNull(resultado);
            Assert.IsFalse(vm.Inscrito);

            vm.CerrarCommand.Execute(null);
            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
        }

        // ---------------- Sugerencia 776: avisos de la ficha al inscribir ----------------

        private const string FALTA_TELEFONO = "A la ficha de la clienta le falta el teléfono: añádalo para poder avisarla del evento.";
        private const string FALTA_CORREO = "A la ficha de la clienta le falta el correo electrónico: añádalo para poder avisarla del evento.";
        private const string FALTAN_AMBOS = "A la ficha de la clienta le faltan el teléfono y el correo electrónico: añádalos para poder avisarla del evento.";

        private InscribirEventoDialogViewModel InscritoConAvisos(List<string> avisos)
        {
            InscribirEventoDialogViewModel vm = Dialogo(Evento(7, DateTime.Today.AddDays(5)));
            InscripcionEventoModel creada = Inscripcion(11, InscripcionEventoModel.RESERVADA);
            creada.Avisos = avisos;
            A.CallTo(() => eventos.Inscribir(A<int>.Ignored, A<InscribirEventoModel>.Ignored)).Returns(creada);
            ((IDialogoNesto)vm).OnDialogOpened(new ParametrosDialogo { { "cliente", "15191" } });
            Assert.IsFalse(vm.HayAvisos, "Antes de inscribir no hay avisos");
            vm.InscribirCommand.Execute(null);
            return vm;
        }

        [TestMethod]
        public void Dialogo_FaltaElTelefono_LoAvisaSinImpedirLaInscripcion()
        {
            InscribirEventoDialogViewModel vm = InscritoConAvisos(new List<string> { FALTA_TELEFONO });

            Assert.IsTrue(vm.Inscrito);
            Assert.IsTrue(vm.HayAvisos);
            Assert.AreEqual(FALTA_TELEFONO, vm.TextoAvisos);
            Assert.IsNull(vm.Mensaje, "No es un error");
        }

        [TestMethod]
        public void Dialogo_FaltaElCorreo_LoAvisa()
        {
            InscribirEventoDialogViewModel vm = InscritoConAvisos(new List<string> { FALTA_CORREO });

            Assert.IsTrue(vm.HayAvisos);
            Assert.AreEqual(FALTA_CORREO, vm.TextoAvisos);
        }

        [TestMethod]
        public void Dialogo_FaltanLosDos_UnSoloAviso()
        {
            InscribirEventoDialogViewModel vm = InscritoConAvisos(new List<string> { FALTAN_AMBOS });

            Assert.AreEqual(1, vm.Avisos.Count);
            Assert.AreEqual(FALTAN_AMBOS, vm.TextoAvisos);
        }

        [TestMethod]
        public void Dialogo_NoFaltaNada_SinAvisos()
        {
            InscribirEventoDialogViewModel vm = InscritoConAvisos(new List<string>());

            Assert.IsTrue(vm.Inscrito);
            Assert.IsFalse(vm.HayAvisos);
            Assert.AreEqual(string.Empty, vm.TextoAvisos);
        }

        [TestMethod]
        public void Dialogo_ApiDeAntesSinAvisos_NoEnseñaNada()
        {
            InscribirEventoDialogViewModel vm = InscritoConAvisos(null);

            Assert.IsTrue(vm.Inscrito);
            Assert.IsFalse(vm.HayAvisos);
        }

        [TestMethod]
        public void Inscripcion_LeeLosAvisosDeLaApi()
        {
            var conAvisos = Newtonsoft.Json.JsonConvert.DeserializeObject<InscripcionEventoModel>(
                "{\"Id\":11,\"Estado\":\"Reservada\",\"Avisos\":[\"" + FALTA_CORREO + "\"]}");
            var sinAvisos = Newtonsoft.Json.JsonConvert.DeserializeObject<InscripcionEventoModel>("{\"Id\":11,\"Estado\":\"Reservada\"}");

            CollectionAssert.AreEqual(new[] { FALTA_CORREO }, conAvisos.Avisos);
            Assert.IsNull(sinAvisos.Avisos);
        }

        // ---------------- Pestaña «Eventos» de la ficha del cliente ----------------

        [TestMethod]
        public async Task FichaCliente_SoloCargaALaVista_YNoRepiteConElMismoCliente()
        {
            A.CallTo(() => eventos.LeerInscripcionesCliente("1", "15191")).Returns(new List<InscripcionEventoModel>
            {
                Inscripcion(1, InscripcionEventoModel.ASISTIO), Inscripcion(2, InscripcionEventoModel.RESERVADA)
            });
            var vm = new InscripcionesClienteViewModel(eventos, dialogos);

            vm.CambiarCliente("1", "15191 ", "0", "Peluquería Pepa");
            A.CallTo(() => eventos.LeerInscripcionesCliente(A<string>.Ignored, A<string>.Ignored)).MustNotHaveHappened();

            vm.ALaVista = true;
            await Task.Yield();
            Assert.AreEqual(2, vm.Inscripciones.Count);

            vm.ALaVista = false;
            vm.CambiarCliente("1", "15191", "1", "Peluquería Pepa");
            vm.ALaVista = true;
            A.CallTo(() => eventos.LeerInscripcionesCliente("1", "15191")).MustHaveHappenedOnceExactly();

            vm.CambiarCliente("1", "20000", "0", "Otro");
            Assert.AreEqual(0, vm.Inscripciones.Count, "Al cambiar de cliente no se queda la lista del anterior");
            A.CallTo(() => eventos.LeerInscripcionesCliente("1", "20000")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task FichaCliente_Inscribir_AbreElDialogoConElCliente_YRecarga()
        {
            A.CallTo(() => dialogos.ShowDialogAsync(InscribirEventoDialogViewModel.NOMBRE, A<ParametrosDialogo>.Ignored))
                .Returns(new ResultadoDialogo(ResultadoBoton.OK));
            var vm = new InscripcionesClienteViewModel(eventos, dialogos);
            Assert.IsFalse(vm.InscribirCommand.CanExecute(null), "Sin cliente no se inscribe");
            vm.CambiarCliente("1", "15191", "0", "Peluquería Pepa");

            await vm.InscribirAsync();

            A.CallTo(() => dialogos.ShowDialogAsync(InscribirEventoDialogViewModel.NOMBRE, A<ParametrosDialogo>.That.Matches(p =>
                p.GetValue<string>("cliente") == "15191" && p.GetValue<string>("contacto") == "0" && p.GetValue<string>("nombre") == "Peluquería Pepa")))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => eventos.LeerInscripcionesCliente("1", "15191")).MustHaveHappenedOnceExactly();
        }

        // ---------------- NestoAPI#690 (contrato del comentario del 22/10/26) ----------------

        [TestMethod]
        public async Task Nuevo_SenalesPorDefecto_CienReembolsableYSesentaNo_YGuardaHorarioYPonente()
        {
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).ReturnsLazily((EventoModel e) => e);
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            vm.NuevoCommand.Execute(null);
            Assert.AreEqual(100, vm.ImporteSenalEdicion);
            Assert.AreEqual(60, vm.ImporteSenalNoReembolsableEdicion);

            vm.TituloEdicion = "Masterclass Maystar";
            vm.TiendaEdicion = vm.Tiendas[0]; // la tienda es obligatoria (Carlos 22/10/26)
            vm.HoraInicioEdicion = "9.30";
            vm.HoraFinEdicion = "13:00";
            vm.PonenteEdicion = " Ana ";
            await vm.GuardarAsync();

            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.That.Matches(e =>
                e.HoraInicio == "09:30" && e.HoraFin == "13:00" && e.Ponente == "Ana"
                && e.ImporteSenal == 100 && e.ImporteSenalNoReembolsable == 60))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Guardar_HorasMalEscritasOFinAntesQueInicio_NoGuarda()
        {
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            vm.NuevoCommand.Execute(null);
            vm.TituloEdicion = "X";
            vm.TiendaEdicion = vm.Tiendas[0]; // la tienda es obligatoria (Carlos 22/10/26)
            vm.HoraInicioEdicion = "las diez";
            await vm.GuardarAsync();
            vm.HoraInicioEdicion = "12:00";
            vm.HoraFinEdicion = "10:00";
            await vm.GuardarAsync();

            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).MustNotHaveHappened();
            CollectionAssert.AreEqual(new[] { "Escriba las horas como 10:00.", "La hora de fin tiene que ser posterior a la de inicio." }, errores);
        }

        [TestMethod]
        public void Evento_NoMandaLosCamposDeSoloLectura_NiLaNoReembolsableSinValor()
        {
            var evento = new EventoModel
            {
                Titulo = "X", ImagenUrl = "https://api.nuevavision.es/api/Eventos/7/Imagen?v=1", Gratuito = true,
                Modalidades = new List<string> { "Gratuita" }, HoraInicio = "10:00"
            };
            JObject json = JObject.Parse(JsonConvert.SerializeObject(evento));

            Assert.IsFalse(json.ContainsKey("ImagenUrl"));
            Assert.IsFalse(json.ContainsKey("Gratuito"));
            Assert.IsFalse(json.ContainsKey("Modalidades"));
            Assert.IsFalse(json.ContainsKey("ImporteSenalNoReembolsable"), "Sin valor no se manda: la API pone 60 al crear y conserva al modificar");
            Assert.AreEqual("10:00", (string)json["HoraInicio"]);
        }

        [TestMethod]
        public void Modalidades_LasDeLaApi_OConLaDeAntesSegunLasSenales()
        {
            EventoModel antiguo = Evento(1, DateTime.Today);
            Assert.AreEqual("Reembolsable", InscribirEventoDialogViewModel.ModalidadesDe(antiguo).Single().Valor);

            EventoModel soloNoReembolsable = Evento(2, DateTime.Today);
            soloNoReembolsable.ImporteSenal = 0;
            soloNoReembolsable.ImporteSenalNoReembolsable = 60;
            soloNoReembolsable.Modalidades = new List<string> { "NoReembolsable" };
            OpcionEvento unica = InscribirEventoDialogViewModel.ModalidadesDe(soloNoReembolsable).Single();
            Assert.AreEqual("NoReembolsable", unica.Valor);
            StringAssert.Contains(unica.Texto, "sin cupón");

            EventoModel gratis = Evento(3, DateTime.Today);
            gratis.ImporteSenal = 0;
            gratis.ImporteSenalNoReembolsable = 0;
            Assert.IsTrue(gratis.EsGratuito);
            Assert.AreEqual("Gratuita", InscribirEventoDialogViewModel.ModalidadesDe(gratis).Single().Valor);
        }

        [TestMethod]
        public void Dialogo_Gratuito_SinFormaDePagoNiEnlace_YConLaModalidad()
        {
            EventoModel gratis = Evento(7, DateTime.Today.AddDays(3));
            gratis.ImporteSenal = 0;
            gratis.ImporteSenalNoReembolsable = 0;
            gratis.Gratuito = true;
            gratis.Modalidades = new List<string> { "Gratuita" };
            InscribirEventoDialogViewModel vm = Dialogo(gratis);
            ((IDialogoNesto)vm).OnDialogOpened(new ParametrosDialogo { { "cliente", "15191" } });
            Assert.IsFalse(vm.HayQuePagar);

            vm.InscribirCommand.Execute(null);

            A.CallTo(() => eventos.Inscribir(7, A<InscribirEventoModel>.That.Matches(p =>
                p.Modalidad == "Gratuita" && p.FormaPago == null && !p.GenerarEnlacePago))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Dialogo_EligeLaNoReembolsable()
        {
            EventoModel evento = Evento(7, DateTime.Today.AddDays(3));
            evento.ImporteSenal = 100;
            evento.ImporteSenalNoReembolsable = 60;
            evento.Modalidades = new List<string> { "Reembolsable", "NoReembolsable" };
            InscribirEventoDialogViewModel vm = Dialogo(evento);
            ((IDialogoNesto)vm).OnDialogOpened(new ParametrosDialogo { { "cliente", "15191" } });
            Assert.IsTrue(vm.HayVariasModalidades);

            vm.Modalidad = vm.Modalidades.Single(m => m.Valor == "NoReembolsable");
            vm.InscribirCommand.Execute(null);

            A.CallTo(() => eventos.Inscribir(7, A<InscribirEventoModel>.That.Matches(p => p.Modalidad == "NoReembolsable" && p.FormaPago == "Tarjeta")))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Dialogo_ConApiDeAntes_NoMandaLaModalidad()
        {
            InscribirEventoDialogViewModel vm = Dialogo(Evento(7, DateTime.Today.AddDays(3)));
            ((IDialogoNesto)vm).OnDialogOpened(new ParametrosDialogo { { "cliente", "15191" } });

            vm.InscribirCommand.Execute(null);

            A.CallTo(() => eventos.Inscribir(7, A<InscribirEventoModel>.That.Matches(p => p.Modalidad == null))).MustHaveHappenedOnceExactly();
            Assert.IsFalse(JObject.Parse(JsonConvert.SerializeObject(new InscribirEventoModel())).ContainsKey("Modalidad"));
        }

        [TestMethod]
        public void Asistencia_ElTextoDeLaApi_OPendienteConfirmadaAsistioNoAsistio()
        {
            Assert.AreEqual("Pendiente", Inscripcion(1, InscripcionEventoModel.PAGADA).TextoAsistencia);
            Assert.AreEqual("Asistió", Inscripcion(2, InscripcionEventoModel.ASISTIO).TextoAsistencia);
            Assert.AreEqual("No asistió", Inscripcion(3, InscripcionEventoModel.NO_ASISTIO).TextoAsistencia);
            InscripcionEventoModel deLaApi = JsonConvert.DeserializeObject<InscripcionEventoModel>(
                @"{""Estado"":""Pagada"",""EstadoAsistencia"":""NoPodraIr"",""EstadoAsistenciaTexto"":""No podrá ir"",""BaseImponibleDia"":123.45," +
                @"""AsistenciaConfirmada"":{""Respuesta"":""NoPodraIr"",""Fecha"":""2026-10-28T10:05:00"",""Usuario"":""Reina"",""Origen"":""Tienda""}}");
            Assert.AreEqual("No podrá ir", deLaApi.TextoAsistencia);
            Assert.AreEqual(123.45m, deLaApi.BaseImponibleDia);
            Assert.AreEqual("Tienda", deLaApi.AsistenciaConfirmada.Origen);

            InscripcionEventoModel inscrita = Inscripcion(4, InscripcionEventoModel.INSCRITA);
            inscrita.Modalidad = InscribirEventoModel.MODALIDAD_GRATUITA;
            Assert.IsTrue(inscrita.EntraEnListaAsistencia, "La gratuita pasa directamente a la lista");
            Assert.IsTrue(inscrita.SePuedeAnular);
            Assert.IsFalse(inscrita.SePuedeMarcarPagada);
            Assert.IsFalse(inscrita.SePuedeFacturar);
        }

        [TestMethod]
        public async Task ConfirmarAsistencia_LaTiendaApuntaLaRespuesta()
        {
            InscripcionEventoModel pagada = Inscripcion(1, InscripcionEventoModel.PAGADA);
            pagada.FechaEvento = DateTime.Today.AddDays(2);
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today.AddDays(2)), pagada);
            InscripcionEventoModel respondida = Inscripcion(1, InscripcionEventoModel.PAGADA);
            respondida.EstadoAsistenciaTexto = "No podrá ir";
            A.CallTo(() => eventos.ConfirmarAsistencia(1, "NoPodraIr")).Returns(respondida);
            vm.InscripcionSeleccionada = vm.Inscripciones.Single();
            Assert.IsTrue(vm.NoPodraIrCommand.CanExecute(null));

            await vm.ConfirmarAsistenciaAsync(AsistenciaConfirmadaModel.NO_PODRA_IR);

            Assert.AreEqual("No podrá ir", vm.InscripcionSeleccionada.TextoAsistencia);
        }

        [TestMethod]
        public async Task Imagen_SubirYQuitar()
        {
            MantenimientoEventosViewModel vm = await MantenimientoConEvento(Evento(7, DateTime.Today.AddDays(2)));
            A.CallTo(() => eventos.SubirImagen(7, @"C:\fotos\cartel.png"))
                .Returns(new EventoModel { Id = 7, ImagenUrl = "https://api.nuevavision.es/api/Eventos/7/Imagen?v=2" });
            vm.ElegirImagen = () => null;
            await vm.SubirImagenAsync();
            A.CallTo(() => eventos.SubirImagen(A<int>.Ignored, A<string>.Ignored)).MustNotHaveHappened();

            vm.ElegirImagen = () => @"C:\fotos\cartel.png";
            Assert.IsTrue(vm.SubirImagenCommand.CanExecute(null));
            await vm.SubirImagenAsync();
            Assert.AreEqual("https://api.nuevavision.es/api/Eventos/7/Imagen?v=2", vm.ImagenUrl);
            Assert.IsTrue(vm.QuitarImagenCommand.CanExecute(null));

            await vm.QuitarImagenAsync();
            A.CallTo(() => eventos.BorrarImagen(7)).MustHaveHappenedOnceExactly();
            Assert.IsNull(vm.ImagenUrl);
        }

        [TestMethod]
        public void Servicio_TipoDeLaImagenPorLaExtension()
        {
            Assert.AreEqual("image/png", EventosService.TipoImagen("cartel.PNG"));
            Assert.AreEqual("image/jpeg", EventosService.TipoImagen("cartel.jpeg"));
            Assert.AreEqual("image/webp", EventosService.TipoImagen("cartel.webp"));
        }

        // ---------------- Servicio ----------------

        [TestMethod]
        public void Servicio_ApiSinLaRuta_SoloEsElCuatrocientosCuatroDeWebApi()
        {
            Assert.IsTrue(EventosService.EsApiSinLaRuta(404, "{\"Message\":\"No HTTP resource was found that matches the request URI 'x'.\"}"));
            Assert.IsTrue(EventosService.EsApiSinLaRuta(404, ""));
            Assert.IsFalse(EventosService.EsApiSinLaRuta(404, "{\"error\":{\"message\":\"No existe el evento 5.\"}}"), "Un 404 de negocio trae su mensaje");
            Assert.IsFalse(EventosService.EsApiSinLaRuta(400, ""));
        }

        [TestMethod]
        public void Servicio_UrlDeLasInscripcionesDelCliente()
        {
            Assert.AreEqual("Clientes/1/15191/Inscripciones", EventosService.UrlInscripcionesCliente(" 1 ", "15191 "));
            Assert.AreEqual($"Clientes/{Constantes.Empresas.EMPRESA_DEFECTO}/15191/Inscripciones", EventosService.UrlInscripcionesCliente(null, "15191"));
        }

        [TestMethod]
        public void Modelo_LaInscripcionSeLeeDelJsonDeLaApi()
        {
            const string json = "{\"Id\":11,\"EventoId\":7,\"Estado\":\"NoAsistio\",\"EstadoTexto\":\"No asistió\",\"Origen\":\"Telefono\"," +
                "\"Facturacion\":{\"Estado\":\"NoAutomatizable\",\"Factura\":null,\"Mensaje\":\"El importe no coincide\"}}";

            InscripcionEventoModel inscripcion = JsonConvert.DeserializeObject<InscripcionEventoModel>(json);

            Assert.AreEqual("Teléfono", inscripcion.TextoOrigen);
            Assert.AreEqual("No automatizable", inscripcion.TextoFacturacion);
            Assert.IsTrue(inscripcion.EntraEnListaAsistencia);
            Assert.IsTrue(inscripcion.SePuedeFacturar);
            Assert.IsFalse(inscripcion.SePuedeMarcarPagada);
        }
    }
}
