using ControlesUsuario.Behaviors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xaml.Behaviors;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Carlos 29/09/26: la cuenta del prepago (y cualquier TextBox con CuentaContableBehavior) se expande en el
    /// momento (572.13 → 57200013) y las celdas de plantilla con nuestros behaviors se editan con el teclado.
    /// </summary>
    [TestClass]
    public class EdicionCuentaContableTecladoTests
    {
        private class PrepagoFalso
        {
            public string CuentaContable { get; set; }
        }

        private const string PLANTILLA =
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
            "xmlns:i='http://schemas.microsoft.com/xaml/behaviors' " +
            "xmlns:b='clr-namespace:ControlesUsuario.Behaviors;assembly=ControlesUsuario'>{0}</DataTemplate>";

        private static (TextBox TextBox, CuentaContableBehavior Behavior, PrepagoFalso Origen) TextBoxConBehavior(bool siempreActivo = true,
            byte? tipoLineaActual = null)
        {
            PrepagoFalso origen = new PrepagoFalso();
            TextBox textBox = new TextBox { DataContext = origen };
            _ = textBox.SetBinding(TextBox.TextProperty, new Binding(nameof(PrepagoFalso.CuentaContable))); // LostFocus, como en la vista
            CuentaContableBehavior behavior = new CuentaContableBehavior { SiempreActivo = siempreActivo, TipoLineaActual = tipoLineaActual };
            Interaction.GetBehaviors(textBox).Add(behavior);
            return (textBox, behavior, origen);
        }

        [TestMethod]
        public void ExpandirTexto_CuentaAbreviada_LlegaExpandidaALaPropiedadEnlazada()
        {
            EjecutarEnSTA(() =>
            {
                var (textBox, behavior, origen) = TextBoxConBehavior();
                textBox.Text = "572.13";

                behavior.ExpandirTexto();
                textBox.GetBindingExpression(TextBox.TextProperty).UpdateSource(); // lo que hace el LostFocus

                Assert.AreEqual("57200013", textBox.Text);
                Assert.AreEqual("57200013", origen.CuentaContable, "Antes se quedaba «572.13» en el prepago");
            });
        }

        [TestMethod]
        public void ExpandirTexto_LineaDeProducto_NoTocaElTexto()
        {
            EjecutarEnSTA(() =>
            {
                var (textBox, behavior, _) = TextBoxConBehavior(siempreActivo: false, tipoLineaActual: 1);
                textBox.Text = "572.13";

                behavior.ExpandirTexto();

                Assert.AreEqual("572.13", textBox.Text);
            });
        }

        [TestMethod]
        public void ExpandirTexto_FormatoInvalido_NoTocaElTexto()
        {
            EjecutarEnSTA(() =>
            {
                var (textBox, behavior, _) = TextBoxConBehavior();
                textBox.Text = "572.13.45";

                behavior.ExpandirTexto();

                Assert.AreEqual("572.13.45", textBox.Text);
            });
        }

        [TestMethod]
        public void TieneBehaviorDeEdicion_SoloLasColumnasConNuestrosBehaviors()
        {
            EjecutarEnSTA(() =>
            {
                DataGridTemplateColumn conCuenta = Columna("<TextBox><i:Interaction.Behaviors><b:CuentaContableBehavior SiempreActivo='True'/></i:Interaction.Behaviors></TextBox>");
                DataGridTemplateColumn conAutocomplete = Columna("<Grid><TextBox><i:Interaction.Behaviors><b:AutocompleteBehavior SiempreActivo='True'/></i:Interaction.Behaviors></TextBox></Grid>");
                DataGridTemplateColumn normal = Columna("<TextBox/>");
                DataGridTemplateColumn sinPlantilla = new DataGridTemplateColumn();

                Assert.IsTrue(EdicionCeldaConTeclado.TieneBehaviorDeEdicion(conCuenta));
                Assert.IsTrue(EdicionCeldaConTeclado.TieneBehaviorDeEdicion(conAutocomplete));
                Assert.IsFalse(EdicionCeldaConTeclado.TieneBehaviorDeEdicion(normal), "Las demás columnas de plantilla siguen como estaban");
                Assert.IsFalse(EdicionCeldaConTeclado.TieneBehaviorDeEdicion(sinPlantilla));
            });
        }

        private static DataGridTemplateColumn Columna(string contenido)
            => new DataGridTemplateColumn { CellEditingTemplate = (DataTemplate)XamlReader.Parse(string.Format(PLANTILLA, contenido)) };

        private static void EjecutarEnSTA(Action action)
        {
            Exception capturada = null;
            var hilo = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { capturada = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (capturada != null)
            {
                throw new AssertFailedException(capturada.Message, capturada);
            }
        }
    }
}
