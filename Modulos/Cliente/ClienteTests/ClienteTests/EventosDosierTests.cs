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
    /// Sugerencia 1332 (Reina; Carlos 18/11/26): en la ficha del evento se sube, se abre y se quita el dosier (PDF), que la API manda
    /// dos días antes, con el recordatorio (NestoAPI#895). Solo quien mantiene el evento.
    /// </summary>
    [TestClass]
    public class EventosDosierTests
    {
        private static readonly DateTime AHORA = new(2026, 11, 18, 10, 0, 0);
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();

        public EventosDosierTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
        }

        private static DosierEventoModel Dosier(int enviados = 0) => new()
        {
            EventoId = 5, Nombre = "Dosier Peeling.pdf", Fecha = new DateTime(2026, 11, 18, 9, 15, 0), Tamano = 1000,
            EnvioPrevisto = new DateTime(2026, 11, 20, 15, 0, 0), Enviados = enviados
        };

        private async Task<MantenimientoEventosViewModel> Mantenimiento(string grupo, DosierEventoModel dosier)
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(grupo)).Returns(true);
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel>
            {
                new() { Id = 5, Empresa = "1", Titulo = "Masterclass Peeling", Fecha = AHORA.Date.AddDays(2), Almacen = "REI", Activo = true, Dosier = dosier }
            });
            A.CallTo(() => eventos.LeerInscripciones(A<int>.Ignored)).Returns(new List<InscripcionEventoModel>());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos) { Ahora = () => AHORA };
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            return vm;
        }

        [TestMethod]
        public async Task Cargar_EnsenaElDosierYCuandoSale()
        {
            var vm = await Mantenimiento(Constantes.GruposSeguridad.TIENDAS, Dosier(0));

            Assert.AreEqual("Dosier Peeling.pdf (subido el 18/11 a las 09:15). Sale solo el 20/11 a las 15:00.", vm.TextoDosier);
            Assert.IsTrue(vm.AbrirDosierCommand.CanExecute(null));
            Assert.IsTrue(vm.QuitarDosierCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task SinDosier_LoDiceYSoloSePuedeSubir()
        {
            var vm = await Mantenimiento(Constantes.GruposSeguridad.TIENDAS, null);

            Assert.AreEqual(MantenimientoEventosViewModel.SIN_DOSIER, vm.TextoDosier);
            StringAssert.Contains(vm.TextoDosier, "sale dos días antes, con el recordatorio", "NestoAPI#895: ya no sale 2 horas antes");
            Assert.IsTrue(vm.SubirDosierCommand.CanExecute(null));
            Assert.IsFalse(vm.AbrirDosierCommand.CanExecute(null));
            Assert.IsFalse(vm.QuitarDosierCommand.CanExecute(null));
        }

        [TestMethod]
        public void Texto_YaEnviado()
        {
            DosierEventoModel enviado = Dosier(12);
            Assert.AreEqual("Dosier Peeling.pdf (subido el 18/11 a las 09:15). Enviado a 12 inscritas. Les llega a quienes se inscriban ahora.",
                enviado.Texto(new DateTime(2026, 11, 20, 16, 0, 0)));
        }

        [TestMethod]
        public async Task SinPermiso_NoSePuedeTocar()
        {
            var vm = await Mantenimiento("Ventas", Dosier());

            Assert.IsFalse(vm.SubirDosierCommand.CanExecute(null));
            Assert.IsFalse(vm.AbrirDosierCommand.CanExecute(null));
            Assert.IsFalse(vm.QuitarDosierCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Subir_MandaElPdfYPoneElNuevo()
        {
            var vm = await Mantenimiento(Constantes.GruposSeguridad.TIENDAS, null);
            vm.ElegirDosier = () => "C:\\dosier.pdf";
            A.CallTo(() => eventos.SubirDosier(5, "C:\\dosier.pdf", false)).Returns(Dosier());

            await vm.SubirDosierAsync();

            Assert.AreEqual(0, errores.Count, string.Join(" | ", errores));
            Assert.AreEqual("Dosier Peeling.pdf", vm.Dosier.Nombre);
            Assert.AreEqual("Dosier Peeling.pdf", vm.Seleccionado.Dosier.Nombre);
        }

        [TestMethod]
        public async Task Sustituir_YaEnviado_PreguntaYSiCancelaNoSube()
        {
            var vm = await Mantenimiento(Constantes.GruposSeguridad.TIENDAS, Dosier(3));
            vm.ElegirDosier = () => "C:\\nuevo.pdf";
            int preguntadoCon = 0;
            vm.PreguntarReenvioDosier = n => { preguntadoCon = n; return null; };

            await vm.SubirDosierAsync();

            Assert.AreEqual(3, preguntadoCon);
            A.CallTo(() => eventos.SubirDosier(A<int>._, A<string>._, A<bool>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Sustituir_YaEnviado_SiReenviar_LoPideALaApi_SiNo_SoloALasQueNoLoTienen()
        {
            var vm = await Mantenimiento(Constantes.GruposSeguridad.TIENDAS, Dosier(3));
            vm.ElegirDosier = () => "C:\\nuevo.pdf";
            A.CallTo(() => eventos.SubirDosier(5, "C:\\nuevo.pdf", A<bool>._)).Returns(Dosier(3));

            vm.PreguntarReenvioDosier = n => true;
            await vm.SubirDosierAsync();
            A.CallTo(() => eventos.SubirDosier(5, "C:\\nuevo.pdf", true)).MustHaveHappenedOnceExactly();

            vm.PreguntarReenvioDosier = n => false;
            await vm.SubirDosierAsync();
            A.CallTo(() => eventos.SubirDosier(5, "C:\\nuevo.pdf", false)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Sustituir_SinEnviar_NoPregunta()
        {
            var vm = await Mantenimiento(Constantes.GruposSeguridad.TIENDAS, Dosier(0));
            vm.ElegirDosier = () => "C:\\nuevo.pdf";
            bool preguntado = false;
            vm.PreguntarReenvioDosier = n => { preguntado = true; return true; };

            await vm.SubirDosierAsync();

            Assert.IsFalse(preguntado);
            A.CallTo(() => eventos.SubirDosier(5, "C:\\nuevo.pdf", false)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void TextosDeLaPregunta()
        {
            Assert.AreEqual("Sí, reenviar a las 3 que ya lo tienen", MantenimientoEventosViewModel.TextoReenviarDosier(3));
            Assert.AreEqual("Sí, reenviar a la que ya lo tiene", MantenimientoEventosViewModel.TextoReenviarDosier(1));
            StringAssert.Contains(MantenimientoEventosViewModel.PreguntaSustituirDosier(3), "ya les ha llegado a 3 inscritas");
        }

        [TestMethod]
        public async Task Guardar_SinHoraDeInicio_NoSeGuarda()
        {
            var vm = await Mantenimiento(Constantes.GruposSeguridad.TIENDAS, null);
            vm.NuevoCommand.Execute(null);
            vm.TituloEdicion = "Masterclass sin hora";
            vm.FechaEdicion = AHORA.Date.AddDays(3);
            vm.HoraInicioEdicion = null;

            await vm.GuardarAsync();

            CollectionAssert.Contains(errores, MantenimientoEventosViewModel.FALTA_HORA_INICIO);
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Guardar_EventoPasadoSinHora_SeGuarda()
        {
            var vm = await Mantenimiento(Constantes.GruposSeguridad.TIENDAS, null);
            vm.FechaEdicion = AHORA.Date.AddDays(-10);
            vm.HoraInicioEdicion = null;

            await vm.GuardarAsync();

            CollectionAssert.DoesNotContain(errores, MantenimientoEventosViewModel.FALTA_HORA_INICIO);
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Abrir_DescargaYLoAbre()
        {
            var vm = await Mantenimiento(Constantes.GruposSeguridad.DIRECCION, Dosier());
            byte[] pdf = { 1, 2, 3 };
            A.CallTo(() => eventos.DescargarDosier(5)).Returns(pdf);
            (byte[] contenido, string nombre) abierto = default;
            vm.AbrirPdf = (c, n) => abierto = (c, n);

            await vm.AbrirDosierAsync();

            CollectionAssert.AreEqual(pdf, abierto.contenido);
            Assert.AreEqual("Dosier Peeling.pdf", abierto.nombre);
        }

        [TestMethod]
        public async Task Quitar_ConfirmaYLoQuita()
        {
            var vm = await Mantenimiento(Constantes.GruposSeguridad.TIENDAS, Dosier());
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);

            await vm.QuitarDosierAsync();

            A.CallTo(() => eventos.BorrarDosier(5)).MustHaveHappenedOnceExactly();
            Assert.IsNull(vm.Dosier);
            Assert.AreEqual(MantenimientoEventosViewModel.SIN_DOSIER, vm.TextoDosier);
        }

        [TestMethod]
        public void Dosier_NoSeMandaAlGuardarElEvento()
        {
            var evento = new EventoModel { Id = 5, Titulo = "X", Dosier = Dosier() };

            Assert.IsFalse(JsonConvert.SerializeObject(evento).Contains("Dosier"));
        }
    }
}
