using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#497 (NestoAPI#545): borrar un vídeo duplicado desde la ventana Vídeos, con confirmación,
    /// y marcar en la lista los duplicados (mismo VideoId de YouTube).
    /// </summary>
    [TestClass]
    public class VideosViewModelBorrarTests
    {
        private IProductoService _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private IConfiguracion _configuracion = null!;
        private VideosViewModel _sut = null!;

        [TestInitialize]
        public void Setup()
        {
            _servicio = A.Fake<IProductoService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => _servicio.CargarVideos(A<int>._, A<int>._)).Returns(Task.FromResult(new List<VideoLookupModel>()));
            _sut = new VideosViewModel(_servicio, _dialogos, _configuracion, A.Fake<IServicioNavegacion>());
        }

        private void ConPermiso()
        {
            A.CallTo(() => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE)).Returns(true);
        }

        // ShowConfirmationAsync devuelve true solo si el usuario pulsa Aceptar.
        private void ConfirmacionResponde(ResultadoBoton respuesta)
        {
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._))
                .Returns(Task.FromResult(respuesta == ResultadoBoton.OK));
        }

        private static VideoModel Video1981()
        {
            return new VideoModel
            {
                Id = 1981,
                VideoId = "abc123",
                Titulo = "Protocolo facial",
                Productos = [new ProductoVideoModel { Id = 10 }, new ProductoVideoModel { Id = 11 }]
            };
        }

        [TestMethod]
        public void PuedeBorrarVideos_SinGrupo_NoVeElBoton()
        {
            _sut.VideoCompletoSeleccionado = Video1981();

            Assert.IsFalse(_sut.PuedeBorrarVideos);
            Assert.IsFalse(_sut.BorrarVideoCommand.CanExecute(null));
        }

        [TestMethod]
        public void PuedeBorrarVideos_TiendaOnlineDireccionEInformatica()
        {
            foreach (string grupo in new[] { Constantes.GruposSeguridad.TIENDA_ON_LINE, Constantes.GruposSeguridad.DIRECCION, Constantes.GruposSeguridad.INFORMATICA })
            {
                IConfiguracion configuracion = A.Fake<IConfiguracion>();
                A.CallTo(() => configuracion.UsuarioEnGrupo(grupo)).Returns(true);
                var sut = new VideosViewModel(_servicio, _dialogos, configuracion, A.Fake<IServicioNavegacion>());
                Assert.IsTrue(sut.PuedeBorrarVideos, grupo);
            }
        }

        [TestMethod]
        public async Task BorrarVideo_ConfirmaQueSi_BorraYRecargaLaLista()
        {
            ConPermiso();
            ConfirmacionResponde(ResultadoBoton.OK);
            _sut.VideoCompletoSeleccionado = Video1981();

            Assert.IsTrue(_sut.BorrarVideoCommand.CanExecute(null));
            await _sut.BorrarVideoCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.BorrarVideo(1981)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CargarVideos(0, A<int>._)).MustHaveHappenedOnceExactly();
            Assert.IsNull(_sut.VideoCompletoSeleccionado);
        }

        [TestMethod]
        public async Task BorrarVideo_ConfirmaQueNo_NoBorra()
        {
            ConPermiso();
            ConfirmacionResponde(ResultadoBoton.Cancel);
            _sut.VideoCompletoSeleccionado = Video1981();

            await _sut.BorrarVideoCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.BorrarVideo(A<int>._)).MustNotHaveHappened();
            Assert.IsNotNull(_sut.VideoCompletoSeleccionado);
        }

        [TestMethod]
        public async Task BorrarVideo_LaApiDiceQueNoEsDuplicado_EnsenaSuMensajeYNoRecarga()
        {
            ConPermiso();
            ConfirmacionResponde(ResultadoBoton.OK);
            const string motivo = "Este vídeo no está duplicado. Para retirar un vídeo usa la baja, no el borrado.";
            A.CallTo(() => _servicio.BorrarVideo(1981)).ThrowsAsync(new Exception(motivo));
            _sut.VideoCompletoSeleccionado = Video1981();

            await _sut.BorrarVideoCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowError(motivo)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CargarVideos(A<int>._, A<int>._)).MustNotHaveHappened();
            Assert.IsFalse(_sut.EstaBorrando);
        }

        [TestMethod]
        public void TextoConfirmacionBorrado_DiceTituloVideoIdYProductos()
        {
            Assert.AreEqual(
                "¿Seguro que quieres borrar el vídeo \"Protocolo facial\" (YouTube abc123) y sus 2 productos? No se puede deshacer.",
                VideosViewModel.TextoConfirmacionBorrado(Video1981()));
        }

        [TestMethod]
        public async Task CargarVideos_MarcaLosQueCompartenVideoId()
        {
            A.CallTo(() => _servicio.CargarVideos(A<int>._, A<int>._)).Returns(Task.FromResult(new List<VideoLookupModel>
            {
                new() { Id = 1980, VideoId = "abc123" },
                new() { Id = 1981, VideoId = "abc123" },
                new() { Id = 1990, VideoId = "otro" },
                new() { Id = 1, VideoId = null },
                new() { Id = 2, VideoId = "" }
            }));

            await _sut.CargarVideosIniciales();

            CollectionAssert.AreEquivalent(new[] { 1980, 1981 }, _sut.Videos.Where(v => v.EsDuplicado).Select(v => v.Id).ToList());
        }

        [TestMethod]
        public void MarcarDuplicados_TrasBorrarUnoDeLosDos_ElOtroDejaDeEstarMarcado()
        {
            var queda = new VideoLookupModel { Id = 1980, VideoId = "abc123", EsDuplicado = true };

            VideosViewModel.MarcarDuplicados([queda, new VideoLookupModel { Id = 1990, VideoId = "otro" }]);

            Assert.IsFalse(queda.EsDuplicado);
        }

        // ---- Carlos 28/09/26: dar de baja desde la ventana Vídeos ----

        [TestMethod]
        public async Task DarDeBaja_ConfirmaQueSi_LlamaALaApiYRecarga()
        {
            ConPermiso();
            ConfirmacionResponde(ResultadoBoton.OK);
            _sut.VideoCompletoSeleccionado = Video1981();

            Assert.IsTrue(_sut.DarDeBajaVideoCommand.CanExecute(null));
            await _sut.DarDeBajaVideoCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.DarDeBajaVideo(1981)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.BorrarVideo(A<int>._)).MustNotHaveHappened();
            A.CallTo(() => _servicio.CargarVideos(0, A<int>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task DarDeBaja_ConfirmaQueNo_NoHaceNada()
        {
            ConPermiso();
            ConfirmacionResponde(ResultadoBoton.Cancel);
            _sut.VideoCompletoSeleccionado = Video1981();

            await _sut.DarDeBajaVideoCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.DarDeBajaVideo(A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void DarDeBaja_SinGrupo_NoSePuede()
        {
            _sut.VideoCompletoSeleccionado = Video1981();

            Assert.IsFalse(_sut.DarDeBajaVideoCommand.CanExecute(null));
        }
    }
}
