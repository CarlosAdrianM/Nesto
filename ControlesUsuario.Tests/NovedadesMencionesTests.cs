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
    /// Nesto#491 (NestoAPI#537): @menciones en Novedades. Reglas puras (detectar la @ activa, filtrar,
    /// sustituir, trocear para resaltar) y el desplegable (carga cacheada por ventana, filtrado, inserción).
    /// </summary>
    [TestClass]
    public class NovedadesMencionesTests
    {
        private static Mencionable M(string nombre) => new Mencionable { Nombre = nombre, Clave = "NUEVAVISION\\" + nombre, Aplicacion = "Nesto" };

        private static readonly List<Mencionable> TODOS = new List<Mencionable> { M("Alfredo"), M("Carlos"), M("María"), M("Marcos"), M("Rosa") };

        #region Detectar

        [TestMethod]
        public void Detectar_TrasLaArroba_FiltroVacio()
        {
            MencionEnCurso m = Menciones.Detectar("Hola @", 6);

            Assert.IsNotNull(m);
            Assert.AreEqual(5, m.Inicio);
            Assert.AreEqual(6, m.Fin);
            Assert.AreEqual("", m.Filtro);
        }

        [TestMethod]
        public void Detectar_AlPrincipio_DevuelveLoEscritoHastaElCursor()
        {
            MencionEnCurso m = Menciones.Detectar("@alf", 4);

            Assert.AreEqual(0, m.Inicio);
            Assert.AreEqual("alf", m.Filtro);
        }

        [TestMethod]
        public void Detectar_CursorEnMedioDelNombre_FinLlegaAlFinalDeLaPalabra()
        {
            MencionEnCurso m = Menciones.Detectar("hola @alfredo que tal", 8);

            Assert.AreEqual(5, m.Inicio);
            Assert.AreEqual("al", m.Filtro);
            Assert.AreEqual(13, m.Fin);
        }

        [TestMethod]
        public void Detectar_UnCorreo_NoEsMencion()
        {
            Assert.IsNull(Menciones.Detectar("pepe@nuevavision", 16));
            Assert.IsNull(Menciones.Detectar("pepe@", 5));
        }

        [TestMethod]
        public void Detectar_TrasPuntoOArroba_NoEsMencion()
        {
            Assert.IsNull(Menciones.Detectar("a.@b", 4));
            Assert.IsNull(Menciones.Detectar("@@b", 3));
        }

        [TestMethod]
        public void Detectar_TrasUnEspacioOTrasLaPalabra_NoHayMencion()
        {
            Assert.IsNull(Menciones.Detectar("@alfredo ", 9));
            Assert.IsNull(Menciones.Detectar("sin arroba", 5));
            Assert.IsNull(Menciones.Detectar("", 0));
        }

        [TestMethod]
        public void Detectar_NombreQueEmpiezaPorDigito_NoEsMencion()
        {
            Assert.IsNull(Menciones.Detectar("@1a", 3));
        }

        [TestMethod]
        public void Detectar_TrasParentesisOSaltoDeLinea_SiEsMencion()
        {
            Assert.AreEqual("ro", Menciones.Detectar("(@ro", 4).Filtro);
            Assert.AreEqual("ro", Menciones.Detectar("hola\n@ro", 8).Filtro);
        }

        #endregion

        #region Filtrar

        [TestMethod]
        public void Filtrar_SinFiltro_TodosPorOrdenAlfabetico()
        {
            List<Mencionable> r = Menciones.Filtrar(TODOS, "");

            CollectionAssert.AreEqual(new[] { "Alfredo", "Carlos", "Marcos", "María", "Rosa" }, r.Select(x => x.Nombre).ToArray());
        }

        [TestMethod]
        public void Filtrar_SinTildesNiMayusculas()
        {
            List<Mencionable> r = Menciones.Filtrar(TODOS, "MARI");

            CollectionAssert.AreEqual(new[] { "María" }, r.Select(x => x.Nombre).ToArray());
            Assert.AreEqual("María", Menciones.Filtrar(TODOS, "maría").Single().Nombre);
        }

        [TestMethod]
        public void Filtrar_PrimeroLosQueEmpiezanLuegoLosQueContienen()
        {
            List<Mencionable> r = Menciones.Filtrar(TODOS, "os");

            // Ninguno empieza por «os»: Carlos, Marcos y Rosa lo contienen.
            CollectionAssert.AreEqual(new[] { "Carlos", "Marcos", "Rosa" }, r.Select(x => x.Nombre).ToArray());

            r = Menciones.Filtrar(TODOS, "r");
            Assert.AreEqual("Rosa", r[0].Nombre);
            CollectionAssert.AreEquivalent(new[] { "Rosa", "Alfredo", "Carlos", "Marcos", "María" }, r.Select(x => x.Nombre).ToArray());
        }

        [TestMethod]
        public void Filtrar_NadieCasa_ListaVacia()
        {
            Assert.AreEqual(0, Menciones.Filtrar(TODOS, "zz").Count);
            Assert.AreEqual(0, Menciones.Filtrar(null, "a").Count);
        }

        #endregion

        #region Sustituir y Trocear

        [TestMethod]
        public void Sustituir_PoneElNombreConEspacioYElCursorDetras()
        {
            string texto = "hola @alf";
            (string Texto, int Cursor) r = Menciones.Sustituir(texto, Menciones.Detectar(texto, 9), "Alfredo");

            Assert.AreEqual("hola @Alfredo ", r.Texto);
            Assert.AreEqual(14, r.Cursor);
        }

        [TestMethod]
        public void Sustituir_EnMedioDelTexto_SustituyeLaPalabraEnteraSinDuplicarElEspacio()
        {
            string texto = "hola @alxx que tal";
            (string Texto, int Cursor) r = Menciones.Sustituir(texto, Menciones.Detectar(texto, 8), "Alfredo");

            Assert.AreEqual("hola @Alfredo que tal", r.Texto);
            Assert.AreEqual(14, r.Cursor);
        }

        [TestMethod]
        public void Trocear_SeparaLasMencionesSinTocarElResto()
        {
            List<TrozoTexto> trozos = Menciones.Trocear("Hola @Carlos, mira esto (y @María) pepe@nuevavision.es");

            CollectionAssert.AreEqual(new[] { "Hola ", "@Carlos", ", mira esto (y ", "@María", ") pepe@nuevavision.es" },
                trozos.Select(t => t.Texto).ToArray());
            CollectionAssert.AreEqual(new[] { false, true, false, true, false }, trozos.Select(t => t.EsMencion).ToArray());
            Assert.AreEqual(0, Menciones.Trocear(null).Count);
        }

        #endregion

        #region Desplegable (AutocompletadoMenciones) y ViewModel

        private INovedadesService servicio;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<INovedadesService>();
            A.CallTo(() => servicio.LeerMencionables()).Returns(Task.FromResult(TODOS.ToList()));
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario>()));
        }

        [TestMethod]
        public async Task Actualizar_AlEscribirArroba_AbreConTodosYElPrimeroSeleccionado()
        {
            var auto = new AutocompletadoMenciones(new ListaMencionables(servicio));

            await auto.Actualizar("Hola @", 6);

            Assert.IsTrue(auto.Abierto);
            Assert.AreEqual(5, auto.Sugerencias.Count);
            Assert.AreEqual("Alfredo", auto.Seleccionada.Nombre);
        }

        [TestMethod]
        public async Task Actualizar_FiltraSegunSeEscribeYCierraSiNadieCasa()
        {
            var auto = new AutocompletadoMenciones(new ListaMencionables(servicio));

            await auto.Actualizar("@ma", 3);
            CollectionAssert.AreEqual(new[] { "Marcos", "María" }, auto.Sugerencias.Select(s => s.Nombre).ToArray());

            await auto.Actualizar("@maz", 4);
            Assert.IsFalse(auto.Abierto);

            await auto.Actualizar("@ma ", 4);
            Assert.IsFalse(auto.Abierto);
        }

        [TestMethod]
        public async Task Actualizar_SinArroba_NoPideNadaALaApi()
        {
            var auto = new AutocompletadoMenciones(new ListaMencionables(servicio));

            await auto.Actualizar("hola", 4);

            Assert.IsFalse(auto.Abierto);
            A.CallTo(() => servicio.LeerMencionables()).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Mover_Y_Elegir_InsertaElNombreConEspacio()
        {
            var auto = new AutocompletadoMenciones(new ListaMencionables(servicio));
            await auto.Actualizar("mira @ma", 8);

            auto.Mover(1);
            auto.Mover(1); // no se sale de la lista
            (string Texto, int Cursor)? r = auto.Elegir();

            Assert.AreEqual("mira @María ", r.Value.Texto);
            Assert.AreEqual(12, r.Value.Cursor);
            Assert.IsFalse(auto.Abierto);
        }

        [TestMethod]
        public async Task Elegir_ConRaton_UsaElElegido()
        {
            var auto = new AutocompletadoMenciones(new ListaMencionables(servicio));
            await auto.Actualizar("@", 1);

            (string Texto, int Cursor)? r = auto.Elegir(TODOS.Single(m => m.Nombre == "Rosa"));

            Assert.AreEqual("@Rosa ", r.Value.Texto);
        }

        [TestMethod]
        public void Elegir_Cerrado_NoHaceNada()
        {
            var auto = new AutocompletadoMenciones(new ListaMencionables(servicio));

            Assert.IsNull(auto.Elegir());
        }

        [TestMethod]
        public async Task Descartar_NoSeReabreEnLaMismaArroba_PeroSiEnOtra()
        {
            var auto = new AutocompletadoMenciones(new ListaMencionables(servicio));
            await auto.Actualizar("@a", 2);

            auto.Descartar();
            await auto.Actualizar("@al", 3);
            Assert.IsFalse(auto.Abierto);

            await auto.Actualizar("@al @", 5);
            Assert.IsTrue(auto.Abierto);
        }

        [TestMethod]
        public async Task SiLaApiFalla_SinDesplegableYSinError()
        {
            A.CallTo(() => servicio.LeerMencionables()).ThrowsAsync(new InvalidOperationException("caída"));
            var auto = new AutocompletadoMenciones(new ListaMencionables(servicio));

            await auto.Actualizar("@", 1);
            await auto.Actualizar("@a", 2);

            Assert.IsFalse(auto.Abierto);
            A.CallTo(() => servicio.LeerMencionables()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Ventana_UnaSolaPeticionParaTodosLosCuadros()
        {
            var vm = new NovedadesDialogViewModel(servicio, null, _ => false);
            var parametros = new DialogParameters();
            parametros.Add("novedades", new List<NovedadUsuario>
            {
                new NovedadUsuario { Id = 1, Version = "1.10.31.0", Titulo = "a", VotosPositivos = 0 },
                new NovedadUsuario { Id = 2, Version = "1.10.31.0", Titulo = "b", VotosPositivos = 0 }
            });
            vm.OnDialogOpened(parametros);

            await vm.Novedades[0].MencionesComentario.Actualizar("@c", 2);
            await vm.Novedades[1].MencionesComentario.Actualizar("@", 1);
            await vm.MencionesSugerencia.Actualizar("@r", 2);

            Assert.AreEqual("Carlos", vm.Novedades[0].MencionesComentario.Seleccionada.Nombre);
            Assert.IsTrue(vm.Novedades[1].MencionesComentario.Abierto);
            Assert.AreEqual("Rosa", vm.MencionesSugerencia.Seleccionada.Nombre);
            A.CallTo(() => servicio.LeerMencionables()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Ventana_ElegirEnElComentario_DejaElTextoListoParaEnviar()
        {
            var vm = new NovedadesDialogViewModel(servicio, null, _ => false);
            var parametros = new DialogParameters();
            parametros.Add("novedades", new List<NovedadUsuario> { new NovedadUsuario { Id = 1, Version = "1.10.31.0", Titulo = "a", VotosPositivos = 0 } });
            vm.OnDialogOpened(parametros);
            NovedadItem novedad = vm.Novedades[0];
            novedad.NuevoTexto = "Esto no va, @alf";

            await novedad.MencionesComentario.Actualizar(novedad.NuevoTexto, novedad.NuevoTexto.Length);
            novedad.NuevoTexto = novedad.MencionesComentario.Elegir().Value.Texto;

            Assert.AreEqual("Esto no va, @Alfredo ", novedad.NuevoTexto);
            Assert.IsTrue(novedad.EnviarComentarioCommand.CanExecute(null));
        }

        #endregion
    }
}
