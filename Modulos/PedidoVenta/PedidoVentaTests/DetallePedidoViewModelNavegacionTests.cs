using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Unity;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#490 (4C.4): desde el detalle del pedido, «cargar producto» abre la ficha del producto de la línea.
    /// Escrita contra el IRegionManager de Prism ANTES de migrar la navegación (d7b3b306); al pasar a
    /// IServicioNavegacion solo ha cambiado el tipo del fake.
    /// </summary>
    [TestClass]
    public class DetallePedidoViewModelNavegacionTests
    {
        [TestMethod]
        public void CargarProducto_AbreLaFichaDelProductoDeLaLinea()
        {
            IServicioNavegacion navegacion = A.Fake<IServicioNavegacion>();
            var vm = new DetallePedidoViewModel(navegacion, A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(), new WeakReferenceMessenger(),
                A.Fake<IServicioDialogos>(), A.Fake<IUnityContainer>(), A.Fake<IServicioAutenticacion>());
            ParametrosNavegacion recibidos = null;
            A.CallTo(() => navegacion.RequestNavigate("MainRegion", "ProductoView", A<ParametrosNavegacion>._))
                .Invokes((string _, string _, ParametrosNavegacion p) => recibidos = p);

            vm.CargarProductoCommand.Execute(new LineaPedidoVentaDTO { Producto = "38093" });

            Assert.IsNotNull(recibidos);
            Assert.AreEqual("38093", recibidos.GetValue<string>("numeroProductoParameter"));
        }
    }
}
