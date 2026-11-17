using FakeItEasy;
using CommunityToolkit.Mvvm.Messaging;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Novedades 17/11/26 (Manuel, 44950): la ficha enseñaba la referencia de un producto con los máximos y mínimos de otro. Al
    /// buscar un producto mientras aún llegaba el control de stock del anterior (más lento), la respuesta vieja tapaba la nueva.
    /// </summary>
    [TestClass]
    public class ProductoViewModelCargaSolapadaTests
    {
        private const string GRANDE = "11111";
        private const string PEQUENO = "44950";

        private static ControlStockProductoModel Control(string producto, int minimo, int maximo) => new ControlStockProductoModel
        {
            ProductoId = producto,
            StockMinimoActual = minimo,
            ControlesStocksAlmacen = new List<ControlStockAlmacenModel>
            {
                new ControlStockAlmacenModel { Almacen = Constantes.Almacenes.ALMACEN_CENTRAL, StockMaximoActual = maximo, Multiplos = 1 }
            }
        };

        [TestMethod]
        public async Task CargarProducto_LaRespuestaLentaDelAnteriorLlegaDespues_LaFichaSigueConElControlDelNuevo()
        {
            var controlLentoDelGrande = new TaskCompletionSource<ControlStockProductoModel>();
            var servicio = A.Fake<IProductoService>();
            A.CallTo(() => servicio.LeerProducto(GRANDE)).Returns(new ProductoModel { Producto = GRANDE, Nombre = "GRANDE", Estado = Constantes.Productos.Estados.EN_STOCK });
            A.CallTo(() => servicio.LeerProducto(PEQUENO)).Returns(new ProductoModel { Producto = PEQUENO, Nombre = "LANA", Estado = Constantes.Productos.Estados.EN_STOCK });
            A.CallTo(() => servicio.LeerVariantes(A<string>._)).Returns(new List<VarianteModel>());
            A.CallTo(() => servicio.LeerControlStock(GRANDE)).Returns(controlLentoDelGrande.Task);
            A.CallTo(() => servicio.LeerControlStock(PEQUENO)).Returns(Control(PEQUENO, 4, 6));
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS)).Returns(true);
            var vm = new ProductoViewModel(A.Fake<IServicioNavegacion>(), configuracion, servicio,
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IServicioAutenticacion>());

            Task cargaDelGrande = vm.CargarProducto(GRANDE);
            await vm.CargarProducto(PEQUENO);
            controlLentoDelGrande.SetResult(Control(GRANDE, 295, 555));
            await cargaDelGrande;

            Assert.AreEqual(PEQUENO, vm.ProductoActual.Producto);
            Assert.AreEqual(PEQUENO, vm.ControlStock.Model.ProductoId);
            Assert.AreEqual(4, vm.ControlStock.StockMinimoActual);
            Assert.AreEqual(6, vm.ControlStock.ControlesStocksAlmacen.Single().Model.StockMaximoActual);
        }
    }
}
