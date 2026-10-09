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
    /// API ya las manda filtradas) y el conmutador «Ver todas las novedades» trae y enseña el resto. Dirección e
    /// Informática pueden cambiar a quién afecta cada novedad.
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

        // ---- Editor de a quién afecta (Dirección / Informática) ----

        [TestMethod]
        public void TextoPerfiles_DeCadaNovedad()
        {
            var vm = Vm(N(1, "1.10.40.0", "Almacén", "Tiendas"), N(2, "1.10.40.0"));

            Assert.AreEqual("Para: Almacén y Tiendas", vm.Novedades.Single(n => n.Id == 1).Perfiles.Texto);
            Assert.IsFalse(vm.Novedades.Single(n => n.Id == 2).Perfiles.HayTexto, "Para todos no se dice a quien no edita");
            Assert.IsFalse(vm.Novedades.Single(n => n.Id == 1).Perfiles.PuedeEditar);
        }

        [TestMethod]
        public async Task Editor_Direccion_GuardaLosMarcados()
        {
            Perfiles(new PerfilesUsuarioNovedades { VeTodas = true, PuedeEditar = true, Disponibles = new List<string> { "Vendedores", "Almacén", "Tiendas", "Administración" } });
            var vm = Vm(N(2, "1.10.40.0"));
            PerfilesNovedadItem perfiles = vm.Novedades.Single().Perfiles;
            Assert.IsTrue(perfiles.PuedeEditar);
            Assert.AreEqual("Para todos", perfiles.Texto);

            perfiles.EditarCommand.Execute(null);
            perfiles.Opciones.Single(o => o.Nombre == "Tiendas").Marcado = true;
            perfiles.Opciones.Single(o => o.Nombre == "Vendedores").Marcado = true;
            await perfiles.Guardar();

            A.CallTo(() => servicio.CambiarPerfiles(2, A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "Vendedores", "Tiendas" }))).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Para: Vendedores y Tiendas", perfiles.Texto);
            Assert.IsFalse(perfiles.Editando);
        }

        [TestMethod]
        public async Task Editor_TodosMarcados_EsParaTodos()
        {
            Perfiles(new PerfilesUsuarioNovedades { VeTodas = true, PuedeEditar = true });
            var vm = Vm(N(1, "1.10.40.0", "Almacén"));
            PerfilesNovedadItem perfiles = vm.Novedades.Single().Perfiles;

            perfiles.EditarCommand.Execute(null);
            foreach (OpcionPerfilNovedad opcion in perfiles.Opciones)
            {
                opcion.Marcado = true;
            }
            await perfiles.Guardar();

            A.CallTo(() => servicio.CambiarPerfiles(1, A<IEnumerable<string>>.That.IsEmpty())).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Para todos", perfiles.Texto);
        }

        [TestMethod]
        public async Task Editor_SiLaApiFalla_DiceElMotivoYSigueAbierto()
        {
            Perfiles(new PerfilesUsuarioNovedades { VeTodas = true, PuedeEditar = true });
            A.CallTo(() => servicio.CambiarPerfiles(A<int>._, A<IEnumerable<string>>._))
                .ThrowsAsync(new InvalidOperationException("No se pudo guardar a quién afecta la novedad: «X» no es un perfil."));
            var vm = Vm(N(1, "1.10.40.0", "Almacén"));
            PerfilesNovedadItem perfiles = vm.Novedades.Single().Perfiles;

            perfiles.EditarCommand.Execute(null);
            await perfiles.Guardar();

            Assert.IsTrue(perfiles.HayMensaje);
            Assert.IsTrue(perfiles.Editando);
            Assert.AreEqual("Para: Almacén", perfiles.Texto);
        }

        [TestMethod]
        public void Editor_LasSugerenciasNoLlevanPerfiles()
        {
            var contexto = new ContextoPerfilesNovedades(servicio) { MisPerfiles = new PerfilesUsuarioNovedades { VeTodas = true, PuedeEditar = true } };

            var sugerencia = new PerfilesNovedadItem(50, null, true, contexto);

            Assert.IsFalse(sugerencia.PuedeEditar);
            Assert.IsFalse(sugerencia.Mostrar);
        }

        [TestMethod]
        public void Unir_ConYAlFinal()
        {
            Assert.AreEqual("Almacén", ContextoPerfilesNovedades.Unir(new[] { "Almacén" }));
            Assert.AreEqual("Vendedores, Almacén y Tiendas", ContextoPerfilesNovedades.Unir(new[] { "Vendedores", "Almacén", "Tiendas" }));
        }
    }
}
