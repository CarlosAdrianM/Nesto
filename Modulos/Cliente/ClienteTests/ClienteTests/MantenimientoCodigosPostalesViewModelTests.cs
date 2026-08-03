using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using Prism.Services.Dialogs;
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
        private readonly IDialogService dialogService;

        public MantenimientoCodigosPostalesViewModelTests()
        {
            servicio = A.Fake<ICodigosPostalesService>();
            configuracion = A.Fake<IConfiguracion>();
            dialogService = A.Fake<IDialogService>();
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
            vm.AnnadirVendedorGrupoCommand.Execute(); // fila vacía sin rellenar
            A.CallTo(() => servicio.Guardar(A<CodigoPostalModel>.Ignored)).Returns(Ermesinde);

            await vm.GuardarAsync();

            A.CallTo(() => servicio.Guardar(A<CodigoPostalModel>.That.Matches(
                c => c.VendedoresGrupoProducto.Count == 1))).MustHaveHappenedOnceExactly();
        }
    }
}
