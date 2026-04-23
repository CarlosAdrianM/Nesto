using ControlesUsuario.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

namespace ControlesUsuario.Tests.Converters
{
    /// <summary>
    /// Tests para <see cref="ImporteOTextoConverter"/> — NestoAPI#185 / Nesto#353.
    /// Verifica que los valores decimales se formatean como moneda salvo en el caso
    /// centinela -1 (sin tramo superior), en el que se devuelve el texto del
    /// parámetro para que el UI pueda mostrar mensajes como "No hay más saltos".
    /// </summary>
    [TestClass]
    public class ImporteOTextoConverterTests
    {
        private ImporteOTextoConverter _converter;
        private CultureInfo _spanishCulture;

        [TestInitialize]
        public void Setup()
        {
            _converter = new ImporteOTextoConverter();
            _spanishCulture = new CultureInfo("es-ES");
        }

        [TestMethod]
        public void Convert_ImportePositivo_FormateaComoMoneda()
        {
            var resultado = _converter.Convert(1234.56m, typeof(string), null, _spanishCulture);

            // "1.234,56 €" en es-ES (el separador de miles puede variar por versión
            // del CLDR, así que solo comprobamos contenido relevante).
            Assert.IsNotNull(resultado);
            var texto = resultado.ToString();
            Assert.IsTrue(texto.Contains("1"));
            Assert.IsTrue(texto.Contains("234"));
            Assert.IsTrue(texto.Contains("56"));
            Assert.IsTrue(texto.Contains("€"));
        }

        [TestMethod]
        public void Convert_ImporteCero_FormateaComoMoneda()
        {
            var resultado = _converter.Convert(0m, typeof(string), null, _spanishCulture);

            var texto = resultado.ToString();
            Assert.IsTrue(texto.Contains("0"));
            Assert.IsTrue(texto.Contains("€"));
        }

        [TestMethod]
        public void Convert_Centinela_DevuelveTextoDelParametro()
        {
            var resultado = _converter.Convert(-1m, typeof(string), "No hay más saltos", _spanishCulture);

            Assert.AreEqual("No hay más saltos", resultado);
        }

        [TestMethod]
        public void Convert_CentinelaConParametroNulo_DevuelveCadenaVacia()
        {
            // Fallback cuando alguien olvida el ConverterParameter en el binding.
            var resultado = _converter.Convert(-1m, typeof(string), null, _spanishCulture);

            Assert.AreEqual(string.Empty, resultado);
        }

        [TestMethod]
        public void Convert_ValorNoDecimal_DevuelveValorTalCual()
        {
            // Si el binding pasa algo que no es decimal (p. ej. null o string) no
            // intentamos formatearlo; WPF lo manejará por defecto.
            var resultado = _converter.Convert(null, typeof(string), "fallback", _spanishCulture);

            Assert.IsNull(resultado);
        }

        [TestMethod]
        public void ConvertBack_DevuelveValorSinTransformar()
        {
            // El converter no participa en escritura del modelo; ConvertBack es
            // pasarela para cumplir el contrato de IValueConverter.
            var resultado = _converter.ConvertBack("cualquier cosa", typeof(decimal), null, _spanishCulture);

            Assert.AreEqual("cualquier cosa", resultado);
        }
    }
}
