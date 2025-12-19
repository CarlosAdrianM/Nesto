using ControlesUsuario.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

namespace ControlesUsuario.Tests.Converters
{
    /// <summary>
    /// Tests para PercentageConverter - Issue #266
    /// Verifica la conversión bidireccional entre valores decimales (0.30) y porcentajes para UI ("30,00 %")
    /// El usuario escribe valores entre 0 y 100, internamente se guarda entre 0 y 1
    /// </summary>
    [TestClass]
    public class PercentageConverterTests
    {
        private PercentageConverter _converter;
        private CultureInfo _spanishCulture;

        [TestInitialize]
        public void Setup()
        {
            _converter = new PercentageConverter();
            _spanishCulture = new CultureInfo("es-ES");
        }

        #region Convert Tests (Modelo -> UI)

        [TestMethod]
        public void Convert_DecimalToPercentage_FormatsCorrectly()
        {
            // Arrange
            decimal value = 0.30m;

            // Act
            var result = _converter.Convert(value, typeof(string), null, _spanishCulture);

            // Assert
            Assert.AreEqual("30,00 %", result);
        }

        [TestMethod]
        public void Convert_ZeroValue_ReturnsZeroPercent()
        {
            // Arrange
            decimal value = 0m;

            // Act
            var result = _converter.Convert(value, typeof(string), null, _spanishCulture);

            // Assert
            Assert.AreEqual("0,00 %", result);
        }

        [TestMethod]
        public void Convert_FullValue_ReturnsHundredPercent()
        {
            // Arrange
            decimal value = 1.0m;

            // Act
            var result = _converter.Convert(value, typeof(string), null, _spanishCulture);

            // Assert
            Assert.AreEqual("100,00 %", result);
        }

        [TestMethod]
        public void Convert_FractionalValue_FormatsWithDecimals()
        {
            // Arrange
            decimal value = 0.4567m;

            // Act
            var result = _converter.Convert(value, typeof(string), null, _spanishCulture);

            // Assert
            Assert.AreEqual("45,67 %", result);
        }

        [TestMethod]
        public void Convert_NullValue_ReturnsZeroPercent()
        {
            // Act
            var result = _converter.Convert(null, typeof(string), null, _spanishCulture);

            // Assert
            Assert.AreEqual("0,00 %", result);
        }

        #endregion

        #region ConvertBack Tests (UI -> Modelo)

        [TestMethod]
        public void ConvertBack_StringNumber_DividesByHundred()
        {
            // Arrange - Usuario escribe "30"
            string value = "30";

            // Act
            var result = _converter.ConvertBack(value, typeof(decimal), null, _spanishCulture);

            // Assert - Internamente se guarda como 0.30
            Assert.AreEqual(0.30m, result);
        }

        [TestMethod]
        public void ConvertBack_StringWithPercent_RemovesPercentAndDivides()
        {
            // Arrange - Usuario escribe "30 %"
            string value = "30 %";

            // Act
            var result = _converter.ConvertBack(value, typeof(decimal), null, _spanishCulture);

            // Assert
            Assert.AreEqual(0.30m, result);
        }

        [TestMethod]
        public void ConvertBack_StringWithDecimal_ParsesCorrectly()
        {
            // Arrange - Usuario escribe "12,34" (con coma española)
            string value = "12,34";

            // Act
            var result = _converter.ConvertBack(value, typeof(decimal), null, _spanishCulture);

            // Assert
            Assert.AreEqual(0.1234m, result);
        }

        [TestMethod]
        public void ConvertBack_FormattedPercentString_ParsesCorrectly()
        {
            // Arrange - Valor formateado completo "45,00 %"
            string value = "45,00 %";

            // Act
            var result = _converter.ConvertBack(value, typeof(decimal), null, _spanishCulture);

            // Assert
            Assert.AreEqual(0.45m, result);
        }

        [TestMethod]
        public void ConvertBack_ZeroString_ReturnsZero()
        {
            // Arrange
            string value = "0";

            // Act
            var result = _converter.ConvertBack(value, typeof(decimal), null, _spanishCulture);

            // Assert
            Assert.AreEqual(0m, result);
        }

        [TestMethod]
        public void ConvertBack_HundredString_ReturnsOne()
        {
            // Arrange
            string value = "100";

            // Act
            var result = _converter.ConvertBack(value, typeof(decimal), null, _spanishCulture);

            // Assert
            Assert.AreEqual(1.0m, result);
        }

