using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Models;
using Nesto.Modulos.PedidoCompra.ViewModels;
using Prism.Events;
using Prism.Regions;
using Prism.Services.Dialogs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PedidoCompraTests
{
    /// <summary>
    /// ELMAH 09/09/26 (Santiago, Nesto 1.10.25.6): al abrir un pedido de compra,
    /// CargarOfertasYDescuentos tiraba NullReferenceException como tarea no observada.
    /// </summary>
    [TestClass]
    public class DetallePedidoCompraViewModelTests
    {
        private IPedidoCompraService _servicio;

        [TestInitialize]
        public void Setup()
        {
            _servicio = A.Fake<IPedidoCompraService>();
        }

        private DetallePedidoCompraViewModel CrearVm()
        {
            return new DetallePedidoCompraViewModel(_servicio, A.Fake<IDialogService>(), A.Fake<IRegionManager>(), null,
                A.Fake<IConfiguracion>(), A.Fake<IEventAggregator>(), A.Fake<IServicioAutenticacion>());
        }

        private PedidoCompraWrapper PedidoCon(params LineaPedidoCompraDTO[] lineas)
        {
            var pedido = new PedidoCompraWrapper(new PedidoCompraDTO { Empresa = "1", Proveedor = "123", CodigoIvaProveedor = "G21" }, _servicio);
            var id = 0;
            foreach (var linea in lineas)
            {
                // Con Id 0 el wrapper la trata como línea nueva del DataGrid (le cambia el tipo y
                // consulta el producto): aquí simulamos líneas ya existentes del pedido.
                linea.Id = ++id;
                pedido.Lineas.Add(new LineaPedidoCompraWrapper(linea, _servicio) { Pedido = pedido });
            }
            return pedido;
        }

        private static LineaPedidoCompraDTO LineaProducto(string producto, int cantidad = 1, decimal precio = 10M)
        {
            return new LineaPedidoCompraDTO { TipoLinea = Constantes.LineasPedido.TiposLinea.PRODUCTO, Producto = producto, Cantidad = cantidad, PrecioUnitario = precio };
        }

        [TestMethod]
        public async Task CargarOfertasYDescuentos_ProductoQueNoVieneDeLaApi_SeSaltaYSigueConElResto()
        {
            A.CallTo(() => _servicio.LeerProducto("1", "AAA", "123", "G21")).Returns(Task.FromResult<LineaPedidoCompraDTO>(null));
            A.CallTo(() => _servicio.LeerProducto("1", "BBB", "123", "G21")).Returns(Task.FromResult(new LineaPedidoCompraDTO
            {
                Ofertas = new List<OfertaCompra>(),
                Descuentos = new List<DescuentoCantidadCompra> { new DescuentoCantidadCompra { CantidadMinima = 0, Precio = 8M } }
            }));
            var pedido = PedidoCon(LineaProducto("AAA"), LineaProducto("BBB"));

            await CrearVm().CargarOfertasYDescuentos(pedido);

            Assert.AreEqual(2, pedido.Lineas.Count, "No se toca ninguna línea");
            Assert.IsNotNull(pedido.Lineas[1].Model.Descuentos, "La segunda línea sí carga sus descuentos aunque la primera fallara");
            Assert.AreEqual(1, pedido.Lineas[1].Model.Descuentos.Count);
        }

        [TestMethod]
        public async Task CargarOfertasYDescuentos_LineaDeTextoOSinProducto_NoPreguntaALaApi()
        {
            var texto = new LineaPedidoCompraDTO { TipoLinea = Constantes.LineasPedido.TiposLinea.LINEA_TEXTO, Producto = null, Cantidad = 1 };
            var sinProducto = new LineaPedidoCompraDTO { TipoLinea = Constantes.LineasPedido.TiposLinea.PRODUCTO, Producto = "  ", Cantidad = 1 };
            var pedido = PedidoCon(texto, sinProducto);

            await CrearVm().CargarOfertasYDescuentos(pedido);

            A.CallTo(() => _servicio.LeerProducto(A<string>._, A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual(2, pedido.Lineas.Count);
        }

        [TestMethod]
        public async Task CargarOfertasYDescuentos_ProductoSinOfertasYLineaRepetidaACero_NoRevienta()
        {
            // Segunda línea del mismo producto a 0 €: el código busca ofertas en la primera para
            // fusionarlas; si la API no manda la lista (null) no puede tirar NullReferenceException.
            A.CallTo(() => _servicio.LeerProducto("1", "AAA", "123", "G21")).Returns(Task.FromResult(new LineaPedidoCompraDTO { Ofertas = null, Descuentos = null }));
            var pedido = PedidoCon(LineaProducto("AAA", cantidad: 6, precio: 10M), LineaProducto("AAA", cantidad: 1, precio: 0M));

            await CrearVm().CargarOfertasYDescuentos(pedido);

            Assert.AreEqual(2, pedido.Lineas.Count, "Sin ofertas no hay nada que fusionar: las dos líneas se quedan");
            Assert.IsNotNull(pedido.Lineas[0].Model.Ofertas, "Se normaliza a lista vacía para el resto del código");
        }
    }
}
