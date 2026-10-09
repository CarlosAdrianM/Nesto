using FakeItEasy;
using CommunityToolkit.Mvvm.Messaging;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// NestoAPI#581: pestaña «Sustitución» de la ficha. Compras da de alta «servid la 45685 en lugar de la 25539» (caso
    /// real: pedido 927489 del 30/09/26) y todo el mundo la ve en la ficha.
    /// </summary>
    [TestClass]
    public class ProductoViewModelSustitucionTests
    {
        private const string PANTALON = "25539";

        private static SustitucionProductoDTO Activa(bool vigente = true, string estado = "Vigente") => new SustitucionProductoDTO
        {
            Id = 7,
            Producto = PANTALON,
            ProductoSustituto = "45685",
            MientrasNoHayaStock = true,
            Vigente = vigente,
            Estado = estado,
            Aviso = "Compras pide servir la 45685 (PANTALON PRESOTERAPIA COD040308) en lugar de la 25539 mientras no haya stock."
        };

        private static SustitucionProductoDTO Anulada() => new SustitucionProductoDTO
        {
            Id = 3,
            Producto = PANTALON,
            ProductoSustituto = "17404",
            Estado = "Anulada",
            FechaAnulacion = new DateTime(2026, 9, 30)
        };

        private static async Task<(ProductoViewModel vm, IServicioSustitucionesProducto sustituciones, IServicioDialogos dialogos)> CrearViewModel(
            bool esDeCompras = true, List<SustitucionProductoDTO> lista = null)
        {
            var servicio = A.Fake<IProductoService>();
            A.CallTo(() => servicio.LeerProducto(PANTALON)).Returns(new ProductoModel { Producto = PANTALON, Nombre = "PANTALON PRESOTERAPIA LOTE RT" });
            A.CallTo(() => servicio.LeerVariantes(A<string>._)).Returns(new List<VarianteModel>());
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS)).Returns(esDeCompras);
            var dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);
            var vm = new ProductoViewModel(A.Fake<IServicioNavegacion>(), configuracion, servicio,
                new WeakReferenceMessenger(), dialogos, A.Fake<IServicioAutenticacion>());
            var sustituciones = A.Fake<IServicioSustitucionesProducto>();
            A.CallTo(() => sustituciones.Listar(A<string>._, PANTALON)).Returns(lista ?? new List<SustitucionProductoDTO>());
            vm.ServicioSustituciones = sustituciones;
            await vm.CargarProducto(PANTALON);
            return (vm, sustituciones, dialogos);
        }

        [TestMethod]
        public async Task AlCargar_LaActivaSeVeEnLaFichaConSuAviso()
        {
            var (vm, _, _) = await CrearViewModel(lista: new List<SustitucionProductoDTO> { Activa(), Anulada() });

            Assert.AreEqual(2, vm.Sustituciones.Count);
            Assert.IsTrue(vm.HaySustitucionActiva);
            Assert.AreEqual(7, vm.SustitucionActiva.Id);
            Assert.AreEqual(Activa().Aviso, vm.TextoSustitucionActiva);
        }

        [TestMethod]
        public async Task AlCargar_SiAhoraNoAvisa_DiceElPorque()
        {
            var (vm, _, _) = await CrearViewModel(lista: new List<SustitucionProductoDTO> { Activa(false, "Sin efecto: hay stock") });

            StringAssert.EndsWith(vm.TextoSustitucionActiva, "(ahora no avisa: sin efecto: hay stock)");
        }

        [TestMethod]
        public async Task AlCargar_SoloAnuladas_NoHayActiva_YLaApiSinEndpointTampocoRompe()
        {
            var (vm, _, _) = await CrearViewModel(lista: new List<SustitucionProductoDTO> { Anulada() });
            var (vmSinEndpoint, sustituciones, dialogos) = await CrearViewModel();
            A.CallTo(() => sustituciones.Listar(A<string>._, PANTALON)).Returns(Task.FromResult<List<SustitucionProductoDTO>>(null));
            await vmSinEndpoint.CargarSustitucionesAsync();

            Assert.IsFalse(vm.HaySustitucionActiva);
            Assert.AreEqual(0, vmSinEndpoint.Sustituciones.Count);
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Guardar_MandaElAltaConLoQueHayEnElFormulario_YRecarga()
        {
            var (vm, sustituciones, _) = await CrearViewModel();
            vm.NuevoSustituto = " 45685 ";
            vm.NuevoMotivoSustitucion = "El proveedor tarda";
            vm.NuevaSustitucionFechaHasta = new DateTime(2026, 10, 31);

            Assert.IsTrue(vm.GuardarSustitucionCommand.CanExecute(null));
            await vm.GuardarSustitucionAsync();

            A.CallTo(() => sustituciones.Crear(PANTALON, A<NuevaSustitucionProductoDTO>.That.Matches(n =>
                n.ProductoSustituto == "45685" && n.Motivo == "El proveedor tarda" && n.MientrasNoHayaStock
                && n.FechaHasta == new DateTime(2026, 10, 31) && n.Empresa == Constantes.Empresas.EMPRESA_DEFECTO))).MustHaveHappenedOnceExactly();
            A.CallTo(() => sustituciones.Listar(A<string>._, PANTALON)).MustHaveHappenedTwiceExactly();
            Assert.IsNull(vm.NuevoSustituto, "El formulario se vacía");
            Assert.IsTrue(vm.NuevaSustitucionMientrasNoHayaStock);
        }

        [TestMethod]
        public async Task Guardar_SinStockNiFecha_NoSePuede()
        {
            var (vm, _, _) = await CrearViewModel();
            vm.NuevoSustituto = "45685";
            vm.NuevaSustitucionMientrasNoHayaStock = false;

            Assert.IsFalse(vm.GuardarSustitucionCommand.CanExecute(null));
            vm.NuevaSustitucionFechaHasta = new DateTime(2026, 10, 31);
            Assert.IsTrue(vm.GuardarSustitucionCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task SinSerDeCompras_NoSeTocaNada()
        {
            var (vm, _, _) = await CrearViewModel(esDeCompras: false, lista: new List<SustitucionProductoDTO> { Activa() });
            vm.NuevoSustituto = "45685";

            Assert.IsFalse(vm.PuedeEditarSustituciones);
            Assert.IsFalse(vm.GuardarSustitucionCommand.CanExecute(null));
            Assert.IsFalse(vm.AnularSustitucionCommand.CanExecute(null));
            Assert.IsTrue(vm.HaySustitucionActiva, "Pero la ve en la ficha");
        }

        [TestMethod]
        public async Task Anular_PideConfirmacion_YAnulaLaActiva()
        {
            var (vm, sustituciones, dialogos) = await CrearViewModel(lista: new List<SustitucionProductoDTO> { Activa() });

            Assert.IsTrue(vm.AnularSustitucionCommand.CanExecute(null));
            await vm.AnularSustitucionAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync("Anular sustitución", A<string>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => sustituciones.Anular(Constantes.Empresas.EMPRESA_DEFECTO, PANTALON, 7)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Guardar_ConUnaActiva_PreguntaAntesDeCambiarla()
        {
            var (vm, sustituciones, dialogos) = await CrearViewModel(lista: new List<SustitucionProductoDTO> { Activa() });
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(false);
            vm.NuevoSustituto = "17404";

            await vm.GuardarSustitucionAsync();

            A.CallTo(() => sustituciones.Crear(A<string>._, A<NuevaSustitucionProductoDTO>._)).MustNotHaveHappened();
        }
    }
}
