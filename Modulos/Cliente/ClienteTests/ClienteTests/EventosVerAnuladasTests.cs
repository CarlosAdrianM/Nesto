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
    /// Sugerencia 1141 (Paloma): las inscripciones anuladas estorban en la lista de inscritos. Con «Ver anuladas» desmarcado
    /// (por defecto) no se ven; el usuario lo puede marcar y se le recuerda (parámetro de usuario EventosVerAnuladas).
    /// Las caducadas se siguen viendo: todavía se pueden pagar tarde, marcar pagadas o reenviar el enlace.
    /// </summary>
    [TestClass]
    public class EventosVerAnuladasTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();

        public EventosVerAnuladasTests()
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
        }

        private static InscripcionEventoModel Inscripcion(int id, string estado = InscripcionEventoModel.PAGADA) =>
            new()
            {
                Id = id, EventoId = 7, Evento = "Masterclass", FechaEvento = DateTime.Today.AddDays(5), Empresa = "1",
                Cliente = "1519" + id, Contacto = "0", Nombre = "CLIENTA " + id, Estado = estado,
                Modalidad = InscribirEventoModel.MODALIDAD_REEMBOLSABLE
            };

        private async Task<MantenimientoEventosViewModel> Mantenimiento(params InscripcionEventoModel[] inscritos)
        {
            var evento = new EventoModel { Id = 7, Empresa = "1", Titulo = "Masterclass", Fecha = DateTime.Today.AddDays(5), Almacen = "REI", Activo = true };
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { evento });
            A.CallTo(() => eventos.LeerInscripciones(7)).ReturnsLazily(() => inscritos.Select(i => Inscripcion(i.Id, i.Estado)).ToList());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            await vm.CargarInscripcionesAsync();
            return vm;
        }

        private static List<int> Ids(MantenimientoEventosViewModel vm) => vm.InscripcionesVisibles.Select(i => i.Id).ToList();

        [TestMethod]
        public async Task PorDefecto_LasAnuladasNoSeVen_LasCaducadasSi()
        {
            var vm = await Mantenimiento(Inscripcion(1), Inscripcion(2, InscripcionEventoModel.ANULADA),
                Inscripcion(3, InscripcionEventoModel.CADUCADA), Inscripcion(4, InscripcionEventoModel.RESERVADA));

            Assert.IsFalse(vm.VerAnuladas);
            CollectionAssert.AreEqual(new List<int> { 1, 3, 4 }, Ids(vm));
        }

        [TestMethod]
        public async Task MarcandoVerAnuladas_SeVenTodas()
        {
            var vm = await Mantenimiento(Inscripcion(1), Inscripcion(2, InscripcionEventoModel.ANULADA), Inscripcion(3));

            vm.VerAnuladas = true;
            CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, Ids(vm));

            vm.VerAnuladas = false;
            CollectionAssert.AreEqual(new List<int> { 1, 3 }, Ids(vm));
        }

        [TestMethod]
        public async Task ElContadorDeLaCabecera_NoCambiaConElFiltro()
        {
            var vm = await Mantenimiento(Inscripcion(1), Inscripcion(2, InscripcionEventoModel.ANULADA));
            string resumen = vm.ResumenInscripciones;

            vm.VerAnuladas = true;

            Assert.AreEqual(resumen, vm.ResumenInscripciones);
            StringAssert.StartsWith(resumen, "1 inscritos");
        }

        [TestMethod]
        public async Task AlMarcarlo_SeGuardaEnLosParametrosDelUsuario()
        {
            var vm = await Mantenimiento(Inscripcion(1));

            vm.VerAnuladas = true;
            A.CallTo(() => configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.EventosVerAnuladas, "1"))
                .MustHaveHappenedOnceExactly();

            vm.VerAnuladas = false;
            A.CallTo(() => configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.EventosVerAnuladas, "0"))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SeRecuerda_AlAbrirLaPantalla_SinVolverAGuardarlo()
        {
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.EventosVerAnuladas)).Returns("1");

            var vm = await Mantenimiento(Inscripcion(1), Inscripcion(2, InscripcionEventoModel.ANULADA));

            Assert.IsTrue(vm.VerAnuladas);
            CollectionAssert.AreEqual(new List<int> { 1, 2 }, Ids(vm));
            A.CallTo(() => configuracion.GuardarParametro(A<string>.Ignored, Parametros.Claves.EventosVerAnuladas, A<string>.Ignored))
                .MustNotHaveHappened();
        }

        [TestMethod]
        public async Task SiNoSePuedeLeerElParametro_Desmarcado()
        {
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.EventosVerAnuladas)).Throws(new Exception("sin fila"));

            var vm = await Mantenimiento(Inscripcion(1), Inscripcion(2, InscripcionEventoModel.ANULADA));

            Assert.IsFalse(vm.VerAnuladas);
            CollectionAssert.AreEqual(new List<int> { 1 }, Ids(vm));
        }
    }
}
