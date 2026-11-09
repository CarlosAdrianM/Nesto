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
    /// Novedades 1132/813 (Paloma, 09/11/26): evento 6 «Rentabiliza cada ML de tus viales» creado con 400 € de señal
    /// reembolsable y una inscripción ya pagada; puso las dos señales a 0 (curso por compra) y «no me deja modificarlo»:
    /// la API rechaza que un evento con inscripciones de pago pase a ser gratuito. La ficha se carga bien; lo que faltaba
    /// es decirle por qué y qué hacer, sin llamar a la API, y que la ficha que se queda (lo que ella escribió) no es lo guardado.
    /// </summary>
    [TestClass]
    public class EventosSenalesConInscritosTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();

        public EventosSenalesConInscritosTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).ReturnsLazily((EventoModel e) => e);
        }

        private static EventoModel Viales(int ocupadas, decimal reembolsable = 400, decimal noReembolsable = 0) => new()
        {
            Id = 6,
            Empresa = "1",
            Titulo = "Rentabiliza cada ML de tus viales",
            Fecha = new DateTime(2026, 11, 23),
            Almacen = "ALC",
            Formato = EventoModel.FORMATO_MIXTO,
            ImporteSenal = reembolsable,
            ImporteSenalNoReembolsable = noReembolsable,
            PedidoMinimoCupon = 0,
            PlazasOcupadas = ocupadas,
            Activo = true
        };

        private async Task<MantenimientoEventosViewModel> ConElEvento(EventoModel evento)
        {
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { evento });
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            return vm;
        }

        [TestMethod]
        public async Task AlElegirElEvento_LaFichaTraeSusSenales()
        {
            MantenimientoEventosViewModel vm = await ConElEvento(Viales(1));

            Assert.AreEqual(400m, vm.ImporteSenalEdicion);
            Assert.AreEqual(0m, vm.ImporteSenalNoReembolsableEdicion);
            Assert.IsTrue(vm.SePuedeEditar, "Tiendas mantiene los eventos de su tienda");
        }

        [TestMethod]
        public async Task ConInscripcionesDePago_PonerLasDosSenalesA0_AvisaQueHacerYNoLlamaALaApi()
        {
            MantenimientoEventosViewModel vm = await ConElEvento(Viales(1));

            vm.ImporteSenalEdicion = 0;
            await vm.GuardarAsync();

            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).MustNotHaveHappened();
            Assert.AreEqual(1, errores.Count);
            StringAssert.Contains(errores[0], "gratuito");
            StringAssert.Contains(errores[0], "anule antes");
            StringAssert.Contains(errores[0], "Recargar");
        }

        [TestMethod]
        public async Task ConInscripcionesDePago_PasarLaSenalAReembolsableANoReembolsable_SeGuardaEntero()
        {
            MantenimientoEventosViewModel vm = await ConElEvento(Viales(1));

            vm.ImporteSenalEdicion = 0;
            vm.ImporteSenalNoReembolsableEdicion = 400;
            await vm.GuardarAsync();

            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.That.Matches(e => e.Id == 6
                && e.Titulo == "Rentabiliza cada ML de tus viales" && e.Fecha == new DateTime(2026, 11, 23)
                && e.Almacen == "ALC" && e.Formato == EventoModel.FORMATO_MIXTO
                && e.ImporteSenal == 0 && e.ImporteSenalNoReembolsable == 400 && e.Activo)))
                .MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task SinInscritos_PuedePasarAGratuito()
        {
            MantenimientoEventosViewModel vm = await ConElEvento(Viales(0));

            vm.ImporteSenalEdicion = 0;
            await vm.GuardarAsync();

            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.That.Matches(e => e.ImporteSenal == 0 && e.ImporteSenalNoReembolsable == 0)))
                .MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task GratuitoConInscritos_PonerSenal_AvisaYNoLlamaALaApi()
        {
            MantenimientoEventosViewModel vm = await ConElEvento(Viales(2, 0, 0));

            vm.ImporteSenalNoReembolsableEdicion = 60;
            await vm.GuardarAsync();

            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).MustNotHaveHappened();
            Assert.AreEqual(1, errores.Count);
            StringAssert.Contains(errores[0], "de pago");
        }

        [TestMethod]
        public async Task SiLaApiLoRechaza_EnsenaSuMensajeYQueNoSeHaGuardado()
        {
            MantenimientoEventosViewModel vm = await ConElEvento(Viales(1));
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored))
                .Throws(new Exception("No se pudo guardar el evento: El evento tiene que tener un título."));

            vm.ImporteSenalNoReembolsableEdicion = 50;
            await vm.GuardarAsync();

            Assert.AreEqual(1, errores.Count);
            StringAssert.Contains(errores[0], "El evento tiene que tener un título.");
            StringAssert.Contains(errores[0], "No se ha guardado ningún cambio");
            StringAssert.Contains(errores[0], "Recargar");
            Assert.AreEqual(50m, vm.ImporteSenalNoReembolsableEdicion, "Lo escrito se queda para poder corregirlo");
        }
    }
}
