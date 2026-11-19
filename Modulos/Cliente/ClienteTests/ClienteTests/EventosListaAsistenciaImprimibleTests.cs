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
    /// <summary>Sugerencia 1362 (Pilar): la hoja para pasar lista el día de la formación.</summary>
    [TestClass]
    public class EventosListaAsistenciaImprimibleTests
    {
        private static readonly EventoModel EVENTO = new()
        {
            Id = 7, Empresa = "1", Titulo = "Masterclass Cloasma", Fecha = new DateTime(2026, 11, 20), HoraInicio = "10:00", HoraFin = "13:00",
            Almacen = "REI", Activo = true, Formato = EventoModel.FORMATO_PRESENCIAL
        };

        private static InscripcionEventoModel Inscripcion(int id, string nombre, string estado = InscripcionEventoModel.PAGADA, string estadoTexto = "Pagada",
            string modalidad = InscribirEventoModel.MODALIDAD_REEMBOLSABLE) => new()
            {
                Id = id, EventoId = 7, Empresa = "1", Cliente = "1519" + id, Contacto = "0", Nombre = nombre, Estado = estado, EstadoTexto = estadoTexto,
                Modalidad = modalidad, FechaEvento = EVENTO.Fecha
            };

        [TestMethod]
        public void Filas_SinAnuladas_YPorNombre()
        {
            var lista = new[]
            {
                Inscripcion(1, "ZURITA"),
                Inscripcion(2, "álvarez"),
                Inscripcion(3, "BELLEZA ANULADA", InscripcionEventoModel.ANULADA, "Anulada"),
                Inscripcion(4, "Martín", InscripcionEventoModel.CADUCADA, "Caducada"),
                null
            };

            List<InscripcionEventoModel> filas = ListaAsistenciaImprimible.Filas(lista);

            CollectionAssert.AreEqual(new[] { 2, 4, 1 }, filas.Select(i => i.Id).ToArray());
        }

        [TestMethod]
        public void Tabla_UnaCeldaPorColumna()
        {
            InscripcionEventoModel conPersona = Inscripcion(1, "PELUQUERÍA LA RUBIA");
            conPersona.PersonaContacto = new PersonaContactoEventoModel { Numero = "2", Nombre = "Ana Luna", Telefono = " 600111222 " };
            conPersona.Asistentes = 2;
            InscripcionEventoModel gratuita = Inscripcion(2, "ESTÉTICA SOL", InscripcionEventoModel.INSCRITA, "Inscrita", InscribirEventoModel.MODALIDAD_GRATUITA);

            List<string[]> tabla = ListaAsistenciaImprimible.Tabla(EVENTO, new[] { conPersona, gratuita });

            Assert.AreEqual(2, tabla.Count);
            Assert.IsTrue(tabla.All(f => f.Length == ListaAsistenciaImprimible.COLUMNAS.Count));
            CollectionAssert.AreEqual(new[] { "1", "ESTÉTICA SOL (15192/0)", "", "1", "Gratuita", "Presencial", "", "" }, tabla[0]);
            CollectionAssert.AreEqual(new[] { "2", "PELUQUERÍA LA RUBIA (15191/0) — Ana Luna", "600111222", "2", "Pagada", "Presencial", "", "" }, tabla[1]);
        }

        [TestMethod]
        public void Telefono_ElDeLaPersona_SiNoElDeLaFichaDelCliente()
        {
            InscripcionEventoModel conPersona = Inscripcion(1, "A");
            conPersona.PersonaContacto = new PersonaContactoEventoModel { Numero = "2", Nombre = "Ana", Telefono = "600111222" };
            conPersona.Telefono = "915555555";
            InscripcionEventoModel personaSinTelefono = Inscripcion(2, "B");
            personaSinTelefono.PersonaContacto = new PersonaContactoEventoModel { Numero = "3", Nombre = "Marta", Telefono = " " };
            personaSinTelefono.Telefono = " 916666666 ";
            InscripcionEventoModel sinPersona = Inscripcion(3, "C");
            sinPersona.Telefono = "917777777";
            InscripcionEventoModel sinNada = Inscripcion(4, "D");

            Assert.AreEqual("600111222", ListaAsistenciaImprimible.TextoTelefono(conPersona));
            Assert.AreEqual("916666666", ListaAsistenciaImprimible.TextoTelefono(personaSinTelefono));
            Assert.AreEqual("917777777", ListaAsistenciaImprimible.TextoTelefono(sinPersona));
            Assert.AreEqual(string.Empty, ListaAsistenciaImprimible.TextoTelefono(sinNada));
            Assert.AreEqual("917777777", ListaAsistenciaImprimible.Tabla(EVENTO, new[] { sinPersona })[0][2], "la hoja usa la misma regla");
        }

        [TestMethod]
        public void Columnas_LasPedidas()
        {
            CollectionAssert.AreEqual(new[] { "Nº", "Cliente", "Teléfono", "Pers.", "Pago", "Asistencia", "Asiste", "Firma" },
                ListaAsistenciaImprimible.COLUMNAS.Select(c => c.Titulo).ToArray());
        }

        [TestMethod]
        public void FormaAsistencia_LaDelInscritoEnUnMixto_ODelEvento()
        {
            var mixto = new EventoModel { Formato = EventoModel.FORMATO_MIXTO };
            var online = new EventoModel { Formato = EventoModel.FORMATO_ONLINE };
            InscripcionEventoModel sinIndicar = Inscripcion(1, "A");
            InscripcionEventoModel enLinea = Inscripcion(2, "B");
            enLinea.FormaAsistencia = InscripcionEventoModel.FORMA_ONLINE;

            Assert.AreEqual("Sin indicar", ListaAsistenciaImprimible.TextoFormaAsistencia(sinIndicar, mixto));
            Assert.AreEqual("Online", ListaAsistenciaImprimible.TextoFormaAsistencia(enLinea, mixto));
            Assert.AreEqual("Online", ListaAsistenciaImprimible.TextoFormaAsistencia(sinIndicar, online));
            Assert.AreEqual("Presencial", ListaAsistenciaImprimible.TextoFormaAsistencia(sinIndicar, EVENTO));
        }

        [TestMethod]
        public void Cabecera_TituloFechaHoraTienda_YResumen()
        {
            Assert.AreEqual("Masterclass Cloasma", ListaAsistenciaImprimible.Titulo(EVENTO));
            Assert.AreEqual("Viernes 20/11/2026 · 10:00 – 13:00 · Reina", ListaAsistenciaImprimible.Subtitulo(EVENTO));
            InscripcionEventoModel dos = Inscripcion(1, "A");
            dos.Asistentes = 2;
            Assert.AreEqual("2 inscripciones · 3 asistentes", ListaAsistenciaImprimible.Resumen(new[] { dos, Inscripcion(2, "B") }));
        }

        private static async Task<(MantenimientoEventosViewModel vm, IImpresoraListaAsistencia impresora, IServicioDialogos dialogos)> Mantenimiento(params InscripcionEventoModel[] inscritos)
        {
            var eventos = A.Fake<IEventosService>();
            var configuracion = A.Fake<IConfiguracion>();
            var dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { EVENTO });
            A.CallTo(() => eventos.LeerInscripciones(7)).ReturnsLazily(() => inscritos.ToList());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            var impresora = A.Fake<IImpresoraListaAsistencia>();
            vm.ImpresoraLista = impresora;
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            await vm.CargarInscripcionesAsync();
            return (vm, impresora, dialogos);
        }

        [TestMethod]
        public async Task ImprimirLista_ConElEventoYTodosLosInscritos()
        {
            var (vm, impresora, _) = await Mantenimiento(Inscripcion(1, "A"), Inscripcion(2, "B"));

            Assert.IsTrue(vm.ImprimirListaCommand.CanExecute(null));
            vm.ImprimirListaCommand.Execute(null);

            A.CallTo(() => impresora.Imprimir(A<EventoModel>.That.Matches(e => e.Id == 7),
                A<IEnumerable<InscripcionEventoModel>>.That.Matches(l => l.Count() == 2))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ImprimirLista_SinInscritos_Deshabilitado()
        {
            var (vm, _, _) = await Mantenimiento(Inscripcion(3, "X", InscripcionEventoModel.ANULADA, "Anulada"));

            Assert.IsFalse(vm.ImprimirListaCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task ImprimirLista_SiFalla_MensajeDeUsted()
        {
            var (vm, impresora, dialogos) = await Mantenimiento(Inscripcion(1, "A"));
            A.CallTo(() => impresora.Imprimir(A<EventoModel>._, A<IEnumerable<InscripcionEventoModel>>._)).Throws(new InvalidOperationException("La impresora no responde."));

            vm.ImprimirLista();

            A.CallTo(() => dialogos.ShowError("No se ha podido imprimir la lista de «Masterclass Cloasma»: La impresora no responde.")).MustHaveHappenedOnceExactly();
        }
    }
}
