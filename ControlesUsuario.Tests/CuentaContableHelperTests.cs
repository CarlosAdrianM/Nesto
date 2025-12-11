using ControlesUsuario.Behaviors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Tests para CuentaContableHelper - Issue #258
    /// Verifica la expansión y validación de cuentas contables con formato abreviado.
    /// </summary>
    [TestClass]
    public class CuentaContableHelperTests
    {
        #region ExpandirCuenta Tests

        [TestMethod]
        public void ExpandirCuenta_ConPunto_ExpandeCorrectamente()
        {
            // Arrange & Act & Assert
            Assert.AreEqual("57200013", CuentaContableHelper.ExpandirCuenta("572.13"));
            Assert.AreEqual("43000001", CuentaContableHelper.ExpandirCuenta("4300.1"));
            Assert.AreEqual("57000000", CuentaContableHelper.ExpandirCuenta("57."));
            Assert.AreEqual("40000001", CuentaContableHelper.ExpandirCuenta("4.1"));
        }

        [TestMethod]
        public void ExpandirCuenta_SinPunto_YaCompleta_DevuelveSinCambios()
        {
            // Arrange
            var cuentaCompleta = "57200013";

            // Act
            var resultado = CuentaContableHelper.ExpandirCuenta(cuentaCompleta);

            // Assert
            Assert.AreEqual("57200013", resultado);
        }

        [TestMethod]
        public void ExpandirCuenta_SinPunto_Corta_RellenaCerosALaDerecha()
        {
            // Arrange & Act & Assert
            Assert.AreEqual("57200000", CuentaContableHelper.ExpandirCuenta("572"));
            Assert.AreEqual("43000000", CuentaContableHelper.ExpandirCuenta("43"));
            Assert.AreEqual("40000000", CuentaContableHelper.ExpandirCuenta("4"));
        }

        [TestMethod]
        public void ExpandirCuenta_Vacia_DevuelveVacio()
        {
            Assert.AreEqual(string.Empty, CuentaContableHelper.ExpandirCuenta(""));
            Assert.AreEqual(string.Empty, CuentaContableHelper.ExpandirCuenta(null));
            Assert.AreEqual(string.Empty, CuentaContableHelper.ExpandirCuenta("   "));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ExpandirCuenta_ExcedeLongitud_LanzaExcepcion()
        {
            // Cuenta que excede 8 dígitos
            CuentaContableHelper.ExpandirCuenta("123456789");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ExpandirCuenta_ConPunto_ExcedeLongitud_LanzaExcepcion()
        {
            // 5 + 4 = 9 dígitos, excede el límite de 8
            CuentaContableHelper.ExpandirCuenta("12345.6789");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ExpandirCuenta_MultiplePuntos_LanzaExcepcion()
        {
            CuentaContableHelper.ExpandirCuenta("572.13.45");
        }

        [TestMethod]
        public void ExpandirCuenta_ConEspacios_LosTrimea()
        {
            Assert.AreEqual("57200013", CuentaContableHelper.ExpandirCuenta("  572.13  "));
            Assert.AreEqual("57200013", CuentaContableHelper.ExpandirCuenta("57200013 "));
        }

        #endregion

        #region TryExpandirCuenta Tests

        [TestMethod]
        public void TryExpandirCuenta_CuentaValida_DevuelveTrue()
        {
            // Arrange
            string resultado;

            // Act
            var exito = CuentaContableHelper.TryExpandirCuenta("572.13", out resultado);

            // Assert
            Assert.IsTrue(exito);
            Assert.AreEqual("57200013", resultado);
        }

        [TestMethod]
        public void TryExpandirCuenta_CuentaInvalida_DevuelveFalse()
        {
            // Arrange
            string resultado;

            // Act
            var exito = CuentaContableHelper.TryExpandirCuenta("123456789", out resultado);

            // Assert
            Assert.IsFalse(exito);
            Assert.IsNull(resultado);
        }

        [TestMethod]
        public void TryExpandirCuenta_MultiplePuntos_DevuelveFalse()
        {
            // Arrange
            string resultado;

            // Act
            var exito = CuentaContableHelper.TryExpandirCuenta("57.2.13", out resultado);

            // Assert
            Assert.IsFalse(exito);
            Assert.IsNull(resultado);
        }

        #endregion

        #region EsCuentaValida Tests

        [TestMethod]
        public void EsCuentaValida_CuentaCorrecta_DevuelveTrue()
        {
            Assert.IsTrue(CuentaContableHelper.EsCuentaValida("57200013"));
            Assert.IsTrue(CuentaContableHelper.EsCuentaValida("43000001"));
            Assert.IsTrue(CuentaContableHelper.EsCuentaValida("00000000"));
        }

        [TestMethod]
        public void EsCuentaValida_LongitudIncorrecta_DevuelveFalse()
        {
            Assert.IsFalse(CuentaContableHelper.EsCuentaValida("572"));
            Assert.IsFalse(CuentaContableHelper.EsCuentaValida("123456789"));
        }

        [TestMethod]
        public void EsCuentaValida_ConLetras_DevuelveFalse()
        {
            Assert.IsFalse(CuentaContableHelper.EsCuentaValida("5720001A"));
            Assert.IsFalse(CuentaContableHelper.EsCuentaValida("ABCDEFGH"));
        }

        [TestMethod]
        public void EsCuentaValida_VaciaONula_DevuelveFalse()
        {
            Assert.IsFalse(CuentaContableHelper.EsCuentaValida(""));
            Assert.IsFalse(CuentaContableHelper.EsCuentaValida(null));
            Assert.IsFalse(CuentaContableHelper.EsCuentaValida("   "));
        }

        [TestMethod]
        public void EsCuentaValida_ConEspacios_LosTrimea()
        {
            Assert.IsTrue(CuentaContableHelper.EsCuentaValida("  57200013  "));
        }

        #endregion

        #region AbreviarCuenta Tests

        [TestMethod]
        public void AbreviarCuenta_ConCerosEnMedio_Abrevia()
        {
            Assert.AreEqual("572.13", CuentaContableHelper.AbreviarCuenta("57200013"));
            // El código abrevia al primer grupo de ceros consecutivos
            // "43000001" tiene ceros desde posición 2, así que queda "43.1"
            Assert.AreEqual("43.1", CuentaContableHelper.AbreviarCuenta("43000001"));
        }

        [TestMethod]
        public void AbreviarCuenta_TerminaEnCeros_AbreviaConPunto()
        {
            Assert.AreEqual("572.", CuentaContableHelper.AbreviarCuenta("57200000"));
            Assert.AreEqual("43.", CuentaContableHelper.AbreviarCuenta("43000000"));
        }

        [TestMethod]
        public void AbreviarCuenta_SinCerosEnMedio_DevuelveSinCambios()
        {
            // Cuenta sin ceros en medio no se puede abreviar
            Assert.AreEqual("12345678", CuentaContableHelper.AbreviarCuenta("12345678"));
        }

        [TestMethod]
        public void AbreviarCuenta_VaciaONula_DevuelveVacio()
        {
            Assert.AreEqual(string.Empty, CuentaContableHelper.AbreviarCuenta(""));
            Assert.AreEqual(string.Empty, CuentaContableHelper.AbreviarCuenta(null));
        }

        [TestMethod]
        public void AbreviarCuenta_LongitudIncorrecta_DevuelveSinCambios()
        {
            Assert.AreEqual("572", CuentaContableHelper.AbreviarCuenta("572"));
            Assert.AreEqual("123456789", CuentaContableHelper.AbreviarCuenta("123456789"));
        }

        #endregion

        #region Casos de uso reales

        [TestMethod]
        public void CasoReal_CuentaBanco_Expande()
        {
            // 572 = Bancos, .13 = subcuenta 13
            Assert.AreEqual("57200013", CuentaContableHelper.ExpandirCuenta("572.13"));
        }

        [TestMethod]
        public void CasoReal_CuentaClientes_Expande()
        {
            // 430 = Clientes, .12345 = cliente 12345
            Assert.AreEqual("43012345", CuentaContableHelper.ExpandirCuenta("430.12345"));
        }

        [TestMethod]
        public void CasoReal_CuentaProveedores_Expande()
        {
            // 400 = Proveedores
            Assert.AreEqual("40000123", CuentaContableHelper.ExpandirCuenta("400.123"));
        }

        [TestMethod]
        public void CasoReal_ExpandirYAbreviar_Simetrico()
        {
            // Expandir y abreviar es simétrico cuando la abreviatura usa
            // el primer grupo de ceros (comportamiento del helper)
            var original = "572.13";
            var expandida = CuentaContableHelper.ExpandirCuenta(original);
            var abreviada = CuentaContableHelper.AbreviarCuenta(expandida);

            Assert.AreEqual(original, abreviada);
            Assert.AreEqual("57200013", expandida);
        }

        #endregion
    }
}
