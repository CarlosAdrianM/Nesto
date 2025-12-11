using ControlesUsuario.Models;
using ControlesUsuario.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Tests para ProductoBehavior - Issue #258
    /// Verifica la búsqueda y validación de productos en líneas de pedido.
    /// </summary>
    [TestClass]
    public class ProductoBehaviorTests
    {
        #region ProductoDTO Tests

        [TestMethod]
        public void ProductoDTO_PropiedadesPorDefecto_SonCorrectas()
        {
            // Arrange & Act
            var dto = new ProductoDTO();

            // Assert
            Assert.IsNull(dto.Producto);
            Assert.IsNull(dto.Nombre);
            Assert.AreEqual(0m, dto.Precio);
            Assert.IsFalse(dto.AplicarDescuento);
            Assert.AreEqual(0m, dto.Descuento);
            Assert.IsNull(dto.Iva);
            Assert.AreEqual(0, dto.Stock);
            Assert.AreEqual(0, dto.CantidadReservada);
            Assert.AreEqual(0, dto.CantidadDisponible);
        }

        [TestMethod]
        public void ProductoDTO_AsignarPropiedades_FuncionaCorrectamente()
        {
            // Arrange
            var dto = new ProductoDTO
            {
                Producto = "AA-0001",
                Nombre = "Producto de prueba",
                Precio = 99.99m,
                AplicarDescuento = true,
                Descuento = 0.15m,
                Iva = "G21",
                Stock = 100,
                CantidadReservada = 10,
                CantidadDisponible = 90
            };

            // Assert
            Assert.AreEqual("AA-0001", dto.Producto);
            Assert.AreEqual("Producto de prueba", dto.Nombre);
            Assert.AreEqual(99.99m, dto.Precio);
            Assert.IsTrue(dto.AplicarDescuento);
            Assert.AreEqual(0.15m, dto.Descuento);
            Assert.AreEqual("G21", dto.Iva);
            Assert.AreEqual(100, dto.Stock);
            Assert.AreEqual(10, dto.CantidadReservada);
            Assert.AreEqual(90, dto.CantidadDisponible);
        }

        #endregion

        #region IServicioProducto Behavior Tests (usando mock inline)

        [TestMethod]
        public async Task ServicioProducto_ProductoExistente_DevuelveDTO()
        {
            // Arrange
            var servicio = new MockServicioProducto();
            servicio.ConfigurarRespuesta("1", "AA-0001", new ProductoDTO
            {
                Producto = "AA-0001",
                Nombre = "Champú Profesional",
                Precio = 15.50m,
                AplicarDescuento = true,
                Descuento = 0.10m,
                Iva = "G21"
            });

            // Act
            var resultado = await servicio.BuscarProducto("1", "AA-0001", "15191", "", 1);

            // Assert
            Assert.IsNotNull(resultado);
            Assert.AreEqual("AA-0001", resultado.Producto);
            Assert.AreEqual("Champú Profesional", resultado.Nombre);
            Assert.AreEqual(15.50m, resultado.Precio);
            Assert.IsTrue(resultado.AplicarDescuento);
            Assert.AreEqual(0.10m, resultado.Descuento);
            Assert.AreEqual("G21", resultado.Iva);
        }

        [TestMethod]
        public async Task ServicioProducto_ProductoNoExistente_DevuelveNull()
        {
            // Arrange
            var servicio = new MockServicioProducto();
            // No configuramos ningún producto

            // Act
            var resultado = await servicio.BuscarProducto("1", "NOEXISTE", "15191", "", 1);

            // Assert
            Assert.IsNull(resultado);
        }

        [TestMethod]
        public async Task ServicioProducto_DiferenteCantidad_PuedeAfectarPrecio()
        {
            // Arrange
            var servicio = new MockServicioProducto();
            servicio.ConfigurarRespuesta("1", "AA-0001", 1, new ProductoDTO
            {
                Producto = "AA-0001",
                Nombre = "Champú Profesional",
                Precio = 15.50m
            });
            servicio.ConfigurarRespuesta("1", "AA-0001", 10, new ProductoDTO
            {
                Producto = "AA-0001",
                Nombre = "Champú Profesional",
                Precio = 14.00m // Precio con descuento por cantidad
            });

            // Act
            var resultado1 = await servicio.BuscarProducto("1", "AA-0001", "15191", "", 1);
            var resultado10 = await servicio.BuscarProducto("1", "AA-0001", "15191", "", 10);

            // Assert
            Assert.AreEqual(15.50m, resultado1.Precio);
            Assert.AreEqual(14.00m, resultado10.Precio);
        }

        #endregion

        #region TipoLinea Validation Tests

        [TestMethod]
        public void DebeValidar_TipoLineaProducto_DevuelveTrue()
        {
            // Arrange
            var helper = new ProductoValidationHelper
            {
                TipoLineaActual = 1,
                TipoLineaRequerido = 1,
                SiempreActivo = false
            };

            // Act & Assert
            Assert.IsTrue(helper.DebeValidar());
        }

        [TestMethod]
        public void DebeValidar_TipoLineaCuenta_DevuelveFalse()
        {
            // Arrange
            var helper = new ProductoValidationHelper
            {
                TipoLineaActual = 2, // Cuenta contable
                TipoLineaRequerido = 1,
                SiempreActivo = false
            };

            // Act & Assert
            Assert.IsFalse(helper.DebeValidar());
        }

        [TestMethod]
        public void DebeValidar_TipoLineaTexto_DevuelveFalse()
        {
            // Arrange
            var helper = new ProductoValidationHelper
            {
                TipoLineaActual = 0, // Texto
                TipoLineaRequerido = 1,
                SiempreActivo = false
            };

            // Act & Assert
            Assert.IsFalse(helper.DebeValidar());
        }

        [TestMethod]
        public void DebeValidar_SiempreActivo_DevuelveTrue()
        {
            // Arrange
            var helper = new ProductoValidationHelper
            {
                TipoLineaActual = 0, // Texto (normalmente no validaría)
                TipoLineaRequerido = 1,
                SiempreActivo = true
            };

            // Act & Assert
            Assert.IsTrue(helper.DebeValidar());
        }

        [TestMethod]
        public void DebeValidar_TipoLineaNull_DevuelveFalse()
        {
            // Arrange
            var helper = new ProductoValidationHelper
            {
                TipoLineaActual = null,
                TipoLineaRequerido = 1,
                SiempreActivo = false
            };

            // Act & Assert
            Assert.IsFalse(helper.DebeValidar());
        }

        #endregion
    }

    #region Test Helpers

    /// <summary>
    /// Mock del servicio de productos para tests.
    /// </summary>
    internal class MockServicioProducto : IServicioProducto
    {
        private readonly System.Collections.Generic.Dictionary<string, ProductoDTO> _productos =
            new System.Collections.Generic.Dictionary<string, ProductoDTO>();

        public void ConfigurarRespuesta(string empresa, string producto, ProductoDTO dto)
        {
            var key = $"{empresa}:{producto}";
            _productos[key] = dto;
        }

        public void ConfigurarRespuesta(string empresa, string producto, short cantidad, ProductoDTO dto)
        {
            var key = $"{empresa}:{producto}:{cantidad}";
            _productos[key] = dto;
        }

        public Task<ProductoDTO> BuscarProducto(string empresa, string producto, string cliente, string contacto, short cantidad)
        {
            // Primero buscar con cantidad específica
            var keyConCantidad = $"{empresa}:{producto}:{cantidad}";
            if (_productos.TryGetValue(keyConCantidad, out var dtoConCantidad))
            {
                return Task.FromResult(dtoConCantidad);
            }

            // Si no hay, buscar sin cantidad
            var key = $"{empresa}:{producto}";
            if (_productos.TryGetValue(key, out var dto))
            {
                return Task.FromResult(dto);
            }

            return Task.FromResult<ProductoDTO>(null);
        }
    }

    /// <summary>
    /// Helper para tests de validación de TipoLinea.
    /// Extrae la lógica de DebeValidar del behavior para poder testearla sin WPF.
    /// </summary>
    internal class ProductoValidationHelper
    {
        public byte? TipoLineaActual { get; set; }
        public byte TipoLineaRequerido { get; set; } = 1;
        public bool SiempreActivo { get; set; }

        public bool DebeValidar()
        {
            if (SiempreActivo)
            {
                return true;
            }

            if (!TipoLineaActual.HasValue)
            {
                return false;
            }

            return TipoLineaActual.Value == TipoLineaRequerido;
        }
    }

    #endregion
}
