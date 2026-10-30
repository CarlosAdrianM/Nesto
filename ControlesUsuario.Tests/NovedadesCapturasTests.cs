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
    /// Nesto#578 (NestoAPI#754, sugerencia 900): varias capturas en las sugerencias, en los avisos de «Algo no funciona» y
    /// en los comentarios: Ctrl+V varias veces, miniaturas con «Quitar» y, al leer, todas las de cada uno.
    /// </summary>
    [TestClass]
    public class NovedadesCapturasTests
    {
        private INovedadesService servicio;
        private IPortapapelesImagenes portapapeles;
        private Queue<byte[]> copiadas;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<INovedadesService>();
            portapapeles = A.Fake<IPortapapelesImagenes>();
            copiadas = new Queue<byte[]>();
            A.CallTo(() => portapapeles.HayImagen()).ReturnsLazily(() => copiadas.Count > 0);
            A.CallTo(() => portapapeles.LeerImagenPng()).ReturnsLazily(() => copiadas.Dequeue());
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario>()));
            A.CallTo(() => servicio.LeerComentarios(A<int>._)).Returns(Task.FromResult(new List<ComentarioNovedad>()));
        }

        private static NovedadUsuario N(int id) => new NovedadUsuario
        {
            Id = id, Version = "1.10.53.0", Titulo = "x", Categoria = "Nuevo", VotosPositivos = 0, VotosNegativos = 0, NumeroComentarios = 0
        };

        private static NovedadUsuario S(int id, bool imagen = false, List<CapturaNovedad> capturas = null) => new NovedadUsuario
        {
            Id = id, Version = null, Titulo = "Sugerencia", TextoOriginal = "Sugerencia", Categoria = "Nuevo", Estado = "Pendiente",
            SugeridaNombre = "MariaJose", SugeridaFecha = new DateTime(2026, 10, 30), TieneImagen = imagen, Capturas = capturas,
            VotosPositivos = 0, VotosNegativos = 0, NumeroComentarios = 0
        };

        private NovedadesDialogViewModel AbrirVm(params NovedadUsuario[] novedades)
        {
            // Sin preguntas: no se ofrece adjuntar lo copiado al abrir (cada test pega lo suyo)
            var vm = new NovedadesDialogViewModel(servicio, portapapeles, p => false);
            var parametros = new ParametrosDialogo();
            parametros.Add("novedades", novedades.ToList());
            vm.OnDialogOpened(parametros);
            return vm;
        }

        private void Copiar(params byte[][] imagenes)
        {
            foreach (byte[] imagen in imagenes)
            {
                copiadas.Enqueue(imagen);
            }
        }

        // ---- Pegar varias ----

        [TestMethod]
        public void PegarVariasVeces_SeAcumulanEnOrden()
        {
            var vm = AbrirVm(N(1));
            Copiar(new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 });

            Assert.IsTrue(vm.PegarImagenSugerencia());
            Assert.IsTrue(vm.PegarImagenSugerencia());
            Assert.IsTrue(vm.PegarImagenSugerencia());

            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, vm.CapturasSugerencia.Items.Select(i => i.Png[0]).ToList());
            Assert.IsTrue(vm.TieneImagenSugerencia);
        }

        [TestMethod]
        public void Quitar_SoloQuitaEsa()
        {
            var vm = AbrirVm(N(1));
            Copiar(new byte[] { 1 }, new byte[] { 2 });
            vm.PegarImagenSugerencia();
            vm.PegarImagenSugerencia();

            vm.CapturasSugerencia.Items[0].QuitarCommand.Execute(null);

            Assert.AreEqual(2, vm.CapturasSugerencia.Items.Single().Png[0]);
            vm.CapturasSugerencia.Items[0].QuitarCommand.Execute(null);
            Assert.IsFalse(vm.TieneImagenSugerencia);
        }

        [TestMethod]
        public void PegarLaMismaDosVeces_SeAvisaYNoSeRepite()
        {
            var vm = AbrirVm(N(1));
            Copiar(new byte[] { 1 }, new byte[] { 1 });

            vm.PegarImagenSugerencia();
            vm.PegarImagenSugerencia();

            Assert.AreEqual(1, vm.CapturasSugerencia.Numero);
            Assert.AreEqual(CapturasPegadas.AVISO_REPETIDA, vm.MensajeSugerencias);
        }

        [TestMethod]
        public void MasDelMaximo_SeAvisaDeUsted()
        {
            var vm = AbrirVm(N(1));
            Copiar(Enumerable.Range(1, CapturasPegadas.MAXIMO + 1).Select(i => new byte[] { (byte)i }).ToArray());

            for (int i = 0; i <= CapturasPegadas.MAXIMO; i++)
            {
                vm.PegarImagenSugerencia();
            }

            Assert.AreEqual(CapturasPegadas.MAXIMO, vm.CapturasSugerencia.Numero);
            Assert.AreEqual(CapturasPegadas.AVISO_MAXIMO, vm.MensajeSugerencias);
        }

        [TestMethod]
        public void TextosDelFormulario_DeUstedYConVariasCapturas()
        {
            var vm = AbrirVm(N(1));

            StringAssert.Contains(vm.TextoAyudaFormulario, "puede pegar una o varias capturas");
        }

        // ---- Enviar ----

        [TestMethod]
        public async Task EnviarSugerencia_MandaTodasEnOrdenYLasEnseñaSinVolverAPedirlas()
        {
            A.CallTo(() => servicio.Sugerir(A<string>._, A<IReadOnlyList<byte[]>>._)).Returns(Task.FromResult(S(60, imagen: true,
                capturas: new List<CapturaNovedad> { new CapturaNovedad { Id = 1, Orden = 1 }, new CapturaNovedad { Id = 2, Orden = 2 } })));
            var vm = AbrirVm(N(1));
            await vm.AbrirOCerrarSugerencia();
            Copiar(new byte[] { 1 }, new byte[] { 2 });
            vm.PegarImagenSugerencia();
            vm.PegarImagenSugerencia();
            vm.TextoSugerencia = "Dos pantallas";

            await vm.EnviarSugerencia();

            A.CallTo(() => servicio.Sugerir("Dos pantallas", A<IReadOnlyList<byte[]>>.That.Matches(l => l.Count == 2 && l[0][0] == 1 && l[1][0] == 2)))
                .MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.TieneImagenSugerencia);
            NovedadItem creada = vm.Novedades.Single(n => n.Id == 60);
            Assert.AreEqual(2, creada.ImagenesNovedad.Count);
            A.CallTo(() => servicio.LeerCaptura(A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task EnviarSugerencia_ConUnaApiQueNoConoceLaLista_SoloSeEnseñaLaPrimera()
        {
            A.CallTo(() => servicio.Sugerir(A<string>._, A<IReadOnlyList<byte[]>>._)).Returns(Task.FromResult(S(60, imagen: true, capturas: null)));
            var vm = AbrirVm(N(1));
            await vm.AbrirOCerrarSugerencia();
            Copiar(new byte[] { 1 }, new byte[] { 2 });
            vm.PegarImagenSugerencia();
            vm.PegarImagenSugerencia();
            vm.TextoSugerencia = "Dos pantallas";

            await vm.EnviarSugerencia();

            Assert.AreEqual(1, vm.Novedades.Single(n => n.Id == 60).ImagenesNovedad.Single().Imagen[0]);
        }

        [TestMethod]
        public async Task AvisarAlgoNoFunciona_TambienConVariasCapturas()
        {
            A.CallTo(() => servicio.AvisarAlgoNoFunciona(A<string>._, A<IReadOnlyList<byte[]>>._, A<string>._)).Returns(Task.FromResult(S(61)));
            var vm = AbrirVm(N(1));
            await vm.AbrirOCerrarIncidencia();
            Copiar(new byte[] { 1 }, new byte[] { 2 });
            vm.PegarImagenSugerencia();
            vm.PegarImagenSugerencia();
            vm.TextoSugerencia = "Falla al guardar";

            await vm.EnviarSugerencia();

            A.CallTo(() => servicio.AvisarAlgoNoFunciona("Falla al guardar", A<IReadOnlyList<byte[]>>.That.Matches(l => l.Count == 2), A<string>._))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Comentar_ConVariasCapturas_LasMandaYLasEnseña()
        {
            A.CallTo(() => servicio.Comentar(A<int>._, A<string>._, A<IReadOnlyList<byte[]>>._)).Returns(Task.FromResult(new ComentarioNovedad
            {
                Id = 50, NovedadId = 1, Texto = "Mire", TieneImagen = true, EsMio = true,
                Capturas = new List<CapturaNovedad> { new CapturaNovedad { Id = 1 }, new CapturaNovedad { Id = 2 } }
            }));
            NovedadItem item = AbrirVm(N(1)).Novedades.Single();
            Copiar(new byte[] { 1 }, new byte[] { 2 });
            item.PegarImagen();
            item.PegarImagen();
            item.NuevoTexto = "Mire";

            await item.EnviarComentario();

            A.CallTo(() => servicio.Comentar(1, "Mire", A<IReadOnlyList<byte[]>>.That.Matches(l => l.Count == 2))).MustHaveHappenedOnceExactly();
            Assert.AreEqual(2, item.Comentarios.Single().Imagenes.Count);
            Assert.IsFalse(item.TieneImagenAdjunta);
        }

        [TestMethod]
        public async Task Comentar_SinCapturas_MandaNull()
        {
            A.CallTo(() => servicio.Comentar(A<int>._, A<string>._, A<IReadOnlyList<byte[]>>._))
                .Returns(Task.FromResult(new ComentarioNovedad { Id = 50, Texto = "Hola" }));
            NovedadItem item = AbrirVm(N(1)).Novedades.Single();
            item.NuevoTexto = "Hola";

            await item.EnviarComentario();

            A.CallTo(() => servicio.Comentar(1, "Hola", null)).MustHaveHappenedOnceExactly();
        }

        // ---- Ver ----

        [TestMethod]
        public async Task Sugerencias_ConVariasCapturas_SePideCadaUnaPorSuIdEnOrden()
        {
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario>
            {
                S(50, imagen: true, capturas: new List<CapturaNovedad> { new CapturaNovedad { Id = 8, Orden = 2 }, new CapturaNovedad { Id = 7, Orden = 1 } })
            }));
            A.CallTo(() => servicio.LeerCaptura(7)).Returns(Task.FromResult(new byte[] { 7 }));
            A.CallTo(() => servicio.LeerCaptura(8)).Returns(Task.FromResult(new byte[] { 8 }));
            var vm = AbrirVm(N(1));

            await vm.IrASugerencias();

            NovedadItem sugerencia = vm.Novedades.Single(n => n.Id == 50);
            CollectionAssert.AreEqual(new byte[] { 7, 8 }, sugerencia.ImagenesNovedad.Select(i => i.Imagen[0]).ToList());
            A.CallTo(() => servicio.LeerImagenNovedad(A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Sugerencias_UnaCapturaQueFalla_LoDiceSinRomperLasDemas()
        {
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario>
            {
                S(50, imagen: true, capturas: new List<CapturaNovedad> { new CapturaNovedad { Id = 7, Orden = 1 }, new CapturaNovedad { Id = 8, Orden = 2 } })
            }));
            A.CallTo(() => servicio.LeerCaptura(7)).Throws(new InvalidOperationException("No se pudo leer la imagen"));
            A.CallTo(() => servicio.LeerCaptura(8)).Returns(Task.FromResult(new byte[] { 8 }));
            var vm = AbrirVm(N(1));

            await vm.IrASugerencias();

            NovedadItem sugerencia = vm.Novedades.Single(n => n.Id == 50);
            Assert.IsTrue(sugerencia.ImagenesNovedad[0].NoDisponible);
            Assert.AreEqual(8, sugerencia.ImagenesNovedad[1].Imagen[0]);
        }

        [TestMethod]
        public async Task Comentarios_ConCapturasYDeAntes_CadaUnoComoToca()
        {
            A.CallTo(() => servicio.LeerComentarios(1)).Returns(Task.FromResult(new List<ComentarioNovedad>
            {
                new ComentarioNovedad { Id = 10, TieneImagen = true, Capturas = new List<CapturaNovedad> { new CapturaNovedad { Id = 3 }, new CapturaNovedad { Id = 4 } } },
                new ComentarioNovedad { Id = 11, TieneImagen = true },
                new ComentarioNovedad { Id = 12, TieneImagen = false }
            }));
            A.CallTo(() => servicio.LeerCaptura(A<int>._)).Returns(Task.FromResult(new byte[] { 1 }));
            A.CallTo(() => servicio.LeerImagenComentario(11)).Returns(Task.FromResult(new byte[] { 2 }));
            NovedadItem item = AbrirVm(N(1)).Novedades.Single();

            await item.AbrirOCerrarComentarios();

            Assert.AreEqual(2, item.Comentarios.Single(c => c.Id == 10).Imagenes.Count);
            Assert.AreEqual(2, item.Comentarios.Single(c => c.Id == 11).Imagenes.Single().Imagen[0]);
            Assert.AreEqual(0, item.Comentarios.Single(c => c.Id == 12).Imagenes.Count);
            A.CallTo(() => servicio.LeerImagenComentario(10)).MustNotHaveHappened();
        }
    }
}
