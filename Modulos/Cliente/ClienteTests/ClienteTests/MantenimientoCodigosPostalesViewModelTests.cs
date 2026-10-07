using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#442: mantenimiento de códigos postales (NestoAPI#378). Solo Dirección y Tienda
    /// online; permite corregir país, ruta, vendedor y vendedores por grupo de producto.
    /// </summary>
    [TestClass]
    public class MantenimientoCodigosPostalesViewModelTests
    {
        private readonly ICodigosPostalesService servicio;
        private readonly IConfiguracion configuracion;
        private readonly IServicioDialogos dialogService;

        public MantenimientoCodigosPostalesViewModelTests()
        {
            servicio = A.Fake<ICodigosPostalesService>();
            configuracion = A.Fake<IConfiguracion>();
            dialogService = A.Fake<IServicioDialogos>();
        }

        private MantenimientoCodigosPostalesViewModel CrearViewModel()
            => new(servicio, configuracion, dialogService);

        private void DarAcceso()
            => A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE)).Returns(true);

        private static CodigoPostalModel Ermesinde => new()
        {
            Empresa = "1",
            Numero = "4445-294",
            Poblacion = "ERMESINDE",
            Provincia = "PORTUGAL",
            Ruta = "00",
            Vendedor = "NV",
            Pais = null,
            VendedoresGrupoProducto = new List<VendedorGrupoProductoCodigoPostalModel>
            {
                new() { GrupoProducto = "PEL", Vendedor = "AH" }
            }
        };

        [TestMethod]
        public async Task Buscar_ConAcceso_CargaResultados()
        {
            DarAcceso();
            A.CallTo(() => servicio.Buscar("4445", null)).Returns(new List<CodigoPostalModel> { Ermesinde });
            var vm = CrearViewModel();
            vm.Filtro = "4445";

            await vm.BuscarAsync();

            Assert.AreEqual(1, vm.Resultados.Count);
            Assert.AreEqual("4445-294", vm.Resultados.Single().Numero);
        }

        [TestMethod]
        public async Task Buscar_SinAcceso_NoLlamaAlServicio()
        {
            // Ni dirección ni tienda online
            var vm = CrearViewModel();
            vm.Filtro = "4445";

            await vm.BuscarAsync();

            Assert.AreEqual(0, vm.Resultados.Count);
            A.CallTo(() => servicio.Buscar(A<string>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public void Seleccionar_RellenaLaEdicionConUnaCopia()
        {
            var vm = CrearViewModel();
            CodigoPostalModel original = Ermesinde;

            vm.Seleccionado = original;

            Assert.AreEqual("ERMESINDE", vm.PoblacionEdicion);
            Assert.AreEqual("00", vm.RutaEdicion);
            Assert.IsNull(vm.PaisEdicion);
            Assert.AreEqual(1, vm.VendedoresGrupoProducto.Count);
            // Editar la copia no toca la fila del grid hasta guardar
            vm.VendedoresGrupoProducto.First().Vendedor = "JM";
            Assert.AreEqual("AH", original.VendedoresGrupoProducto.First().Vendedor);
        }

        [TestMethod]
        public async Task Guardar_EnviaLoEditadoYRefrescaLaFila()
        {
            DarAcceso();
            var vm = CrearViewModel();
            CodigoPostalModel original = Ermesinde;
            vm.Resultados.Add(original);
            vm.Seleccionado = original;
            vm.PaisEdicion = "PT";
            vm.ProvinciaEdicion = "PORTO";
            CodigoPostalModel devuelto = Ermesinde;
            devuelto.Pais = "PT";
            devuelto.Provincia = "PORTO";
            A.CallTo(() => servicio.Guardar(A<CodigoPostalModel>.That.Matches(
                    c => c.Numero == "4445-294" && c.Pais == "PT" && c.Provincia == "PORTO")))
                .Returns(devuelto);

            await vm.GuardarAsync();

            A.CallTo(() => servicio.Guardar(A<CodigoPostalModel>.Ignored)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("PT", vm.Resultados.Single().Pais, "La fila del grid se refresca con lo guardado");
        }

        [TestMethod]
        public async Task Guardar_IgnoraVendedoresGrupoIncompletos()
        {
            DarAcceso();
            var vm = CrearViewModel();
            CodigoPostalModel original = Ermesinde;
            vm.Resultados.Add(original);
            vm.Seleccionado = original;
            vm.AnnadirVendedorGrupoCommand.Execute(null); // fila vacía sin rellenar
            A.CallTo(() => servicio.Guardar(A<CodigoPostalModel>.Ignored)).Returns(Ermesinde);

            await vm.GuardarAsync();

            A.CallTo(() => servicio.Guardar(A<CodigoPostalModel>.That.Matches(
                c => c.VendedoresGrupoProducto.Count == 1))).MustHaveHappenedOnceExactly();
        }

        // NestoAPI#596: «4430 999» y «4430-999» son el mismo código postal

        private static CodigoPostalModel Cp(string numero, string pais = "PT") => new()
        {
            Empresa = "1",
            Numero = numero,
            Poblacion = "VILA DO CONDE",
            Provincia = "PORTO",
            Ruta = "00",
            Vendedor = "NV",
            Pais = pais
        };

        [DataTestMethod]
        [DataRow("4430 999")]
        [DataRow("4430999")]
        [DataRow("4430-999")]
        public async Task Buscar_CodigoPostalPortuguesEnCualquierFormato_EncuentraLosTresYEnseniaElCanonico(string tecleado)
        {
            DarAcceso();
            A.CallTo(() => servicio.Buscar("4430", null)).Returns(new List<CodigoPostalModel>
            {
                Cp("4430-998"), Cp("4430 999"), Cp("4430-999")
            });
            var vm = CrearViewModel();
            vm.Filtro = tecleado;

            await vm.BuscarAsync();

            Assert.AreEqual("4430-999", vm.Filtro, "Lo tecleado se sustituye por el canónico");
            Assert.AreEqual(2, vm.Resultados.Count, "Solo el 4430-999, en sus dos formatos");
            Assert.IsTrue(vm.Resultados.All(r => r.NumeroCanonico == "4430-999"));
        }

        [TestMethod]
        public async Task Buscar_DuplicadoPorFormato_AvisaYMarcaElQueNoEsCanonico()
        {
            DarAcceso();
            A.CallTo(() => servicio.Buscar("4430", null)).Returns(new List<CodigoPostalModel>
            {
                Cp("4430 999"), Cp("4430-999"), Cp("4430-998")
            });
            var vm = CrearViewModel();
            vm.Filtro = "4430";

            await vm.BuscarAsync();

            Assert.IsTrue(vm.Resultados.Single(r => r.Numero == "4430 999").DuplicadoPorFormato);
            Assert.IsFalse(vm.Resultados.Single(r => r.Numero == "4430-999").DuplicadoPorFormato);
            Assert.IsFalse(vm.Resultados.Single(r => r.Numero == "4430-998").DuplicadoPorFormato);
            StringAssert.Contains(vm.AvisoFormato, "4430 999");
            StringAssert.Contains(vm.AvisoFormato, "4430-999");
        }

        [TestMethod]
        public async Task Buscar_SinDuplicados_NoAvisa()
        {
            DarAcceso();
            A.CallTo(() => servicio.Buscar("4445", null)).Returns(new List<CodigoPostalModel> { Ermesinde });
            var vm = CrearViewModel();
            vm.Filtro = "4445";

            await vm.BuscarAsync();

            Assert.IsNull(vm.AvisoFormato);
        }

        [TestMethod]
        public async Task Buscar_PrefijoEspanolDeCuatroCifras_NoSeRellenaConElCero()
        {
            // «2800» es buscar todos los 2800x de Madrid, no el 02800
            DarAcceso();
            var vm = CrearViewModel();
            vm.Filtro = "2800";

            await vm.BuscarAsync();

            A.CallTo(() => servicio.Buscar("2800", null)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("2800", vm.Filtro);
        }

        [TestMethod]
        public void NumeroCanonico_EsElFormatoDeLaApi()
        {
            Assert.AreEqual("4480-670", Cp("4480670").NumeroCanonico);
            Assert.AreEqual("4480-670", Cp("4480 670", null).NumeroCanonico);
            Assert.IsFalse(Cp("4480 670").EnFormatoCanonico);
            Assert.IsTrue(Cp("4480-670").EnFormatoCanonico);
            Assert.IsTrue(Cp("28004", "ES").EnFormatoCanonico);
        }

        [TestMethod]
        public async Task Guardar_DuplicadoPorFormato_AvisaYNoLoGuarda()
        {
            DarAcceso();
            A.CallTo(() => servicio.Buscar("4430", null)).Returns(new List<CodigoPostalModel>
            {
                Cp("4430 999"), Cp("4430-999")
            });
            var vm = CrearViewModel();
            CodigoPostalModel duplicado = Cp("4430 999");
            vm.Resultados.Add(duplicado);
            vm.Seleccionado = duplicado;

            await vm.GuardarAsync();

            A.CallTo(() => servicio.Guardar(A<CodigoPostalModel>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => dialogService.ShowError(A<string>.That.Contains("4430-999"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Guardar_NoCanonicoSinGemelo_SeGuarda()
        {
            DarAcceso();
            A.CallTo(() => servicio.Buscar("4480", null)).Returns(new List<CodigoPostalModel> { Cp("4480 670") });
            A.CallTo(() => servicio.Guardar(A<CodigoPostalModel>.Ignored)).Returns(Cp("4480 670"));
            var vm = CrearViewModel();
            CodigoPostalModel fila = Cp("4480 670");
            vm.Resultados.Add(fila);
            vm.Seleccionado = fila;

            await vm.GuardarAsync();

            A.CallTo(() => servicio.Guardar(A<CodigoPostalModel>.That.Matches(c => c.Numero == "4480 670"))).MustHaveHappenedOnceExactly();
        }
    }
}
