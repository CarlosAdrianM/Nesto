using ControlesUsuario.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Tests para SelectorFacturas y FacturaClienteDTO.
    /// Issue #279 - SelectorFacturas
    /// </summary>
    [TestClass]
    public class SelectorFacturasTests
    {
        #region FacturaClienteDTO.EsRectificativa Tests

        [TestMethod]
        public void EsRectificativa_DocumentoEmpiezaConR_DevuelveTrue()
        {
            // Arrange
            var factura = new FacturaClienteDTO
            {
                Documento = "RV26/000123",
                Importe = 100m
            };

            // Act & Assert
            Assert.IsTrue(factura.EsRectificativa);
        }

        [TestMethod]
        public void EsRectificativa_DocumentoEmpiezaConRMinuscula_DevuelveTrue()
        {
            // Arrange
            var factura = new FacturaClienteDTO
            {
                Documento = "rv26/000123",
                Importe = 100m
            };

            // Act & Assert
            Assert.IsTrue(factura.EsRectificativa);
        }

        [TestMethod]
        public void EsRectificativa_DocumentoNormal_DevuelveFalse()
        {
            // Arrange
            var factura = new FacturaClienteDTO
            {
                Documento = "NV26/000123",
                Importe = 100m
            };

            // Act & Assert
            Assert.IsFalse(factura.EsRectificativa);
        }

        [TestMethod]
        public void EsRectificativa_ImporteNegativo_DevuelveTrue()
        {
            // Arrange
            var factura = new FacturaClienteDTO
            {
                Documento = "NV26/000123",
                Importe = -100m
            };

            // Act & Assert
            Assert.IsTrue(factura.EsRectificativa);
        }

        [TestMethod]
        public void EsRectificativa_DocumentoNulo_DevuelveFalseSiImportePositivo()
        {
            // Arrange
            var factura = new FacturaClienteDTO
            {
                Documento = null,
                Importe = 100m
            };

            // Act & Assert
            Assert.IsFalse(factura.EsRectificativa);
        }

        [TestMethod]
        public void EsRectificativa_DocumentoConEspaciosAlInicio_DevuelveTrue()
        {
            // Arrange
            var factura = new FacturaClienteDTO
            {
                Documento = "  RV26/000123",
                Importe = 100m
            };

            // Act & Assert
            Assert.IsTrue(factura.EsRectificativa);
        }

        #endregion

        #region Seleccion Tests

        [TestMethod]
        public void Seleccionada_CambiaValor_NotificaPropertyChanged()
        {
            // Arrange
            var factura = new FacturaClienteDTO();
            var propertyChangedRaised = false;
            factura.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FacturaClienteDTO.Seleccionada))
                {
                    propertyChangedRaised = true;
                }
            };

            // Act
            factura.Seleccionada = true;

            // Assert
            Assert.IsTrue(propertyChangedRaised);
            Assert.IsTrue(factura.Seleccionada);
        }

        #endregion

        #region Filtrado Tests

        [TestMethod]
        public void FiltrarFacturas_PorDocumento_DevuelveCoincidencias()
        {
            // Arrange
            var facturas = new List<FacturaClienteDTO>
            {
                new FacturaClienteDTO { Documento = "NV26/000123", Concepto = "Venta normal" },
                new FacturaClienteDTO { Documento = "NV26/000456", Concepto = "Otra venta" },
                new FacturaClienteDTO { Documento = "RV26/000789", Concepto = "Rectificativa" }
            };
            string filtro = "000123";

            // Act
            var filtradas = facturas.Where(f =>
                f.Documento?.ToLowerInvariant().Contains(filtro.ToLowerInvariant()) ?? false).ToList();

            // Assert
            Assert.AreEqual(1, filtradas.Count);
            Assert.AreEqual("NV26/000123", filtradas[0].Documento);
        }

        [TestMethod]
        public void FiltrarFacturas_PorConcepto_DevuelveCoincidencias()
        {
            // Arrange
            var facturas = new List<FacturaClienteDTO>
            {
                new FacturaClienteDTO { Documento = "NV26/000123", Concepto = "Venta normal" },
                new FacturaClienteDTO { Documento = "NV26/000456", Concepto = "Otra venta" },
                new FacturaClienteDTO { Documento = "RV26/000789", Concepto = "Rectificativa de factura" }
            };
            string filtro = "rectificativa";

            // Act
            var filtradas = facturas.Where(f =>
                f.Concepto?.ToLowerInvariant().Contains(filtro.ToLowerInvariant()) ?? false).ToList();

            // Assert
            Assert.AreEqual(1, filtradas.Count);
            Assert.AreEqual("RV26/000789", filtradas[0].Documento);
        }

        #endregion

        #region Totales Tests

        [TestMethod]
        public void CalcularTotal_FacturasSeleccionadas_SumaCorrectamente()
        {
            // Arrange
            var facturas = new List<FacturaClienteDTO>
            {
                new FacturaClienteDTO { Documento = "NV26/000123", Importe = 100m, Seleccionada = true },
                new FacturaClienteDTO { Documento = "NV26/000456", Importe = 200m, Seleccionada = false },
                new FacturaClienteDTO { Documento = "NV26/000789", Importe = 300m, Seleccionada = true }
            };

            // Act
            var total = facturas.Where(f => f.Seleccionada).Sum(f => f.Importe);
            var cantidad = facturas.Count(f => f.Seleccionada);

            // Assert
            Assert.AreEqual(400m, total);
            Assert.AreEqual(2, cantidad);
        }

        [TestMethod]
        public void CalcularTotal_ConRectificativas_SumaNegativos()
        {
            // Arrange
            var facturas = new List<FacturaClienteDTO>
            {
                new FacturaClienteDTO { Documento = "NV26/000123", Importe = 100m, Seleccionada = true },
                new FacturaClienteDTO { Documento = "RV26/000456", Importe = -50m, Seleccionada = true }
            };

            // Act
            var total = facturas.Where(f => f.Seleccionada).Sum(f => f.Importe);

            // Assert
            Assert.AreEqual(50m, total);
        }

        [TestMethod]
        public void CalcularTotal_NingunaSeleccionada_DevuelveCero()
        {
            // Arrange
            var facturas = new List<FacturaClienteDTO>
            {
                new FacturaClienteDTO { Documento = "NV26/000123", Importe = 100m, Seleccionada = false },
                new FacturaClienteDTO { Documento = "NV26/000456", Importe = 200m, Seleccionada = false }
            };

            // Act
            var total = facturas.Where(f => f.Seleccionada).Sum(f => f.Importe);

            // Assert
            Assert.AreEqual(0m, total);
        }

        #endregion

        #region Seleccionar Todas Tests

        [TestMethod]
        public void SeleccionarTodas_MarcaTodasLasFacturas()
        {
            // Arrange
            var facturas = new List<FacturaClienteDTO>
            {
                new FacturaClienteDTO { Seleccionada = false },
                new FacturaClienteDTO { Seleccionada = false },
                new FacturaClienteDTO { Seleccionada = true }
            };

            // Act
            foreach (var f in facturas)
            {
                f.Seleccionada = true;
            }

            // Assert
            Assert.IsTrue(facturas.All(f => f.Seleccionada));
        }

        [TestMethod]
        public void DeseleccionarTodas_DesmarcaTodasLasFacturas()
        {
            // Arrange
            var facturas = new List<FacturaClienteDTO>
            {
                new FacturaClienteDTO { Seleccionada = true },
                new FacturaClienteDTO { Seleccionada = true },
                new FacturaClienteDTO { Seleccionada = false }
            };

            // Act
            foreach (var f in facturas)
            {
                f.Seleccionada = false;
            }

            // Assert
            Assert.IsTrue(facturas.All(f => !f.Seleccionada));
        }

        #endregion
    }
}
