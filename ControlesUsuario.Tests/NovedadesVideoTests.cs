using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// NestoAPI#711: el vídeo corto de una novedad (como mucho uno por versión). El botón solo sale si la novedad lo
    /// tiene; NO se descarga nada hasta pulsarlo; se reproduce desde un temporal que se borra al cerrar.
    /// </summary>
    [TestClass]
    public class NovedadesVideoTests
    {
        private INovedadesService servicio;
        private IServicioVideosNovedades servicioVideos;
        private IReproductorVideoNovedad reproductor;
        private string carpeta;
        private List<string> reproducidos;
        private List<bool> existiaAlReproducir;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<INovedadesService>();
            servicioVideos = A.Fake<IServicioVideosNovedades>();
            reproductor = A.Fake<IReproductorVideoNovedad>();
            carpeta = Path.Combine(Path.GetTempPath(), "NestoTestsVideos", Guid.NewGuid().ToString("N"));
            reproducidos = new List<string>();
            existiaAlReproducir = new List<bool>();
            A.CallTo(() => servicioVideos.Descargar(A<int>._)).Returns(Task.FromResult(new byte[] { 0, 0, 0, 24, (byte)'f', (byte)'t', (byte)'y', (byte)'p' }));
            A.CallTo(() => reproductor.Reproducir(A<string>._, A<string>._)).Invokes((string ruta, string titulo) =>
            {
                reproducidos.Add(ruta);
                existiaAlReproducir.Add(File.Exists(ruta));
            });
        }

        [TestCleanup]
        public void Limpiar()
        {
            try { Directory.Delete(carpeta, true); } catch (Exception) { }
        }

        private ContextoVideosNovedades Contexto() => new ContextoVideosNovedades(servicioVideos, reproductor, carpeta);

        private NovedadItem Item(NovedadUsuario n, ContextoVideosNovedades videos)
        {
            var vm = new NovedadesDialogViewModel(servicio, A.Fake<IPortapapelesImagenes>(), _ => false, null, videos);
            var parametros = new ParametrosDialogo();
            parametros.Add("novedades", new List<NovedadUsuario> { n });
            vm.OnDialogOpened(parametros);
            return vm.Novedades.Single();
        }

        private static NovedadUsuario Novedad(bool tieneVideo, long? tamano = 2_200_000)
            => new NovedadUsuario { Id = 782, Version = "1.10.50.0", Titulo = "Terminar reposición", Categoria = "Mejorado", TieneVideo = tieneVideo, VideoTamano = tieneVideo ? tamano : null };

        // ---- Cuándo sale el botón ----

        [TestMethod]
        public void ConVideo_SaleElBotonYSePuedePulsar()
        {
            NovedadItem item = Item(Novedad(true), Contexto());

            Assert.IsTrue(item.TieneVideo);
            Assert.AreEqual("▶ Ver vídeo", item.Video.TextoBoton);
            Assert.IsTrue(item.Video.VerCommand.CanExecute(null));
            StringAssert.Contains(item.Video.TextoAyuda, "2,1 MB");
        }

        [TestMethod]
        public void SinVideo_NoSaleElBoton()
        {
            NovedadItem item = Item(Novedad(false), Contexto());

            Assert.IsFalse(item.TieneVideo);
            Assert.IsNull(item.Video);
        }

        [TestMethod]
        public void SinServicioDeVideos_NoSaleElBoton()
        {
            NovedadItem item = Item(Novedad(true), null);

            Assert.IsFalse(item.TieneVideo);
        }

        [TestMethod]
        public void SinReproductor_ElBotonNoSePuedePulsar()
        {
            NovedadItem item = Item(Novedad(true), new ContextoVideosNovedades(servicioVideos, null, carpeta));

            Assert.IsFalse(item.Video.VerCommand.CanExecute(null));
        }

        [TestMethod]
        public void ApiViejaSinLaPropiedad_SinVideo()
        {
            var n = JsonConvert.DeserializeObject<NovedadUsuario>("{\"Id\":1,\"Version\":\"1.10.49.0\",\"Titulo\":\"a\",\"Categoria\":\"Nuevo\"}");

            Assert.IsFalse(Item(n, Contexto()).TieneVideo);
        }

        // ---- Solo se descarga al pulsar ----

        [TestMethod]
        public void AlAbrirLaVentana_NoSeDescargaNada()
        {
            _ = Item(Novedad(true), Contexto());

            A.CallTo(() => servicioVideos.Descargar(A<int>._)).MustNotHaveHappened();
            A.CallTo(() => servicioVideos.RegistrarReproduccion(A<int>._)).MustNotHaveHappened();
            A.CallTo(() => reproductor.Reproducir(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task AlPulsar_DescargaApuntaReproduceYBorraElTemporal()
        {
            NovedadItem item = Item(Novedad(true), Contexto());

            await item.Video.VerCommand.ExecuteAsync(null);

            A.CallTo(() => servicioVideos.Descargar(782)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicioVideos.RegistrarReproduccion(782)).MustHaveHappenedOnceExactly();
            A.CallTo(() => reproductor.Reproducir(A<string>._, "Terminar reposición")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, reproducidos.Count);
            Assert.IsTrue(existiaAlReproducir[0], "Se reproduce desde un fichero local ya escrito");
            StringAssert.StartsWith(reproducidos[0], carpeta);
            StringAssert.EndsWith(reproducidos[0], ".mp4");
            Assert.IsFalse(File.Exists(reproducidos[0]), "Al cerrar el reproductor se borra el temporal");
            Assert.IsFalse(item.Video.HayMensaje);
        }

        [TestMethod]
        public async Task CadaVezQueSePulsa_SeVuelveADescargar_SinGuardarNada()
        {
            NovedadItem item = Item(Novedad(true), Contexto());

            await item.Video.VerCommand.ExecuteAsync(null);
            await item.Video.VerCommand.ExecuteAsync(null);

            A.CallTo(() => servicioVideos.Descargar(782)).MustHaveHappenedTwiceExactly();
            Assert.AreEqual(0, Directory.GetFiles(carpeta).Length);
        }

        [TestMethod]
        public async Task SiFallaLaDescarga_MensajeYNoSeReproduceNiSeCuenta()
        {
            A.CallTo(() => servicioVideos.Descargar(A<int>._)).ThrowsAsync(new InvalidOperationException("No existe"));
            NovedadItem item = Item(Novedad(true), Contexto());

            await item.Video.VerCommand.ExecuteAsync(null);

            Assert.IsTrue(item.Video.HayMensaje);
            StringAssert.Contains(item.Video.Mensaje, "No se pudo descargar el vídeo");
            Assert.IsFalse(item.Video.Descargando);
            Assert.IsTrue(item.Video.VerCommand.CanExecute(null), "Se puede volver a intentar");
            A.CallTo(() => reproductor.Reproducir(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => servicioVideos.RegistrarReproduccion(A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task SiFallaApuntarLaReproduccion_SeVeIgual()
        {
            A.CallTo(() => servicioVideos.RegistrarReproduccion(A<int>._)).ThrowsAsync(new Exception("Sin red"));
            NovedadItem item = Item(Novedad(true), Contexto());

            await item.Video.VerCommand.ExecuteAsync(null);

            A.CallTo(() => reproductor.Reproducir(A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(item.Video.HayMensaje);
        }

        [TestMethod]
        public async Task SiFallaElReproductor_MensajeYSeBorraElTemporal()
        {
            A.CallTo(() => reproductor.Reproducir(A<string>._, A<string>._)).Invokes((string ruta, string titulo) => reproducidos.Add(ruta))
                .Throws(new InvalidOperationException("Sin códec"));
            NovedadItem item = Item(Novedad(true), Contexto());

            await item.Video.VerCommand.ExecuteAsync(null);

            StringAssert.Contains(item.Video.Mensaje, "No se pudo reproducir el vídeo");
            Assert.IsFalse(File.Exists(reproducidos.Single()));
        }

        [TestMethod]
        public async Task MientrasDescarga_ElBotonDiceDescargandoYNoSePuedePulsar()
        {
            var descarga = new TaskCompletionSource<byte[]>();
            A.CallTo(() => servicioVideos.Descargar(A<int>._)).Returns(descarga.Task);
            NovedadItem item = Item(Novedad(true), Contexto());

            Task viendo = item.Video.VerCommand.ExecuteAsync(null);

            Assert.IsTrue(item.Video.Descargando);
            Assert.AreEqual("Descargando el vídeo…", item.Video.TextoBoton);
            Assert.IsFalse(item.Video.VerCommand.CanExecute(null));
            descarga.SetResult(new byte[] { 1, 2, 3 });
            await viendo;
            Assert.IsFalse(item.Video.Descargando);
            Assert.AreEqual("▶ Ver vídeo", item.Video.TextoBoton);
        }
    }
}
