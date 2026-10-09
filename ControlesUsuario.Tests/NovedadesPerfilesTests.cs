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
    /// Sugerencia 551 (Alberto Sancho): la ventana de Novedades enseña por defecto las del puesto del usuario (la
    /// API ya las manda filtradas) y el conmutador «Ver todas las novedades» trae y enseña el resto. A quién afecta cada
    /// novedad se pone por script (09/10/26): la ventana solo lo enseña.
    /// </summary>
    [TestClass]
    public class NovedadesPerfilesTests
    {
        private INovedadesService servicio;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<INovedadesService>();
            A.CallTo(() => servicio.LeerSugerencias()).Returns(Task.FromResult(new List<NovedadUsuario>()));
            A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).Returns(Task.FromResult(new List<NovedadUsuario>()));
            A.CallTo(() => servicio.ObtenerTodasLasNovedades()).Returns(Task.FromResult(new List<NovedadUsuario>
            {
                N(1, "1.10.40.0", "Almacén"),
                N(2, "1.10.40.0"),
                N(3, "1.10.40.0", "Vendedores"),
                N(4, "1.10.39.1", "Administración")
            }));
            Perfiles(new PerfilesUsuarioNovedades { Perfiles = new List<string> { "Almacén" } });
        }

        private void Perfiles(PerfilesUsuarioNovedades perfiles)
            => A.CallTo(() => servicio.LeerMisPerfiles()).Returns(Task.FromResult(perfiles));

        private static NovedadUsuario N(int id, string version, params string[] perfiles) => new NovedadUsuario
        {
            Id = id,
            Version = version,
            Titulo = "Novedad " + id,
            Categoria = "Nuevo",
            Perfiles = perfiles.Length == 0 ? null : perfiles.ToList()
        };

        /// <summary>Lo que manda la API sin ?todas=true a alguien del almacén: las suyas y las de todos.</summary>
        private NovedadesDialogViewModel Vm(params NovedadUsuario[] novedades)
        {
            var vm = new NovedadesDialogViewModel(servicio, null, _ => false);
            var parametros = new DialogParameters();
            parametros.Add("novedades", (novedades.Length == 0 ? new[] { N(1, "1.10.40.0", "Almacén"), N(2, "1.10.40.0") } : novedades).ToList());
            vm.OnDialogOpened(parametros);
            return vm;
        }

        private static List<int> Ids(NovedadesDialogViewModel vm) => vm.Novedades.Select(n => n.Id).OrderBy(i => i).ToList();

        [TestMethod]
        public void AlAbrir_ConFiltro_EnseñaElConmutadorYDeQuéPerfilesSon()
        {
            var vm = Vm();

            Assert.IsTrue(vm.MostrarConmutadorPerfiles);
            Assert.IsFalse(vm.VerTodas);
            Assert.AreEqual("Te enseñamos las novedades de Almacén y las que son para todos.", vm.TextoPerfiles);
            CollectionAssert.AreEqual(new[] { 1, 2 }, Ids(vm));
        }

        [TestMethod]
        public void AlAbrir_QuienLasVeTodas_NoTieneConmutador()
        {
            Perfiles(new PerfilesUsuarioNovedades { VeTodas = true, PuedeEditar = true });

            var vm = Vm();

            Assert.IsFalse(vm.MostrarConmutadorPerfiles);
            Assert.IsNull(vm.TextoPerfiles);
        }

        [TestMethod]
        public void AlAbrir_SiLaApiNoSabeDePerfiles_TodoComoAntes()
        {
            A.CallTo(() => servicio.LeerMisPerfiles()).ThrowsAsync(new InvalidOperationException("No se pudo saber tus perfiles de novedades (error 404)."));

            var vm = Vm();

            Assert.IsFalse(vm.MostrarConmutadorPerfiles);
            CollectionAssert.AreEqual(new[] { 1, 2 }, Ids(vm));
        }

        [TestMethod]
        public async Task VerTodas_PideLasDeTodosLosPerfilesYLasEnseña()
        {
            var vm = Vm();

            await vm.CambiarVerTodas(true);

            Assert.IsTrue(vm.VerTodas);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, Ids(vm), "La versión 1.10.40.0 con la de vendedores");
            vm.VersionAnteriorCommand.Execute(null);
            CollectionAssert.AreEqual(new[] { 4 }, Ids(vm), "Y la versión que solo tenía novedades de administración");
            A.CallTo(() => servicio.ObtenerTodasLasNovedades()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task QuitarVerTodas_LasVuelveAEsconderSinPreguntarALaApi()
        {
            var vm = Vm();
            await vm.CambiarVerTodas(true);

            await vm.CambiarVerTodas(false);
            await vm.CambiarVerTodas(true);
            await vm.CambiarVerTodas(false);

            CollectionAssert.AreEqual(new[] { 1, 2 }, Ids(vm));
            Assert.IsFalse(vm.VersionAnteriorCommand.CanExecute(null), "La versión de administración ya no se ve");
            A.CallTo(() => servicio.ObtenerTodasLasNovedades()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task QuitarVerTodas_EnUnaVersionQueSeQuedaVacia_VaALaMasNueva()
        {
            var vm = Vm();
            await vm.CambiarVerTodas(true);
            vm.VersionAnteriorCommand.Execute(null);
            Assert.AreEqual("Versión 1.10.39.1", vm.VersionActual);

            await vm.CambiarVerTodas(false);

            Assert.AreEqual("Versión 1.10.40.0", vm.VersionActual);
        }

        [TestMethod]
        public async Task IrANovedadComentario_DeOtroPerfil_LaBuscaEntreTodasYEnciendeVerTodas()
        {
            var vm = Vm();

            await vm.IrANovedadComentario(4, null);

            Assert.AreEqual(4, vm.NovedadDestacada.Id);
            Assert.IsTrue(vm.VerTodas);
            Assert.AreEqual("Versión 1.10.39.1", vm.VersionActual);
        }

        [TestMethod]
        public void SinFiltro_LasDeOtrosPerfilesQueVengan_SeVen()
        {
            // Dirección: la API ya le manda todas y no se esconde nada
            Perfiles(new PerfilesUsuarioNovedades { VeTodas = true, PuedeEditar = true });

            var vm = Vm(N(1, "1.10.40.0", "Almacén"), N(3, "1.10.40.0", "Vendedores"));

            CollectionAssert.AreEqual(new[] { 1, 3 }, Ids(vm));
        }

        // ---- A quién afecta cada novedad (solo se enseña: se pone por script) ----

        [TestMethod]
        public void TextoPerfiles_DeCadaNovedad()
        {
            var vm = Vm(N(1, "1.10.40.0", "Almacén", "Tiendas"), N(2, "1.10.40.0"));

            Assert.AreEqual("Para: Almacén y Tiendas", vm.Novedades.Single(n => n.Id == 1).Perfiles.Texto);
            Assert.IsFalse(vm.Novedades.Single(n => n.Id == 2).Perfiles.HayTexto, "Para todos no se dice");
        }

        [TestMethod]
        public void TextoPerfiles_ParaTodosNoSeDiceNiADireccion()
        {
            // Decisión de Carlos (09/10/26): los perfiles se ponen por script, como los adjuntos
            Perfiles(new PerfilesUsuarioNovedades { VeTodas = true, PuedeEditar = true });

            var vm = Vm(N(2, "1.10.40.0"));

            Assert.IsNull(vm.Novedades.Single().Perfiles.Texto);
            Assert.IsFalse(vm.Novedades.Single().Perfiles.HayTexto);
        }

        [TestMethod]
        public void TextoPerfiles_SinPerfilesNiRepetidos()
        {
            Assert.IsFalse(new PerfilesNovedadItem(null).HayTexto);
            Assert.AreEqual("Para: Almacén", new PerfilesNovedadItem(new[] { " Almacén ", "almacén", "" }).Texto);
        }

        [TestMethod]
        public void Unir_ConYAlFinal()
        {
            Assert.AreEqual("Almacén", ContextoPerfilesNovedades.Unir(new[] { "Almacén" }));
            Assert.AreEqual("Vendedores, Almacén y Tiendas", ContextoPerfilesNovedades.Unir(new[] { "Vendedores", "Almacén", "Tiendas" }));
        }
    }
}