        [TestMethod]
        public void ConvertBack_NullValue_ReturnsZero()
        {
            // Act
            var result = _converter.ConvertBack(null, typeof(decimal), null, _spanishCulture);

            // Assert
            Assert.AreEqual(0m, result);
        }

        [TestMethod]
        public void ConvertBack_EmptyString_ReturnsZero()
        {
            // Act
            var result = _converter.ConvertBack("", typeof(decimal), null, _spanishCulture);

            // Assert
            Assert.AreEqual(0m, result);
        }

        [TestMethod]
        public void ConvertBack_WhitespaceString_ReturnsZero()
        {
            // Act
            var result = _converter.ConvertBack("   ", typeof(decimal), null, _spanishCulture);

            // Assert
            Assert.AreEqual(0m, result);
        }

        [TestMethod]
        public void ConvertBack_InvalidString_ReturnsZero()
        {
            // Arrange
            string value = "abc";

            // Act
            var result = _converter.ConvertBack(value, typeof(decimal), null, _spanishCulture);

            // Assert
            Assert.AreEqual(0m, result);
        }

        [TestMethod]
        public void ConvertBack_DecimalValue_ReturnsAsIs()
        {
            // Arrange - Si el valor ya es decimal (no viene del TextBox), devolverlo tal cual
            // Esto puede pasar en DataGrid cuando el binding pasa el valor del source en vez del TextBox
            decimal value = 0.45m;

            // Act
            var result = _converter.ConvertBack(value, typeof(decimal), null, _spanishCulture);

            // Assert
            Assert.AreEqual(0.45m, result);
        }

        [TestMethod]
        public void ConvertBack_DoubleValue_ReturnsAsDecimal()
        {
            // Arrange - Si el valor es double, convertirlo a decimal
            double value = 0.45;

            // Act
            var result = _converter.ConvertBack(value, typeof(decimal), null, _spanishCulture);

            // Assert
            Assert.AreEqual(0.45m, result);
        }

        #endregion

        #region Round-trip Tests

        [TestMethod]
        public void RoundTrip_ConvertThenConvertBack_PreservesValue()
        {
            // Arrange
            decimal originalValue = 0.45m;

            // Act
            var displayValue = _converter.Convert(originalValue, typeof(string), null, _spanishCulture);
            var backValue = _converter.ConvertBack(displayValue, typeof(decimal), null, _spanishCulture);

            // Assert
            Assert.AreEqual(originalValue, backValue);
        }

        [TestMethod]
        public void RoundTrip_UserTypesNumber_ConvertsCorrectly()
        {
            // Simula el flujo completo:
            // 1. Modelo tiene 0.45 (45%)
            // 2. UI muestra "45,00 %"
            // 3. Usuario borra y escribe "30"
            // 4. ConvertBack convierte "30" a 0.30
            // 5. Convert muestra "30,00 %"

            // Arrange
            decimal originalModelValue = 0.45m;
            string userInput = "30";

            // Act
            var initialDisplay = _converter.Convert(originalModelValue, typeof(string), null, _spanishCulture);
            var newModelValue = _converter.ConvertBack(userInput, typeof(decimal), null, _spanishCulture);
            var finalDisplay = _converter.Convert(newModelValue, typeof(string), null, _spanishCulture);

            // Assert
            Assert.AreEqual("45,00 %", initialDisplay);
            Assert.AreEqual(0.30m, newModelValue);
            Assert.AreEqual("30,00 %", finalDisplay);
        }

        #endregion

        #region Culture-specific Tests

        [TestMethod]
        public void ConvertBack_WithDotInSpanishCulture_TreatedAsThousandsSeparator()
        {
            // Arrange - En cultura española, el punto es separador de miles
            // "12.34" se interpreta como 1234 (doce mil trescientos cuarenta)
            string value = "12.34";

            // Act
            var result = _converter.ConvertBack(value, typeof(decimal), null, _spanishCulture);

            // Assert - 1234 / 100 = 12.34
            Assert.AreEqual(12.34m, result);
        }

        [TestMethod]
        public void ConvertBack_WithCommaInSpanishCulture_TreatedAsDecimalSeparator()
        {
            // Arrange - En cultura española, la coma es el separador decimal
            string value = "12,34";

            // Act
            var result = _converter.ConvertBack(value, typeof(decimal), null, _spanishCulture);

            // Assert - 12.34 / 100 = 0.1234
            Assert.AreEqual(0.1234m, result);
        }

        #endregion
    }
}
