using ControlesUsuario.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using System.Collections.Generic;
using System.Linq;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#372: la ventana "Qué hay de nuevo" debe mostrar SOLO una versión a la vez (la más nueva
    /// al abrir), no todas mezcladas, con navegación anterior/siguiente entre versiones.
    /// </summary>
    [TestClass]
    public class NovedadesDialogViewModelTests
    {
        private static NovedadUsuario N(string version, string titulo)
            => new NovedadUsuario { Version = version, Titulo = titulo, Categoria = "Nuevo" };

        private static NovedadesDialogViewModel CrearVm(params NovedadUsuario[] novedades)
        {
            var vm = new NovedadesDialogViewModel();
            var p = new DialogParameters();
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
        public void EnLaMasNueva_NoSePuedeIrASiguiente_PeroSiAAnterior()
        {
            var vm = CrearVm(N("1.10.7.0", "a"), N("1.10.8.0", "b"));

            Assert.IsFalse(vm.VersionSiguienteCommand.CanExecute()); // no hay una más nueva
            Assert.IsTrue(vm.VersionAnteriorCommand.CanExecute());   // sí hay una anterior
        }

        [TestMethod]
        public void VersionAnterior_MuestraLaVersionMasAntigua_YHabilitaSiguiente()
        {
            var vm = CrearVm(N("1.10.7.0", "a"), N("1.10.8.0", "b"));

            vm.VersionAnteriorCommand.Execute();

            Assert.AreEqual("Versión 1.10.7.0", vm.VersionActual);
            Assert.IsTrue(vm.VersionSiguienteCommand.CanExecute());  // ahora sí hay una más nueva
            Assert.IsFalse(vm.VersionAnteriorCommand.CanExecute());  // ya no hay más antigua
        }

        [TestMethod]
        public void SinNovedades_NoRompe()
        {
            var vm = CrearVm();

            Assert.AreEqual(0, vm.Novedades.Count);
            Assert.IsFalse(vm.VersionAnteriorCommand.CanExecute());
            Assert.IsFalse(vm.VersionSiguienteCommand.CanExecute());
        }
    }
}
