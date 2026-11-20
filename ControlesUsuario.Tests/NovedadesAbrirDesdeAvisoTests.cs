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
    /// Campana (20/11/26, «tarda mucho al abrir desde una notificación»): al abrir Novedades desde un aviso se pide primero
    /// esa novedad por su id y se salta a ella sin esperar a las listas ni a ninguna captura; lo demás llega después.
    /// </summary>
    [TestClass]
    public class NovedadesAbrirDesdeAvisoTests
    {
        private INovedadesService servicio;
        private TaskCompletionSource<List<NovedadUsuario>> novedades;
        private TaskCompletionSource<List<NovedadUsuario>> sugerencias;
        private TaskCompletionSource<byte[]> capturas;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<INovedadesService>();
            novedades = new TaskCompletionSource<List<NovedadUsuario>>();
            sugerencias = new TaskCompletionSource<List<NovedadUsuario>>();
            capturas = new TaskCompletionSource<byte[]>();
            // Listas y capturas que no terminan hasta que el test quiera
            A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).ReturnsLazily(() => novedades.Task);
            A.CallTo(() => servicio.ObtenerTodasLasNovedades()).ReturnsLazily(() => novedades.Task);
            A.CallTo(() => servicio.LeerSugerencias()).ReturnsLazily(() => sugerencias.Task);
            A.CallTo(() => servicio.LeerCaptura(A<int>._)).ReturnsLazily(() => capturas.Task);
            A.CallTo(() => servicio.LeerImagenNovedad(A<int>._)).ReturnsLazily(() => capturas.Task);
            A.CallTo(() => servicio.LeerImagenComentario(A<int>._)).ReturnsLazily(() => capturas.Task);
            A.CallTo(() => servicio.LeerMisPerfiles()).Returns(Task.FromResult<PerfilesUsuarioNovedades>(null));
            A.CallTo(() => servicio.LeerComentarios(A<int>._)).ReturnsLazily(() => Task.FromResult(new List<ComentarioNovedad>
            {
                new ComentarioNovedad { Id = 100, Texto = "Mi pregunta", NombreVisible = "Alfredo", TieneImagen = true },
                new ComentarioNovedad { Id = 101, Texto = "La respuesta", NombreVisible = "Carlos", TieneImagen = true }
            }));
        }

        private static NovedadUsuario N(int id, string version, bool tieneImagen = false)
            => new NovedadUsuario
            {
                Id = id, Version = version, Titulo = "x" + id, Categoria = "Nuevo",
                VotosPositivos = 0, VotosNegativos = 0, NumeroComentarios = 2, TieneImagen = tieneImagen
            };

        private NovedadesDialogViewModel Vm() => new NovedadesDialogViewModel(servicio, null, _ => false);

        private static async Task Terminada(Task tarea)
        {
            Task primera = await Task.WhenAny(tarea, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.AreSame(tarea, primera, "Ha esperado a algo que no debía");
            await tarea;
        }

        private void LeerPorId(NovedadUsuario novedad)
            => A.CallTo(() => servicio.LeerPorId(novedad.Id)).Returns(Task.FromResult(novedad));

        [TestMethod]
        public async Task Sugerencia_SaltaSinEsperarListasNiCapturas_ConElComentarioResaltado()
        {
            LeerPorId(N(51, null, tieneImagen: true));
            NovedadesDialogViewModel vm = Vm();

            await Terminada(vm.IrANovedadComentario(51, 101));

            Assert.IsTrue(vm.EnSugerencias);
            Assert.AreEqual(51, vm.NovedadDestacada.Id);
            Assert.IsTrue(vm.NovedadDestacada.Destacada);
            Assert.IsTrue(vm.Novedades.Contains(vm.NovedadDestacada));
            Assert.IsTrue(vm.NovedadDestacada.ComentariosAbiertos);
            Assert.AreEqual(101, vm.NovedadDestacada.ComentarioDestacado.Id);
            Assert.IsTrue(vm.NovedadDestacada.ComentarioDestacado.Destacado);
            Assert.IsFalse(vm.CargaEnSegundoPlano.IsCompleted, "Las listas siguen llegando en segundo plano");
            A.CallTo(() => servicio.LeerSugerencias()).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.LeerDeOtraAplicacion(A<int>._)).MustNotHaveHappened();
            // Su captura y las de los comentarios ya se han pedido (las primeras)
            A.CallTo(() => servicio.LeerImagenNovedad(51)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.LeerImagenComentario(101)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Incidencia_VaALaListaDeAvisos()
        {
            NovedadUsuario incidencia = N(60, null);
            incidencia.EsIncidencia = true;
            LeerPorId(incidencia);
            NovedadesDialogViewModel vm = Vm();

            await Terminada(vm.IrANovedadComentario(60, null));

            Assert.IsTrue(vm.ListaDeIncidencias);
            Assert.AreEqual(NovedadesDialogViewModel.TITULO_INCIDENCIAS, vm.VersionActual);
            Assert.AreEqual(60, vm.NovedadDestacada.Id);
            Assert.IsTrue(vm.NovedadDestacada.ComentariosAbiertos);
        }

        [TestMethod]
        public async Task NovedadConVersion_SaltaASuVersionSinEsperarListas()
        {
            LeerPorId(N(9, "1.9.0.0"));
            NovedadesDialogViewModel vm = Vm();

            await Terminada(vm.IrANovedadComentario(9, 101));

            Assert.IsFalse(vm.EnSugerencias);
            Assert.AreEqual("Versión 1.9.0.0", vm.VersionActual);
            Assert.AreEqual(9, vm.NovedadDestacada.Id);
            Assert.IsTrue(vm.Novedades.Contains(vm.NovedadDestacada));
            Assert.AreEqual(101, vm.NovedadDestacada.ComentarioDestacado.Id);
        }

        [TestMethod]
        public async Task DeOtraAplicacion_LlevaSuEtiquetaSinBuscarEnSusListas()
        {
            NovedadUsuario deAriadna = N(1053, null);
            deAriadna.Ambito = AmbitosNovedades.ARIADNA;
            LeerPorId(deAriadna);
            NovedadesDialogViewModel vm = Vm();

            await Terminada(vm.IrANovedadComentario(1053, 101));

            Assert.AreEqual("Sugerencia de Ariadna", vm.NovedadDestacada.EtiquetaAplicacion);
            Assert.AreEqual(101, vm.NovedadDestacada.ComentarioDestacado.Id);
            A.CallTo(() => servicio.LeerDeOtraAplicacion(A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task NoEncontradaPorId_BuscaEnLasListasComoAntesYLoDice()
        {
            A.CallTo(() => servicio.LeerPorId(999)).Returns(Task.FromResult<NovedadUsuario>(null));
            A.CallTo(() => servicio.LeerDeOtraAplicacion(999)).Returns(Task.FromResult<NovedadUsuario>(null));
            novedades.SetResult(new List<NovedadUsuario> { N(1, "1.10.31.0") });
            sugerencias.SetResult(new List<NovedadUsuario>());
            NovedadesDialogViewModel vm = Vm();

            await Terminada(vm.IrANovedadComentario(999, 101));

            Assert.AreEqual(NovedadesDialogViewModel.MENSAJE_NOVEDAD_NO_ENCONTRADA, vm.MensajeSugerencias);
            A.CallTo(() => servicio.LeerDeOtraAplicacion(999)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.LeerComentarios(A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ApiAnterior_SinGetPorId_SeEncuentraEnLasListas()
        {
            A.CallTo(() => servicio.LeerPorId(A<int>._)).Returns(Task.FromResult<NovedadUsuario>(null));
            novedades.SetResult(new List<NovedadUsuario>());
            sugerencias.SetResult(new List<NovedadUsuario> { N(50, null), N(51, null) });
            capturas.SetResult(new byte[] { 1 });
            NovedadesDialogViewModel vm = Vm();

            await Terminada(vm.IrANovedadComentario(51, 101));

            Assert.IsTrue(vm.EnSugerencias);
            Assert.AreEqual(51, vm.NovedadDestacada.Id);
            Assert.AreEqual(101, vm.NovedadDestacada.ComentarioDestacado.Id);
        }

        [TestMethod]
        public async Task Sugerencia_CuandoTodoTermina_EnsenaLoMismoQueAntesConLaMismaAbierta()
        {
            LeerPorId(N(51, null, tieneImagen: true));
            NovedadesDialogViewModel vm = Vm();
            await Terminada(vm.IrANovedadComentario(51, 101));
            NovedadItem abierta = vm.NovedadDestacada;

            novedades.SetResult(new List<NovedadUsuario> { N(1, "1.10.31.0"), N(2, "1.10.30.0") });
            sugerencias.SetResult(new List<NovedadUsuario> { N(50, null, tieneImagen: true), N(51, null, tieneImagen: true) });
            capturas.SetResult(new byte[] { 7 });
            await Terminada(vm.CargaEnSegundoPlano);

            Assert.IsTrue(vm.EnSugerencias);
            Assert.AreSame(abierta, vm.NovedadDestacada);
            CollectionAssert.AreEquivalent(new[] { 50, 51 }, vm.Novedades.Select(n => n.Id).ToList());
            Assert.AreSame(abierta, vm.Novedades.Single(n => n.Id == 51), "La abierta no se sustituye por la de la lista");
            Assert.IsTrue(abierta.ComentariosAbiertos);
            Assert.AreEqual(101, abierta.ComentarioDestacado.Id);
            Assert.IsTrue(abierta.ImagenesNovedad.All(i => i.Imagen != null));
            Assert.IsTrue(abierta.Comentarios.All(c => c.Imagenes.All(i => i.Imagen != null)));
            Assert.IsTrue(vm.Novedades.Single(n => n.Id == 50).ImagenesNovedad.Single().Imagen != null);
            // Ya se puede ir a las versiones
            Assert.IsTrue(vm.VersionAnteriorCommand.CanExecute(null));
            A.CallTo(() => servicio.LeerImagenNovedad(51)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Novedad_CuandoTodoTermina_SeQuedaEnSuVersionYSePuedeNavegar()
        {
            LeerPorId(N(2, "1.10.30.0"));
            NovedadesDialogViewModel vm = Vm();
            await Terminada(vm.IrANovedadComentario(2, 101));
            NovedadItem abierta = vm.NovedadDestacada;

            novedades.SetResult(new List<NovedadUsuario> { N(1, "1.10.31.0"), N(2, "1.10.30.0"), N(3, "1.10.29.0") });
            sugerencias.SetResult(new List<NovedadUsuario>());
            capturas.SetResult(new byte[] { 7 });
            await Terminada(vm.CargaEnSegundoPlano);

            Assert.AreEqual("Versión 1.10.30.0", vm.VersionActual);
            Assert.AreSame(abierta, vm.NovedadDestacada);
            Assert.AreSame(abierta, vm.Novedades.Single());
            Assert.IsTrue(vm.VersionAnteriorCommand.CanExecute(null));
            Assert.IsTrue(vm.VersionSiguienteCommand.CanExecute(null));
            Assert.AreEqual(101, abierta.ComentarioDestacado.Id);
        }

        [TestMethod]
        public async Task SiLasListasFallan_LaAbiertaSigueYNadaRevienta()
        {
            LeerPorId(N(51, null));
            NovedadesDialogViewModel vm = Vm();
            await Terminada(vm.IrANovedadComentario(51, 101));

            novedades.SetException(new InvalidOperationException("caída"));
            sugerencias.SetException(new InvalidOperationException("No se pudo leer las sugerencias"));
            capturas.SetException(new InvalidOperationException("sin imagen"));
            await Terminada(vm.CargaEnSegundoPlano);

            Assert.AreEqual(51, vm.NovedadDestacada.Id);
            Assert.IsTrue(vm.Novedades.Contains(vm.NovedadDestacada));
            Assert.AreEqual(101, vm.NovedadDestacada.ComentarioDestacado.Id);
            Assert.IsTrue(vm.NovedadDestacada.Comentarios.All(c => c.Imagenes.All(i => i.NoDisponible)));
        }

        [TestMethod]
        public async Task IrASugerencias_NoEsperaALasCapturas()
        {
            sugerencias.SetResult(new List<NovedadUsuario> { N(50, null, tieneImagen: true) });
            NovedadesDialogViewModel vm = Vm();

            await Terminada(vm.IrASugerencias());

            Assert.AreEqual(50, vm.Novedades.Single().Id);
            Assert.IsFalse(vm.CapturasSugerencias.IsCompleted);
            capturas.SetResult(new byte[] { 3 });
            await Terminada(vm.CapturasSugerencias);
            Assert.IsNotNull(vm.Novedades.Single().ImagenesNovedad.Single().Imagen);
        }

        [TestMethod]
        public async Task AbrirComentarios_NoEsperaALasCapturasDeLosComentarios()
        {
            var item = new NovedadItem(N(1, "1.10.31.0"), servicio, null, _ => false);

            await Terminada(item.AbrirComentarios(101));

            Assert.AreEqual(2, item.Comentarios.Count);
            Assert.AreEqual(101, item.ComentarioDestacado.Id);
            Assert.IsFalse(item.CapturasComentarios.IsCompleted);
            capturas.SetResult(new byte[] { 3 });
            await Terminada(item.CapturasComentarios);
            Assert.IsTrue(item.Comentarios.All(c => c.Imagenes.Single().Imagen != null));
        }

        [TestMethod]
        public async Task SinAviso_AbrirNovedadesNoPidePorId()
        {
            NovedadesDialogViewModel vm = Vm();
            var parametros = new ParametrosDialogo();
            parametros.Add("novedades", new List<NovedadUsuario> { N(1, "1.10.31.0") });

            vm.OnDialogOpened(parametros);
            await Task.Yield();

            Assert.AreEqual("Versión 1.10.31.0", vm.VersionActual);
            A.CallTo(() => servicio.LeerPorId(A<int>._)).MustNotHaveHappened();
        }
    }
}
