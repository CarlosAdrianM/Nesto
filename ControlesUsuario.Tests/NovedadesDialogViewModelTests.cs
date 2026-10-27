using ControlesUsuario.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System.Collections.Generic;
using System.Linq;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#372: la ventana "Qué hay de nuevo" debe mostrar SOLO una versión a la vez (la más nueva
    /// al abrir), no todas mezcladas, con navegación anterior/siguiente entre versiones.
    /// Nesto#549: «una versión» es la grande (Major.Minor.Build): sus revisiones (1.10.47.0, .1 y .2) salen juntas.
    /// </summary>
    [TestClass]
    public class NovedadesDialogViewModelTests
    {
        private static NovedadUsuario N(string version, string titulo)
            => new NovedadUsuario { Version = version, Titulo = titulo, Categoria = "Nuevo" };

        private static NovedadesDialogViewModel CrearVm(params NovedadUsuario[] novedades)
        {
            var vm = new NovedadesDialogViewModel();
            var p = new ParametrosDialogo();
            p.Add("novedades", novedades.ToList());
            vm.OnDialogOpened(p);
            return vm;
        }

        [TestMethod]
        public void AlAbrir_MuestraSoloLaVersionMasNueva()
        {
            var vm = CrearVm(N("1.10.6.0", "a"), N("1.10.8.0", "b"), N("1.10.7.0", "c"), N("1.10.8.0", "d"));

            Assert.AreEqual("Versión 1.10.8.0", vm.VersionActual);
            Assert.AreEqual(2, vm.Novedades.Count); // las dos de 1.10.8.0
            Assert.IsTrue(vm.Novedades.All(n => n.Version == "1.10.8.0"));
        }

        [TestMethod]
        public void LaCabeceraYElBuscadorLlevanLaFechaDeLaVersion()
        {
            // Carlos (25/09/26): para saber si un arreglo es anterior o posterior a un día concreto
            var a = N("1.10.28.2", "a"); a.Fecha = new System.DateTime(2026, 9, 17);
            var b = N("1.10.28.2", "b"); b.Fecha = new System.DateTime(2026, 9, 18); // una novedad añadida al día siguiente
            var vm = CrearVm(a, b);

            Assert.AreEqual("Versión 1.10.28.2 · 17/09/26", vm.VersionActual, "La fecha de la versión es la de su primera novedad");
            Assert.AreEqual("Versión 1.10.28.2 · 17/09/26", new ResultadoBusquedaNovedad(a).Donde);
            Assert.AreEqual("Versión 1.10.8.0", NovedadesDialogViewModel.ConFecha("Versión 1.10.8.0", default), "Sin fecha, solo el número");
        }

        [TestMethod]
        public void EnLaMasNueva_NoSePuedeIrASiguiente_PeroSiAAnterior()
        {
            var vm = CrearVm(N("1.10.7.0", "a"), N("1.10.8.0", "b"));

            Assert.IsFalse(vm.VersionSiguienteCommand.CanExecute(null)); // no hay una más nueva
            Assert.IsTrue(vm.VersionAnteriorCommand.CanExecute(null));   // sí hay una anterior
        }

        [TestMethod]
        public void VersionAnterior_MuestraLaVersionMasAntigua_YHabilitaSiguiente()
        {
            var vm = CrearVm(N("1.10.7.0", "a"), N("1.10.8.0", "b"));

            vm.VersionAnteriorCommand.Execute(null);

            Assert.AreEqual("Versión 1.10.7.0", vm.VersionActual);
            Assert.IsTrue(vm.VersionSiguienteCommand.CanExecute(null));  // ahora sí hay una más nueva
            Assert.IsFalse(vm.VersionAnteriorCommand.CanExecute(null));  // ya no hay más antigua
        }

        [TestMethod]
        public void ConVersion_AbreEnEsaVersion()
        {
            // Nesto#501: el aviso de versión nueva de la campana abre Novedades en su versión
            var vm = new NovedadesDialogViewModel();
            var p = new ParametrosDialogo
            {
                { "novedades", new List<NovedadUsuario> { N("1.10.7.0", "a"), N("1.10.8.0", "b") } },
                { NovedadesDialogViewModel.PARAMETRO_VERSION, "1.10.7.0" }
            };

            vm.OnDialogOpened(p);

            Assert.AreEqual("Versión 1.10.7.0", vm.VersionActual);
            Assert.IsTrue(vm.VersionSiguienteCommand.CanExecute(null));
        }

        [TestMethod]
        public void ConVersionSinNovedades_AbreEnLaMasNueva()
        {
            var vm = new NovedadesDialogViewModel();
            var p = new ParametrosDialogo
            {
                { "novedades", new List<NovedadUsuario> { N("1.10.7.0", "a"), N("1.10.8.0", "b") } },
                { NovedadesDialogViewModel.PARAMETRO_VERSION, "1.10.9.0" }
            };

            vm.OnDialogOpened(p);

            Assert.AreEqual("Versión 1.10.8.0", vm.VersionActual);
        }

        [TestMethod]
        public void SinNovedades_NoRompe()
        {
            var vm = CrearVm();

            Assert.AreEqual(0, vm.Novedades.Count);
            Assert.IsFalse(vm.VersionAnteriorCommand.CanExecute(null));
            Assert.IsFalse(vm.VersionSiguienteCommand.CanExecute(null));
        }

        #region Nesto#549: las revisiones de una misma versión salen juntas

        // El caso del 22/10/26: 1.10.47.0 (3 gordas), 1.10.47.1 (1) y 1.10.47.2 (1); quien venía de la 1.10.46.0 veía solo la de la .2
        private static NovedadUsuario[] TandaDeLa47()
        {
            var v46 = N("1.10.46.0", "Fecha de recepción"); v46.Fecha = new System.DateTime(2026, 10, 15);
            var a = N("1.10.47.0", "Eventos y cupones"); a.Fecha = new System.DateTime(2026, 10, 22);
            var b = N("1.10.47.0", "Cursos desde la plantilla"); b.Fecha = new System.DateTime(2026, 10, 22);
            var c = N("1.10.47.0", "Puede ofrecer en el rapport"); c.Fecha = new System.DateTime(2026, 10, 22);
            var d = N("1.10.47.1", "Klarna"); d.Fecha = new System.DateTime(2026, 10, 22);
            var e = N("1.10.47.2", "Modificar con la plantilla un pedido con picking"); e.Fecha = new System.DateTime(2026, 10, 23);
            // Desordenadas a propósito: la ventana las ordena
            return new[] { d, v46, a, e, b, c };
        }

        [TestMethod]
        public void RevisionesDeLaMismaVersion_SalenJuntasEnUnaPagina_DeLaMasNuevaALaMasAntigua()
        {
            var vm = CrearVm(TandaDeLa47());

            Assert.AreEqual("Versión 1.10.47.0 – 1.10.47.2 · 22/10/26", vm.VersionActual, "Título con el rango de revisiones y la fecha de la primera");
            Assert.AreEqual(5, vm.Novedades.Count, "Las 5 de la 1.10.47.*, todas en la primera página");
            CollectionAssert.AreEqual(
                new[] { "1.10.47.2", "1.10.47.1", "1.10.47.0", "1.10.47.0", "1.10.47.0" },
                vm.Novedades.Select(n => n.Version).ToList(), "De la revisión más nueva a la más antigua");
            CollectionAssert.AreEqual(
                new[] { "Eventos y cupones", "Cursos desde la plantilla", "Puede ofrecer en el rapport" },
                vm.Novedades.Where(n => n.Version == "1.10.47.0").Select(n => n.Titulo).ToList(), "Dentro de una revisión, el orden de la API");
            Assert.IsTrue(vm.Novedades.All(n => n.VersionVisible), "Si la página mezcla revisiones, cada tarjeta enseña la suya");
            Assert.AreEqual("1.10.47.2", vm.Novedades[0].TextoVersion);
            Assert.IsTrue(vm.VersionAnteriorCommand.CanExecute(null), "Queda la página de la 1.10.46");
        }

        [TestMethod]
        public void LaPaginaAnterior_EsLaDeLaVersionGrandeAnterior_YSinMezclaNoEnsenaLaRevision()
        {
            var vm = CrearVm(TandaDeLa47());

            vm.VersionAnteriorCommand.Execute(null);

            Assert.AreEqual("Versión 1.10.46.0 · 15/10/26", vm.VersionActual, "Con una sola revisión, el título de siempre");
            Assert.AreEqual(1, vm.Novedades.Count);
            Assert.IsFalse(vm.Novedades[0].VersionVisible, "Una sola revisión: la versión ya está en el título");
            Assert.IsFalse(vm.VersionAnteriorCommand.CanExecute(null), "Solo hay 2 páginas");
        }

        [TestMethod]
        public void ElAvisoDeLaCampanaDeUnaRevision_AbreLaPaginaDeSuVersion()
        {
            // El aviso «Nesto 1.10.47.2 ya está publicado» (AvisarNuevaVersionNesto.ps1 → AbridorNovedades.Abrir("1.10.47.2"))
            var vm = new NovedadesDialogViewModel();
            var p = new ParametrosDialogo
            {
                { "novedades", TandaDeLa47().ToList() },
                { NovedadesDialogViewModel.PARAMETRO_VERSION, "1.10.47.2" }
            };

            vm.OnDialogOpened(p);

            Assert.AreEqual("Versión 1.10.47.0 – 1.10.47.2 · 22/10/26", vm.VersionActual);
            Assert.AreEqual(5, vm.Novedades.Count, "No solo la de la .2: toda la tanda");
        }

        [TestMethod]
        public async System.Threading.Tasks.Task IrAUnaNovedadDeUnaRevision_SaltaALaPaginaDeSuVersion_YLaDestaca()
        {
            // Nesto#477 / #487: el buscador e «Ir a novedad/comentario» buscan la página que contiene la novedad
            NovedadUsuario[] tanda = TandaDeLa47();
            tanda[0].Id = 749; // Klarna, 1.10.47.1
            var vm = CrearVm(tanda);
            vm.VersionAnteriorCommand.Execute(null); // en la 1.10.46

            await vm.IrAResultado(tanda[0]);

            Assert.AreEqual("Versión 1.10.47.0 – 1.10.47.2 · 22/10/26", vm.VersionActual);
            Assert.AreEqual(749, vm.NovedadDestacada.Id);
            Assert.IsTrue(vm.Novedades.Contains(vm.NovedadDestacada));
        }

        [TestMethod]
        public void VersionesQueNoParsean_OVacias_SiguenEnSuPropioGrupo()
        {
            var vm = CrearVm(N("1.10.47.0", "a"), N("1.10.47.2", "b"), N("beta", "c"), N("", "d"), N(null, "e"));

            Assert.AreEqual("Versión 1.10.47.0 – 1.10.47.2", vm.VersionActual);
            vm.VersionAnteriorCommand.Execute(null);
            Assert.AreEqual("Versión beta", vm.VersionActual);
            Assert.AreEqual(1, vm.Novedades.Count);
            vm.VersionAnteriorCommand.Execute(null);
            Assert.AreEqual("Novedades", vm.VersionActual, "Sin versión, la página de siempre");
            Assert.AreEqual(2, vm.Novedades.Count, "La vacía y la null van juntas, como antes");
            Assert.IsFalse(vm.VersionAnteriorCommand.CanExecute(null), "3 páginas en total");
        }

        [TestMethod]
        public void ClaveGrupo_EsMajorMinorBuild()
        {
            Assert.AreEqual("1.10.47", NovedadesDialogViewModel.ClaveGrupo("1.10.47.2"));
            Assert.AreEqual("1.10.47", NovedadesDialogViewModel.ClaveGrupo(" 1.10.47.0 "));
            Assert.AreEqual("1.10.47", NovedadesDialogViewModel.ClaveGrupo("1.10.47"));
            Assert.AreEqual("2.18", NovedadesDialogViewModel.ClaveGrupo("2.18"), "Sin Build (versiones 2.x de Ariadna), tal cual");
            Assert.AreEqual("beta", NovedadesDialogViewModel.ClaveGrupo("beta"));
            Assert.AreEqual("", NovedadesDialogViewModel.ClaveGrupo(null));
        }

        [TestMethod]
        public void TituloPagina_UnaRevision_ComoSiempre_VariasConElRango()
        {
            Assert.AreEqual("Versión 1.10.8.0", NovedadesDialogViewModel.TituloPagina("1.10.8", new[] { "1.10.8.0" }, default));
            Assert.AreEqual("Versión 1.10.47.0 – 1.10.47.2 · 22/10/26",
                NovedadesDialogViewModel.TituloPagina("1.10.47", new[] { "1.10.47.2", "1.10.47.1", "1.10.47.0" }, new System.DateTime(2026, 10, 22)));
            Assert.AreEqual("Novedades", NovedadesDialogViewModel.TituloPagina("", new[] { "" }, default));
        }

        #endregion
    }
}
