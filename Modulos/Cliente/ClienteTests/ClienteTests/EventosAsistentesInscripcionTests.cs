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
    /// Nesto#614 (NestoAPI#811, Reina, sugerencia 1006): una inscripción puede traer varias personas (un centro que compra 2
    /// kits trae 2 personas). Se elige al inscribir, se ve y se cambia en la ficha de la inscripción, y las plazas del evento
    /// las suma la API.
    /// </summary>
    [TestClass]
    public class EventosAsistentesInscripcionTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();
        private readonly List<string> notificaciones = new();

        public EventosAsistentesInscripcionTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => dialogos.ShowNotification(A<string>.Ignored, A<string>.Ignored)).Invokes((string t, string m) => notificaciones.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
        }

        private static EventoModel Evento(int id, int ocupadas = 9) =>
            new() { Id = id, Empresa = "1", Titulo = "Lifting coreano", Fecha = DateTime.Today.AddDays(5), ImporteSenal = 0, Activo = true, Almacen = "REI", Plazas = 18, PlazasOcupadas = ocupadas };

        private static InscripcionEventoModel Inscripcion(int id, int? asistentes, string estado = InscripcionEventoModel.INSCRITA) =>
            new()
            {
                Id = id, EventoId = 7, Evento = "Lifting coreano", FechaEvento = DateTime.Today.AddDays(5), Empresa = "1", Cliente = "8897",
                Contacto = "0", Nombre = "Centro Lola", Estado = estado, EstadoTexto = estado, Asistentes = asistentes
            };

        private async Task<MantenimientoEventosViewModel> Mantenimiento(params InscripcionEventoModel[] inscritos)
        {
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { Evento(7) });
            A.CallTo(() => eventos.LeerInscripciones(7)).Returns(inscritos.ToList());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            await vm.CargarInscripcionesAsync();
            return vm;
        }

        // ---------------- Modelo ----------------

        [TestMethod]
        public void Modelo_ConLaApiDeAntes_SinAsistentes_CuentaUno()
        {
            InscripcionEventoModel vieja = JsonConvert.DeserializeObject<InscripcionEventoModel>("{\"Id\":3,\"Cliente\":\"8897\",\"Contacto\":\"0\"}");

            Assert.IsNull(vieja.Asistentes);
            Assert.AreEqual(1, vieja.NumeroAsistentes);
            Assert.AreEqual("8897/0 · 1 asistente", vieja.ClienteContactoAsistentes);
            Assert.AreEqual(1, vieja.AsistentesEdicion);
        }

        [TestMethod]
        public void Modelo_ConDosAsistentes_LoDiceEnLaTarjeta()
        {
            InscripcionEventoModel inscripcion = JsonConvert.DeserializeObject<InscripcionEventoModel>("{\"Id\":3,\"Cliente\":\"8897\",\"Contacto\":\"0\",\"Asistentes\":2}");

            Assert.AreEqual(2, inscripcion.NumeroAsistentes);
            Assert.AreEqual("8897/0 · 2 asistentes", inscripcion.ClienteContactoAsistentes);
        }

        [TestMethod]
        public void Modelo_AnuladaOCaducada_NoSeCambianLosAsistentes()
        {
            Assert.IsTrue(Inscripcion(1, 2, InscripcionEventoModel.PAGADA).SePuedenCambiarAsistentes);
            Assert.IsFalse(Inscripcion(1, 2, InscripcionEventoModel.ANULADA).SePuedenCambiarAsistentes);
            Assert.IsFalse(Inscripcion(1, 2, InscripcionEventoModel.CADUCADA).SePuedenCambiarAsistentes);
        }

        [TestMethod]
        public void Evento_LasPlazasSonLasQueDiceLaApi()
        {
            Assert.AreEqual("9 / 18", Evento(7, 9).TextoPlazas);
        }

        // ---------------- Inscribir ----------------

        private InscribirEventoDialogViewModel Dialogo()
        {
            A.CallTo(() => eventos.LeerEventos(true)).Returns(new List<EventoModel> { Evento(7) });
            var portapapeles = A.Fake<ControlesUsuario.Services.IServicioPortapapeles>();
            A.CallTo(() => portapapeles.Copiar(A<string>.Ignored)).Returns(true);
            return new InscribirEventoDialogViewModel(eventos, portapapeles);
        }

        [TestMethod]
        public void Inscribir_PorDefectoMandaUnAsistente()
        {
            InscribirEventoDialogViewModel vm = Dialogo();
            ((IDialogoNesto)vm).OnDialogOpened(new ParametrosDialogo { { "eventoId", 7 } });
            vm.Cliente = "8897";

            Assert.AreEqual(1, vm.Asistentes);
            vm.InscribirCommand.Execute(null);

            A.CallTo(() => eventos.Inscribir(7, A<InscribirEventoModel>.That.Matches(p => p.Asistentes == 1))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Inscribir_MandaLosAsistentesElegidos()
        {
            InscribirEventoDialogViewModel vm = Dialogo();
            ((IDialogoNesto)vm).OnDialogOpened(new ParametrosDialogo { { "eventoId", 7 } });
            vm.Cliente = "8897";
            vm.Asistentes = 2;

            vm.InscribirCommand.Execute(null);

            A.CallTo(() => eventos.Inscribir(7, A<InscribirEventoModel>.That.Matches(p => p.Asistentes == 2 && p.Cliente == "8897")))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Inscribir_MenosDeUnoOVacio_EsUno()
        {
            InscribirEventoDialogViewModel vm = Dialogo();
            ((IDialogoNesto)vm).OnDialogOpened(new ParametrosDialogo { { "eventoId", 7 } });
            vm.Cliente = "8897";

            vm.Asistentes = 0;
            Assert.AreEqual(1, vm.Asistentes);
            vm.Asistentes = null;
            vm.InscribirCommand.Execute(null);

            A.CallTo(() => eventos.Inscribir(7, A<InscribirEventoModel>.That.Matches(p => p.Asistentes == 1))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Inscribir_LosAsistentesSeSerializan()
        {
            string json = JsonConvert.SerializeObject(new InscribirEventoModel { Cliente = "8897", Origen = "Nesto", Asistentes = 2 });

            StringAssert.Contains(json, "\"Asistentes\":2");
        }

        // ---------------- Ficha de la inscripción ----------------

        [TestMethod]
        public async Task Cambiar_LlamaAlPut_SustituyeLaInscripcion_YRecargaLasPlazas()
        {
            MantenimientoEventosViewModel vm = await Mantenimiento(Inscripcion(3, null));
            InscripcionEventoModel inscripcion = vm.Inscripciones.Single();
            A.CallTo(() => eventos.CambiarAsistentes(3, 2)).Returns(Inscripcion(3, 2));
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { Evento(7, 10) });
            A.CallTo(() => eventos.LeerInscripciones(7)).Returns(new List<InscripcionEventoModel> { Inscripcion(3, 2) });

            inscripcion.AsistentesEdicion = 2;
            await vm.CambiarAsistentesCommand.ExecuteAsync(inscripcion);

            A.CallTo(() => eventos.CambiarAsistentes(3, 2)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(2, vm.Inscripciones.Single().NumeroAsistentes);
            Assert.AreEqual("10 / 18", vm.Seleccionado.TextoPlazas, "Las plazas ocupadas, las de la API");
            Assert.IsFalse(errores.Any());
        }

        [TestMethod]
        public async Task Cambiar_SinCambio_NoLlamaALaApi()
        {
            MantenimientoEventosViewModel vm = await Mantenimiento(Inscripcion(3, 2));

            await vm.CambiarAsistentesAsync(vm.Inscripciones.Single());

            A.CallTo(() => eventos.CambiarAsistentes(A<int>.Ignored, A<int>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cambiar_ErrorDeLaApi_EnseñaElMensaje_YVuelveAlNumeroQueTenia()
        {
            const string mensaje = "El cliente 8897/0 ha comprado 1 unidad de 45363 (LIFTING COREANO KIT) desde el 21/08/2026 y quiere inscribir 2 asistentes en «Lifting coreano»: hace falta una unidad comprada por asistente.";
            MantenimientoEventosViewModel vm = await Mantenimiento(Inscripcion(3, null));
            InscripcionEventoModel inscripcion = vm.Inscripciones.Single();
            A.CallTo(() => eventos.CambiarAsistentes(3, 2)).Throws(new Exception(mensaje));

            inscripcion.AsistentesEdicion = 2;
            await vm.CambiarAsistentesAsync(inscripcion);

            Assert.AreEqual(mensaje, errores.Single());
            Assert.AreEqual(1, inscripcion.AsistentesEdicion);
            Assert.AreEqual(1, inscripcion.NumeroAsistentes);
        }

        [TestMethod]
        public async Task Cambiar_ConAvisos_LosEnseña()
        {
            MantenimientoEventosViewModel vm = await Mantenimiento(Inscripcion(3, null));
            InscripcionEventoModel guardada = Inscripcion(3, 2);
            guardada.Avisos = new List<string> { "Inscrita por Dirección con menos compra de la que pide el evento." };
            A.CallTo(() => eventos.CambiarAsistentes(3, 2)).Returns(guardada);

            vm.Inscripciones.Single().AsistentesEdicion = 2;
            await vm.CambiarAsistentesAsync(vm.Inscripciones.Single());

            StringAssert.Contains(notificaciones.Single(), "Inscrita por Dirección");
        }

        [TestMethod]
        public async Task Resumen_CuentaLasPersonas()
        {
            MantenimientoEventosViewModel vm = await Mantenimiento(Inscripcion(1, 2), Inscripcion(2, null),
                Inscripcion(3, 3, InscripcionEventoModel.ANULADA));

            StringAssert.StartsWith(vm.ResumenInscripciones, "2 inscritos (3 asistentes)");
        }

        [TestMethod]
        public void MensajeError_TalCual_SinNadaDelante()
        {
            string body = JsonConvert.SerializeObject(new { error = new { message = "En «Lifting coreano» solo queda 1 plaza libre y quiere inscribir 2 asistentes." } });

            Assert.AreEqual("En «Lifting coreano» solo queda 1 plaza libre y quiere inscribir 2 asistentes.",
                EventosService.MensajeError(400, body, "No se pudo cambiar el número de asistentes", talCual: true));
            StringAssert.StartsWith(EventosService.MensajeError(400, body, "No se pudo inscribir"), "No se pudo inscribir: ");
        }
    }
}
