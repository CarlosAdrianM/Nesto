using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Models;
using Nesto.Modulos.PedidoCompra.ViewModels;

namespace PedidoCompraTests
{
    /// <summary>
    /// Nesto#490 (4C.4): desde el detalle del pedido de compra, «cargar producto» abre la ficha del producto.
    /// Escrita contra el IRegionManager de Prism ANTES de migrar la navegación (d7b3b306); al pasar a
    /// IServicioNavegacion solo ha cambiado el tipo del fake: mismas regiones, vistas y claves.
    /// </summary>
    [TestClass]
    public class DetallePedidoCompraViewModelNavegacionTests
    {
        private IServicioNavegacion _navegacion;
        private IPedidoCompraService _servicio;
        private DetallePedidoCompraViewModel _vm;

        [TestInitialize]
        public void Inicializar()
        {
            _navegacion = A.Fake<IServicioNavegacion>();
            _servicio = A.Fake<IPedidoCompraService>();
            _vm = new DetallePedidoCompraViewModel(_servicio, A.Fake<IServicioDialogos>(), _navegacion, null,
                A.Fake<IConfiguracion>(), new WeakReferenceMessenger(), A.Fake<IServicioAutenticacion>());
        }

        [TestMethod]
        public void CargarProducto_AbreLaFichaDelProductoDeLaLinea()
        {
            ParametrosNavegacion recibidos = null;
            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "ProductoView", A<ParametrosNavegacion>._))
                .Invokes((string _, string _, ParametrosNavegacion p) => recibidos = p);

            _vm.CargarProductoCommand.Execute(new LineaPedidoCompraWrapper(new LineaPedidoCompraDTO { Producto = "38093" }, _servicio));

            Assert.IsNotNull(recibidos);
            Assert.AreEqual("38093", recibidos.GetValue<string>("numeroProductoParameter"));
        }

        [TestMethod]
        public void CargarProducto_SinLinea_NoNavega()
        {
            _vm.CargarProductoCommand.Execute(null);

            A.CallTo(_navegacion).Where(c => c.Method.Name == "RequestNavigate").MustNotHaveHappened();
        }
    }
}
