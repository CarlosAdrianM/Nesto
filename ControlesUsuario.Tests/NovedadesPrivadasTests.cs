using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#532 (NestoAPI#650): casilla «Privada» en el cuadro de sugerir, con el aviso que recomienda dejarla
    /// pública, y candado (sin votos) en las privadas de la lista.
    /// </summary>
    [TestClass]
    public class NovedadesPrivadasTests
    {
        private INovedadesService servicio;
        private IPortapapelesImagenes portapapeles;
        private List<string> preguntas;
        private bool respuesta;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<INovedadesService>();
            portapapeles = A.Fake<IPortapapelesImagenes>();
            preguntas = new List<string>();
            respuesta = true;
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario>()));
            A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).Returns(Task.FromResult(new List<NovedadUsuario>()));
        }

        private static NovedadUsuario S(int id, bool privada = false)
            => new NovedadUsuario { Id = id, Version = null, Titulo = "Sugerencia", TextoOriginal = "Sugerencia", Categoria = "Nuevo", Estado = "Pendiente",
                SugeridaNombre = "Manuel", SugeridaFecha = new DateTime(2026, 10, 20), Privada = privada,
                VotosPositivos = 0, VotosNegativos = 0, NumeroComentarios = 0 };

        private async Task<NovedadesDialogViewModel> AbrirCuadro()
        {
            var vm = new NovedadesDialogViewModel(servicio, portapapeles, p => { preguntas.Add(p); return respuesta; });
            var parametros = new ParametrosDialogo();
            parametros.Add("novedades", new List<NovedadUsuario> { new NovedadUsuario { Id = 1, Version = "1.10.44.0", Titulo = "x", Categoria = "Nuevo" } });
            vm.OnDialogOpened(parametros);
            await vm.AbrirOCerrarSugerencia();
            vm.TextoSugerencia = "Que los pedidos de compra avisen";
            return vm;
        }

        [TestMethod]
        public async Task LaCasilla_EstaDesmarcadaYSoloEnLasSugerencias()
        {
            NovedadesDialogViewModel vm = await AbrirCuadro();

            Assert.IsFalse(vm.SugerenciaPrivada);
            Assert.IsTrue(vm.PuedeSerPrivada);
            await vm.AbrirOCerrarIncidencia();
            Assert.IsFalse(vm.PuedeSerPrivada, "«Algo no funciona» no tiene casilla");
        }

        [TestMethod]
        public async Task SinMarcar_SeEnviaPublicaSinPreguntar()
        {
            A.CallTo(() => servicio.Sugerir(A<string>._, A<IReadOnlyList<byte[]>>._)).Returns(Task.FromResult(S(60)));
            NovedadesDialogViewModel vm = await AbrirCuadro();

            await vm.EnviarSugerencia();

            A.CallTo(() => servicio.Sugerir("Que los pedidos de compra avisen", null)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.SugerirPrivada(A<string>._, A<IReadOnlyList<byte[]>>._)).MustNotHaveHappened();
            Assert.IsFalse(preguntas.Contains(NovedadesDialogViewModel.AVISO_SUGERENCIA_PRIVADA));
        }

        [TestMethod]
        public async Task Marcada_AvisaYSiDiceQueSiLaEnviaPrivada()
        {
            A.CallTo(() => servicio.SugerirPrivada(A<string>._, A<IReadOnlyList<byte[]>>._)).Returns(Task.FromResult(S(60, privada: true)));
            NovedadesDialogViewModel vm = await AbrirCuadro();
            vm.SugerenciaPrivada = true;

            await vm.EnviarSugerencia();

            CollectionAssert.Contains(preguntas, NovedadesDialogViewModel.AVISO_SUGERENCIA_PRIVADA);
            A.CallTo(() => servicio.SugerirPrivada("Que los pedidos de compra avisen", null)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.Sugerir(A<string>._, A<IReadOnlyList<byte[]>>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.SugerenciaPrivada, "Para la siguiente, otra vez desmarcada");
            Assert.AreEqual(NovedadesDialogViewModel.MENSAJE_PRIVADA_ENVIADA, vm.MensajeSugerencias);
            Assert.IsTrue(vm.Novedades[0].EsPrivada);
        }

        [TestMethod]
        public async Task Marcada_SiDiceQueNo_NoSeEnviaYSeLeDejaListaParaPublica()
        {
            respuesta = false;
            NovedadesDialogViewModel vm = await AbrirCuadro();
            vm.SugerenciaPrivada = true;

            await vm.EnviarSugerencia();

            A.CallTo(() => servicio.SugerirPrivada(A<string>._, A<IReadOnlyList<byte[]>>._)).MustNotHaveHappened();
            A.CallTo(() => servicio.Sugerir(A<string>._, A<IReadOnlyList<byte[]>>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.SugerenciaPrivada);
            Assert.AreEqual("Que los pedidos de compra avisen", vm.TextoSugerencia);
            Assert.IsTrue(vm.FormularioSugerenciaAbierto);
            Assert.AreEqual(NovedadesDialogViewModel.MENSAJE_PRIVADA_DESMARCADA, vm.MensajeSugerencias);
        }

        [TestMethod]
        public async Task Marcada_ConUnaApiQueNoConoceLasPrivadas_SeDiceQueHaIdoPublica()
        {
            A.CallTo(() => servicio.SugerirPrivada(A<string>._, A<IReadOnlyList<byte[]>>._)).Returns(Task.FromResult(S(60, privada: false)));
            NovedadesDialogViewModel vm = await AbrirCuadro();
            vm.SugerenciaPrivada = true;

            await vm.EnviarSugerencia();

            Assert.AreEqual(NovedadesDialogViewModel.MENSAJE_PRIVADA_NO_ADMITIDA, vm.MensajeSugerencias);
        }

        [TestMethod]
        public async Task Marcada_SiLaApiLaRechaza_SeConservaTodoYSeDiceElMotivo()
        {
            A.CallTo(() => servicio.SugerirPrivada(A<string>._, A<IReadOnlyList<byte[]>>._))
                .Throws(new InvalidOperationException("No se pudo enviar la sugerencia: Todavía no se pueden enviar sugerencias privadas."));
            NovedadesDialogViewModel vm = await AbrirCuadro();
            vm.SugerenciaPrivada = true;

            await vm.EnviarSugerencia();

            Assert.IsTrue(vm.SugerenciaPrivada);
            Assert.AreEqual("Que los pedidos de compra avisen", vm.TextoSugerencia);
            StringAssert.Contains(vm.MensajeSugerencias, "privadas");
        }

        [TestMethod]
        public void UnaPrivada_LlevaCandadoYNoSeVota()
        {
            var item = new NovedadItem(S(70, privada: true), servicio, portapapeles, _ => true);

            Assert.IsTrue(item.EsPrivada);
            StringAssert.StartsWith(item.TextoPrivada, "🔒");
            Assert.IsFalse(item.PuedeVotar);
        }

        [TestMethod]
        public async Task UnaPrivada_MeGustaNoLlamaALaApi()
        {
            var item = new NovedadItem(S(70, privada: true), servicio, portapapeles, _ => true);

            await item.Votar(1);

            A.CallTo(() => servicio.VotarNovedad(A<int>._, A<short>._)).MustNotHaveHappened();
            Assert.AreEqual(0, item.VotosPositivos);
        }

        [TestMethod]
        public void UnaPublica_ComoSiempre()
        {
            var item = new NovedadItem(S(71), servicio, portapapeles, _ => true);

            Assert.IsFalse(item.EsPrivada);
            Assert.IsNull(item.TextoPrivada);
            Assert.IsTrue(item.PuedeVotar);
        }
    }
}
