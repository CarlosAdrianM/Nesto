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
    /// NestoAPI#875 (Novedades 1193 de Reina): el enlace de la conexión online va en su propio campo de la ficha del evento y solo
    /// lo ven (y lo mandan) Tiendas, Administración, Dirección e Informática.
    /// </summary>
    [TestClass]
    public class EventosEnlaceOnlineTests
    {
        private const string TEAMS = "https://teams.microsoft.com/l/meetup-join/abc";
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();
        private readonly List<EventoModel> guardados = new();

        public EventosEnlaceOnlineTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).ReturnsLazily((EventoModel e) =>
            {
                guardados.Add(e);
                return e;
            });
        }

        private async Task<MantenimientoEventosViewModel> Mantenimiento(string grupo, EventoModel evento)
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(grupo)).Returns(true);
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { evento });
            A.CallTo(() => eventos.LeerInscripciones(A<int>.Ignored)).Returns(new List<InscripcionEventoModel>());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            return vm;
        }

        private static EventoModel Evento(string enlace) => new()
        { HoraInicio = "10:00",
            Id = 5, Empresa = "1", Titulo = "Webinar Peeling", Fecha = DateTime.Today.AddDays(20), Almacen = "REI", Formato = EventoModel.FORMATO_ONLINE,
            Activo = true, ImporteSenal = 0, ImporteSenalNoReembolsable = 0, Lugar = "Online", EnlaceOnline = enlace
        };

        [TestMethod]
        public void PuedeVerEnlaceOnline_ElMismoGrupoQueLaApi()
        {
            foreach (string grupo in new[] { Constantes.GruposSeguridad.TIENDAS, Constantes.GruposSeguridad.ADMINISTRACION,
                Constantes.GruposSeguridad.DIRECCION, Constantes.GruposSeguridad.INFORMATICA })
            {
                IConfiguracion c = A.Fake<IConfiguracion>();
                A.CallTo(() => c.UsuarioEnGrupo(grupo)).Returns(true);
                Assert.IsTrue(MantenimientoEventosViewModel.PuedeVerEnlaceOnline(c), grupo);
            }
            IConfiguracion tiendaOnline = A.Fake<IConfiguracion>();
            A.CallTo(() => tiendaOnline.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE)).Returns(true);
            Assert.IsFalse(MantenimientoEventosViewModel.PuedeVerEnlaceOnline(tiendaOnline));
            Assert.IsFalse(MantenimientoEventosViewModel.PuedeVerEnlaceOnline(null));
        }

        [TestMethod]
        public async Task Tienda_VeElEnlaceYLoGuarda()
        {
            var vm = await Mantenimiento(Constantes.GruposSeguridad.TIENDAS, Evento(TEAMS));

            Assert.IsTrue(vm.VeEnlaceOnline);
            Assert.AreEqual(TEAMS, vm.EnlaceOnlineEdicion);
            vm.EnlaceOnlineEdicion = " https://zoom.us/j/1 ";
            await vm.GuardarAsync();

            Assert.AreEqual(0, errores.Count, string.Join(" | ", errores));
            Assert.AreEqual("https://zoom.us/j/1", guardados.Single().EnlaceOnline);
        }

        [TestMethod]
        public async Task Tienda_VaciarloLoQuita()
        {
            var vm = await Mantenimiento(Constantes.GruposSeguridad.TIENDAS, Evento(TEAMS));
            vm.EnlaceOnlineEdicion = "  ";

            await vm.GuardarAsync();

            StringAssert.Contains(JsonConvert.SerializeObject(guardados.Single()), "\"EnlaceOnline\":\"\"");
        }

        [TestMethod]
        public async Task SinPermiso_NiSeVeNiSeManda()
        {
            // Tienda online mantiene la pantalla (TieneAcceso) pero no ve el enlace
            var vm = await Mantenimiento(Constantes.GruposSeguridad.TIENDA_ON_LINE, Evento(null));

            Assert.IsFalse(vm.VeEnlaceOnline);
            await vm.GuardarAsync();

            Assert.IsFalse(JsonConvert.SerializeObject(guardados.Single()).Contains("EnlaceOnline"), "Sin el campo, la API no lo toca");
        }

        [TestMethod]
        public async Task EnlaceNoValido_AvisaDeUstedYNoGuarda()
        {
            var vm = await Mantenimiento(Constantes.GruposSeguridad.DIRECCION, Evento(null));
            vm.EnlaceOnlineEdicion = "teams, pídalo en tienda";

            await vm.GuardarAsync();

            Assert.AreEqual(0, guardados.Count);
            Assert.AreEqual(MantenimientoEventosViewModel.ERROR_ENLACE_ONLINE, errores.Single());
        }

        [TestMethod]
        public void EnlaceOnlineValido()
        {
            Assert.IsTrue(MantenimientoEventosViewModel.EnlaceOnlineValido(null));
            Assert.IsTrue(MantenimientoEventosViewModel.EnlaceOnlineValido(TEAMS));
            Assert.IsFalse(MantenimientoEventosViewModel.EnlaceOnlineValido("www.zoom.us/j/1"));
            Assert.IsFalse(MantenimientoEventosViewModel.EnlaceOnlineValido("ftp://x.es/a"));
            Assert.IsFalse(MantenimientoEventosViewModel.EnlaceOnlineValido("https://x.es/a b"));
        }
    }
}
