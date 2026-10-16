using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using Newtonsoft.Json;

namespace Producto.Tests
{
    /// <summary>
    /// NestoAPI#628 (ficha = Excel, Carlos 16/10/26): «en Nesto sería bueno que con el tooltip sobre la cantidad nos diga cómo la ha
    /// calculado». La API manda, por almacén, la explicación, la fórmula y la acción de lo sugerido; la ficha lo enseña en el
    /// tooltip del mínimo sugerido (el de ALG) y del máximo sugerido de cada almacén. Una API vieja no lo manda: sin tooltip.
    /// </summary>
    [TestClass]
    public class ProductoViewModelControlStockTooltipTests
    {
        private const string PRODUCTO = "42274";

        private const string EXPLICACION_ALG = "Maduro. 6 meses: 1,7 uds/mes, ponderada 1,4 × 1,04 estacional; proveedor 20 días + 7 de seguridad; " +
            "múltiplos de 12 (se respetan al pedir, no en el máximo); objetivo 90 días → máximo 3, mínimo 2 (hoy 2/1).";
        private const string EXPLICACION_REI = "Media de 24 meses en REI: 24 uds, 1,0 uds/mes; seguridad 7 + 2 × 20 días del proveedor = 47 días " +
            "→ máximo 1,0 × 47 ÷ 30 = 1,57, redondeado hacia arriba 2.";

        // Lo que devuelve hoy GET api/ControlesStock?productoId=42274 (contrato aditivo de NestoAPI#628)
        private static readonly string JSON_NUEVO = JsonConvert.SerializeObject(new
        {
            ProductoId = PRODUCTO,
            StockMinimoActual = 1,
            StockMinimoCalculado = 2,
            ControlesStocksAlmacen = new object[]
            {
                new { Almacen = "ALG", StockMaximoActual = 2, StockMaximoCalculado = 3, Multiplos = 12, YaExiste = true,
                    Explicacion = EXPLICACION_ALG, Formula = "B: ponderada × (7 + 2 × días)", Accion = "Ajustar máx/mín", CalculadoComoExcel = true },
                new { Almacen = "REI", StockMaximoActual = 1, StockMaximoCalculado = 2, Multiplos = 1, YaExiste = true,
                    Explicacion = EXPLICACION_REI, Formula = "Media de 24 meses × (7 + 2 × días)", Accion = (string)null, CalculadoComoExcel = false },
                new { Almacen = "ALC", StockMaximoActual = 0, StockMaximoCalculado = 0, Multiplos = 1, YaExiste = false }
            }
        });

        // Lo que devolvía la API antes de NestoAPI#628
        private static readonly string JSON_VIEJO = JsonConvert.SerializeObject(new
        {
            ProductoId = PRODUCTO,
            StockMinimoActual = 1,
            StockMinimoCalculado = 4,
            ControlesStocksAlmacen = new object[]
            {
                new { Almacen = "ALG", StockMaximoActual = 2, StockMaximoCalculado = 7, Multiplos = 12, YaExiste = true },
                new { Almacen = "REI", StockMaximoActual = 1, StockMaximoCalculado = 2, Multiplos = 1, YaExiste = true }
            }
        });

        private static async Task<ProductoViewModel> CargarFicha(string json)
        {
            var servicio = A.Fake<IProductoService>();
            A.CallTo(() => servicio.LeerProducto(PRODUCTO)).Returns(new ProductoModel { Producto = PRODUCTO, Nombre = "APRAISE", Estado = Constantes.Productos.Estados.EN_STOCK });
            A.CallTo(() => servicio.LeerVariantes(A<string>._)).Returns(new List<VarianteModel>());
            A.CallTo(() => servicio.LeerControlStock(PRODUCTO)).ReturnsLazily(() => JsonConvert.DeserializeObject<ControlStockProductoModel>(json));
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS)).Returns(true);
            var vm = new ProductoViewModel(A.Fake<IServicioNavegacion>(), configuracion, servicio,
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IServicioAutenticacion>());
            await vm.CargarProducto(PRODUCTO);
            return vm;
        }

        private static ControlStockAlmacenWrapper Almacen(ProductoViewModel vm, string almacen)
            => vm.ControlStock.ControlesStocksAlmacen.Single(c => c.Model.Almacen == almacen);

        [TestMethod]
        public async Task CargarProducto_TooltipDelMinimoSugerido_EsLaExplicacionDelExcelEnAlg()
        {
            ProductoViewModel vm = await CargarFicha(JSON_NUEVO);

            Assert.AreEqual(2, vm.ControlStock.StockMinimoCalculado);
            Assert.IsTrue(vm.ControlStock.TieneTooltipStockMinimoSugerido);
            Assert.AreEqual("Calculado como en el Excel de propuestas de stock.\nFórmula: B: ponderada × (7 + 2 × días)\n" +
                "Acción propuesta: Ajustar máx/mín\n" + EXPLICACION_ALG, vm.ControlStock.TooltipStockMinimoSugerido);
        }

        [TestMethod]
        public async Task CargarProducto_TooltipDelMaximoSugerido_PorAlmacen()
        {
            ProductoViewModel vm = await CargarFicha(JSON_NUEVO);

            ControlStockAlmacenWrapper alg = Almacen(vm, "ALG");
            Assert.AreEqual(3, alg.StockMaximoCalculado);
            Assert.AreEqual(vm.ControlStock.TooltipStockMinimoSugerido, alg.TooltipStockMaximoSugerido);

            ControlStockAlmacenWrapper rei = Almacen(vm, "REI");
            Assert.IsTrue(rei.TieneTooltipStockMaximoSugerido);
            Assert.AreEqual("Fórmula: Media de 24 meses × (7 + 2 × días)\n" + EXPLICACION_REI, rei.TooltipStockMaximoSugerido);

            ControlStockAlmacenWrapper alc = Almacen(vm, "ALC");
            Assert.IsNull(alc.TooltipStockMaximoSugerido);
            Assert.IsFalse(alc.TieneTooltipStockMaximoSugerido);
        }

        [TestMethod]
        public async Task CargarProducto_ApiVieja_SinTooltipYSinRomper()
        {
            ProductoViewModel vm = await CargarFicha(JSON_VIEJO);

            Assert.AreEqual(4, vm.ControlStock.StockMinimoCalculado);
            Assert.IsNull(vm.ControlStock.TooltipStockMinimoSugerido);
            Assert.IsFalse(vm.ControlStock.TieneTooltipStockMinimoSugerido);
            Assert.AreEqual(7, Almacen(vm, "ALG").StockMaximoCalculado);
            Assert.IsTrue(vm.ControlStock.ControlesStocksAlmacen.All(c => !c.TieneTooltipStockMaximoSugerido));
        }

        [TestMethod]
        public void FichaSinControlDeStock_SinTooltip()
        {
            var vacio = new ControlStockProductoWrapper(new ControlStockProductoModel());

            Assert.IsNull(vacio.TooltipStockMinimoSugerido);
            Assert.IsFalse(vacio.TieneTooltipStockMinimoSugerido);
        }
    }
}
