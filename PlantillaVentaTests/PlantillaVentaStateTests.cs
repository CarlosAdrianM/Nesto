using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PlantillaVenta;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Tests para PlantillaVentaState
    /// Issue #296: ComentarioPicking editado por el usuario debe usarse en ToPedidoVentaDTO
    /// </summary>
    [TestClass]
    public class PlantillaVentaStateTests
    {
        [TestMethod]
        public void ToPedidoVentaDTO_ComentarioPickingEditado_UsaValorEditadoNoElDelCliente()
        {
            // Arrange
            var state = new PlantillaVentaState();
            state.Empresa = "1";
            state.Cliente = "10001";
            state.Contacto = "0";
            state.ComentarioPickingCliente = "Comentario original del cliente";
            state.ComentarioPicking = "Comentario editado por el usuario";

            // Act
            var pedido = state.ToPedidoVentaDTO(
                "DIR",
                () => 1,
                () => "NV"
            );

            // Assert
            Assert.AreEqual("Comentario editado por el usuario", pedido.comentarioPicking);
        }

        [TestMethod]
        public void ToPedidoVentaDTO_ComentarioPickingNoEditado_UsaValorInicializado()
        {
            // Arrange: simula el flujo normal donde ambas propiedades se inicializan igual
            var state = new PlantillaVentaState();
            state.Empresa = "1";
            state.Cliente = "10001";
            state.Contacto = "0";
            state.ComentarioPickingCliente = "Comentario del cliente";
            state.ComentarioPicking = "Comentario del cliente";

            // Act
            var pedido = state.ToPedidoVentaDTO(
                "DIR",
                () => 1,
                () => "NV"
            );

            // Assert
            Assert.AreEqual("Comentario del cliente", pedido.comentarioPicking);
        }

        [TestMethod]
        public void ToPedidoVentaDTO_ComentarioPickingNulo_DevuelveNulo()
        {
            // Arrange: cliente sin comentarioPicking
            var state = new PlantillaVentaState();
            state.Empresa = "1";
            state.Cliente = "10001";
            state.Contacto = "0";
            state.ComentarioPickingCliente = null;
            state.ComentarioPicking = null;

            // Act
            var pedido = state.ToPedidoVentaDTO(
                "DIR",
                () => 1,
                () => "NV"
            );

            // Assert
            Assert.IsNull(pedido.comentarioPicking);
        }
    }
}
