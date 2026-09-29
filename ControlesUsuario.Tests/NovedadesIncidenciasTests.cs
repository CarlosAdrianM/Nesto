using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// NestoAPI#558: «🐞 Algo no funciona» junto a «💡 Sugerir una mejora». Mismo cuadro (texto y captura);
    /// se manda como incidencia con la pantalla que había abierta al abrir Novedades.
    /// </summary>
    [TestClass]
    public class NovedadesIncidenciasTests
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
        }

        private static NovedadUsuario N(int id, string version)
            => new NovedadUsuario { Id = id, Version = version, Titulo = "x", Categoria = "Nuevo" };

        private static NovedadUsuario I(int id, string texto, string contexto = null)
            => new NovedadUsuario { Id = id, Version = null, Titulo = texto, TextoOriginal = texto, Categoria = "Incidencia", EsIncidencia = true,
                Estado = "Pendiente", SugeridaNombre = "Alfredo", SugeridaFecha = new DateTime(2026, 9, 29), Contexto = contexto,
                VotosPositivos = 0, VotosNegativos = 0, NumeroComentarios = 0 };

        private NovedadesDialogViewModel AbrirVm(string pantalla = null)
        {
            var vm = new NovedadesDialogViewModel(servicio, portapapeles, p => { preguntas.Add(p); return true; });
            var parametros = new DialogParameters();
            parametros.Add("novedades", new List<NovedadUsuario> { N(1, "1.10.33.0") });
            if (pantalla != null)
            {
                parametros.Add(NovedadesDialogViewModel.PARAMETRO_PANTALLA, pantalla);
            }
            vm.OnDialogOpened(parametros);
            return vm;
        }

        [TestMethod]
        public async Task AlgoNoFunciona_AbreElMismoCuadroEnModoAviso()
        {
            var vm = AbrirVm();

            await vm.AbrirOCerrarIncidencia();

            Assert.IsTrue(vm.FormularioSugerenciaAbierto);
            Assert.IsTrue(vm.FormularioEsIncidencia);
            Assert.IsTrue(vm.EnSugerencias);
            Assert.AreEqual("Enviar aviso", vm.TextoBotonEnviarFormulario);
            StringAssert.StartsWith(vm.TextoAyudaFormulario, "¿Qué no funciona?");
        }

        [TestMethod]
        public async Task ConElCuadroAbierto_ElOtroBotonCambiaDeModoSinPerderLoEscrito()
        {
            var vm = AbrirVm();
            await vm.AbrirOCerrarSugerencia();
            vm.TextoSugerencia = "No me deja guardar";

            await vm.AbrirOCerrarIncidencia();

            Assert.IsTrue(vm.FormularioSugerenciaAbierto);
            Assert.IsTrue(vm.FormularioEsIncidencia);
            Assert.AreEqual("No me deja guardar", vm.TextoSugerencia);

            await vm.AbrirOCerrarIncidencia();
            Assert.IsFalse(vm.FormularioSugerenciaAbierto, "el mismo botón otra vez lo cierra");
        }

        [TestMethod]
        public async Task AlgoNoFunciona_ConImagenCopiada_OfreceAdjuntarlaAlAviso()
        {
            A.CallTo(() => portapapeles.HayImagen()).Returns(true);
            A.CallTo(() => portapapeles.LeerImagenPng()).Returns(new byte[] { 9 });
            var vm = AbrirVm();

            await vm.AbrirOCerrarIncidencia();

            StringAssert.EndsWith(preguntas.Single(), "a tu aviso?");
            Assert.IsTrue(vm.TieneImagenSugerencia);
        }

        [TestMethod]
        public async Task EnviarAviso_MandaIncidenciaConLaPantallaYLaEnseñaArriba()
        {
            A.CallTo(() => servicio.AvisarAlgoNoFunciona(A<string>._, A<byte[]>._, A<string>._))
                .Returns(Task.FromResult(I(558, "Se queda colgado al guardar")));
            var vm = AbrirVm("PlantillaVenta");
            await vm.AbrirOCerrarIncidencia();
            vm.TextoSugerencia = " Se queda colgado al guardar ";

            await vm.EnviarSugerencia();

            A.CallTo(() => servicio.AvisarAlgoNoFunciona("Se queda colgado al guardar", null, "PlantillaVenta")).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.Sugerir(A<string>._, A<byte[]>._)).MustNotHaveHappened();
            Assert.AreEqual(558, vm.Novedades[0].Id);
            Assert.IsTrue(vm.Novedades[0].EsIncidencia);
            Assert.IsFalse(vm.FormularioSugerenciaAbierto);
            StringAssert.Contains(vm.MensajeSugerencias, "Gracias por avisar");
        }

        [TestMethod]
        public async Task EnviarSugerencia_EnModoSugerencia_SigueSiendoSugerencia()
        {
            A.CallTo(() => servicio.Sugerir(A<string>._, A<byte[]>._))
                .Returns(Task.FromResult(new NovedadUsuario { Id = 60, Titulo = "Duplicar", Categoria = "Nuevo" }));
            var vm = AbrirVm("PlantillaVenta");
            await vm.AbrirOCerrarSugerencia();
            vm.TextoSugerencia = "Duplicar";

            await vm.EnviarSugerencia();

            A.CallTo(() => servicio.Sugerir("Duplicar", null)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.AvisarAlgoNoFunciona(A<string>._, A<byte[]>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void Incidencia_EnLaLista_SeDistingueYEnseñaElContextoSiLlega()
        {
            var item = new NovedadItem(I(558, "Se queda colgado", "Versión: Nesto 1.10.33.0"), servicio, null, null);
            var sinContexto = new NovedadItem(I(559, "Otra"), servicio, null, null);

            Assert.IsTrue(item.EsIncidencia);
            StringAssert.StartsWith(item.SugeridaPor, "Avisado por Alfredo");
            Assert.IsTrue(item.TieneContexto);
            Assert.IsFalse(sinContexto.TieneContexto, "a los usuarios la API no les manda el contexto");
        }

        // ---- La pantalla activa (AbridorNovedades) ----

        private sealed class PlantillaVentaView { }

        [TestMethod]
        public async Task AbridorNovedades_PasaLaPantallaActivaSinElSufijoView()
        {
            var dialogos = A.Fake<IDialogService>();
            IDialogParameters parametros = null;
            A.CallTo(() => dialogos.ShowDialog(A<string>._, A<IDialogParameters>._, A<Action<IDialogResult>>._))
                .Invokes((string nombre, IDialogParameters p, Action<IDialogResult> cb) => parametros = p);
            A.CallTo(() => servicio.ObtenerNovedades(null)).Returns(Task.FromResult(new List<NovedadUsuario>()));
            var regionManager = A.Fake<IRegionManager>();
            var region = A.Fake<IRegion>();
            var vistas = A.Fake<IViewsCollection>();
            A.CallTo(() => regionManager.Regions.ContainsRegionWithName("MainRegion")).Returns(true);
            A.CallTo(() => regionManager.Regions["MainRegion"]).Returns(region);
            A.CallTo(() => region.ActiveViews).Returns(vistas);
            A.CallTo(() => vistas.GetEnumerator()).ReturnsLazily(() => new List<object> { new PlantillaVentaView() }.GetEnumerator());

            await new AbridorNovedades(servicio, dialogos, regionManager).Abrir();

            Assert.AreEqual("PlantillaVenta", parametros.GetValue<string>(NovedadesDialogViewModel.PARAMETRO_PANTALLA));
        }

        [TestMethod]
        public async Task AbridorNovedades_SinRegionManager_NoPasaPantalla()
        {
            var dialogos = A.Fake<IDialogService>();
            IDialogParameters parametros = null;
            A.CallTo(() => dialogos.ShowDialog(A<string>._, A<IDialogParameters>._, A<Action<IDialogResult>>._))
                .Invokes((string nombre, IDialogParameters p, Action<IDialogResult> cb) => parametros = p);

            await new AbridorNovedades(servicio, dialogos).Abrir();

            Assert.IsFalse(parametros.ContainsKey(NovedadesDialogViewModel.PARAMETRO_PANTALLA));
        }
    }
}
