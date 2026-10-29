using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#545 (NestoAPI#692): anunciar el evento en las apps. Al guardar (o al subir la imagen) un evento completo y sin
    /// anunciar se pregunta con «No» por defecto; si no está completo se enseña qué falta; «Anunciar otra vez» pide un
    /// reclamo (obligatorio, hasta 60) y manda otraVez=true. La API es un doble (IEventosService).
    /// </summary>
    [TestClass]
    public class EventosAnunciarViewModelTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();
        private readonly List<string> notificaciones = new();

        public EventosAnunciarViewModelTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => dialogos.ShowNotification(A<string>.Ignored, A<string>.Ignored)).Invokes((string t, string m) => notificaciones.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            // Carlos (29/10/26): anuncian Tienda online o Dirección; estos tests son de un usuario que puede anunciar.
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE)).Returns(true);
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
            A.CallTo(() => eventos.LeerInscripciones(A<int>.Ignored)).Returns(new List<InscripcionEventoModel>());
        }

        private static EventoModel Evento(bool completo, DateTime? anunciado = null, params string[] queFalta) =>
            new()
            {
                Id = 7, Empresa = "1", Titulo = "Masterclass Cloasma", Fecha = DateTime.Today.AddDays(10), ImporteSenal = 100,
                Activo = true, Almacen = "REI", Completo = completo, QueFalta = queFalta.ToList(),
                AnunciadoFecha = anunciado, AnunciadoUsuario = anunciado.HasValue ? "carlos" : null
            };

        private static AnuncioEventoModel Anuncio(string mensaje = "Anunciado en 37 dispositivos (12 de NestoApp y 25 de la tienda online).") =>
            new()
            {
                EventoId = 7, AnunciadoFecha = new DateTime(2026, 10, 22, 10, 15, 0), AnunciadoUsuario = "carlos",
                DispositivosNestoApp = 12, DispositivosTiendas = 25, Dispositivos = 37, Mensaje = mensaje
            };

        /// <summary>El mantenimiento con el evento seleccionado; al guardar, la API devuelve <paramref name="guardado"/>.</summary>
        private async Task<MantenimientoEventosViewModel> Mantenimiento(EventoModel enLista, EventoModel guardado = null)
        {
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { enLista });
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).Returns(guardado ?? enLista);
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            return vm;
        }

        private void ResponderPregunta(bool si) =>
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, MantenimientoEventosViewModel.PREGUNTA_ANUNCIAR, A<bool>.Ignored)).Returns(si);

        // ---------------- Al guardar ----------------

        [TestMethod]
        public async Task Guardar_CompletoYSinAnunciar_PreguntaDeUstedConNoPorDefecto()
        {
            var vm = await Mantenimiento(Evento(completo: true));
            ResponderPregunta(false);

            await vm.GuardarAsync();

            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, "El evento está completo. ¿Quiere anunciarlo en las apps?", true))
                .MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task Guardar_ConNo_NoVuelveAPreguntarPorEseEventoEnLaSesion()
        {
            // Carlos (27/10/26): una pregunta por evento. Tras decir «No», los guardados siguientes no insisten.
            var vm = await Mantenimiento(Evento(completo: true));
            ResponderPregunta(false);

            await vm.GuardarAsync();
            await vm.GuardarAsync();
            await vm.GuardarAsync();

            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, MantenimientoEventosViewModel.PREGUNTA_ANUNCIAR, A<bool>.Ignored))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowNotification("Evento guardado", A<string>.Ignored)).MustHaveHappenedTwiceExactly();
            A.CallTo(() => eventos.Anunciar(A<int>.Ignored, A<bool>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Guardar_UsuarioSoloDeTienda_NoPreguntaNiAnuncia()
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE)).Returns(false);
            var vm = await Mantenimiento(Evento(completo: true));

            Assert.IsFalse(vm.TocaPreguntarSiAnunciar(Evento(completo: true)), "La tienda no anuncia: lo hacen Tienda online o Dirección");
            await vm.GuardarAsync();

            A.CallTo(() => eventos.Anunciar(A<int>.Ignored, A<bool>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Guardar_ConNo_NoLlamaAAnunciar()
        {
            var vm = await Mantenimiento(Evento(completo: true));
            ResponderPregunta(false);

            await vm.GuardarAsync();

            A.CallTo(() => eventos.Anunciar(A<int>.Ignored, A<bool>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
            Assert.IsFalse(vm.PuedeAnunciarOtraVez);
        }

        [TestMethod]
        public async Task Guardar_ConSi_AnunciaYAvisaDelNumeroDeDispositivos()
        {
            var vm = await Mantenimiento(Evento(completo: true));
            ResponderPregunta(true);
            A.CallTo(() => eventos.Anunciar(7, false, null)).Returns(Anuncio());

            await vm.GuardarAsync();

            A.CallTo(() => eventos.Anunciar(7, false, null)).MustHaveHappenedOnceExactly();
            CollectionAssert.Contains(notificaciones, "Anunciado en 37 dispositivos (12 de NestoApp y 25 de la tienda online).");
            Assert.IsTrue(vm.PuedeAnunciarOtraVez, "Ya anunciado: sale «Anunciar otra vez»");
            Assert.AreEqual("Anunciado el 22/10/2026 a las 10:15 por carlos", vm.TextoUltimoAnuncio);
        }

        [TestMethod]
        public async Task Guardar_ConSiYSinMensaje_AvisaConElNumero_YSiEstaEncoladoDeLas9()
        {
            var vm = await Mantenimiento(Evento(completo: true));
            ResponderPregunta(true);
            AnuncioEventoModel sinMensaje = Anuncio(mensaje: null);
            sinMensaje.Dispositivos = 5;
            A.CallTo(() => eventos.Anunciar(7, false, null)).Returns(sinMensaje);

            await vm.GuardarAsync();

            CollectionAssert.Contains(notificaciones, "Anunciado en 5 dispositivos.");
            StringAssert.Contains(new AnuncioEventoModel { Encolado = true, ProgramadoPara = DateTime.Today.AddHours(9) }.TextoParaUsuario, "saldrá a las 09:00");
        }

        [TestMethod]
        public async Task Guardar_YaAnunciado_NoPregunta()
        {
            var vm = await Mantenimiento(Evento(completo: true, anunciado: new DateTime(2026, 10, 20, 9, 0, 0)));

            await vm.GuardarAsync();

            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => eventos.Anunciar(A<int>.Ignored, A<bool>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
            Assert.AreEqual(1, notificaciones.Count, "Se avisa de que se ha guardado, como siempre");
        }

        [TestMethod]
        public async Task Guardar_Incompleto_NoPreguntaYEnsenaQueFaltaSinBloquearElGuardado()
        {
            EventoModel incompleto = Evento(false, null, "la imagen", "el ponente");
            var vm = await Mantenimiento(incompleto);

            await vm.GuardarAsync();

            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored)).MustNotHaveHappened();
            Assert.AreEqual("Para anunciarlo en las apps falta: la imagen, el ponente.", vm.TextoQueFalta);
            Assert.IsTrue(vm.HayQueFalta);
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task ApiDeAntes_SinCompletoNiQueFalta_NoPreguntaNiEnsenaNada()
        {
            var vm = await Mantenimiento(new EventoModel { Id = 7, Titulo = "Curso", Fecha = DateTime.Today.AddDays(3), Almacen = "REI" });

            await vm.GuardarAsync();

            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored)).MustNotHaveHappened();
            Assert.AreEqual(string.Empty, vm.TextoQueFalta);
            Assert.IsFalse(vm.PuedeAnunciarOtraVez);
        }

        [TestMethod]
        public async Task SubirImagen_QueLoCompleta_Pregunta()
        {
            var vm = await Mantenimiento(Evento(false, null, "la imagen"));
            Assert.AreEqual("Para anunciarlo en las apps falta: la imagen.", vm.TextoQueFalta);
            vm.ElegirImagen = () => @"C:\imagen.png";
            EventoModel conImagen = Evento(completo: true);
            conImagen.ImagenUrl = "https://ejemplo/imagen.png";
            A.CallTo(() => eventos.SubirImagen(7, @"C:\imagen.png")).Returns(conImagen);
            ResponderPregunta(true);
            A.CallTo(() => eventos.Anunciar(7, false, null)).Returns(Anuncio());

            await vm.SubirImagenAsync();

            Assert.AreEqual(string.Empty, vm.TextoQueFalta);
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, MantenimientoEventosViewModel.PREGUNTA_ANUNCIAR, true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => eventos.Anunciar(7, false, null)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Anunciar_ErrorDeLaApi_SeEnsenaElMensaje()
        {
            var vm = await Mantenimiento(Evento(completo: true));
            ResponderPregunta(true);
            A.CallTo(() => eventos.Anunciar(7, false, null)).Throws(new Exception("No se pudo anunciar el evento: El evento ya se anunció el 22/10/2026."));

            await vm.GuardarAsync();

            CollectionAssert.Contains(errores, "No se pudo anunciar el evento: El evento ya se anunció el 22/10/2026.");
            Assert.IsFalse(vm.EstaOcupado);
        }

        // ---------------- Anunciar otra vez ----------------

        private void DialogoDevuelve(string reclamo, ResultadoBoton boton = ResultadoBoton.OK) =>
            A.CallTo(() => dialogos.ShowDialogAsync(AnunciarEventoDialogViewModel.NOMBRE, A<ParametrosDialogo>.Ignored))
                .Returns(new ResultadoDialogo(boton, new ParametrosDialogo { { AnunciarEventoDialogViewModel.PARAMETRO_RECLAMO, reclamo } }));

        [TestMethod]
        public async Task AnunciarOtraVez_MandaOtraVezYElReclamo_YEnsenaCuandoSeAnuncio()
        {
            var vm = await Mantenimiento(Evento(true, new DateTime(2026, 10, 20, 9, 5, 0)));
            Assert.IsTrue(vm.PuedeAnunciarOtraVez);
            Assert.IsTrue(vm.AnunciarOtraVezCommand.CanExecute(null));
            DialogoDevuelve("  ¡Últimas plazas disponibles!  ");
            A.CallTo(() => eventos.Anunciar(7, true, "¡Últimas plazas disponibles!")).Returns(Anuncio());

            await vm.AnunciarOtraVezAsync();

            A.CallTo(() => dialogos.ShowDialogAsync(AnunciarEventoDialogViewModel.NOMBRE, A<ParametrosDialogo>.That.Matches(p =>
                p.GetValue<int>(AnunciarEventoDialogViewModel.PARAMETRO_EVENTO_ID) == 7
                && p.GetValue<string>(AnunciarEventoDialogViewModel.PARAMETRO_ULTIMO_ANUNCIO) == "Anunciado el 20/10/2026 a las 09:05 por carlos")))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => eventos.Anunciar(7, true, "¡Últimas plazas disponibles!")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Anunciado el 22/10/2026 a las 10:15 por carlos", vm.TextoUltimoAnuncio);
        }

        [TestMethod]
        public async Task AnunciarOtraVez_ReclamoVacio_NoAnunciaYLoExplica()
        {
            var vm = await Mantenimiento(Evento(true, new DateTime(2026, 10, 20, 9, 5, 0)));
            DialogoDevuelve("   ");

            await vm.AnunciarOtraVezAsync();

            A.CallTo(() => eventos.Anunciar(A<int>.Ignored, A<bool>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
            StringAssert.StartsWith(errores.Single(), "Para volver a anunciar el evento hace falta un reclamo");
        }

        [TestMethod]
        public async Task AnunciarOtraVez_ReclamoDeMasDe60_NoAnuncia()
        {
            var vm = await Mantenimiento(Evento(true, new DateTime(2026, 10, 20, 9, 5, 0)));
            DialogoDevuelve(new string('a', 61));

            await vm.AnunciarOtraVezAsync();

            A.CallTo(() => eventos.Anunciar(A<int>.Ignored, A<bool>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
            Assert.AreEqual("El reclamo no puede pasar de 60 caracteres (tiene 61).", errores.Single());
        }

        [TestMethod]
        public async Task AnunciarOtraVez_Cancelado_NoAnuncia()
        {
            var vm = await Mantenimiento(Evento(true, new DateTime(2026, 10, 20, 9, 5, 0)));
            DialogoDevuelve("¡Aún está a tiempo!", ResultadoBoton.Cancel);

            await vm.AnunciarOtraVezAsync();

            A.CallTo(() => eventos.Anunciar(A<int>.Ignored, A<bool>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task AnunciarOtraVez_SinAnunciarNunca_NoSePuede()
        {
            var vm = await Mantenimiento(Evento(completo: true));

            Assert.IsFalse(vm.PuedeAnunciarOtraVez);
            Assert.IsFalse(vm.AnunciarOtraVezCommand.CanExecute(null));
            await vm.AnunciarOtraVezAsync();
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>.Ignored, A<ParametrosDialogo>.Ignored)).MustNotHaveHappened();
        }

        // ---------------- Diálogo del reclamo ----------------

        [TestMethod]
        public void Dialogo_ValidarReclamo_ObligatorioYHasta60()
        {
            Assert.IsNotNull(AnunciarEventoDialogViewModel.ValidarReclamo(null));
            Assert.IsNotNull(AnunciarEventoDialogViewModel.ValidarReclamo("  "));
            Assert.IsNotNull(AnunciarEventoDialogViewModel.ValidarReclamo(new string('a', 61)));
            Assert.IsNull(AnunciarEventoDialogViewModel.ValidarReclamo(new string('a', 60)));
            Assert.IsNull(AnunciarEventoDialogViewModel.ValidarReclamo("¡Solo faltan dos días!"));
        }

        [TestMethod]
        public async Task Dialogo_CargaLosReclamosDeLaApi_ProponeElPrimero_YDevuelveElEscrito()
        {
            A.CallTo(() => eventos.LeerReclamos(7)).Returns(new List<string> { "¡Últimas plazas disponibles!", "¡Solo faltan dos días!", " " });
            var dialogo = new AnunciarEventoDialogViewModel(eventos);
            ResultadoDialogo cerrado = null;
            dialogo.RequestClose += r => cerrado = r;

            dialogo.OnDialogOpened(new ParametrosDialogo
            {
                { AnunciarEventoDialogViewModel.PARAMETRO_EVENTO_ID, 7 },
                { AnunciarEventoDialogViewModel.PARAMETRO_EVENTO, "Masterclass Cloasma" },
                { AnunciarEventoDialogViewModel.PARAMETRO_ULTIMO_ANUNCIO, "Anunciado el 20/10/2026 a las 09:05 por carlos" }
            });
            await dialogo.CargarReclamosAsync();

            CollectionAssert.AreEqual(new[] { "¡Últimas plazas disponibles!", "¡Solo faltan dos días!" }, dialogo.Reclamos.ToArray());
            Assert.AreEqual("¡Últimas plazas disponibles!", dialogo.Reclamo);
            Assert.AreEqual("Anunciado el 20/10/2026 a las 09:05 por carlos", dialogo.UltimoAnuncio);

            dialogo.Reclamo = new string('a', 61);
            Assert.IsFalse(dialogo.AnunciarCommand.CanExecute(null));
            StringAssert.StartsWith(dialogo.Aviso, "El reclamo no puede pasar de 60");
            dialogo.Reclamo = "";
            Assert.IsFalse(dialogo.AnunciarCommand.CanExecute(null));

            dialogo.Reclamo = " ¡Aún está a tiempo! ";
            Assert.IsTrue(dialogo.AnunciarCommand.CanExecute(null));
            dialogo.AnunciarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, cerrado.Result);
            Assert.AreEqual("¡Aún está a tiempo!", cerrado.Parameters.GetValue<string>(AnunciarEventoDialogViewModel.PARAMETRO_RECLAMO));
        }

        // ---------------- Contrato ----------------

        [TestMethod]
        public void Contrato_UrlDeAnunciar_YElEstadoDelAnuncioNoSeManda()
        {
            Assert.AreEqual("Eventos/12/Anunciar", EventosService.UrlAnunciar(12, false));
            Assert.AreEqual("Eventos/12/Anunciar?otraVez=true", EventosService.UrlAnunciar(12, true));

            JObject json = JObject.Parse(JsonConvert.SerializeObject(Evento(true, DateTime.Today, "x")));
            Assert.IsNull(json["Completo"]);
            Assert.IsNull(json["QueFalta"]);
            Assert.IsNull(json["AnunciadoFecha"]);
            Assert.IsNull(json["AnunciadoUsuario"]);

            EventoModel leido = JsonConvert.DeserializeObject<EventoModel>(
                "{\"Id\":7,\"Completo\":false,\"QueFalta\":[\"la imagen\",\"el ponente\"],\"AnunciadoFecha\":\"2026-10-22T10:15:00\",\"AnunciadoUsuario\":\"carlos\"}");
            CollectionAssert.AreEqual(new[] { "la imagen", "el ponente" }, leido.QueFalta);
            Assert.AreEqual(new DateTime(2026, 10, 22, 10, 15, 0), leido.AnunciadoFecha);
            Assert.AreEqual("{}", JsonConvert.SerializeObject(new AnunciarEventoModel()));
        }
    }
}
