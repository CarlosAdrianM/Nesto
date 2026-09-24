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
    /// Nesto#477: desde la campana (te han contestado en Novedades) la ventana se abre en la novedad, con sus
    /// comentarios desplegados y el comentario de la notificación resaltado.
    /// </summary>
    [TestClass]
    public class NovedadesIrAComentarioTests
    {
        private INovedadesService servicio;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<INovedadesService>();
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario>()));
            A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).Returns(Task.FromResult(new List<NovedadUsuario>()));
            A.CallTo(() => servicio.LeerComentarios(A<int>._)).Returns(Task.FromResult(new List<ComentarioNovedad>
            {
                new ComentarioNovedad { Id = 100, Texto = "Mi pregunta", NombreVisible = "Alfredo" },
                new ComentarioNovedad { Id = 101, Texto = "La respuesta", NombreVisible = "Claude (asistente IA)" }
            }));
        }

        private static NovedadUsuario N(int id, string version)
            => new NovedadUsuario { Id = id, Version = version, Titulo = "x", Categoria = "Nuevo", VotosPositivos = 0, VotosNegativos = 0, NumeroComentarios = 2 };

        private NovedadesDialogViewModel Vm(params NovedadUsuario[] novedades)
        {
            var vm = new NovedadesDialogViewModel(servicio, null, _ => false);
            var parametros = new DialogParameters();
            parametros.Add("novedades", novedades.ToList());
            vm.OnDialogOpened(parametros);
            return vm;
        }

        [TestMethod]
        public async Task IrANovedadComentario_Cargada_SaltaASuVersionAbreComentariosYResaltaElComentario()
        {
            var vm = Vm(N(1, "1.10.31.0"), N(2, "1.10.30.0"));

            await vm.IrANovedadComentario(2, 101);

            Assert.AreEqual("Versión 1.10.30.0", vm.VersionActual);
            NovedadItem novedad = vm.NovedadDestacada;
            Assert.AreEqual(2, novedad.Id);
            Assert.IsTrue(novedad.ComentariosAbiertos);
            Assert.AreEqual(101, novedad.ComentarioDestacado.Id);
            Assert.IsTrue(novedad.ComentarioDestacado.Destacado);
            Assert.IsFalse(novedad.Comentarios.Single(c => c.Id == 100).Destacado);
            A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task IrANovedadComentario_NoCargada_PideTodasLasNovedades()
        {
            A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).Returns(Task.FromResult(new List<NovedadUsuario> { N(1, "1.10.31.0"), N(9, "1.9.0.0") }));
            var vm = Vm();

            await vm.IrANovedadComentario(9, 101);

            Assert.AreEqual("Versión 1.9.0.0", vm.VersionActual);
            Assert.AreEqual(9, vm.NovedadDestacada.Id);
            Assert.AreEqual(101, vm.NovedadDestacada.ComentarioDestacado.Id);
        }

        [TestMethod]
        public async Task IrANovedadComentario_SinVersion_EsUnaSugerencia_VaASugerencias()
        {
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario> { N(50, null), N(51, null) }));
            var vm = Vm(N(1, "1.10.31.0"));

            await vm.IrANovedadComentario(51, 101);

            Assert.IsTrue(vm.EnSugerencias);
            Assert.AreEqual(51, vm.NovedadDestacada.Id);
            Assert.IsTrue(vm.NovedadDestacada.ComentariosAbiertos);
            Assert.AreEqual(101, vm.NovedadDestacada.ComentarioDestacado.Id);
        }

        [TestMethod]
        public async Task IrANovedadComentario_QueNoExiste_LoDiceSinRomper()
        {
            var vm = Vm(N(1, "1.10.31.0"));

            await vm.IrANovedadComentario(999, 101);

            Assert.AreEqual(NovedadesDialogViewModel.MENSAJE_NOVEDAD_NO_ENCONTRADA, vm.MensajeSugerencias);
            A.CallTo(() => servicio.LeerComentarios(A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task IrANovedadComentario_ComentarioBorrado_AbreLosComentariosSinResaltarNinguno()
        {
            var vm = Vm(N(1, "1.10.31.0"));

            await vm.IrANovedadComentario(1, 555);

            Assert.IsTrue(vm.NovedadDestacada.ComentariosAbiertos);
            Assert.IsNull(vm.NovedadDestacada.ComentarioDestacado);
            Assert.IsFalse(vm.NovedadDestacada.Comentarios.Any(c => c.Destacado));
        }

        [TestMethod]
        public void OnDialogOpened_ConNovedadIdYComentarioId_SaltaSolo()
        {
            var vm = new NovedadesDialogViewModel(servicio, null, _ => false);
            var parametros = new DialogParameters();
            parametros.Add(NovedadesDialogViewModel.PARAMETRO_NOVEDAD_ID, 1);
            parametros.Add(NovedadesDialogViewModel.PARAMETRO_COMENTARIO_ID, 101);
            A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).Returns(Task.FromResult(new List<NovedadUsuario> { N(1, "1.10.31.0") }));

            vm.OnDialogOpened(parametros);

            // Con los fakes todo completa en síncrono.
            Assert.AreEqual(1, vm.NovedadDestacada?.Id);
            Assert.AreEqual(101, vm.NovedadDestacada.ComentarioDestacado?.Id);
        }
    }
}
