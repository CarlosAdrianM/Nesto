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
        #region UsuarioHaModificadoComentarioPicking (Issue #297)

        [TestMethod]
        public void UsuarioHaModificadoComentarioPicking_AmbosIguales_RetornaFalse()
        {
            var state = new PlantillaVentaState();
            state.ComentarioPickingCliente = "Dejar en recepción";
            state.ComentarioPicking = "Dejar en recepción";

            Assert.IsFalse(state.UsuarioHaModificadoComentarioPicking());
        }

        [TestMethod]
        public void UsuarioHaModificadoComentarioPicking_AmbosNulos_RetornaFalse()
        {
            var state = new PlantillaVentaState();
            state.ComentarioPickingCliente = null;
            state.ComentarioPicking = null;

            Assert.IsFalse(state.UsuarioHaModificadoComentarioPicking());
        }

        [TestMethod]
        public void UsuarioHaModificadoComentarioPicking_AmbosVacios_RetornaFalse()
        {
            var state = new PlantillaVentaState();
            state.ComentarioPickingCliente = "";
            state.ComentarioPicking = "";

            Assert.IsFalse(state.UsuarioHaModificadoComentarioPicking());
        }

        [TestMethod]
        public void UsuarioHaModificadoComentarioPicking_UnoNuloOtroVacio_RetornaFalse()
        {
            var state = new PlantillaVentaState();
            state.ComentarioPickingCliente = null;
            state.ComentarioPicking = "";

            Assert.IsFalse(state.UsuarioHaModificadoComentarioPicking());
        }

        [TestMethod]
        public void UsuarioHaModificadoComentarioPicking_UsuarioHaEditado_RetornaTrue()
        {
            var state = new PlantillaVentaState();
            state.ComentarioPickingCliente = "Dejar en recepción";
            state.ComentarioPicking = "Dejar en recepción - PREGUNTAR POR MARÍA";

            Assert.IsTrue(state.UsuarioHaModificadoComentarioPicking());
        }

        [TestMethod]
        public void UsuarioHaModificadoComentarioPicking_ClienteNuloUsuarioConTexto_RetornaTrue()
        {
            var state = new PlantillaVentaState();
            state.ComentarioPickingCliente = null;
            state.ComentarioPicking = "Texto nuevo del usuario";

            Assert.IsTrue(state.UsuarioHaModificadoComentarioPicking());
        }

        #endregion

        #region ActualizarComentarioPickingAlCambiarContacto (Issue #297)

        [TestMethod]
        public void ActualizarComentarioPicking_NoMantener_ActualizaAmbos()
        {
            var state = new PlantillaVentaState();
            state.ComentarioPickingCliente = "Original";
            state.ComentarioPicking = "Original";

            state.ActualizarComentarioPickingAlCambiarContacto("Nuevo contacto", false);

            Assert.AreEqual("Nuevo contacto", state.ComentarioPickingCliente);
            Assert.AreEqual("Nuevo contacto", state.ComentarioPicking);
        }

        [TestMethod]
        public void ActualizarComentarioPicking_Mantener_SoloActualizaCliente()
        {
            var state = new PlantillaVentaState();
            state.ComentarioPickingCliente = "Original";
            state.ComentarioPicking = "Texto personalizado del usuario";

            state.ActualizarComentarioPickingAlCambiarContacto("Nuevo contacto", true);

            Assert.AreEqual("Nuevo contacto", state.ComentarioPickingCliente);
            Assert.AreEqual("Texto personalizado del usuario", state.ComentarioPicking);
        }

        [TestMethod]
        public void ActualizarComentarioPicking_NuevoContactoSinComentario_LimpiaSiNoMantiene()
        {
            var state = new PlantillaVentaState();
            state.ComentarioPickingCliente = "Original";
            state.ComentarioPicking = "Original";

            state.ActualizarComentarioPickingAlCambiarContacto(null, false);

            Assert.IsNull(state.ComentarioPickingCliente);
            Assert.IsNull(state.ComentarioPicking);
        }

        #endregion
    }
}
