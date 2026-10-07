using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// NestoAPI#591: mantenimiento de eventos, marcar/quitar la señal desde el extracto del cliente y la ventana
    /// «Señales de eventos». La lógica (estados, validaciones) vive en la API; los VM piden y pintan.
    /// </summary>
    [TestClass]
    public class EventosSenalesViewModelTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly IServicioNavegacion navegacion = A.Fake<IServicioNavegacion>();
        private readonly IExtractoClienteService extracto = A.Fake<IExtractoClienteService>();
        private readonly List<string> errores = new();

        public EventosSenalesViewModelTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
        }

        private void EnGrupo(string grupo) => A.CallTo(() => configuracion.UsuarioEnGrupo(grupo)).Returns(true);

        private static EventoModel Evento(int id, string titulo, DateTime fecha) =>
            new() { Id = id, Empresa = "1", Titulo = titulo, Fecha = fecha, ImporteSenal = 50, Activo = true };

        private static ExtractoClienteModel Apunte(int id, decimal pendiente) =>
            new() { Id = id, Empresa = "1", Cliente = "15191", Contacto = "0", Concepto = "S/Pago a cuenta curso", Importe = pendiente, ImportePendiente = pendiente };

        // ---------------- Mantenimiento de eventos ----------------

        [TestMethod]
        public void PuedeMantener_SoloTiendaOnlineDireccionEInformatica()
        {
            Assert.IsFalse(MantenimientoEventosViewModel.PuedeMantener(configuracion));
            EnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
            Assert.IsFalse(MantenimientoEventosViewModel.PuedeMantener(configuracion), "Administración marca señales, no mantiene eventos");
            EnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE);
            Assert.IsTrue(MantenimientoEventosViewModel.PuedeMantener(configuracion));
        }

        [TestMethod]
        public async Task Mantenimiento_NuevoYGuardar_CreaElEventoYRecarga()
        {
            EnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE);
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored))
                .ReturnsLazily((EventoModel e) => new EventoModel { Id = 7, Titulo = e.Titulo, Fecha = e.Fecha, ImporteSenal = e.ImporteSenal, Activo = e.Activo });
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { Evento(7, "Masterclass Cloasma", new DateTime(2026, 10, 13)) });
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);

            vm.NuevoCommand.Execute(null);
            vm.TituloEdicion = "  Masterclass Cloasma ";
            vm.FechaEdicion = new DateTime(2026, 10, 13, 10, 0, 0);
            vm.ImporteSenalEdicion = 50;
            await vm.GuardarAsync();

            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.That.Matches(e =>
                e.Id == 0 && e.Titulo == "Masterclass Cloasma" && e.Fecha == new DateTime(2026, 10, 13) && e.ImporteSenal == 50 && e.Activo)))
                .MustHaveHappenedOnceExactly();
            Assert.AreEqual(7, vm.Seleccionado?.Id);
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task Mantenimiento_EditarElSeleccionado_MandaSuId()
        {
            EnGrupo(Constantes.GruposSeguridad.DIRECCION);
            EventoModel existente = Evento(3, "Curso peelings", new DateTime(2026, 11, 3));
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { existente });
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).ReturnsLazily((EventoModel e) => e);
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();

            vm.Seleccionado = vm.Eventos.Single();
            vm.ActivoEdicion = false;
            await vm.GuardarAsync();

            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.That.Matches(e => e.Id == 3 && !e.Activo && e.Titulo == "Curso peelings")))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Mantenimiento_SinTituloOSinPermiso_NoGuarda()
        {
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            vm.NuevoCommand.Execute(null);
            vm.TituloEdicion = "X";
            await vm.GuardarAsync(); // sin permiso

            EnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE);
            vm.TituloEdicion = " ";
            await vm.GuardarAsync(); // sin título

            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).MustNotHaveHappened();
            Assert.AreEqual(2, errores.Count);
        }

        // ---------------- Extracto: marcar y quitar la señal ----------------

        private ExtractoClienteViewModel Extracto() =>
            new(extracto, dialogos, new WeakReferenceMessenger(), eventos, configuracion);

        private async Task<ExtractoClienteViewModel> ExtractoCargado(params ExtractoClienteModel[] apuntes)
        {
            A.CallTo(() => extracto.LeerExtractoPendiente("15191")).Returns(apuntes.ToList());
            ExtractoClienteViewModel vm = Extracto();
            vm.ClienteSeleccionado = "15191";
            await vm.CargarAsync();
            return vm;
        }

        [TestMethod]
        public async Task Extracto_AlCargar_PintaQueApuntesSonSenal()
        {
            A.CallTo(() => eventos.LeerSenalesCliente("15191")).Returns(new List<SenalEventoModel>
            {
                new() { Id = 9, Empresa = "1", NumOrdenExtracto = 101, Evento = "Masterclass Cloasma", FechaEvento = new DateTime(2026, 10, 13) }
            });

            ExtractoClienteViewModel vm = await ExtractoCargado(Apunte(101, -50), Apunte(102, -30));

            Assert.AreEqual("Señal: Masterclass Cloasma 13/10", vm.Movimientos.Single(m => m.Id == 101).SenalTexto);
            Assert.IsFalse(vm.Movimientos.Single(m => m.Id == 102).EsSenal);
        }

        [TestMethod]
        public async Task Extracto_SinSerAdministracion_NoPuedeMarcar()
        {
            EnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE);
            ExtractoClienteViewModel vm = await ExtractoCargado(Apunte(101, -50));

            vm.MovimientoSeleccionado = vm.Movimientos.Single();

            Assert.IsFalse(vm.PuedeMarcarSenales);
            Assert.IsFalse(vm.MarcarSenalCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Extracto_SoloSeMarcaUnApunteAFavorQueNoEsYaSenal()
        {
            EnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
            A.CallTo(() => eventos.LeerSenalesCliente("15191")).Returns(new List<SenalEventoModel>
            {
                new() { Id = 9, Empresa = "1", NumOrdenExtracto = 103, Evento = "X" }
            });
            ExtractoClienteViewModel vm = await ExtractoCargado(Apunte(101, -50), Apunte(102, 120), Apunte(103, -50));

            vm.MovimientoSeleccionado = vm.Movimientos.Single(m => m.Id == 101);
            Assert.IsTrue(vm.MarcarSenalCommand.CanExecute(null));
            Assert.IsFalse(vm.QuitarSenalCommand.CanExecute(null));
            vm.MovimientoSeleccionado = vm.Movimientos.Single(m => m.Id == 102);
            Assert.IsFalse(vm.MarcarSenalCommand.CanExecute(null), "Una factura no es un cobro a favor");
            vm.MovimientoSeleccionado = vm.Movimientos.Single(m => m.Id == 103);
            Assert.IsFalse(vm.MarcarSenalCommand.CanExecute(null), "Ya es señal");
            Assert.IsTrue(vm.QuitarSenalCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Extracto_MarcarSenal_EligeElEventoYLlamaALaApi()
        {
            EnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
            A.CallTo(() => eventos.LeerEventos(true)).Returns(new List<EventoModel> { Evento(4, "Masterclass Cloasma", new DateTime(2026, 10, 13)) });
            A.CallTo(() => dialogos.ShowDialogAsync(ElegirEventoDialogViewModel.NOMBRE, A<ParametrosDialogo>.Ignored))
                .Returns(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "eventoId", 4 } }));
            A.CallTo(() => eventos.MarcarSenal(4, A<MarcarSenalEventoModel>.Ignored))
                .Returns(new SenalEventoModel { Id = 11, EventoId = 4, Empresa = "1", NumOrdenExtracto = 101, Evento = "Masterclass Cloasma", FechaEvento = new DateTime(2026, 10, 13) });
            ExtractoClienteViewModel vm = await ExtractoCargado(Apunte(101, -50));
            vm.MovimientoSeleccionado = vm.Movimientos.Single();

            await vm.MarcarSenalAsync();

            A.CallTo(() => eventos.MarcarSenal(4, A<MarcarSenalEventoModel>.That.Matches(p =>
                p.NumOrdenExtracto == 101 && p.Empresa == "1" && p.Cliente == "15191" && p.Contacto == "0")))
                .MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.Movimientos.Single().EsSenal);
            Assert.IsTrue(vm.QuitarSenalCommand.CanExecute(null));
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task Extracto_MarcarSenal_SiCancelaElDialogo_NoLlamaALaApi()
        {
            EnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
            A.CallTo(() => eventos.LeerEventos(true)).Returns(new List<EventoModel> { Evento(4, "Masterclass", new DateTime(2026, 10, 13)) });
            A.CallTo(() => dialogos.ShowDialogAsync(ElegirEventoDialogViewModel.NOMBRE, A<ParametrosDialogo>.Ignored))
                .Returns(new ResultadoDialogo(ResultadoBoton.Cancel));
            ExtractoClienteViewModel vm = await ExtractoCargado(Apunte(101, -50));
            vm.MovimientoSeleccionado = vm.Movimientos.Single();

            await vm.MarcarSenalAsync();

            A.CallTo(() => eventos.MarcarSenal(A<int>.Ignored, A<MarcarSenalEventoModel>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Extracto_MarcarSenal_SinEventosActivos_AvisaSinAbrirElDialogo()
        {
            EnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
            A.CallTo(() => eventos.LeerEventos(true)).Returns(new List<EventoModel>());
            ExtractoClienteViewModel vm = await ExtractoCargado(Apunte(101, -50));
            vm.MovimientoSeleccionado = vm.Movimientos.Single();

            await vm.MarcarSenalAsync();

            Assert.AreEqual(1, errores.Count);
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>.Ignored, A<ParametrosDialogo>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Extracto_QuitarSenal_ConfirmaYLlamaALaApi()
        {
            EnGrupo(Constantes.GruposSeguridad.INFORMATICA);
            A.CallTo(() => eventos.LeerSenalesCliente("15191")).Returns(new List<SenalEventoModel>
            {
                new() { Id = 9, Empresa = "1", NumOrdenExtracto = 101, Evento = "Masterclass" }
            });
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).Returns(true);
            ExtractoClienteViewModel vm = await ExtractoCargado(Apunte(101, -50));
            vm.MovimientoSeleccionado = vm.Movimientos.Single();

            await vm.QuitarSenalAsync();

            A.CallTo(() => eventos.QuitarSenal(9)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.Movimientos.Single().EsSenal);
        }

        [TestMethod]
        public async Task Extracto_SiFallanLasSenales_ElExtractoSeVeIgualConUnAviso()
        {
            A.CallTo(() => eventos.LeerSenalesCliente(A<string>.Ignored)).ThrowsAsync(new Exception("sin tablas"));

            ExtractoClienteViewModel vm = await ExtractoCargado(Apunte(101, -50));

            Assert.AreEqual(1, vm.Movimientos.Count);
            StringAssert.Contains(vm.AvisoSenales, "sin tablas");
            Assert.AreEqual(0, errores.Count);
        }

        // ---------------- Diálogo de elegir evento ----------------

        [TestMethod]
        public void ElegirEvento_PreseleccionaElPrimeroYDevuelveSuId()
        {
            var vm = new ElegirEventoDialogViewModel();
            IDialogResult resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.OnDialogOpened(new DialogParameters
            {
                { "eventos", new List<EventoModel> { Evento(4, "A", new DateTime(2026, 10, 13)), Evento(5, "B", new DateTime(2026, 10, 20)) } },
                { "apunte", "101" }
            });
            vm.Seleccionado = vm.Eventos[1];
            vm.AceptarCommand.Execute(null);

            Assert.AreEqual(ButtonResult.OK, resultado.Result);
            Assert.AreEqual(5, resultado.Parameters.GetValue<int>("eventoId"));
        }

        // ---------------- Señales de eventos ----------------

        [TestMethod]
        public async Task Senales_AlLlegar_CargaEventosYTodasLasSenales()
        {
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { Evento(4, "Masterclass", new DateTime(2026, 10, 13)) });
            A.CallTo(() => eventos.LeerSenales(null, null)).Returns(new List<SenalEventoModel>
            {
                new() { Id = 1, Cliente = "15191", ImportePendiente = 50, Estado = SenalEventoModel.LIBERADA },
                new() { Id = 2, Cliente = "20000", ImportePendiente = 20, Estado = SenalEventoModel.SIN_COMPRA }
            });
            var vm = new SenalesEventosViewModel(eventos, dialogos, navegacion);

            await vm.CargarAsync();

            Assert.AreEqual(2, vm.Eventos.Count, "«Todos los eventos» más el evento");
            Assert.AreEqual(0, vm.EventoSeleccionado.Id);
            Assert.AreEqual(2, vm.Senales.Count);
            Assert.AreEqual(70, vm.TotalPendiente);
        }

        [TestMethod]
        public async Task Senales_FiltrarPorEstadoYEvento_PideALaApiConEsosFiltros()
        {
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { Evento(4, "Masterclass", new DateTime(2026, 10, 13)) });
            var vm = new SenalesEventosViewModel(eventos, dialogos, navegacion);
            await vm.CargarAsync();

            vm.EstadoSeleccionado = vm.Estados.Single(e => e.Texto == "Sin compra");
            vm.EventoSeleccionado = vm.Eventos.Single(e => e.Id == 4);
            await vm.CargarSenalesAsync();

            A.CallTo(() => eventos.LeerSenales(SenalEventoModel.SIN_COMPRA, 4)).MustHaveHappened();
        }

        [TestMethod]
        public void Senales_DobleClic_AbreElExtractoDelCliente()
        {
            var vm = new SenalesEventosViewModel(eventos, dialogos, navegacion);

            vm.AbrirExtractoCommand.Execute(new SenalEventoModel { Cliente = "15191 " });

            A.CallTo(() => navegacion.RequestNavigate("MainRegion", "ExtractoClienteView",
                A<ParametrosNavegacion>.That.Matches(p => p.GetValue<string>("cliente") == "15191"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Senales_PuedeVer_AdministracionYTiendaOnline()
        {
            Assert.IsFalse(SenalesEventosViewModel.PuedeVer(configuracion));
            EnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
            Assert.IsTrue(SenalesEventosViewModel.PuedeVer(configuracion));
        }
    }
}
