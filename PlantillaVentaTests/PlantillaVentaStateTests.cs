using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PlantillaVenta;
using System.Collections.Generic;
using System.Linq;

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

        #region SuPedido / P.O. (Issue #260 / NestoAPI#58)

        [TestMethod]
        public void ToPedidoVentaDTO_SuPedidoConValor_SeIncluyeEnDTO()
        {
            // Arrange
            var state = new PlantillaVentaState();
            state.Empresa = "1";
            state.Cliente = "10001";
            state.Contacto = "0";
            state.SuPedido = "PO-2026-001";

            // Act
            var pedido = state.ToPedidoVentaDTO(
                "DIR",
                () => 1,
                () => "NV"
            );

            // Assert
            Assert.AreEqual("PO-2026-001", pedido.suPedido);
        }

        [TestMethod]
        public void ToPedidoVentaDTO_SuPedidoNulo_DevuelveNulo()
        {
            // Arrange
            var state = new PlantillaVentaState();
            state.Empresa = "1";
            state.Cliente = "10001";
            state.Contacto = "0";
            state.SuPedido = null;

            // Act
            var pedido = state.ToPedidoVentaDTO(
                "DIR",
                () => 1,
                () => "NV"
            );

            // Assert
            Assert.IsNull(pedido.suPedido);
        }

        [TestMethod]
        public void Limpiar_RestableceSuPedidoANothing()
        {
            // Arrange
            var state = new PlantillaVentaState();
            state.SuPedido = "PO-123";

            // Act
            state.Limpiar();

            // Assert
            Assert.IsNull(state.SuPedido);
        }

        [TestMethod]
        public void ToPedidoVentaDTO_PersonalizarOferta_SegundaUnidadConPrecioYDescuento()
        {
            // Nesto#371: con personalizarOferta, la 2ª línea (oferta) va a precioOferta + descuentoOferta
            // (sin aplicar descuentos del cliente: AplicarDescuento=False), en vez de gratis.
            var state = new PlantillaVentaState();
            state.Empresa = "1";
            state.Cliente = "10001";
            state.Contacto = "0";
            state.LineasProducto = new List<LineaPlantillaVenta>
            {
                new LineaPlantillaVenta
                {
                    producto = "40583",
                    texto = "Anubis",
                    cantidad = 1,
                    precio = 20.40m,
                    cantidadOferta = 1,
                    personalizarOferta = true,
                    precioOferta = 20.40m,
                    descuentoOferta = 0.5m,
                    iva = "G21"
                }
            };

            var pedido = state.ToPedidoVentaDTO("DIR", () => 1, () => "NV");

            var lineas = pedido.Lineas.Where(l => l.Producto == "40583").ToList();
            Assert.AreEqual(2, lineas.Count); // la cobrada + la 2ª unidad personalizada
            var lineaOferta = lineas.Single(l => l.DescuentoLinea == 0.5m);
            Assert.AreEqual(20.40m, lineaOferta.PrecioUnitario);
            Assert.IsFalse(lineaOferta.AplicarDescuento);
        }

        [TestMethod]
        public void ToPedidoVentaDTO_OfertaSinPersonalizar_SegundaUnidadGratis()
        {
            // Sin personalizar, la 2ª unidad (oferta) sigue yendo gratis (precio 0).
            var state = new PlantillaVentaState();
            state.Empresa = "1";
            state.Cliente = "10001";
            state.Contacto = "0";
            state.LineasProducto = new List<LineaPlantillaVenta>
            {
                new LineaPlantillaVenta
                {
                    producto = "40583",
                    texto = "Anubis",
                    cantidad = 1,
                    precio = 20.40m,
                    cantidadOferta = 1,
                    iva = "G21"
                }
            };

            var pedido = state.ToPedidoVentaDTO("DIR", () => 1, () => "NV");

            var lineaOferta = pedido.Lineas.Where(l => l.Producto == "40583").Single(l => l.PrecioUnitario == 0m);
            Assert.AreEqual(0m, lineaOferta.DescuentoLinea);
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
