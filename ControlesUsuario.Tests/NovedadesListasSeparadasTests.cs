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
    /// Novedades (08/10/26): «Sugerir una mejora» y «Algo no funciona» ya no enseñan la misma lista. Sin elegir
    /// ninguno se ve la versión; por «Sugerir una mejora», solo sugerencias; por «Algo no funciona», solo avisos.
    /// </summary>
    [TestClass]
    public class NovedadesListasSeparadasTests
    {
        private INovedadesService servicio;
        private IPortapapelesImagenes portapapeles;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<INovedadesService>();
            portapapeles = A.Fake<IPortapapelesImagenes>();
            A.CallTo(() => portapapeles.HayImagen()).Returns(false);
            A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).Returns(Task.FromResult(new List<NovedadUsuario>()));
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario>
            {
                S(50, "Un botón para duplicar"), I(60, "Se queda colgado"), S(51, "Otra idea")
            }));
        }

        private static NovedadUsuario N(int id, string version)
            => new NovedadUsuario { Id = id, Version = version, Titulo = "x", Categoria = "Nuevo" };

        private static NovedadUsuario S(int id, string texto)
            => new NovedadUsuario { Id = id, Version = null, Titulo = texto, TextoOriginal = texto, Categoria = "Nuevo", Estado = "Pendiente",
                SugeridaNombre = "Carlos", SugeridaFecha = new DateTime(2026, 9, 24) };

        private static NovedadUsuario I(int id, string texto)
            => new NovedadUsuario { Id = id, Version = null, Titulo = texto, TextoOriginal = texto, Categoria = "Incidencia", EsIncidencia = true,
                Estado = "Pendiente", SugeridaNombre = "Alfredo", SugeridaFecha = new DateTime(2026, 9, 29) };

        private NovedadesDialogViewModel AbrirVm()
        {
            var vm = new NovedadesDialogViewModel(servicio, portapapeles, _ => false);
            var parametros = new DialogParameters();
            parametros.Add("novedades", new List<NovedadUsuario> { N(1, "1.10.38.0") });
            vm.OnDialogOpened(parametros);
            return vm;
        }

        [TestMethod]
        public void SinElegirNinguno_SeVenLasNovedadesDeLaVersion()
        {
            var vm = AbrirVm();

            Assert.IsFalse(vm.EnSugerencias);
            Assert.AreEqual("Versión 1.10.38.0", vm.VersionActual);
            Assert.AreEqual(1, vm.Novedades.Single().Id);
            A.CallTo(() => servicio.LeerSugerencias()).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task SugerirUnaMejora_SoloSugerencias()
        {
            var vm = AbrirVm();

            await vm.AbrirOCerrarSugerencia();

            CollectionAssert.AreEqual(new[] { 50, 51 }, vm.Novedades.Select(n => n.Id).ToArray());
            Assert.AreEqual(NovedadesDialogViewModel.TITULO_SUGERENCIAS, vm.VersionActual);
            Assert.IsFalse(vm.HayMensajeListaVacia);
        }

        [TestMethod]
        public async Task AlgoNoFunciona_SoloIncidencias()
        {
            var vm = AbrirVm();

            await vm.AbrirOCerrarIncidencia();

            Assert.AreEqual(60, vm.Novedades.Single().Id);
            Assert.AreEqual(NovedadesDialogViewModel.TITULO_INCIDENCIAS, vm.VersionActual);
            StringAssert.Contains(vm.TextoCabeceraSugerencias, "no funcionan");
        }

        [TestMethod]
        public async Task ConElCuadroAbierto_CambiarDeBoton_CambiaLaLista()
        {
            var vm = AbrirVm();
            await vm.AbrirOCerrarSugerencia();

            await vm.AbrirOCerrarIncidencia();
            Assert.AreEqual(60, vm.Novedades.Single().Id);

            await vm.AbrirOCerrarSugerencia();
            Assert.AreEqual(2, vm.Novedades.Count);
            Assert.IsTrue(vm.Novedades.All(n => !n.EsIncidencia));
        }

        [TestMethod]
        public async Task ListaDeSugerenciasVacia_MensajeDeSugerencias()
        {
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario> { I(60, "Falla") }));
            var vm = AbrirVm();

            await vm.AbrirOCerrarSugerencia();

            Assert.AreEqual(0, vm.Novedades.Count);
            Assert.AreEqual(NovedadesDialogViewModel.MENSAJE_SIN_SUGERENCIAS, vm.MensajeListaVacia);
            Assert.IsNull(vm.MensajeSugerencias);
        }

        [TestMethod]
        public async Task ListaDeIncidenciasVacia_MensajeDeIncidencias()
        {
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario> { S(50, "Idea") }));
            var vm = AbrirVm();

            await vm.AbrirOCerrarIncidencia();

            Assert.AreEqual(0, vm.Novedades.Count);
            Assert.AreEqual(NovedadesDialogViewModel.MENSAJE_SIN_INCIDENCIAS, vm.MensajeListaVacia);
        }

        [TestMethod]
        public async Task SiLaApiFalla_NoSeDiceQueLaListaEsteVacia()
        {
            A.CallTo(() => servicio.LeerSugerencias()).Throws(new InvalidOperationException("error 500"));
            var vm = AbrirVm();

            await vm.AbrirOCerrarIncidencia();

            Assert.IsFalse(vm.HayMensajeListaVacia);
            Assert.AreEqual("error 500", vm.MensajeSugerencias);
        }

        [TestMethod]
        public async Task EnviarUnaSugerenciaDesdeLaListaDeAvisos_PasaALaDeSugerencias()
        {
            A.CallTo(() => servicio.Sugerir(A<string>._, A<byte[]>._)).Returns(Task.FromResult(S(70, "Nueva")));
            var vm = AbrirVm();
            await vm.AbrirOCerrarIncidencia();
            await vm.AbrirOCerrarSugerencia(); // con el cuadro abierto, solo cambia de modo
            vm.TextoSugerencia = "Nueva";

            await vm.EnviarSugerencia();

            Assert.AreEqual(70, vm.Novedades[0].Id);
            Assert.IsTrue(vm.Novedades.All(n => !n.EsIncidencia));
            Assert.AreEqual(NovedadesDialogViewModel.TITULO_SUGERENCIAS, vm.VersionActual);
        }

        [TestMethod]
        public async Task BuscadorEnUnAviso_LlevaALaListaDeAvisos()
        {
            var vm = AbrirVm();

            await vm.IrAResultado(I(60, "Se queda colgado"));

            Assert.IsTrue(vm.ListaDeIncidencias);
            Assert.AreEqual(60, vm.NovedadDestacada.Id);
            Assert.IsTrue(vm.Novedades.Contains(vm.NovedadDestacada));
            Assert.IsFalse(vm.FormularioSugerenciaAbierto);
        }

        [TestMethod]
        public async Task CampanaEnUnAviso_LlevaALaListaDeAvisos()
        {
            var vm = AbrirVm();

            await vm.IrANovedadComentario(60, null);

            Assert.IsTrue(vm.ListaDeIncidencias);
            Assert.AreEqual(60, vm.NovedadDestacada.Id);
            Assert.IsTrue(vm.Novedades.Contains(vm.NovedadDestacada));
        }

        [TestMethod]
        public async Task LaFlecha_DesdeLaVersionMasNueva_EnseñaLasSugerencias()
        {
            var vm = AbrirVm();

            await vm.IrASugerencias();

            Assert.AreEqual(2, vm.Novedades.Count);
            Assert.IsTrue(vm.Novedades.All(n => !n.EsIncidencia));
        }
    }
}
