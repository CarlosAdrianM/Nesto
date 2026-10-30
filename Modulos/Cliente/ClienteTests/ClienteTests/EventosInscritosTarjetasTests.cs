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
    /// Nesto#572 (sugerencias 902 y 862): los inscritos del evento en tarjetas. La seleccionada se despliega con el detalle, la
    /// anterior se pliega, y desplegar una con «Ver detalle» la selecciona (los botones actúan sobre la que se ve abierta).
    /// </summary>
    [TestClass]
    public class EventosInscritosTarjetasTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();

        public EventosInscritosTarjetasTests()
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
        }

        private static InscripcionEventoModel Inscripcion(int id, string estado = InscripcionEventoModel.PAGADA) =>
            new()
            {
                Id = id, EventoId = 7, Evento = "Masterclass", FechaEvento = DateTime.Today.AddDays(5), Empresa = "1",
                Cliente = "15191", Contacto = "0", Nombre = "LUNA", Estado = estado, Modalidad = InscribirEventoModel.MODALIDAD_REEMBOLSABLE
            };

        private async Task<MantenimientoEventosViewModel> Mantenimiento(params InscripcionEventoModel[] inscritos)
        {
            var evento = new EventoModel { Id = 7, Empresa = "1", Titulo = "Masterclass", Fecha = DateTime.Today.AddDays(5), Almacen = "REI", Activo = true };
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { evento });
            A.CallTo(() => eventos.LeerInscripciones(7)).ReturnsLazily(() => inscritos.Select(Copia).ToList());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            await vm.CargarInscripcionesAsync();
            return vm;
        }

        /// <summary>Como la API: cada lectura devuelve objetos nuevos.</summary>
        private static InscripcionEventoModel Copia(InscripcionEventoModel i) => Inscripcion(i.Id, i.Estado);

        [TestMethod]
        public async Task Seleccionar_DespliegaLaTarjetaYPliegaLaAnterior()
        {
            var vm = await Mantenimiento(Inscripcion(1), Inscripcion(2));
            Assert.IsFalse(vm.Inscripciones.Any(i => i.Desplegada), "Al cargar, todas plegadas");

            vm.InscripcionSeleccionada = vm.Inscripciones[0];
            Assert.IsTrue(vm.Inscripciones[0].Desplegada);

            vm.InscripcionSeleccionada = vm.Inscripciones[1];
            Assert.IsFalse(vm.Inscripciones[0].Desplegada, "La anterior se pliega");
            Assert.IsTrue(vm.Inscripciones[1].Desplegada);

            vm.InscripcionSeleccionada = null;
            Assert.IsFalse(vm.Inscripciones.Any(i => i.Desplegada));
        }

        [TestMethod]
        public async Task VerDetalle_DeOtraTarjeta_LaSelecciona()
        {
            var vm = await Mantenimiento(Inscripcion(1), Inscripcion(2, InscripcionEventoModel.RESERVADA));
            vm.InscripcionSeleccionada = vm.Inscripciones[0];

            vm.Inscripciones[1].Desplegada = true;

            Assert.AreSame(vm.Inscripciones[1], vm.InscripcionSeleccionada, "Los botones actúan sobre la que se ve abierta");
            Assert.IsFalse(vm.Inscripciones[0].Desplegada);
            Assert.IsTrue(vm.AnularInscripcionCommand.CanExecute(null), "Anular se habilita para la reservada recién desplegada");
        }

        [TestMethod]
        public async Task OcultarDetalle_PliegaSinPerderLaSeleccion()
        {
            var vm = await Mantenimiento(Inscripcion(1), Inscripcion(2));
            vm.InscripcionSeleccionada = vm.Inscripciones[0];

            vm.Inscripciones[0].Desplegada = false;

            Assert.AreSame(vm.Inscripciones[0], vm.InscripcionSeleccionada);
            Assert.IsFalse(vm.Inscripciones[0].Desplegada);
        }

        [TestMethod]
        public async Task Recargar_LaSeleccionadaSigueDesplegada()
        {
            var vm = await Mantenimiento(Inscripcion(1), Inscripcion(2));
            vm.InscripcionSeleccionada = vm.Inscripciones[1];

            await vm.CargarInscripcionesAsync();

            Assert.AreEqual(2, vm.InscripcionSeleccionada.Id);
            Assert.IsTrue(vm.InscripcionSeleccionada.Desplegada);
            Assert.IsFalse(vm.Inscripciones[0].Desplegada);
        }

        [TestMethod]
        public void TextoResumen_Pagada_FormaPagoImporteAsistenciaYPersona()
        {
            var inscripcion = Inscripcion(1);
            inscripcion.FormaPago = "Tarjeta ";
            inscripcion.ImportePagado = 20m;
            inscripcion.FechaPago = new DateTime(2026, 10, 12);
            inscripcion.EstadoAsistenciaTexto = "Confirmada";
            inscripcion.PersonaContacto = new PersonaContactoEventoModel { Contacto = "0", Numero = "1", Nombre = "ANA LUNA", Telefono = " 600 " };

            Assert.AreEqual($"Tarjeta · pagado {20m:C} el 12/10/2026 · asistencia: Confirmada · asiste: ANA LUNA", inscripcion.TextoResumen);
            Assert.AreEqual("600", inscripcion.TelefonoPersona);
        }

        [TestMethod]
        public void TextoResumen_Reservada_DiceHastaCuandoPuedePagar()
        {
            var inscripcion = Inscripcion(1, InscripcionEventoModel.RESERVADA);
            inscripcion.FormaPago = "Transferencia";
            inscripcion.FechaLimitePago = new DateTime(2026, 11, 3);

            Assert.AreEqual("Transferencia · pagar hasta 03/11/2026 · asistencia: Pendiente", inscripcion.TextoResumen);
        }

        [TestMethod]
        public void TextoResumen_Gratuita_SinFormaDePago()
        {
            var inscripcion = Inscripcion(1, InscripcionEventoModel.INSCRITA);
            inscripcion.Modalidad = InscribirEventoModel.MODALIDAD_GRATUITA;
            inscripcion.FormaPago = "Tarjeta";

            Assert.AreEqual("gratuita · asistencia: Pendiente", inscripcion.TextoResumen);
        }

        [TestMethod]
        public void ActualizarPersona_AvisaDelResumenYDelTelefono()
        {
            var inscripcion = Inscripcion(1);
            var avisadas = new List<string>();
            inscripcion.PropertyChanged += (_, e) => avisadas.Add(e.PropertyName);

            inscripcion.ActualizarPersona(new InscripcionEventoModel
            {
                PersonaContacto = new PersonaContactoEventoModel { Contacto = "0", Numero = "2", Nombre = "EVA", Telefono = "611" }
            });

            CollectionAssert.Contains(avisadas, nameof(InscripcionEventoModel.TextoResumen));
            CollectionAssert.Contains(avisadas, nameof(InscripcionEventoModel.TelefonoPersona));
            StringAssert.Contains(inscripcion.TextoResumen, "asiste: EVA");
        }

        [TestMethod]
        public void TextoInscritoPor_QuienYCuando()
        {
            var inscripcion = Inscripcion(1);
            inscripcion.UsuarioCreacion = "NUEVAVISION\\Reina ";
            inscripcion.FechaCreacion = new DateTime(2026, 10, 12, 10, 30, 0);

            Assert.AreEqual("NUEVAVISION\\Reina el 12/10/2026 10:30", inscripcion.TextoInscritoPor);
        }
    }
}
