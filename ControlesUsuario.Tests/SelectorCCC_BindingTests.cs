using Microsoft.VisualStudio.TestTools.UnitTesting;
using FakeItEasy;
using ControlesUsuario;
using ControlesUsuario.Services;
using ControlesUsuario.Models;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.ComponentModel;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Tests para verificar que el binding TwoWay de SelectorCCC funciona correctamente.
    /// Carlos 20/11/24: Test en ROJO para probar que cambiar el CCC en el combo actualiza el pedido.
    ///
    /// PROBLEMA DETECTADO:
    /// - Al cambiar el CCC en el combo, NO se actualiza pedido.ccc
    /// - El binding TwoWay no está propagando los cambios
    ///
    /// SOLUCIÓN ESPERADA:
    /// - Cuando el usuario cambia CCCSeleccionado, debe propagarse a pedido.ccc automáticamente
    /// </summary>
    [TestClass]
    public class SelectorCCC_BindingTests
    {
        /// <summary>
        /// Test en ROJO: Verifica que cambiar CCCSeleccionado actualiza el binding TwoWay.
        /// Carlos 20/11/24: Este test debería FALLAR inicialmente, demostrando el problema.
        /// </summary>
        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("Binding")]
        [TestCategory("TwoWay")]
        public void CCCSeleccionado_AlCambiarValor_DeberiaNotificarPropertyChanged()
        {
            // Arrange
            var servicioCCC = A.Fake<IServicioCCC>();
            SelectorCCC sut = null;
            bool propertyChangedFired = false;
            string propertyName = null;

            Thread thread = new Thread(() =>
            {
                sut = new SelectorCCC(servicioCCC);

                // Suscribirse al evento PropertyChanged
                ((INotifyPropertyChanged)sut).PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(SelectorCCC.CCCSeleccionado))
                    {
                        propertyChangedFired = true;
                        propertyName = e.PropertyName;
                    }
                };

                // Act: Cambiar el valor de CCCSeleccionado
                sut.CCCSeleccionado = "1";
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert: El PropertyChanged debería haberse disparado para notificar al binding
            Assert.IsTrue(
                propertyChangedFired,
                "PropertyChanged debería dispararse cuando CCCSeleccionado cambia para que el binding TwoWay funcione"
            );
            Assert.AreEqual(
                nameof(SelectorCCC.CCCSeleccionado),
                propertyName,
                "El PropertyChanged debería ser para CCCSeleccionado"
            );
        }

        /// <summary>
        /// Test en ROJO: Simula el escenario real de cambio de CCC en el combo.
        /// Carlos 20/11/24: Este test debería FALLAR, demostrando que el binding no funciona.
        /// </summary>
        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("Binding")]
        [TestCategory("TwoWay")]
        public void CCCSeleccionado_AlCambiarDe1A2_DeberiaActualizarValor()
        {
            // Arrange
            var servicioCCC = A.Fake<IServicioCCC>();
            SelectorCCC sut = null;
            string valorFinal = null;

            Thread thread = new Thread(() =>
            {
                sut = new SelectorCCC(servicioCCC);

                // Establecer valor inicial
                sut.CCCSeleccionado = "1";

                // Act: Simular cambio de usuario en el combo (de "1" a "2")
                sut.CCCSeleccionado = "2";

                // Leer el valor final
                valorFinal = sut.CCCSeleccionado;
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert: El valor debería haber cambiado a "2"
            Assert.AreEqual(
                "2",
                valorFinal,
                "CCCSeleccionado debería cambiar de '1' a '2' cuando el usuario selecciona un CCC diferente"
            );
            Assert.IsNotNull(sut, "El control debería haberse creado correctamente");
        }

        /// <summary>
        /// Test en ROJO: Verifica que cambiar de CCC NULL a CCC con valor funciona.
        /// Carlos 20/11/24: Caso común al cambiar de "(Sin CCC)" a un CCC real.
        /// </summary>
        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("Binding")]
        [TestCategory("TwoWay")]
        public void CCCSeleccionado_AlCambiarDeNullA1_DeberiaActualizarValor()
        {
            // Arrange
            var servicioCCC = A.Fake<IServicioCCC>();
            SelectorCCC sut = null;
            string valorInicial = null;
            string valorFinal = null;

            Thread thread = new Thread(() =>
            {
                sut = new SelectorCCC(servicioCCC);

                // Estado inicial: NULL (Sin CCC)
                sut.CCCSeleccionado = null;
                valorInicial = sut.CCCSeleccionado;

                // Act: Usuario selecciona un CCC
                sut.CCCSeleccionado = "1";
                valorFinal = sut.CCCSeleccionado;
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.IsNull(valorInicial, "Valor inicial debería ser NULL");
            Assert.AreEqual(
                "1",
                valorFinal,
                "CCCSeleccionado debería cambiar de NULL a '1' cuando el usuario selecciona un CCC"
            );
        }

        /// <summary>
        /// Test en ROJO: Verifica que cambiar de CCC con valor a NULL funciona.
        /// Carlos 20/11/24: Caso común al cambiar a "(Sin CCC)".
        /// </summary>
        [TestMethod]
        [TestCategory("SelectorCCC")]
        [TestCategory("Binding")]
        [TestCategory("TwoWay")]
        public void CCCSeleccionado_AlCambiarDe1ANull_DeberiaActualizarValor()
        {
            // Arrange
            var servicioCCC = A.Fake<IServicioCCC>();
            SelectorCCC sut = null;
            string valorInicial = null;
            string valorFinal = "NOT_NULL"; // Valor centinela

            Thread thread = new Thread(() =>
            {
                sut = new SelectorCCC(servicioCCC);

                // Estado inicial: CCC = "1"
                sut.CCCSeleccionado = "1";
                valorInicial = sut.CCCSeleccionado;

                // Act: Usuario selecciona "(Sin CCC)"
                sut.CCCSeleccionado = null;
                valorFinal = sut.CCCSeleccionado;
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.AreEqual("1", valorInicial, "Valor inicial debería ser '1'");
            Assert.IsNull(
                valorFinal,
                "CCCSeleccionado debería cambiar de '1' a NULL cuando el usuario selecciona '(Sin CCC)'"
            );
        }
    }
}
