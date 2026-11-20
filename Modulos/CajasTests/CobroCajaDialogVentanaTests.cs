using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Xml.Linq;

namespace CajasTests
{
    /// <summary>
    /// Sugerencia 1400 (Reina, 20/11/26): la ventana del cobro en caja de la factura abría con el tamaño por defecto de los diálogos
    /// (tope de 600 de ancho) y cortaba los importes y «Contabilizar». Debe traer su propio estilo de ventana. La vista no se puede
    /// crear en un test (su SelectorCliente necesita el contenedor), así que se lee el XAML.
    /// </summary>
    [TestClass]
    public class CobroCajaDialogVentanaTests
    {
        private static Dictionary<string, string> SettersDelEstiloDeVentana([CallerFilePath] string estaRuta = null)
        {
            string xaml = Path.Combine(Path.GetDirectoryName(estaRuta), "..", "Cajas", "Views", "CobroCajaDialog.xaml");
            XElement estilo = XDocument.Load(xaml).Root.Elements().Single(e => e.Name.LocalName == "DialogoNesto.EstiloVentana")
                .Elements().Single(e => e.Name.LocalName == "Style");
            Assert.AreEqual("Window", estilo.Attribute("TargetType").Value);
            return estilo.Elements().Where(e => e.Name.LocalName == "Setter")
                .ToDictionary(e => e.Attribute("Property").Value, e => e.Attribute("Value").Value);
        }

        [TestMethod]
        public void CobroCajaDialog_TraeSuTamanoDeVentana_SinElTopeDeLosDialogos()
        {
            Dictionary<string, string> setters = SettersDelEstiloDeVentana();

            Assert.AreEqual("Manual", setters["SizeToContent"]);
            Assert.AreEqual("CanResize", setters["ResizeMode"]);
            double ancho = double.Parse(setters["Width"], CultureInfo.InvariantCulture);
            double alto = double.Parse(setters["Height"], CultureInfo.InvariantCulture);
            Assert.IsTrue(ancho >= 1000 && ancho <= 1280, "el contenido necesita unos 1000 de ancho y tiene que caber en 1280 (tiendas)");
            Assert.IsTrue(alto >= 620 && alto <= 1024 - 40, "y de alto, con la barra de tareas");
            Assert.IsFalse(setters.ContainsKey("MaxWidth"), "sin tope de ancho");
        }

        [TestMethod]
        public void CobroCajaDialog_ElEstiloSoloFijaPropiedadesQueAdmitenEstilo()
        {
            // WindowStartupLocation, p. ej., no es una propiedad de dependencia: en un Setter revienta al abrir la ventana.
            foreach (string propiedad in SettersDelEstiloDeVentana().Keys)
            {
                Assert.IsNotNull(DependencyPropertyDescriptor.FromName(propiedad, typeof(Window), typeof(Window)),
                    $"«{propiedad}» no se puede fijar en el estilo de una ventana");
            }
        }
    }
}
