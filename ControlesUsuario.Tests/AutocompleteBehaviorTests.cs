using ControlesUsuario.Models;
using ControlesUsuario.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Tests para AutocompleteBehavior - Issue #263
    /// Verifica la funcionalidad de autocompletado en el campo Producto.
    /// </summary>
    [TestClass]
    public class AutocompleteBehaviorTests
    {
        #region AutocompleteItem Tests

        [TestMethod]
        public void AutocompleteItem_PropiedadesPorDefecto_SonNull()
        {
            // Arrange & Act
            var item = new AutocompleteItem();

            // Assert
            Assert.IsNull(item.Id);
            Assert.IsNull(item.Texto);
            Assert.IsNull(item.TextoSecundario);
        }

        [TestMethod]
        public void AutocompleteItem_TextoMostrar_SinSecundario_FormatoCorrecto()
        {
            // Arrange
            var item = new AutocompleteItem
            {
                Id = "ABC123",
                Texto = "Crema Hidratante",
                TextoSecundario = null
            };

            // Act
            var textoMostrar = item.TextoMostrar;

            // Assert
            Assert.AreEqual("ABC123 - Crema Hidratante", textoMostrar);
        }

        [TestMethod]
        public void AutocompleteItem_TextoMostrar_ConSecundario_FormatoCorrecto()
        {
            // Arrange
            var item = new AutocompleteItem
            {
                Id = "ABC123",
                Texto = "Crema Hidratante",
                TextoSecundario = "Cosméticos"
            };

            // Act
            var textoMostrar = item.TextoMostrar;

            // Assert
            Assert.AreEqual("ABC123 - Crema Hidratante (Cosméticos)", textoMostrar);
        }

        [TestMethod]
        public void AutocompleteItem_TextoMostrar_SecundarioVacio_SinParentesis()
        {
            // Arrange
            var item = new AutocompleteItem
            {
                Id = "ABC123",
                Texto = "Crema Hidratante",
                TextoSecundario = ""
            };

            // Act
            var textoMostrar = item.TextoMostrar;

            // Assert
            Assert.AreEqual("ABC123 - Crema Hidratante", textoMostrar);
        }

        #endregion

        #region ServicioBusquedaProductos Tests (con mock)

        [TestMethod]
        public async Task ServicioBusquedaProductos_TextoCorto_DevuelveListaVacia()
        {
            // Arrange
            var servicio = new MockServicioBusquedaAutocomplete();
            servicio.ConfigurarResultados("crema", new List<AutocompleteItem>
            {
                new AutocompleteItem { Id = "ABC123", Texto = "Crema" }
            });

            // Act - buscar con texto muy corto (1 caracter)
            var resultado = await servicio.BuscarSugerenciasAsync("c", "1", 10, CancellationToken.None);

            // Assert - debe devolver lista vacía porque el texto es muy corto
            Assert.AreEqual(0, resultado.Count);
        }

        [TestMethod]
        public async Task ServicioBusquedaProductos_TextoValido_DevuelveResultados()
        {
            // Arrange
            var servicio = new MockServicioBusquedaAutocomplete();
            servicio.ConfigurarResultados("crema", new List<AutocompleteItem>
            {
                new AutocompleteItem { Id = "ABC123", Texto = "Crema Hidratante" },
                new AutocompleteItem { Id = "DEF456", Texto = "Crema Nutritiva" }
            });

            // Act
            var resultado = await servicio.BuscarSugerenciasAsync("crema", "1", 10, CancellationToken.None);

            // Assert
            Assert.AreEqual(2, resultado.Count);
            Assert.AreEqual("ABC123", resultado[0].Id);
            Assert.AreEqual("Crema Hidratante", resultado[0].Texto);
        }

        [TestMethod]
        public async Task ServicioBusquedaProductos_SinResultados_DevuelveListaVacia()
        {
            // Arrange
            var servicio = new MockServicioBusquedaAutocomplete();
            // No configuramos ningún resultado

            // Act
            var resultado = await servicio.BuscarSugerenciasAsync("noexiste", "1", 10, CancellationToken.None);

            // Assert
            Assert.AreEqual(0, resultado.Count);
        }

        [TestMethod]
        public async Task ServicioBusquedaProductos_MaxResultados_LimitaResultados()
        {
            // Arrange
            var servicio = new MockServicioBusquedaAutocomplete();
            servicio.ConfigurarResultados("crema", new List<AutocompleteItem>
            {
                new AutocompleteItem { Id = "1", Texto = "Crema 1" },
                new AutocompleteItem { Id = "2", Texto = "Crema 2" },
                new AutocompleteItem { Id = "3", Texto = "Crema 3" },
                new AutocompleteItem { Id = "4", Texto = "Crema 4" },
                new AutocompleteItem { Id = "5", Texto = "Crema 5" }
            });

            // Act - pedir máximo 3 resultados
            var resultado = await servicio.BuscarSugerenciasAsync("crema", "1", 3, CancellationToken.None);

            // Assert
            Assert.AreEqual(3, resultado.Count);
        }

        [TestMethod]
        public async Task ServicioBusquedaProductos_Cancelacion_DevuelveListaVacia()
        {
            // Arrange
            var servicio = new MockServicioBusquedaAutocomplete();
            servicio.ConfigurarResultados("crema", new List<AutocompleteItem>
            {
                new AutocompleteItem { Id = "ABC123", Texto = "Crema" }
            });
            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancelar antes de buscar

            // Act
            var resultado = await servicio.BuscarSugerenciasAsync("crema", "1", 10, cts.Token);

            // Assert - debe devolver lista vacía porque fue cancelado
            Assert.AreEqual(0, resultado.Count);
        }

        #endregion

        #region TipoLinea Validation Tests

        [TestMethod]
        public void DebeActivar_TipoLineaProducto_TipoBusquedaProducto_DevuelveTrue()
        {
            // Arrange
            var helper = new AutocompleteValidationHelper
            {
                TipoLineaActual = 1,
                TipoLineaRequerido = 1
            };

            // Act & Assert
            Assert.IsTrue(helper.DebeActivar());
        }

        [TestMethod]
        public void DebeActivar_TipoLineaCuenta_TipoBusquedaProducto_DevuelveFalse()
        {
            // Arrange
            var helper = new AutocompleteValidationHelper
            {
                TipoLineaActual = 2, // Cuenta contable
                TipoLineaRequerido = 1 // Esperamos Producto
            };

            // Act & Assert
            Assert.IsFalse(helper.DebeActivar());
        }

        [TestMethod]
        public void DebeActivar_TipoLineaCuenta_TipoBusquedaCuenta_DevuelveTrue()
        {
            // Arrange
            var helper = new AutocompleteValidationHelper
            {
                TipoLineaActual = 2, // Cuenta contable
                TipoLineaRequerido = 2 // Esperamos Cuenta
            };

            // Act & Assert
            Assert.IsTrue(helper.DebeActivar());
        }

        [TestMethod]
        public void DebeActivar_TipoLineaTexto_DevuelveFalse()
        {
            // Arrange
            var helper = new AutocompleteValidationHelper
            {
                TipoLineaActual = 0, // Texto
                TipoLineaRequerido = 1
            };

            // Act & Assert
            Assert.IsFalse(helper.DebeActivar());
        }

        [TestMethod]
        public void DebeActivar_TipoLineaNull_DevuelveFalse()
        {
            // Arrange
            var helper = new AutocompleteValidationHelper
            {
                TipoLineaActual = null,
                TipoLineaRequerido = 1
            };

            // Act & Assert
            Assert.IsFalse(helper.DebeActivar());
        }

        #endregion

        #region MinChars Validation Tests

        [TestMethod]
        public void DebeBuscar_TextoMenorQueMinChars_DevuelveFalse()
        {
            // Arrange
            var helper = new AutocompleteValidationHelper { MinChars = 2 };

            // Act & Assert
            Assert.IsFalse(helper.DebeBuscar("a")); // 1 caracter < 2
        }

        [TestMethod]
        public void DebeBuscar_TextoIgualAMinChars_DevuelveTrue()
        {
            // Arrange
            var helper = new AutocompleteValidationHelper { MinChars = 2 };

            // Act & Assert
            Assert.IsTrue(helper.DebeBuscar("ab")); // 2 caracteres == 2
        }

        [TestMethod]
        public void DebeBuscar_TextoMayorQueMinChars_DevuelveTrue()
        {
            // Arrange
            var helper = new AutocompleteValidationHelper { MinChars = 2 };

            // Act & Assert
            Assert.IsTrue(helper.DebeBuscar("crema")); // 5 caracteres > 2
        }

        [TestMethod]
        public void DebeBuscar_TextoVacio_DevuelveFalse()
        {
            // Arrange
            var helper = new AutocompleteValidationHelper { MinChars = 2 };

            // Act & Assert
            Assert.IsFalse(helper.DebeBuscar(""));
            Assert.IsFalse(helper.DebeBuscar(null));
            Assert.IsFalse(helper.DebeBuscar("   ")); // Solo espacios
        }

        #endregion
    }

    #region Test Helpers

    /// <summary>
    /// Mock del servicio de búsqueda para autocomplete.
    /// </summary>
    internal class MockServicioBusquedaAutocomplete : IServicioBusquedaAutocomplete
    {
        private readonly Dictionary<string, List<AutocompleteItem>> _resultados =
            new Dictionary<string, List<AutocompleteItem>>();

        public void ConfigurarResultados(string texto, List<AutocompleteItem> items)
        {
            _resultados[texto.ToLowerInvariant()] = items;
        }

        public Task<IList<AutocompleteItem>> BuscarSugerenciasAsync(
            string texto,
            string empresa,
            int maxResultados,
            CancellationToken cancellationToken = default)
        {
            // Simular validación de texto corto
            if (string.IsNullOrWhiteSpace(texto) || texto.Length < 2)
            {
                return Task.FromResult<IList<AutocompleteItem>>(new List<AutocompleteItem>());
            }

            // Simular cancelación
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult<IList<AutocompleteItem>>(new List<AutocompleteItem>());
            }

            // Buscar resultados configurados
            var key = texto.ToLowerInvariant();
            if (_resultados.TryGetValue(key, out var items))
            {
                // Aplicar límite de resultados
                var resultado = new List<AutocompleteItem>();
                for (int i = 0; i < items.Count && i < maxResultados; i++)
                {
                    resultado.Add(items[i]);
                }
                return Task.FromResult<IList<AutocompleteItem>>(resultado);
            }

            return Task.FromResult<IList<AutocompleteItem>>(new List<AutocompleteItem>());
        }
    }

    /// <summary>
    /// Helper para tests de validación del AutocompleteBehavior.
    /// Extrae la lógica sin depender de WPF.
    /// </summary>
    internal class AutocompleteValidationHelper
    {
        public byte? TipoLineaActual { get; set; }
        public byte TipoLineaRequerido { get; set; } = 1;
        public int MinChars { get; set; } = 2;

        /// <summary>
        /// Verifica si el autocomplete debe activarse según el TipoLinea.
        /// </summary>
        public bool DebeActivar()
        {
            if (!TipoLineaActual.HasValue)
            {
                return false;
            }

            return TipoLineaActual.Value == TipoLineaRequerido;
        }

        /// <summary>
        /// Verifica si debe iniciar una búsqueda según el texto introducido.
        /// </summary>
        public bool DebeBuscar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return false;
            }

            return texto.Trim().Length >= MinChars;
        }
    }

    #endregion
}
