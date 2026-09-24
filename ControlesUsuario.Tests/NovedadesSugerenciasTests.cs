using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#487 (NestoAPI#526/#527): página «Sugerencias pendientes» por delante de la versión más nueva,
    /// botón para sugerir una característica y buscador que salta a la versión de lo encontrado.
    /// </summary>
    [TestClass]
    public class NovedadesSugerenciasTests
    {
        private INovedadesService servicio;
        private IPortapapelesImagenes portapapeles;
        private List<string> preguntas;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<INovedadesService>();
            portapapeles = A.Fake<IPortapapelesImagenes>();
            preguntas = new List<string>();
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario>()));
            A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).Returns(Task.FromResult(new List<NovedadUsuario>()));
        }

        private static NovedadUsuario N(int id, string version, string titulo = "x")
            => new NovedadUsuario { Id = id, Version = version, Titulo = titulo, Categoria = "Nuevo", VotosPositivos = 0, VotosNegativos = 0, NumeroComentarios = 0 };

        private static NovedadUsuario S(int id, string texto, bool imagen = false)
            => new NovedadUsuario { Id = id, Version = null, Titulo = texto, TextoOriginal = texto, Categoria = "Nuevo", Estado = "Pendiente",
                SugeridaNombre = "Carlos", SugeridaFecha = new DateTime(2026, 9, 24), TieneImagen = imagen,
                VotosPositivos = 0, VotosNegativos = 0, NumeroComentarios = 0 };

        private NovedadesDialogViewModel AbrirVm(params NovedadUsuario[] novedades)
        {
            var vm = new NovedadesDialogViewModel(servicio, portapapeles, p => { preguntas.Add(p); return true; });
            var parametros = new DialogParameters();
            parametros.Add("novedades", novedades.ToList());
            vm.OnDialogOpened(parametros);
            return vm;
        }

        // ---- Navegación ----

        [TestMethod]
        public void EnLaVersionMasNueva_ConServicio_LaFlechaDerechaLlevaASugerencias()
        {
            var vm = AbrirVm(N(1, "1.10.30.0"), N(2, "1.10.29.0"));

            Assert.IsTrue(vm.VersionSiguienteCommand.CanExecute(null));
            vm.VersionSiguienteCommand.Execute(null);

            Assert.IsTrue(vm.EnSugerencias);
            Assert.AreEqual(NovedadesDialogViewModel.TITULO_SUGERENCIAS, vm.VersionActual);
            A.CallTo(() => servicio.LeerSugerencias()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void SinServicio_NoHayPaginaDeSugerencias()
        {
            var vm = new NovedadesDialogViewModel();
            var parametros = new DialogParameters();
            parametros.Add("novedades", new List<NovedadUsuario> { N(1, "1.10.30.0") });
            vm.OnDialogOpened(parametros);

            Assert.IsFalse(vm.VersionSiguienteCommand.CanExecute(null));
            Assert.IsFalse(vm.PuedeSugerir);
        }

        [TestMethod]
        public async Task EnSugerencias_ListaLasDeLaApi_YLaFlechaIzquierdaVuelveALaVersionMasNueva()
        {
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario> { S(50, "Un botón para duplicar"), S(51, "Otra idea") }));
            var vm = AbrirVm(N(1, "1.10.30.0"), N(2, "1.10.29.0"));

            await vm.IrASugerencias();

            Assert.AreEqual(2, vm.Novedades.Count);
            Assert.IsTrue(vm.Novedades.All(n => n.EsSugerencia));
            Assert.AreEqual("Un botón para duplicar", vm.Novedades[0].TextoOriginal);
            StringAssert.StartsWith(vm.Novedades[0].SugeridaPor, "Sugerida por Carlos el 24/09/2026");
            Assert.IsFalse(vm.VersionSiguienteCommand.CanExecute(null), "No hay nada por delante de Sugerencias");
            Assert.IsTrue(vm.VersionAnteriorCommand.CanExecute(null));

            vm.VersionAnteriorCommand.Execute(null);

            Assert.IsFalse(vm.EnSugerencias);
            Assert.AreEqual("Versión 1.10.30.0", vm.VersionActual);
        }

        [TestMethod]
        public async Task Sugerencias_SeCarganUnaSolaVez()
        {
            var vm = AbrirVm(N(1, "1.10.30.0"));

            await vm.IrASugerencias();
            vm.VersionAnteriorCommand.Execute(null);
            await vm.IrASugerencias();

            A.CallTo(() => servicio.LeerSugerencias()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Sugerencias_SiLaApiFalla_SeDiceYSeReintentaAlVolver()
        {
            A.CallTo(() => servicio.LeerSugerencias()).Throws(new InvalidOperationException("No se pudo leer las sugerencias (error 500)."));
            var vm = AbrirVm(N(1, "1.10.30.0"));

            await vm.IrASugerencias();

            Assert.AreEqual("No se pudo leer las sugerencias (error 500).", vm.MensajeSugerencias);
            Assert.AreEqual(0, vm.Novedades.Count);

            vm.VersionAnteriorCommand.Execute(null);
            await vm.IrASugerencias();
            A.CallTo(() => servicio.LeerSugerencias()).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task Sugerencias_ConCaptura_SePideLaImagen()
        {
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario> { S(50, "Con captura", imagen: true), S(51, "Sin captura") }));
            A.CallTo(() => servicio.LeerImagenNovedad(50)).Returns(Task.FromResult(new byte[] { 1, 2, 3 }));
            var vm = AbrirVm(N(1, "1.10.30.0"));

            await vm.IrASugerencias();

            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, vm.Novedades.Single(n => n.Id == 50).ImagenNovedad);
            A.CallTo(() => servicio.LeerImagenNovedad(51)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Sugerencia_SeVotaConElMismoEndpointQueLasNovedades()
        {
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario> { S(50, "Idea") }));
            var vm = AbrirVm(N(1, "1.10.30.0"));
            await vm.IrASugerencias();

            await vm.Novedades.Single().Votar(1);

            A.CallTo(() => servicio.VotarNovedad(50, 1)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, vm.Novedades.Single().VotosPositivos);
        }

        // ---- Sugerir ----

        [TestMethod]
        public async Task Sugerir_AbreElCuadroEnLaPaginaDeSugerencias()
        {
            var vm = AbrirVm(N(1, "1.10.30.0"));

            await vm.AbrirOCerrarSugerencia();

            Assert.IsTrue(vm.FormularioSugerenciaAbierto);
            Assert.IsTrue(vm.EnSugerencias);
        }

        [TestMethod]
        public async Task Sugerir_ConImagenCopiada_OfreceAdjuntarla()
        {
            A.CallTo(() => portapapeles.HayImagen()).Returns(true);
            A.CallTo(() => portapapeles.LeerImagenPng()).Returns(new byte[] { 9 });
            var vm = AbrirVm(N(1, "1.10.30.0"));

            await vm.AbrirOCerrarSugerencia();

            Assert.AreEqual(1, preguntas.Count);
            StringAssert.Contains(preguntas[0], "sugerencia");
            Assert.IsTrue(vm.TieneImagenSugerencia);
        }

        [TestMethod]
        public void PegarImagenSugerencia_DeMasDe2Mb_NoSeAdjuntaYSeAvisa()
        {
            A.CallTo(() => portapapeles.HayImagen()).Returns(true);
            A.CallTo(() => portapapeles.LeerImagenPng()).Returns(new byte[NovedadItem.TAMANO_MAXIMO_IMAGEN + 1]);
            var vm = AbrirVm(N(1, "1.10.30.0"));

            bool habiaImagen = vm.PegarImagenSugerencia();

            Assert.IsTrue(habiaImagen);
            Assert.IsFalse(vm.TieneImagenSugerencia);
            StringAssert.Contains(vm.MensajeSugerencias, "2 MB");
        }

        [TestMethod]
        public void EnviarSugerencia_SinTexto_NoSePuede()
        {
            var vm = AbrirVm(N(1, "1.10.30.0"));

            Assert.IsFalse(vm.EnviarSugerenciaCommand.CanExecute(null));
            vm.TextoSugerencia = "Poder duplicar un pedido";
            Assert.IsTrue(vm.EnviarSugerenciaCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task EnviarSugerencia_LaCreaYApareceArribaDeLaListaResaltada()
        {
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario> { S(50, "Una anterior") }));
            A.CallTo(() => servicio.Sugerir(A<string>._, A<byte[]>._)).Returns(Task.FromResult(S(60, "Poder duplicar un pedido")));
            var vm = AbrirVm(N(1, "1.10.30.0"));
            await vm.AbrirOCerrarSugerencia();
            vm.TextoSugerencia = "  Poder duplicar un pedido ";

            await vm.EnviarSugerencia();

            A.CallTo(() => servicio.Sugerir("Poder duplicar un pedido", null)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(60, vm.Novedades[0].Id);
            Assert.AreEqual(2, vm.Novedades.Count);
            Assert.AreSame(vm.Novedades[0], vm.NovedadDestacada);
            Assert.IsTrue(vm.Novedades[0].Destacada);
            Assert.IsFalse(vm.FormularioSugerenciaAbierto);
            Assert.IsNull(vm.TextoSugerencia);
        }

        [TestMethod]
        public async Task EnviarSugerencia_ConImagen_LaMandaYLaEnseñaSinVolverAPedirla()
        {
            A.CallTo(() => portapapeles.HayImagen()).Returns(true);
            A.CallTo(() => portapapeles.LeerImagenPng()).Returns(new byte[] { 7, 7 });
            var creada = S(60, "Con captura", imagen: true);
            A.CallTo(() => servicio.Sugerir(A<string>._, A<byte[]>._)).Returns(Task.FromResult(creada));
            var vm = AbrirVm(N(1, "1.10.30.0"));
            await vm.AbrirOCerrarSugerencia();
            vm.TextoSugerencia = "Con captura";

            await vm.EnviarSugerencia();

            A.CallTo(() => servicio.Sugerir("Con captura", A<byte[]>.That.Matches(b => b.Length == 2))).MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new byte[] { 7, 7 }, vm.Novedades[0].ImagenNovedad);
            Assert.IsFalse(vm.TieneImagenSugerencia);
        }

        [TestMethod]
        public async Task EnviarSugerencia_SiLaApiFalla_SeConservaElTextoYSeDiceElMotivo()
        {
            A.CallTo(() => servicio.Sugerir(A<string>._, A<byte[]>._)).Throws(new InvalidOperationException("No se pudo enviar la sugerencia: El texto es obligatorio"));
            var vm = AbrirVm(N(1, "1.10.30.0"));
            await vm.AbrirOCerrarSugerencia();
            vm.TextoSugerencia = "Algo";

            await vm.EnviarSugerencia();

            Assert.AreEqual("Algo", vm.TextoSugerencia);
            Assert.IsTrue(vm.FormularioSugerenciaAbierto);
            StringAssert.Contains(vm.MensajeSugerencias, "No se pudo enviar la sugerencia");
        }

        // ---- Buscador ----

        [TestMethod]
        public async Task Buscar_ListaLosResultadosConSuVersionOSugerencia()
        {
            A.CallTo(() => servicio.Buscar("reembolso")).Returns(Task.FromResult(new List<NovedadUsuario> { N(2, "1.10.29.0", "Reembolsos"), S(50, "Reembolso parcial") }));
            var vm = AbrirVm(N(1, "1.10.30.0"), N(2, "1.10.29.0"));
            vm.TextoBusqueda = " reembolso ";

            await vm.Buscar();

            Assert.AreEqual(2, vm.ResultadosBusqueda.Count);
            Assert.AreEqual("Versión 1.10.29.0", vm.ResultadosBusqueda[0].Donde);
            Assert.AreEqual("Sugerencia", vm.ResultadosBusqueda[1].Donde);
            Assert.IsTrue(vm.HayResultadosBusqueda);
        }

        [TestMethod]
        public async Task Buscar_SinResultados_LoDice()
        {
            A.CallTo(() => servicio.Buscar(A<string>._)).Returns(Task.FromResult(new List<NovedadUsuario>()));
            var vm = AbrirVm(N(1, "1.10.30.0"));
            vm.TextoBusqueda = "nada";

            await vm.Buscar();

            Assert.IsFalse(vm.HayResultadosBusqueda);
            Assert.IsTrue(vm.HayMensajeBusqueda);
        }

        [TestMethod]
        public async Task Buscar_BadRequestDeLaApi_SeEnseñaElMotivo()
        {
            A.CallTo(() => servicio.Buscar(A<string>._)).Throws(new InvalidOperationException("No se pudo buscar: Escribe al menos una palabra de 2 letras"));
            var vm = AbrirVm(N(1, "1.10.30.0"));
            vm.TextoBusqueda = "a";

            await vm.Buscar();

            Assert.AreEqual("No se pudo buscar: Escribe al menos una palabra de 2 letras", vm.MensajeBusqueda);
        }

        [TestMethod]
        public async Task ElegirResultado_DeUnaVersionCargada_SaltaAEllaYLaResalta()
        {
            var vm = AbrirVm(N(1, "1.10.30.0"), N(2, "1.10.29.0", "Reembolsos"), N(3, "1.10.28.0"));

            await vm.IrAResultado(N(2, "1.10.29.0", "Reembolsos"));

            Assert.AreEqual("Versión 1.10.29.0", vm.VersionActual);
            Assert.AreEqual(2, vm.NovedadDestacada.Id);
            Assert.IsTrue(vm.Novedades.Contains(vm.NovedadDestacada));
            Assert.IsTrue(vm.NovedadDestacada.Destacada);
            A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ElegirResultado_DeUnaVersionNoCargada_PideTodasYSaltaASuVersion()
        {
            // El popup de arranque solo trae las versiones nuevas: la encontrada es de una antigua.
            A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).Returns(Task.FromResult(new List<NovedadUsuario>
            {
                N(1, "1.10.30.0"), N(9, "1.9.0.0", "Antigua"), N(8, "1.9.0.0", "Otra antigua")
            }));
            var vm = AbrirVm(N(1, "1.10.30.0"));

            await vm.IrAResultado(N(9, "1.9.0.0", "Antigua"));

            Assert.AreEqual("Versión 1.9.0.0", vm.VersionActual);
            Assert.AreEqual(2, vm.Novedades.Count);
            Assert.AreEqual(9, vm.NovedadDestacada.Id);
            // La que ya estaba no se duplica
            vm.VersionSiguienteCommand.Execute(null);
            Assert.AreEqual(1, vm.Novedades.Count);
        }

        [TestMethod]
        public async Task ElegirResultado_Sugerencia_SaltaASugerenciasConEllaResaltada()
        {
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario> { S(50, "Una"), S(51, "Otra") }));
            var vm = AbrirVm(N(1, "1.10.30.0"));

            await vm.IrAResultado(S(51, "Otra"));

            Assert.IsTrue(vm.EnSugerencias);
            Assert.AreEqual(51, vm.NovedadDestacada.Id);
            Assert.IsTrue(vm.Novedades.Contains(vm.NovedadDestacada));
        }

        [TestMethod]
        public async Task ElegirResultado_SugerenciaCerrada_SeEnseñaIgualParaPoderComentarla()
        {
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario> { S(50, "Una") }));
            var vm = AbrirVm(N(1, "1.10.30.0"));

            await vm.IrAResultado(S(70, "Descartada"));

            Assert.AreEqual(2, vm.Novedades.Count);
            Assert.AreEqual(70, vm.NovedadDestacada.Id);
            Assert.IsTrue(vm.NovedadDestacada.TieneFeedback);
        }

        [TestMethod]
        public async Task ElegirOtroResultado_QuitaElResaltadoAlAnterior()
        {
            var vm = AbrirVm(N(1, "1.10.30.0"), N(2, "1.10.29.0"));
            await vm.IrAResultado(N(1, "1.10.30.0"));
            NovedadItem primera = vm.NovedadDestacada;

            await vm.IrAResultado(N(2, "1.10.29.0"));

            Assert.IsFalse(primera.Destacada);
            Assert.AreEqual(2, vm.NovedadDestacada.Id);
        }
    }
}
