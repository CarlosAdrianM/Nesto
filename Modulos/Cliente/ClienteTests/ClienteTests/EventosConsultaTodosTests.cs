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
    /// Nesto#619 (sugerencia 1021 de Sancho, Carlos 04/11/26): la pantalla de eventos es para todos. Sin permisos de
    /// mantenimiento se ven los eventos y los inscritos y se puede inscribir (la API acota a quién); lo demás, deshabilitado.
    /// Un comercial ve por defecto solo a sus clientes inscritos.
    /// </summary>
    [TestClass]
    public class EventosConsultaTodosTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();

        public EventosConsultaTodosTests()
        {
            // Un comercial: ningún grupo de los que mantienen eventos, con su vendedor en los parámetros.
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(A<string>.Ignored)).Returns(false);
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("ALG");
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.Vendedor)).Returns("JE");
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).Returns(true);
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored)).Returns(true);
        }

        private void HacerDeAdministracion() =>
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)).Returns(true);

        private static EventoModel Evento(int id, DateTime fecha) =>
            new() { Id = id, Empresa = "1", Titulo = "Masterclass Cloasma", Fecha = fecha, ImporteSenal = 50, Activo = true, Almacen = "REI" };

        private static InscripcionEventoModel Inscripcion(int id, string cliente, string estado, bool? mio, params string[] vendedores) =>
            new()
            {
                Id = id, EventoId = 7, Evento = "Masterclass Cloasma", Empresa = "1", Cliente = cliente, Contacto = "0",
                Nombre = "Cliente " + cliente, Estado = estado, EstadoTexto = estado, EsDeMisClientes = mio,
                Vendedores = vendedores.Length == 0 ? null : vendedores.ToList()
            };

        private async Task<MantenimientoEventosViewModel> Pantalla(EventoModel evento, params InscripcionEventoModel[] inscritos)
        {
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { evento });
            A.CallTo(() => eventos.LeerInscripciones(evento.Id)).Returns(inscritos.ToList());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            await vm.CargarInscripcionesAsync();
            return vm;
        }

        [TestMethod]
        public void PuedeVer_CualquierUsuario_PuedeMantener_SoloLosDeSiempre()
        {
            Assert.IsTrue(MantenimientoEventosViewModel.PuedeVer(configuracion));
            Assert.IsFalse(MantenimientoEventosViewModel.PuedeMantener(configuracion));
            Assert.IsFalse(MantenimientoEventosViewModel.PuedeVer(null));
        }

        [TestMethod]
        public async Task SinPermisos_SeCarganLosEventosYLosInscritos()
        {
            MantenimientoEventosViewModel vm = await Pantalla(Evento(7, DateTime.Today.AddDays(5)),
                Inscripcion(1, "15191", InscripcionEventoModel.PAGADA, true, "JE"),
                Inscripcion(2, "20000", InscripcionEventoModel.RESERVADA, false, "NV"));

            Assert.AreEqual(1, vm.Eventos.Count);
            Assert.AreEqual(2, vm.Inscripciones.Count);
            Assert.IsTrue(vm.SoloConsulta);
            Assert.IsFalse(errores.Any());
        }

        [TestMethod]
        public async Task SinPermisos_LosBotonesDeEscrituraDeshabilitados_InscribirYCopiarEnlaceNo()
        {
            var ajena = Inscripcion(2, "20000", InscripcionEventoModel.RESERVADA, false, "NV");
            ajena.UrlPago = "https://api.nuevavision.es/pago/x";
            MantenimientoEventosViewModel vm = await Pantalla(Evento(7, DateTime.Today.AddDays(-1)),
                Inscripcion(1, "15191", InscripcionEventoModel.PAGADA, true, "JE"), ajena);
            vm.SoloMisClientes = false;
            vm.InscripcionSeleccionada = vm.Inscripciones.Single(i => i.Id == 2);

            Assert.IsFalse(vm.NuevoCommand.CanExecute(null));
            Assert.IsFalse(vm.SePuedeEditar);
            Assert.IsFalse(vm.GuardarCommand.CanExecute(null));
            Assert.IsFalse(vm.SubirImagenCommand.CanExecute(null));
            Assert.IsFalse(vm.CrearProductoCursoCommand.CanExecute(null));
            Assert.IsFalse(vm.MarcarPagadaCommand.CanExecute(null));
            Assert.IsFalse(vm.ConfirmaAsistenciaCommand.CanExecute(null));
            Assert.IsFalse(vm.CambiarAsistentesCommand.CanExecute(null));
            Assert.IsFalse(vm.AnularInscripcionCommand.CanExecute(null), "La reserva de otro comercial no la anula");
            Assert.IsFalse(vm.SePuedePasarLista, "La lista la pasan la tienda, Administración y Dirección");
            Assert.IsFalse(vm.GuardarAsistenciaCommand.CanExecute(null));
            Assert.IsTrue(vm.InscribirCommand.CanExecute(null), "Inscribir, todos (la API acota a sus clientes)");
            Assert.IsTrue(vm.CopiarEnlaceCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task SinPermisos_AnulaLaReservaDeSuCliente()
        {
            MantenimientoEventosViewModel vm = await Pantalla(Evento(7, DateTime.Today.AddDays(5)),
                Inscripcion(1, "15191", InscripcionEventoModel.RESERVADA, true, "JE"));
            vm.InscripcionSeleccionada = vm.InscripcionesVisibles.Single();

            Assert.IsTrue(vm.AnularInscripcionCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task ConPermisos_LosBotonesDeEscrituraHabilitados()
        {
            HacerDeAdministracion();
            MantenimientoEventosViewModel vm = await Pantalla(Evento(7, DateTime.Today.AddDays(-1)),
                Inscripcion(1, "20000", InscripcionEventoModel.RESERVADA, false, "NV"),
                Inscripcion(2, "20001", InscripcionEventoModel.PAGADA, false, "NV"));
            vm.InscripcionSeleccionada = vm.Inscripciones.Single(i => i.Id == 1);

            Assert.IsFalse(vm.SoloConsulta);
            Assert.IsTrue(vm.NuevoCommand.CanExecute(null));
            Assert.IsTrue(vm.GuardarCommand.CanExecute(null));
            Assert.IsTrue(vm.MarcarPagadaCommand.CanExecute(null));
            Assert.IsTrue(vm.AnularInscripcionCommand.CanExecute(null));
            Assert.IsTrue(vm.SePuedePasarLista);
        }

        [TestMethod]
        public async Task Inscribir_403DeLaApi_MensajeDeUstedSinExcepcion()
        {
            const string soloSusClientes = "Solo puede inscribir a sus clientes (si es jefe de equipo, también a los de su equipo). Para inscribir a este cliente, pídaselo a su comercial o a Administración.";
            MantenimientoEventosViewModel vm = await Pantalla(Evento(7, DateTime.Today.AddDays(5)));
            A.CallTo(() => dialogos.ShowDialogAsync(InscribirEventoDialogViewModel.NOMBRE, A<ParametrosDialogo>.Ignored))
                .ThrowsAsync(new Exception(EventosService.MensajeError(403, JsonConvert.SerializeObject(new { Message = soloSusClientes }), "No se pudo inscribir al cliente")));

            await vm.InscribirAsync();

            Assert.AreEqual(soloSusClientes, errores.Single());
        }

        [TestMethod]
        public void MensajeError_403SinMensajeOConElDeWebApi_NoTienePermisoPara()
        {
            Assert.AreEqual("No tiene permiso para guardar el evento.", EventosService.MensajeError(403, "", "No se pudo guardar el evento"));
            Assert.AreEqual("No tiene permiso para cargar los eventos.",
                EventosService.MensajeError(401, JsonConvert.SerializeObject(new { Message = "Authorization has been denied for this request." }), "No se pudieron cargar los eventos"));
            Assert.AreEqual("No tiene permiso para hacerlo (Otra cosa).", EventosService.SinPermisoPara("Otra cosa"));
        }

        [TestMethod]
        public async Task Guardar_SinPermisos_AvisaYNoLlamaALaApi()
        {
            MantenimientoEventosViewModel vm = await Pantalla(Evento(7, DateTime.Today.AddDays(5)));

            await vm.GuardarAsync();

            Assert.AreEqual(MantenimientoEventosViewModel.SIN_PERMISO_MANTENER, errores.Single());
            StringAssert.StartsWith(errores.Single(), "No tiene permiso para");
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task CambiarPersonaAsiste_SinPermisos_AvisaYVuelveALaQueHabia()
        {
            MantenimientoEventosViewModel vm = await Pantalla(Evento(7, DateTime.Today.AddDays(5)),
                Inscripcion(1, "15191", InscripcionEventoModel.PAGADA, true, "JE"));
            InscripcionEventoModel inscripcion = vm.Inscripciones.Single();
            inscripcion.NumeroPersonaAsiste = "2";

            await vm.CambiarPersonaAsisteAsync(inscripcion);

            Assert.AreEqual(MantenimientoEventosViewModel.SIN_PERMISO_INSCRIPCION, errores.Last());
            Assert.AreEqual(string.Empty, inscripcion.NumeroPersonaAsiste ?? string.Empty);
            A.CallTo(() => eventos.CambiarPersonaContacto(A<int>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
        }

        // ---------------- «Solo mis clientes» ----------------

        [TestMethod]
        public async Task SoloMisClientes_ComercialPorDefectoMarcado_SoloLosSuyos()
        {
            MantenimientoEventosViewModel vm = await Pantalla(Evento(7, DateTime.Today.AddDays(5)),
                Inscripcion(1, "15191", InscripcionEventoModel.PAGADA, true, "JE"),
                Inscripcion(2, "20000", InscripcionEventoModel.PAGADA, false, "NV"));

            Assert.IsTrue(vm.HayFiltroMisClientes);
            Assert.IsTrue(vm.SoloMisClientes);
            Assert.AreEqual(1, vm.InscripcionesVisibles.Count);
            Assert.AreEqual("15191", vm.InscripcionesVisibles.Single().Cliente);
            StringAssert.Contains(vm.ResumenInscripciones, "De sus clientes: 1.");
            Assert.AreEqual(2, vm.Inscripciones.Count, "El resumen y el aforo siguen contando a todos");
        }

        [TestMethod]
        public async Task SoloMisClientes_AlDesmarcarlo_SeVenTodos()
        {
            MantenimientoEventosViewModel vm = await Pantalla(Evento(7, DateTime.Today.AddDays(5)),
                Inscripcion(1, "15191", InscripcionEventoModel.PAGADA, true, "JE"),
                Inscripcion(2, "20000", InscripcionEventoModel.PAGADA, false, "NV"));

            vm.SoloMisClientes = false;

            Assert.AreEqual(2, vm.InscripcionesVisibles.Count);
        }

        [TestMethod]
        public async Task SoloMisClientes_NingunoSuyo_AvisaDeQuitarElFiltro()
        {
            MantenimientoEventosViewModel vm = await Pantalla(Evento(7, DateTime.Today.AddDays(5)),
                Inscripcion(2, "20000", InscripcionEventoModel.PAGADA, false, "NV"));

            Assert.AreEqual(0, vm.InscripcionesVisibles.Count);
            StringAssert.StartsWith(vm.AvisoFiltroMisClientes, "Ninguno de sus clientes está inscrito");
        }

        [TestMethod]
        public async Task SoloMisClientes_AdministracionPorDefectoDesmarcado()
        {
            HacerDeAdministracion();
            MantenimientoEventosViewModel vm = await Pantalla(Evento(7, DateTime.Today.AddDays(5)),
                Inscripcion(1, "15191", InscripcionEventoModel.PAGADA, true, "JE"),
                Inscripcion(2, "20000", InscripcionEventoModel.PAGADA, false, "NV"));

            Assert.IsFalse(vm.SoloMisClientes);
            Assert.AreEqual(2, vm.InscripcionesVisibles.Count);
        }

        [TestMethod]
        public async Task SoloMisClientes_SinVendedor_PorDefectoDesmarcado()
        {
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.Vendedor)).Returns(string.Empty);
            MantenimientoEventosViewModel vm = await Pantalla(Evento(7, DateTime.Today.AddDays(5)),
                Inscripcion(2, "20000", InscripcionEventoModel.PAGADA, false, "NV"));

            Assert.IsFalse(vm.SoloMisClientes);
            Assert.AreEqual(1, vm.InscripcionesVisibles.Count);
        }

        [TestMethod]
        public async Task SoloMisClientes_ConLaApiDeAntes_SinFiltroYSeVenTodos()
        {
            MantenimientoEventosViewModel vm = await Pantalla(Evento(7, DateTime.Today.AddDays(5)),
                Inscripcion(1, "15191", InscripcionEventoModel.PAGADA, null),
                Inscripcion(2, "20000", InscripcionEventoModel.PAGADA, null));

            Assert.IsFalse(vm.HayFiltroMisClientes);
            Assert.AreEqual(2, vm.InscripcionesVisibles.Count);
        }

        [TestMethod]
        public async Task SoloMisClientes_Marcado_NoSePasaListaConLaListaRecortada()
        {
            HacerDeAdministracion();
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.Vendedor)).Returns("JE");
            MantenimientoEventosViewModel vm = await Pantalla(Evento(7, DateTime.Today.AddDays(-1)),
                Inscripcion(1, "15191", InscripcionEventoModel.PAGADA, true, "JE"),
                Inscripcion(2, "20000", InscripcionEventoModel.PAGADA, false, "NV"));
            Assert.IsTrue(vm.SePuedePasarLista);

            vm.SoloMisClientes = true;

            Assert.IsFalse(vm.SePuedePasarLista);
            StringAssert.StartsWith(vm.AvisoListaAsistencia, "Quite «Solo mis clientes»");
        }

        [TestMethod]
        public void TextoVendedores_UnoOVarios()
        {
            Assert.AreEqual(" · Vendedor: JE", Inscripcion(1, "1", "Pagada", true, "JE").TextoVendedores);
            Assert.AreEqual(" · Vendedores: NV, PA", Inscripcion(1, "1", "Pagada", true, "NV", "PA").TextoVendedores);
            Assert.AreEqual(string.Empty, Inscripcion(1, "1", "Pagada", null).TextoVendedores);
        }

        [TestMethod]
        public void InscripcionEventoModel_LeeVendedoresYEsDeMisClientesDelJson()
        {
            var leida = JsonConvert.DeserializeObject<InscripcionEventoModel>("{\"Id\":1,\"Vendedores\":[\"JE\",\"PA\"],\"EsDeMisClientes\":true}");
            CollectionAssert.AreEqual(new[] { "JE", "PA" }, leida.Vendedores);
            Assert.AreEqual(true, leida.EsDeMisClientes);
        }
    }
}
