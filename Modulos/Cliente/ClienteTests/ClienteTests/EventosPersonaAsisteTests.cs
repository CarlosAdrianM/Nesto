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
    /// Nesto#566 (NestoAPI#731): en los inscritos, la persona que asiste (combo de personas de contacto que se guarda con el
    /// PUT), y el correo y la dirección de la ficha; en el diálogo de inscribir, el mismo combo (opcional).
    /// </summary>
    [TestClass]
    public class EventosPersonaAsisteTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();

        public EventosPersonaAsisteTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
        }

        private static PersonaContactoEventoModel Persona(string numero, string nombre, string correo = null) =>
            new() { Contacto = "0", Numero = numero, Nombre = nombre, Correo = correo };

        private static InscripcionEventoModel Inscripcion(int id, PersonaContactoEventoModel persona = null) =>
            new()
            {
                Id = id, EventoId = 7, Evento = "Masterclass", Empresa = "1", Cliente = "15191", Contacto = "0",
                Nombre = "LUNA", Estado = InscripcionEventoModel.INSCRITA, PersonaContacto = persona
            };

        private async Task<MantenimientoEventosViewModel> Mantenimiento(params InscripcionEventoModel[] inscritos)
        {
            var evento = new EventoModel { Id = 7, Empresa = "1", Titulo = "Masterclass", Fecha = DateTime.Today.AddDays(5), Almacen = "REI", Activo = true };
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { evento });
            A.CallTo(() => eventos.LeerInscripciones(7)).Returns(inscritos.ToList());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            await vm.CargarInscripcionesAsync();
            return vm;
        }

        [TestMethod]
        public void InscripcionEventoModel_LeeLaPersonaElCorreoYLaDireccionDeLaApi()
        {
            string json = "{\"Id\":3,\"Cliente\":\"15191\",\"Contacto\":\"0\",\"PersonaContacto\":{\"Contacto\":\"0\",\"Numero\":\"1\",\"Nombre\":\"ANA LUNA\",\"Correo\":\"ana@luna.es\",\"Telefono\":\"600\"},"
                + "\"Correo\":\"ana@luna.es\",\"Direccion\":\"Calle Mayor 1, 28001 Madrid (Madrid)\",\"PedidoJustificante\":930001}";

            InscripcionEventoModel leida = JsonConvert.DeserializeObject<InscripcionEventoModel>(json);

            Assert.AreEqual("ANA LUNA", leida.TextoAsiste);
            Assert.AreEqual("1", leida.NumeroPersonaAsiste);
            Assert.AreEqual("ana@luna.es", leida.Correo);
            Assert.AreEqual("Calle Mayor 1, 28001 Madrid (Madrid)", leida.Direccion);
            Assert.AreEqual(930001, leida.PedidoJustificante);
        }

        [TestMethod]
        public void TextoAsiste_PersonaBorradaDeLaFicha_LoDice()
        {
            var inscripcion = Inscripcion(3, new PersonaContactoEventoModel { Contacto = "0", Numero = "4" });

            Assert.AreEqual("Persona 4 (ya no está en la ficha)", inscripcion.TextoAsiste);
            Assert.IsTrue(inscripcion.PersonasDisponibles.Any(p => p.Numero == "4"), "La persona actual sale en el combo aunque no esté en la ficha");
        }

        [TestMethod]
        public async Task SeleccionarInscripcion_CargaLasPersonasDeSuClienteYContacto()
        {
            A.CallTo(() => eventos.LeerPersonasContacto("1", "15191", "0"))
                .Returns(new List<PersonaContactoEventoModel> { Persona("1", "ANA LUNA"), Persona("2", "EVA LUNA") });
            var vm = await Mantenimiento(Inscripcion(3));

            vm.InscripcionSeleccionada = vm.Inscripciones.Single();
            await vm.CargarPersonasContactoAsync(vm.InscripcionSeleccionada);

            CollectionAssert.AreEqual(new[] { "", "1", "2" }, vm.InscripcionSeleccionada.PersonasDisponibles.Select(p => p.Numero).ToArray());
            A.CallTo(() => eventos.LeerPersonasContacto("1", "15191", "0")).MustHaveHappenedOnceExactly(); // la segunda vez, de la caché
            A.CallTo(() => eventos.CambiarPersonaContacto(A<int>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ElegirPersona_LaGuardaConElPutYPintaLoQueDevuelveLaApi()
        {
            A.CallTo(() => eventos.CambiarPersonaContacto(3, "2")).Returns(new InscripcionEventoModel
            {
                Id = 3, PersonaContacto = Persona("2", "EVA LUNA", "eva@luna.es"), Correo = "eva@luna.es", Direccion = "Calle Mayor 1"
            });
            var vm = await Mantenimiento(Inscripcion(3, Persona("1", "ANA LUNA")));
            InscripcionEventoModel inscripcion = vm.Inscripciones.Single();

            inscripcion.NumeroPersonaAsiste = "2";
            await Task.Yield();

            A.CallTo(() => eventos.CambiarPersonaContacto(3, "2")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("EVA LUNA", inscripcion.TextoAsiste);
            Assert.AreEqual("eva@luna.es", inscripcion.Correo);
            Assert.AreEqual("2", inscripcion.NumeroPersonaAsiste);
        }

        [TestMethod]
        public async Task ElegirSinIndicar_QuitaLaPersona()
        {
            A.CallTo(() => eventos.CambiarPersonaContacto(3, "")).Returns(new InscripcionEventoModel { Id = 3 });
            var vm = await Mantenimiento(Inscripcion(3, Persona("1", "ANA LUNA")));
            InscripcionEventoModel inscripcion = vm.Inscripciones.Single();

            inscripcion.NumeroPersonaAsiste = string.Empty;
            await vm.CambiarPersonaAsisteAsync(inscripcion);

            A.CallTo(() => eventos.CambiarPersonaContacto(3, "")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(string.Empty, inscripcion.TextoAsiste);
        }

        [TestMethod]
        public async Task ElegirPersona_SiLaApiNoLaAcepta_EnsenaElMensajeYVuelveALaDeAntes()
        {
            A.CallTo(() => eventos.CambiarPersonaContacto(3, "9"))
                .ThrowsAsync(new Exception("No se pudo cambiar la persona que asiste: La persona de contacto 9 no es de la ficha del cliente 15191/0."));
            var vm = await Mantenimiento(Inscripcion(3, Persona("1", "ANA LUNA")));
            InscripcionEventoModel inscripcion = vm.Inscripciones.Single();

            inscripcion.NumeroPersonaAsiste = "9";
            await Task.Yield();

            Assert.AreEqual(1, errores.Count);
            StringAssert.Contains(errores[0], "no es de la ficha");
            Assert.AreEqual("1", inscripcion.NumeroPersonaAsiste);
            A.CallTo(() => eventos.CambiarPersonaContacto(A<int>.Ignored, A<string>.Ignored)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task InscribirDialogo_MandaLaPersonaElegida()
        {
            A.CallTo(() => eventos.LeerPersonasContacto("1", "15191", "0"))
                .Returns(new List<PersonaContactoEventoModel> { Persona("1", "ANA LUNA"), Persona("2", "EVA LUNA") });
            A.CallTo(() => eventos.Inscribir(A<int>.Ignored, A<InscribirEventoModel>.Ignored)).Returns(new InscripcionEventoModel { Id = 9 });
            var vm = new InscribirEventoDialogViewModel(eventos) { EsperaCargaPersonasMs = 0 };
            vm.Evento = new EventoModel { Id = 7, Titulo = "Masterclass", Fecha = DateTime.Today.AddDays(5), Gratuito = true };
            vm.Cliente = "15191";
            vm.Contacto = "0";
            await vm.CargarPersonasAsync();

            vm.PersonaAsiste = vm.Personas.Single(p => p.Numero == "2");
            await vm.InscribirAsync();

            A.CallTo(() => eventos.Inscribir(7, A<InscribirEventoModel>.That.Matches(p => p.PersonaContacto == "2"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task InscribirDialogo_SinPersona_NoLaManda()
        {
            A.CallTo(() => eventos.LeerPersonasContacto(A<string>.Ignored, A<string>.Ignored, A<string>.Ignored))
                .ThrowsAsync(new Exception("API sin la ruta"));
            A.CallTo(() => eventos.Inscribir(A<int>.Ignored, A<InscribirEventoModel>.Ignored)).Returns(new InscripcionEventoModel { Id = 9 });
            var vm = new InscribirEventoDialogViewModel(eventos) { EsperaCargaPersonasMs = 0 };
            vm.Evento = new EventoModel { Id = 7, Titulo = "Masterclass", Fecha = DateTime.Today.AddDays(5), Gratuito = true };
            vm.Cliente = "15191";
            await vm.CargarPersonasAsync();

            await vm.InscribirAsync();

            Assert.AreEqual(1, vm.Personas.Count, "Solo «(sin indicar)»: el fallo al leer las personas no impide inscribir");
            A.CallTo(() => eventos.Inscribir(7, A<InscribirEventoModel>.That.Matches(p => p.PersonaContacto == null))).MustHaveHappenedOnceExactly();
            Assert.IsFalse(JsonConvert.SerializeObject(new InscribirEventoModel()).Contains("PersonaContacto"), "Sin persona no se manda el campo");
        }

        [TestMethod]
        public void UrlPersonasContacto_ConYSinContacto()
        {
            Assert.AreEqual("Eventos/PersonasContacto?empresa=1&cliente=15191&contacto=0", EventosService.UrlPersonasContacto("1", " 15191 ", "0 "));
            Assert.AreEqual("Eventos/PersonasContacto?empresa=1&cliente=15191", EventosService.UrlPersonasContacto(null, "15191", null));
        }
    }
}
