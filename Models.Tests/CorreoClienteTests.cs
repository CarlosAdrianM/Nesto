using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Models;
using Nesto.Models.Nesto.Models;
using System.Runtime.InteropServices;

namespace Models.Tests
{
    /// <summary>
    /// Descripción resumida de CorreoClienteTests
    /// </summary>
    [TestClass]
    public class CorreoClienteTests
    {
        //public CorreoClienteTests()
        //{
        //    //
        //    // TODO: Agregar aquí la lógica del constructor
        //    //
        //}

        //private TestContext testContextInstance;

        ///// <summary>
        /////Obtiene o establece el contexto de las pruebas que proporciona
        /////información y funcionalidad para la serie de pruebas actual.
        /////</summary>
        //public TestContext TestContext
        //{
        //    get
        //    {
        //        return testContextInstance;
        //    }
        //    set
        //    {
        //        testContextInstance = value;
        //    }
        //}

        #region Atributos de prueba adicionales
        //
        // Puede usar los siguientes atributos adicionales conforme escribe las pruebas:
        //
        // Use ClassInitialize para ejecutar el código antes de ejecutar la primera prueba en la clase
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup para ejecutar el código una vez ejecutadas todas las pruebas en una clase
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Usar TestInitialize para ejecutar el código antes de ejecutar cada prueba 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup para ejecutar el código una vez ejecutadas todas las pruebas
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void CorreoCliente_SiSoloHayUnCorreo_DevuelveEse()
        {
            List<PersonasContactoCliente> personasContactoCliente = new List<PersonasContactoCliente>
            {
                new PersonasContactoCliente
                {
                    CorreoElectrónico = "persona@servidor.com"
                }
            };
            CorreoCliente correo = new CorreoCliente(personasContactoCliente);

            string correoAgencia = correo.CorreoAgencia();

            Assert.AreEqual("persona@servidor.com", correoAgencia);
        }

        [TestMethod]
        public void CorreoCliente_SiHayVariosCorreos_DevuelveElCargo26()
        {
            List<PersonasContactoCliente> personasContactoCliente = new List<PersonasContactoCliente>
            {
                new PersonasContactoCliente
                {
                    CorreoElectrónico = "persona@servidor.com",
                    Cargo = 1
                },
                new PersonasContactoCliente
                {
                    CorreoElectrónico = "agencia@miagencia.com",
                    Cargo = 26
                }
            };
            CorreoCliente correo = new CorreoCliente(personasContactoCliente);

            string correoAgencia = correo.CorreoAgencia();

            Assert.AreEqual("agencia@miagencia.com", correoAgencia);
        }
    }
}
