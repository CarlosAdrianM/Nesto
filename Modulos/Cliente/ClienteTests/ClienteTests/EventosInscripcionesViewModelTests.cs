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
            Assert.IsNull(otro.TiendaEdicion.Codigo, "Algete no hace eventos: sin tienda");
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
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.That.Contains("no han venido 1, que pierden el cupón")))
                .MustHaveHappened();
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
