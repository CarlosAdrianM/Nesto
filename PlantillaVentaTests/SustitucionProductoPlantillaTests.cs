using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// NestoAPI#581: al meter en la plantilla un producto que Compras pide sustituir («servid la 45685 en lugar de la
    /// 25539»), se avisa sin bloquear y, si el vendedor quiere, las unidades pasan al sustituto con un clic.
    /// </summary>
    [TestClass]
    public class SustitucionProductoPlantillaTests
    {
        private static SustitucionProductoDTO Sustitucion() => new SustitucionProductoDTO
        {
            Id = 7,
            Producto = "25539",
            ProductoSustituto = "45685",
            NombreSustituto = "PANTALON PRESOTERAPIA COD040308",
            Vigente = true,
            Aviso = "Compras pide servir la 45685 (PANTALON PRESOTERAPIA COD040308) en lugar de la 25539 mientras no haya stock."
        };

        private static (PlantillaVentaViewModel vm, IServicioDialogos dialogos, IServicioSustitucionesProducto sustituciones, IPlantillaVentaService servicio)
            CrearViewModel(bool acepta, SustitucionProductoDTO sustitucion)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            // Una dirección que no contesta: el precio y el stock que se piden al actualizar la línea fallan dentro de su Try
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(acepta);
            IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
            var vm = new PlantillaVentaViewModel(A.Fake<IUnityContainer>(), A.Fake<IServicioNavegacion>(), configuracion, servicio,
                new WeakReferenceMessenger(), dialogos, A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "34867", contacto = "0", iva = "G21", cifNif = "12345678A" };

            IServicioSustitucionesProducto sustituciones = A.Fake<IServicioSustitucionesProducto>();
            A.CallTo(() => sustituciones.LeerVigente(A<string>._, A<string>._, A<int>._)).Returns(Task.FromResult<SustitucionProductoDTO>(null));
            A.CallTo(() => sustituciones.LeerVigente("1", "25539", A<int>._)).Returns(Task.FromResult(sustitucion));
            vm.ComprobadorSustitucion = new ComprobadorSustitucionProducto(sustituciones);
            return (vm, dialogos, sustituciones, servicio);
        }

        private static LineaPlantillaVenta Linea(PlantillaVentaViewModel vm, string producto, int cantidad, int oferta = 0)
        {
            var linea = new LineaPlantillaVenta { producto = producto, texto = "PANTALON PRESOTERAPIA", cantidad = cantidad, cantidadOferta = oferta, precio = 10M, cantidadVendida = 3 };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);
            return linea;
        }

        [TestMethod]
        public async Task Acepta_LasUnidadesPasanAlSustitutoQueYaEstaEnLaPlantilla()
        {
            var (vm, dialogos, _, _) = CrearViewModel(true, Sustitucion());
            LineaPlantillaVenta original = Linea(vm, "25539", 100, 2);
            LineaPlantillaVenta sustituto = Linea(vm, "45685", 0);

            await vm.ComprobarSustitucionAsync(original);

            Assert.AreEqual(0, original.cantidad);
            Assert.AreEqual(0, original.cantidadOferta);
            Assert.AreEqual(100, sustituto.cantidad);
            Assert.AreEqual(2, sustituto.cantidadOferta);
            A.CallTo(() => dialogos.ShowConfirmationAnswer(ComprobadorSustitucionProducto.TITULO,
                A<string>.That.Contains("¿Poner la 45685 en su lugar?"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Acepta_ElSustitutoNoEstaEnLaPlantilla_SeBuscaYSeAnade()
        {
            var (vm, _, _, servicio) = CrearViewModel(true, Sustitucion());
            LineaPlantillaVenta original = Linea(vm, "25539", 100);
            A.CallTo(() => servicio.BuscarLineaProducto("1", "45685"))
                .Returns(Task.FromResult(new LineaPlantillaVenta { producto = "45685", texto = "PANTALON PRESOTERAPIA COD040308", precio = 12M }));

            await vm.ComprobarSustitucionAsync(original);

            LineaPlantillaVenta nueva = vm.ListaFiltrableProductos.ListaOriginal.OfType<LineaPlantillaVenta>().SingleOrDefault(l => l.producto == "45685");
            Assert.IsNotNull(nueva, "El sustituto entra en la plantilla");
            Assert.AreEqual(100, nueva.cantidad);
            Assert.AreEqual(0, original.cantidad);
        }

        [TestMethod]
        public async Task Acepta_PeroElSustitutoNoSeEncuentra_AvisaYNoTocaNada()
        {
            var (vm, dialogos, _, servicio) = CrearViewModel(true, Sustitucion());
            LineaPlantillaVenta original = Linea(vm, "25539", 100);
            A.CallTo(() => servicio.BuscarLineaProducto("1", "45685")).Returns(Task.FromResult<LineaPlantillaVenta>(null));

            await vm.ComprobarSustitucionAsync(original);

            Assert.AreEqual(100, original.cantidad);
            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("45685"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Rechaza_NoCambiaNada_YNoVuelveAPreguntarPorEseProducto()
        {
            var (vm, dialogos, _, _) = CrearViewModel(false, Sustitucion());
            LineaPlantillaVenta original = Linea(vm, "25539", 100);

            await vm.ComprobarSustitucionAsync(original);
            original.cantidad = 120;
            await vm.ComprobarSustitucionAsync(original);

            Assert.AreEqual(120, original.cantidad);
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SinSustitucion_NiSePregunta()
        {
            var (vm, dialogos, _, _) = CrearViewModel(true, null);
            LineaPlantillaVenta original = Linea(vm, "25539", 100);

            await vm.ComprobarSustitucionAsync(original);

            Assert.AreEqual(100, original.cantidad);
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void AlMeterElProducto_SePreguntaConLaCantidad()
        {
            var (vm, dialogos, sustituciones, _) = CrearViewModel(false, Sustitucion());
            LineaPlantillaVenta original = Linea(vm, "25539", 100, 5);

            vm.cmdActualizarProductosPedido.Execute(original);

            A.CallTo(() => sustituciones.LeerVigente("1", "25539", 105)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(ComprobadorSustitucionProducto.TITULO, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AlQuitarElProducto_NoSePregunta()
        {
            var (vm, _, sustituciones, _) = CrearViewModel(false, Sustitucion());
            LineaPlantillaVenta original = Linea(vm, "25539", 0);

            vm.cmdActualizarProductosPedido.Execute(original);

            A.CallTo(() => sustituciones.LeerVigente(A<string>._, A<string>._, A<int>._)).MustNotHaveHappened();
        }
    }
}
