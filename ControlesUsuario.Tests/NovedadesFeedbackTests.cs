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
    /// NestoAPI#520: votos y comentarios en la ventana de Novedades. El feedback nunca rompe la ventana:
    /// sin contadores de la API se ve como siempre, y los fallos se cuentan al usuario sin cerrarla.
    /// </summary>
    [TestClass]
    public class NovedadesFeedbackTests
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
            A.CallTo(() => servicio.LeerComentarios(A<int>._)).Returns(Task.FromResult(new List<ComentarioNovedad>()));
        }

        private static NovedadUsuario ConFeedback(int positivos = 0, int negativos = 0, short? miVoto = null, int comentarios = 0)
            => new NovedadUsuario { Id = 7, Version = "1.10.29.1", Titulo = "Algo", Categoria = "Nuevo",
                VotosPositivos = positivos, VotosNegativos = negativos, MiVoto = miVoto, NumeroComentarios = comentarios };

        private NovedadItem Item(NovedadUsuario n) => AbrirVm(n).Novedades.Single();

        private NovedadesDialogViewModel AbrirVm(params NovedadUsuario[] novedades)
        {
            var vm = new NovedadesDialogViewModel(servicio, portapapeles, p => { preguntas.Add(p); return respuesta; });
            var parametros = new DialogParameters();
            parametros.Add("novedades", novedades.ToList());
            vm.OnDialogOpened(parametros);
            return vm;
        }

        // ---- Sin feedback: la ventana como siempre ----

        [TestMethod]
        public void SinContadoresDeLaApi_NoHayFeedback()
        {
            var item = Item(new NovedadUsuario { Id = 1, Version = "1.10.29.0", Titulo = "x" });

            Assert.IsFalse(item.TieneFeedback, "Tablas aún no creadas en prod: la ventana se ve como hoy");
        }

        [TestMethod]
        public void SinServicio_NoHayFeedbackAunqueVenganContadores()
        {
            var vm = new NovedadesDialogViewModel();
            var parametros = new DialogParameters();
            parametros.Add("novedades", new List<NovedadUsuario> { ConFeedback(3) });
            vm.OnDialogOpened(parametros);

            Assert.IsFalse(vm.Novedades.Single().TieneFeedback);
        }

        // ---- Votos ----

        [TestMethod]
        public async Task Votar_SinVotoPrevio_SumaYLlamaALaApi()
        {
            var item = Item(ConFeedback(positivos: 2));

            await item.Votar(1);

            Assert.AreEqual(3, item.VotosPositivos);
            Assert.IsTrue(item.EsMeGusta);
            A.CallTo(() => servicio.VotarNovedad(7, 1)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Votar_ElMismoVoto_LoQuita()
        {
            var item = Item(ConFeedback(positivos: 3, miVoto: 1));

            await item.Votar(1);

            Assert.AreEqual(2, item.VotosPositivos);
            Assert.IsNull(item.MiVoto);
            A.CallTo(() => servicio.VotarNovedad(7, 0)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Votar_ElContrario_LoCambia()
        {
            var item = Item(ConFeedback(positivos: 3, negativos: 1, miVoto: 1));

            await item.Votar(-1);

            Assert.AreEqual(2, item.VotosPositivos);
            Assert.AreEqual(2, item.VotosNegativos);
            Assert.IsTrue(item.EsNoMeGusta);
            A.CallTo(() => servicio.VotarNovedad(7, -1)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Votar_SiLaApiFalla_SeDeshaceYSeAvisa()
        {
            A.CallTo(() => servicio.VotarNovedad(A<int>._, A<short>._)).Throws(new InvalidOperationException("No se pudo guardar el voto: sin conexión"));
            var item = Item(ConFeedback(positivos: 3, miVoto: null));

            await item.Votar(1);

            Assert.AreEqual(3, item.VotosPositivos, "El voto optimista se revierte");
            Assert.IsNull(item.MiVoto);
            StringAssert.Contains(item.Mensaje, "sin conexión");
        }

        // ---- Comentarios ----

        [TestMethod]
        public async Task AbrirComentarios_ConImagenCopiada_PreguntaUnaVezYLaAdjunta()
        {
            A.CallTo(() => portapapeles.HayImagen()).Returns(true);
            A.CallTo(() => portapapeles.LeerImagenPng()).Returns(new byte[] { 1, 2, 3 });
            var item = Item(ConFeedback());

            await item.AbrirOCerrarComentarios();

            Assert.AreEqual(1, preguntas.Count);
            Assert.IsTrue(item.TieneImagenAdjunta);
        }

        [TestMethod]
        public async Task AbrirComentarios_SiNoQuiereLaImagen_NoSeAdjunta()
        {
            respuesta = false;
            A.CallTo(() => portapapeles.HayImagen()).Returns(true);
            var item = Item(ConFeedback());

            await item.AbrirOCerrarComentarios();

            Assert.IsFalse(item.TieneImagenAdjunta);
            A.CallTo(() => portapapeles.LeerImagenPng()).MustNotHaveHappened();
        }

        [TestMethod]
        public void PegarImagen_MayorDe2MB_SeBloqueaAntesDeEnviar()
        {
            A.CallTo(() => portapapeles.HayImagen()).Returns(true);
            A.CallTo(() => portapapeles.LeerImagenPng()).Returns(new byte[NovedadItem.TAMANO_MAXIMO_IMAGEN + 1]);
            var item = Item(ConFeedback());

            bool habiaImagen = item.PegarImagen();

            Assert.IsTrue(habiaImagen, "Ctrl+V se «come» la tecla aunque la imagen no se adjunte");
            Assert.IsFalse(item.TieneImagenAdjunta);
            StringAssert.Contains(item.Mensaje, "2 MB");
        }

        [TestMethod]
        public void PegarImagen_SinImagenCopiada_DejaPasarElTexto()
        {
            A.CallTo(() => portapapeles.HayImagen()).Returns(false);
            var item = Item(ConFeedback());

            Assert.IsFalse(item.PegarImagen(), "Si lo copiado es texto, Ctrl+V pega el texto");
        }

        [TestMethod]
        public async Task Enviar_PublicaConLaImagenYLimpiaElCuadro()
        {
            var png = new byte[] { 9, 9 };
            A.CallTo(() => portapapeles.HayImagen()).Returns(true);
            A.CallTo(() => portapapeles.LeerImagenPng()).Returns(png);
            A.CallTo(() => servicio.Comentar(7, "Va genial", png))
                .Returns(Task.FromResult(new ComentarioNovedad { Id = 50, NovedadId = 7, NombreVisible = "Carlos", Texto = "Va genial", TieneImagen = true, EsMio = true }));
            var item = Item(ConFeedback(comentarios: 2));
            item.PegarImagen();
            item.NuevoTexto = "  Va genial ";

            await item.EnviarComentario();

            Assert.AreEqual(1, item.Comentarios.Count);
            Assert.AreEqual(3, item.NumeroComentarios);
            Assert.IsNull(item.NuevoTexto);
            Assert.IsFalse(item.TieneImagenAdjunta);
        }

        [TestMethod]
        public async Task Enviar_SiLaApiRechaza_SeMuestraSuMensajeYNoSePierdeElTexto()
        {
            A.CallTo(() => servicio.Comentar(A<int>._, A<string>._, A<byte[]>._))
                .Throws(new InvalidOperationException("No se pudo publicar el comentario: La imagen supera 2 MB"));
            var item = Item(ConFeedback());
            item.NuevoTexto = "Hola";

            await item.EnviarComentario();

            StringAssert.Contains(item.Mensaje, "supera 2 MB");
            Assert.AreEqual("Hola", item.NuevoTexto);
        }

        [TestMethod]
        public async Task Borrar_SoloLosPropios()
        {
            A.CallTo(() => servicio.LeerComentarios(7)).Returns(Task.FromResult(new List<ComentarioNovedad>
            {
                new ComentarioNovedad { Id = 1, Texto = "mío", EsMio = true },
                new ComentarioNovedad { Id = 2, Texto = "de otro", EsMio = false }
            }));
            var item = Item(ConFeedback(comentarios: 2));
            await item.AbrirOCerrarComentarios();

            await item.BorrarComentario(item.Comentarios.Single(c => c.Id == 2));
            await item.BorrarComentario(item.Comentarios.Single(c => c.Id == 1));

            A.CallTo(() => servicio.BorrarComentario(2)).MustNotHaveHappened();
            A.CallTo(() => servicio.BorrarComentario(1)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, item.Comentarios.Count);
            Assert.AreEqual(1, item.NumeroComentarios);
        }

        [TestMethod]
        public async Task AbrirComentarios_CargaLasMiniaturasSoloDeLosQueTienenImagen()
        {
            A.CallTo(() => servicio.LeerComentarios(7)).Returns(Task.FromResult(new List<ComentarioNovedad>
            {
                new ComentarioNovedad { Id = 1, TieneImagen = true },
                new ComentarioNovedad { Id = 2, TieneImagen = false }
            }));
            A.CallTo(() => servicio.LeerImagenComentario(1)).Returns(Task.FromResult(new byte[] { 5 }));
            var item = Item(ConFeedback(comentarios: 2));

            await item.AbrirOCerrarComentarios();

            A.CallTo(() => servicio.LeerImagenComentario(1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.LeerImagenComentario(2)).MustNotHaveHappened();
            Assert.IsNotNull(item.Comentarios.Single(c => c.Id == 1).Imagen);
        }

        [TestMethod]
        public async Task AbrirComentarios_SiFallaLaCarga_SeAvisaSinRomper()
        {
            A.CallTo(() => servicio.LeerComentarios(7)).Throws(new InvalidOperationException("No se pudo leer los comentarios (error 500)."));
            var item = Item(ConFeedback());

            await item.AbrirOCerrarComentarios();

            Assert.IsTrue(item.ComentariosAbiertos);
            StringAssert.Contains(item.Mensaje, "error 500");
        }
    }
}
