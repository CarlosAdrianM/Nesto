using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#519 (NestoAPI#616): adjuntos (PDF e imágenes) en las novedades. Con una API vieja (sin la propiedad)
    /// no se ve nada; abrir descarga una vez y reutiliza el temporal; solo Dirección / Informática adjuntan y borran.
    /// </summary>
    [TestClass]
    public class NovedadesAdjuntosTests
    {
        private INovedadesService servicio;
        private IServicioAdjuntosNovedades servicioAdjuntos;
        private List<string> abiertos;
        private List<string> confirmaciones;
        private bool respuestaConfirmar;
        private IList<string> ficherosElegidos;
        private string carpeta;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<INovedadesService>();
            servicioAdjuntos = A.Fake<IServicioAdjuntosNovedades>();
            abiertos = new List<string>();
            confirmaciones = new List<string>();
            respuestaConfirmar = true;
            ficherosElegidos = new List<string>();
            carpeta = Path.Combine(Path.GetTempPath(), "NestoTestsAdjuntos", Guid.NewGuid().ToString("N"));
            A.CallTo(() => servicioAdjuntos.Descargar(A<int>._)).Returns(Task.FromResult(new byte[] { 1, 2, 3 }));
        }

        [TestCleanup]
        public void Limpiar()
        {
            try { Directory.Delete(carpeta, true); } catch (Exception) { }
        }

        private ContextoAdjuntosNovedades Contexto(bool puedeGestionar)
            => new ContextoAdjuntosNovedades(servicioAdjuntos, puedeGestionar, () => ficherosElegidos, abiertos.Add,
                p => { confirmaciones.Add(p); return respuestaConfirmar; }, carpeta);

        private NovedadItem Item(NovedadUsuario n, bool puedeGestionar = false)
        {
            var vm = new NovedadesDialogViewModel(servicio, A.Fake<IPortapapelesImagenes>(), _ => false, Contexto(puedeGestionar));
            var parametros = new DialogParameters();
            parametros.Add("novedades", new List<NovedadUsuario> { n });
            vm.OnDialogOpened(parametros);
            return vm.Novedades.Single();
        }

        private static NovedadUsuario Novedad(params AdjuntoNovedad[] adjuntos)
            => new NovedadUsuario { Id = 7, Version = "1.10.39.0", Titulo = "Cupones", Categoria = "Nuevo", Adjuntos = adjuntos.ToList() };

        private static AdjuntoNovedad Pdf(int id = 1, string nombre = "Normas cupones.pdf", long tamano = 245760)
            => new AdjuntoNovedad { Id = id, Nombre = nombre, Tipo = "application/pdf", Tamano = tamano };

        private AdjuntoNovedadItem Chip(AdjuntoNovedad a, bool puedeGestionar = false)
            => new AdjuntoNovedadItem(a, Contexto(puedeGestionar));

        // ---- Tamaño legible e icono ----

        [TestMethod]
        public void FormatearTamano_BytesKbYMb()
        {
            Assert.AreEqual("0 bytes", AdjuntoNovedadItem.FormatearTamano(0));
            Assert.AreEqual("1 byte", AdjuntoNovedadItem.FormatearTamano(1));
            Assert.AreEqual("512 bytes", AdjuntoNovedadItem.FormatearTamano(512));
            Assert.AreEqual("1 KB", AdjuntoNovedadItem.FormatearTamano(1024));
            Assert.AreEqual("240 KB", AdjuntoNovedadItem.FormatearTamano(245760));
            Assert.AreEqual("1 MB", AdjuntoNovedadItem.FormatearTamano(1024 * 1024 - 100), "1023,9 KB no se enseña como «1024 KB»");
            Assert.AreEqual("1,5 MB", AdjuntoNovedadItem.FormatearTamano(1572864));
            Assert.AreEqual("10 MB", AdjuntoNovedadItem.FormatearTamano(10 * 1024 * 1024));
        }

        [TestMethod]
        public void Texto_NombreYTamano()
        {
            Assert.AreEqual("Normas cupones.pdf · 240 KB", Chip(Pdf()).Texto);
        }

        [TestMethod]
        public void Icono_PorTipo()
        {
            Assert.AreEqual(AdjuntoNovedadItem.ICONO_PDF, Chip(Pdf()).Icono);
            Assert.AreEqual(AdjuntoNovedadItem.ICONO_IMAGEN, Chip(new AdjuntoNovedad { Id = 2, Nombre = "a.png", Tipo = "image/png" }).Icono);
            Assert.AreEqual(AdjuntoNovedadItem.ICONO_IMAGEN, Chip(new AdjuntoNovedad { Id = 3, Nombre = "a.webp", Tipo = "IMAGE/WEBP" }).Icono);
            Assert.AreEqual(AdjuntoNovedadItem.ICONO_OTRO, Chip(new AdjuntoNovedad { Id = 4, Nombre = "a.zip", Tipo = "application/zip" }).Icono);
        }

        [TestMethod]
        public void Icono_SinTipo_SeDeduceDeLaExtension()
        {
            Assert.AreEqual(AdjuntoNovedadItem.ICONO_PDF, Chip(new AdjuntoNovedad { Id = 1, Nombre = "x.PDF" }).Icono);
            Assert.AreEqual(AdjuntoNovedadItem.ICONO_IMAGEN, Chip(new AdjuntoNovedad { Id = 2, Nombre = "foto.jpeg" }).Icono);
        }

        // ---- API vieja: nada ----

        [TestMethod]
        public void ApiSinLaPropiedad_SinChipsNiBoton_AunqueSeaDireccion()
        {
            NovedadUsuario vieja = JsonConvert.DeserializeObject<List<NovedadUsuario>>(
                "[{\"Id\":7,\"Version\":\"1.10.39.0\",\"Titulo\":\"x\",\"Categoria\":\"Nuevo\"}]").Single();

            NovedadItem item = Item(vieja, puedeGestionar: true);

            Assert.IsNull(vieja.Adjuntos);
            Assert.IsFalse(item.HayAdjuntos);
            Assert.IsFalse(item.PuedeAdjuntar, "Con la API vieja el POST daría 404: no se ofrece");
            Assert.IsFalse(item.MostrarAdjuntos);
        }

        [TestMethod]
        public void ConAdjuntos_UnChipPorAdjunto()
        {
            NovedadItem item = Item(Novedad(Pdf(1), new AdjuntoNovedad { Id = 2, Nombre = "b.png", Tipo = "image/png", Tamano = 10 }));

            Assert.AreEqual(2, item.Adjuntos.Count);
            Assert.IsTrue(item.HayAdjuntos);
            Assert.IsTrue(item.MostrarAdjuntos);
        }

        [TestMethod]
        public void SinContextoDeAdjuntos_SinChips()
        {
            var vm = new NovedadesDialogViewModel(servicio, A.Fake<IPortapapelesImagenes>(), _ => false);
            var parametros = new DialogParameters();
            parametros.Add("novedades", new List<NovedadUsuario> { Novedad(Pdf()) });
            vm.OnDialogOpened(parametros);

            Assert.IsFalse(vm.Novedades.Single().HayAdjuntos);
            Assert.IsFalse(vm.Novedades.Single().PuedeAdjuntar);
        }

        // ---- Abrir ----

        [TestMethod]
        public async Task Abrir_DescargaConElNombreOriginalYLoAbre()
        {
            AdjuntoNovedadItem chip = Chip(Pdf(5));

            await chip.Abrir();

            A.CallTo(() => servicioAdjuntos.Descargar(5)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, abiertos.Count);
            Assert.AreEqual(Path.Combine(carpeta, "Normas cupones.pdf"), abiertos[0]);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(abiertos[0]));
            Assert.IsFalse(chip.Descargando);
        }

        [TestMethod]
        public async Task Abrir_DosVeces_DescargaUnaYReutilizaElTemporal()
        {
            AdjuntoNovedadItem chip = Chip(Pdf(5));

            await chip.Abrir();
            await chip.Abrir();

            A.CallTo(() => servicioAdjuntos.Descargar(A<int>._)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(2, abiertos.Count);
            Assert.AreEqual(abiertos[0], abiertos[1]);
        }

        [TestMethod]
        public async Task Abrir_SiBorraronElTemporal_LoVuelveADescargar()
        {
            AdjuntoNovedadItem chip = Chip(Pdf(5));
            await chip.Abrir();
            File.Delete(abiertos[0]);

            await chip.Abrir();

            A.CallTo(() => servicioAdjuntos.Descargar(5)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task Abrir_MismoNombreQueOtro_NoLoPisa()
        {
            await Chip(Pdf(1)).Abrir();
            await Chip(Pdf(2)).Abrir();

            Assert.AreEqual(Path.Combine(carpeta, "Normas cupones.pdf"), abiertos[0]);
            Assert.AreEqual(Path.Combine(carpeta, "Normas cupones (2).pdf"), abiertos[1]);
        }

        [TestMethod]
        public void NombreSeguro_QuitaRutasYCaracteresInvalidos()
        {
            Assert.AreEqual("malo.pdf", CarpetaTemporalAdjuntos.NombreSeguro(@"..\..\malo.pdf"));
            Assert.AreEqual("a_b.pdf", CarpetaTemporalAdjuntos.NombreSeguro("a?b.pdf"));
            Assert.AreEqual("adjunto.pdf", CarpetaTemporalAdjuntos.NombreSeguro(".pdf"));
        }

        [TestMethod]
        public async Task Abrir_SiFallaLaDescarga_AvisoDiscretoEnLaNovedad()
        {
            A.CallTo(() => servicioAdjuntos.Descargar(A<int>._)).ThrowsAsync(new InvalidOperationException("No se pudo descargar el adjunto (error 404)."));
            NovedadItem item = Item(Novedad(Pdf()));

            await item.Adjuntos.Single().Abrir();

            Assert.AreEqual(0, abiertos.Count);
            StringAssert.Contains(item.MensajeAdjuntos, "Normas cupones.pdf");
            StringAssert.Contains(item.MensajeAdjuntos, "error 404");
            Assert.IsFalse(item.Adjuntos.Single().Descargando);
        }

        // ---- Permisos ----

        [TestMethod]
        public void Permisos_ElBotonYLaXSoloParaDireccionOInformatica()
        {
            NovedadItem normal = Item(Novedad(Pdf()), puedeGestionar: false);
            NovedadItem direccion = Item(Novedad(Pdf()), puedeGestionar: true);

            Assert.IsFalse(normal.PuedeAdjuntar);
            Assert.IsFalse(normal.Adjuntos.Single().PuedeBorrar);
            Assert.IsTrue(direccion.PuedeAdjuntar);
            Assert.IsTrue(direccion.Adjuntos.Single().PuedeBorrar);
            Assert.IsTrue(direccion.MostrarAdjuntos, "Sin adjuntos también se ve, para el botón");
        }

        [TestMethod]
        public void EsDireccionOInformatica_MiraLosDosGrupos()
        {
            var configuracion = A.Fake<IConfiguracion>();
            Assert.IsFalse(ContextoAdjuntosNovedades.EsDireccionOInformatica(configuracion));

            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA)).Returns(true);
            Assert.IsTrue(ContextoAdjuntosNovedades.EsDireccionOInformatica(configuracion));

            var direccion = A.Fake<IConfiguracion>();
            A.CallTo(() => direccion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)).Returns(true);
            Assert.IsTrue(ContextoAdjuntosNovedades.EsDireccionOInformatica(direccion));

            var rota = A.Fake<IConfiguracion>();
            A.CallTo(() => rota.UsuarioEnGrupo(A<string>._)).Throws(new Exception("AD caído"));
            Assert.IsFalse(ContextoAdjuntosNovedades.EsDireccionOInformatica(rota));
            Assert.IsFalse(ContextoAdjuntosNovedades.EsDireccionOInformatica(null));
        }

        [TestMethod]
        public async Task Adjuntar_SinPermiso_NoHaceNada()
        {
            ficherosElegidos = new List<string> { "a.pdf" };
            NovedadItem item = Item(Novedad(), puedeGestionar: false);

            await item.Adjuntar();

            A.CallTo(() => servicioAdjuntos.Subir(A<int>._, A<IEnumerable<string>>._)).MustNotHaveHappened();
        }

        // ---- Subir ----

        [TestMethod]
        public async Task Adjuntar_SubeLoElegidoYAnadeLosChips()
        {
            ficherosElegidos = new List<string> { @"C:\x\Normas cupones.pdf", @"C:\x\cartel.png" };
            A.CallTo(() => servicioAdjuntos.Subir(7, A<IEnumerable<string>>._)).Returns(Task.FromResult(new List<AdjuntoNovedad>
            {
                Pdf(10), new AdjuntoNovedad { Id = 11, Nombre = "cartel.png", Tipo = "image/png", Tamano = 2048 }
            }));
            NovedadItem item = Item(Novedad(), puedeGestionar: true);
            Assert.IsFalse(item.HayAdjuntos);

            await item.Adjuntar();

            A.CallTo(() => servicioAdjuntos.Subir(7, A<IEnumerable<string>>.That.Matches(r => r.SequenceEqual(ficherosElegidos))))
                .MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new[] { 10, 11 }, item.Adjuntos.Select(a => a.Id).ToArray());
            Assert.IsTrue(item.HayAdjuntos);
            Assert.IsFalse(item.SubiendoAdjuntos);
        }

        [TestMethod]
        public async Task Adjuntar_SiCancelaElDialogo_NoSube()
        {
            NovedadItem item = Item(Novedad(), puedeGestionar: true);

            await item.Adjuntar();

            A.CallTo(() => servicioAdjuntos.Subir(A<int>._, A<IEnumerable<string>>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Adjuntar_SiLaApiLoRechaza_ElMotivoQuedaEnLaNovedad()
        {
            ficherosElegidos = new List<string> { "grande.pdf" };
            A.CallTo(() => servicioAdjuntos.Subir(A<int>._, A<IEnumerable<string>>._))
                .ThrowsAsync(new InvalidOperationException("No se pudo subir el adjunto: pasa de 10 MB"));
            NovedadItem item = Item(Novedad(), puedeGestionar: true);

            await item.Adjuntar();

            Assert.AreEqual("No se pudo subir el adjunto: pasa de 10 MB", item.MensajeAdjuntos);
            Assert.IsFalse(item.HayAdjuntos);
            Assert.IsTrue(item.AdjuntarCommand.CanExecute(null), "Se puede volver a intentar");
        }

        // ---- Borrar ----

        [TestMethod]
        public async Task Borrar_ConConfirmacion_LlamaALaApiYQuitaElChip()
        {
            NovedadItem item = Item(Novedad(Pdf(1), Pdf(2, "otro.pdf")), puedeGestionar: true);
            AdjuntoNovedadItem chip = item.Adjuntos.First();

            await chip.Borrar();

            Assert.AreEqual(1, confirmaciones.Count);
            StringAssert.Contains(confirmaciones[0], "Normas cupones.pdf");
            A.CallTo(() => servicioAdjuntos.Borrar(1)).MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new[] { 2 }, item.Adjuntos.Select(a => a.Id).ToArray());
        }

        [TestMethod]
        public async Task Borrar_SiNoConfirma_NoBorra()
        {
            respuestaConfirmar = false;
            NovedadItem item = Item(Novedad(Pdf(1)), puedeGestionar: true);

            await item.Adjuntos.Single().Borrar();

            A.CallTo(() => servicioAdjuntos.Borrar(A<int>._)).MustNotHaveHappened();
            Assert.AreEqual(1, item.Adjuntos.Count);
        }

        [TestMethod]
        public async Task Borrar_SinPermiso_NiPreguntaNiBorra()
        {
            NovedadItem item = Item(Novedad(Pdf(1)), puedeGestionar: false);

            await item.Adjuntos.Single().Borrar();

            Assert.AreEqual(0, confirmaciones.Count);
            A.CallTo(() => servicioAdjuntos.Borrar(A<int>._)).MustNotHaveHappened();
            Assert.IsFalse(item.Adjuntos.Single().BorrarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Borrar_SiFalla_SeQuedaElChipYSeAvisa()
        {
            A.CallTo(() => servicioAdjuntos.Borrar(A<int>._)).ThrowsAsync(new InvalidOperationException("No se pudo borrar el adjunto (error 403)."));
            NovedadItem item = Item(Novedad(Pdf(1)), puedeGestionar: true);

            await item.Adjuntos.Single().Borrar();

            Assert.AreEqual(1, item.Adjuntos.Count);
            Assert.AreEqual("No se pudo borrar el adjunto (error 403).", item.MensajeAdjuntos);
        }

        // ---- La vista: los chips son botones enfocables; «Adjuntar…» solo con permiso ----

        [TestMethod]
        public void Vista_PintaUnBotonEnfocablePorChipYElDeAdjuntar()
        {
            EjecutarEnSTA(() =>
            {
                var vm = new NovedadesDialogViewModel(servicio, A.Fake<IPortapapelesImagenes>(), _ => false, Contexto(true));
                var parametros = new DialogParameters();
                parametros.Add("novedades", new List<NovedadUsuario> { Novedad(Pdf(1), new AdjuntoNovedad { Id = 2, Nombre = "b.png", Tipo = "image/png", Tamano = 10 }) });
                vm.OnDialogOpened(parametros);
                var vista = new NovedadesDialog { DataContext = vm };
                vista.Measure(new System.Windows.Size(760, 700));
                vista.Arrange(new System.Windows.Rect(0, 0, 760, 700));
                vista.UpdateLayout();

                List<System.Windows.Controls.Button> botones = Descendientes<System.Windows.Controls.Button>(vista).ToList();
                var chips = botones.Where(b => b.DataContext is AdjuntoNovedadItem && b.Command == ((AdjuntoNovedadItem)b.DataContext).AbrirCommand).ToList();
                Assert.AreEqual(2, chips.Count);
                Assert.IsTrue(chips.All(b => b.Focusable && b.Visibility == System.Windows.Visibility.Visible));
                Assert.IsTrue(botones.Any(b => b.Visibility == System.Windows.Visibility.Visible && Equals(b.Content, "Adjuntar…")));
                Assert.AreEqual(2, botones.Count(b => b.Visibility == System.Windows.Visibility.Visible && Equals(b.Content, "✕") && b.DataContext is AdjuntoNovedadItem));
            });
        }

        private static IEnumerable<T> Descendientes<T>(System.Windows.DependencyObject raiz) where T : System.Windows.DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(raiz); i++)
            {
                var hijo = System.Windows.Media.VisualTreeHelper.GetChild(raiz, i);
                if (hijo is T t)
                {
                    yield return t;
                }
                foreach (T nieto in Descendientes<T>(hijo))
                {
                    yield return nieto;
                }
            }
        }

        private static void EjecutarEnSTA(Action action)
        {
            Exception capturada = null;
            var hilo = new System.Threading.Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { capturada = ex; }
            });
            hilo.SetApartmentState(System.Threading.ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (capturada != null)
            {
                throw new AssertFailedException(capturada.Message, capturada);
            }
        }

        // ---- Sugerencias: también ----

        [TestMethod]
        public async Task Sugerencias_TambienTraenSusChips()
        {
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario>
            {
                new NovedadUsuario { Id = 50, Titulo = "Idea", Adjuntos = new List<AdjuntoNovedad> { Pdf(3) } }
            }));
            var vm = new NovedadesDialogViewModel(servicio, A.Fake<IPortapapelesImagenes>(), _ => false, Contexto(false));
            vm.OnDialogOpened(new DialogParameters());

            await vm.IrASugerencias();

            Assert.AreEqual(3, vm.Novedades.Single().Adjuntos.Single().Id);
        }
    }
}
