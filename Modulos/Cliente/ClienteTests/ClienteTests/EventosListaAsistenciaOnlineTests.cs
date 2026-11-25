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
    /// Gemela de NestoAPI#908: en un evento online no se pasa lista (SePasaLista false: ni casilla «Vino» ni «Guardar lista», y
    /// el texto de la API en su lugar), y la lista sin nadie marcado (409 LISTA_SIN_ASISTENTES) se pregunta con el mensaje de
    /// la API (No por defecto) y, con un sí, se reenvía con Confirmar.
    /// </summary>
    [TestClass]
    public class EventosListaAsistenciaOnlineTests
    {
        private const string MENSAJE_409 = "No ha marcado a nadie: las 2 inscritas pagadas pasarán a «No asistió» y perderán el cupón. Si es así, vuelva a guardar la lista confirmándolo.";

        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();

        public EventosListaAsistenciaOnlineTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
        }

        private static EventoModel Evento(bool? sePasaLista = null, string texto = null)
        {
            var evento = new EventoModel { HoraInicio = "10:00", Id = 7, Empresa = "1", Titulo = "Masterclass Rosácea", Fecha = DateTime.Today, Activo = true, Almacen = "REI" };
            if (sePasaLista.HasValue)
            {
                evento.SePasaLista = sePasaLista.Value;
            }
            evento.TextoListaAsistencia = texto;
            return evento;
        }

        private static InscripcionEventoModel Inscrita(int id, string estado, string modalidad = InscribirEventoModel.MODALIDAD_REEMBOLSABLE) =>
            new()
            {
                Id = id, EventoId = 7, Evento = "Masterclass Rosácea", Empresa = "1", Cliente = "15191", Contacto = "0",
                Nombre = $"Cliente {id}", Estado = estado, EstadoTexto = estado, Modalidad = modalidad
            };

        private async Task<MantenimientoEventosViewModel> Mantenimiento(EventoModel evento, params InscripcionEventoModel[] inscritos)
        {
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { evento });
            A.CallTo(() => eventos.LeerInscripciones(evento.Id)).Returns(inscritos.ToList());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            await vm.CargarInscripcionesAsync();
            return vm;
        }

        // ---------------- Modelo ----------------

        [TestMethod]
        public void EventoModel_SinElCampo_SePasaLista()
        {
            EventoModel evento = JsonConvert.DeserializeObject<EventoModel>("{\"Id\":7,\"Titulo\":\"Masterclass\"}");

            Assert.IsTrue(evento.SePasaLista, "La API de antes no lo manda: se pasa lista como siempre");
            Assert.IsNull(evento.TextoListaAsistencia);
        }

        [TestMethod]
        public void EventoModel_Online_LeeElCampoYNoLoReenvia()
        {
            EventoModel evento = JsonConvert.DeserializeObject<EventoModel>(
                "{\"Id\":7,\"SePasaLista\":false,\"TextoListaAsistencia\":\"Evento online: todas las inscritas pagadas se dan por asistidas.\"}");

            Assert.IsFalse(evento.SePasaLista);
            string json = JsonConvert.SerializeObject(evento);
            StringAssert.DoesNotMatch(json, new System.Text.RegularExpressions.Regex("SePasaLista|TextoListaAsistencia"), "Son de solo lectura");
        }

        [TestMethod]
        public void AsistenciaEventoModel_ConfirmarSoloSeMandaCuandoEsTrue()
        {
            Assert.IsFalse(JsonConvert.SerializeObject(new AsistenciaEventoModel { Asistentes = new List<int> { 1 } }).Contains("Confirmar"),
                "La API de antes no lo conoce");
            StringAssert.Contains(JsonConvert.SerializeObject(new AsistenciaEventoModel { Confirmar = true }), "\"Confirmar\":true");
        }

        [TestMethod]
        public void ListaSinAsistentesException_DesdeRespuesta_LeeCodigoMensajeYDetalles()
        {
            string body = JsonConvert.SerializeObject(new
            {
                error = new { code = "LISTA_SIN_ASISTENTES", message = MENSAJE_409, details = new { perderanElCupon = 2 } }
            });

            ListaSinAsistentesException ex = ListaSinAsistentesException.DesdeRespuesta(body);

            Assert.IsNotNull(ex);
            Assert.AreEqual(MENSAJE_409, ex.Message);
            Assert.AreEqual(2, ex.PerderanElCupon);
        }

        [TestMethod]
        public void ListaSinAsistentesException_DesdeRespuesta_OtroCodigoOCuerpoRaro_Null()
        {
            Assert.IsNull(ListaSinAsistentesException.DesdeRespuesta(JsonConvert.SerializeObject(new { error = new { code = "LISTA_EVENTO_ONLINE", message = "x" } })));
            Assert.IsNull(ListaSinAsistentesException.DesdeRespuesta("no es json"));
            Assert.IsNull(ListaSinAsistentesException.DesdeRespuesta(null));
        }

        [TestMethod]
        public void ListaEventoOnline_ElMensajeDeLaApiTalCual()
        {
            string body = JsonConvert.SerializeObject(new { error = new { code = "LISTA_EVENTO_ONLINE", message = "Evento online: no se pasa lista." } });

            Assert.AreEqual("Evento online: no se pasa lista.",
                EventosService.MensajeError(400, body, "No se pudo guardar la lista de asistencia", talCual: true));
        }

        // ---------------- Evento online: no se ofrece pasar lista ----------------

        [TestMethod]
        public async Task EventoOnline_NoSeOfreceLaListaYSeEnsenaElTextoDeLaApi()
        {
            MantenimientoEventosViewModel vm = await Mantenimiento(
                Evento(sePasaLista: false, texto: "Evento online: todas las inscritas pagadas se dan por asistidas."),
                Inscrita(1, InscripcionEventoModel.PAGADA));

            Assert.IsFalse(vm.SeOfreceListaAsistencia);
            Assert.IsFalse(vm.VeCasillaAsistencia, "Sin casilla «Vino»");
            Assert.IsFalse(vm.SePuedePasarLista);
            Assert.IsFalse(vm.GuardarAsistenciaCommand.CanExecute(null));
            Assert.AreEqual("Evento online: todas las inscritas pagadas se dan por asistidas.", vm.AvisoListaAsistencia);

            await vm.GuardarAsistenciaAsync();
            A.CallTo(() => eventos.PasarLista(A<int>.Ignored, A<IEnumerable<int>>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => eventos.PasarLista(A<int>.Ignored, A<IEnumerable<int>>.Ignored, A<bool>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task EventoOnline_SinTexto_UnoPorDefecto()
        {
            MantenimientoEventosViewModel vm = await Mantenimiento(Evento(sePasaLista: false), Inscrita(1, InscripcionEventoModel.PAGADA));

            Assert.AreEqual("Evento online: no se pasa lista.", vm.AvisoListaAsistencia);
        }

        [TestMethod]
        public async Task EventoConApiDeAntes_SeOfreceLaListaComoSiempre()
        {
            MantenimientoEventosViewModel vm = await Mantenimiento(Evento(), Inscrita(1, InscripcionEventoModel.PAGADA));

            Assert.IsTrue(vm.SeOfreceListaAsistencia);
            Assert.IsTrue(vm.VeCasillaAsistencia);
            Assert.IsTrue(vm.SePuedePasarLista);
        }

        // ---------------- 409 LISTA_SIN_ASISTENTES ----------------

        [TestMethod]
        public async Task ListaSinNadieMarcado_SinPreguntaDeNesto_PreguntaConElMensajeDeLaApiYConSiReenviaConfirmando()
        {
            // Las gratuitas no disparan la pregunta de Nesto; la API sí pregunta
            MantenimientoEventosViewModel vm = await Mantenimiento(Evento(),
                Inscrita(1, InscripcionEventoModel.INSCRITA, InscribirEventoModel.MODALIDAD_GRATUITA),
                Inscrita(2, InscripcionEventoModel.INSCRITA, InscribirEventoModel.MODALIDAD_GRATUITA));
            A.CallTo(() => eventos.PasarLista(7, A<IEnumerable<int>>.Ignored)).ThrowsAsync(new ListaSinAsistentesException(MENSAJE_409, 2));
            A.CallTo(() => dialogos.ShowConfirmationAnswer("Lista de asistencia", MENSAJE_409, true)).Returns(true);

            await vm.GuardarAsistenciaAsync();

            A.CallTo(() => dialogos.ShowConfirmationAnswer("Lista de asistencia", MENSAJE_409, true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => eventos.PasarLista(7, A<IEnumerable<int>>.That.Matches(a => !a.Any()), true)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task ListaSinNadieMarcado_SiDiceNo_NoReenvia()
        {
            MantenimientoEventosViewModel vm = await Mantenimiento(Evento(),
                Inscrita(1, InscripcionEventoModel.INSCRITA, InscribirEventoModel.MODALIDAD_GRATUITA));
            A.CallTo(() => eventos.PasarLista(7, A<IEnumerable<int>>.Ignored)).ThrowsAsync(new ListaSinAsistentesException(MENSAJE_409, 1));
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored)).Returns(false);

            await vm.GuardarAsistenciaAsync();

            A.CallTo(() => dialogos.ShowConfirmationAnswer("Lista de asistencia", MENSAJE_409, true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => eventos.PasarLista(A<int>.Ignored, A<IEnumerable<int>>.Ignored, A<bool>.Ignored)).MustNotHaveHappened();
            Assert.AreEqual(0, errores.Count, "Decir que no no es un error");
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task ListaSinNadieMarcado_YaDijoQueSiALaPreguntaDeNesto_NoSeLeVuelveAPreguntar()
        {
            MantenimientoEventosViewModel vm = await Mantenimiento(Evento(),
                Inscrita(1, InscripcionEventoModel.PAGADA), Inscrita(2, InscripcionEventoModel.PAGADA));
            A.CallTo(() => eventos.PasarLista(7, A<IEnumerable<int>>.Ignored)).ThrowsAsync(new ListaSinAsistentesException(MENSAJE_409, 2));
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored, true)).Returns(true);

            await vm.GuardarAsistenciaAsync();

            // Una sola pregunta: la de Nesto, con los nombres
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored, true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowConfirmationAnswer("Lista de asistencia", MENSAJE_409, A<bool>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => eventos.PasarLista(7, A<IEnumerable<int>>.That.Matches(a => !a.Any()), true)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ListaEventoOnline_OtroError_SeEnsenaConSuMensaje()
        {
            MantenimientoEventosViewModel vm = await Mantenimiento(Evento(), Inscrita(1, InscripcionEventoModel.PAGADA));
            vm.Inscripciones.Single().Asistio = true;
            A.CallTo(() => eventos.PasarLista(7, A<IEnumerable<int>>.Ignored)).ThrowsAsync(new Exception("Evento online: no se pasa lista."));

            await vm.GuardarAsistenciaAsync();

            Assert.AreEqual("Evento online: no se pasa lista.", errores.Single());
            A.CallTo(() => eventos.PasarLista(A<int>.Ignored, A<IEnumerable<int>>.Ignored, A<bool>.Ignored)).MustNotHaveHappened();
        }
    }
}
