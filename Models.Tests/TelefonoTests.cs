using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Models;

namespace Models.Tests
{
    [TestClass]
    public class TelefonoTests
    {
        [TestMethod]
        public void Telefono_SiTieneUnTelefonoFijo_LoDevuelve()
        {
            Telefono telefono = new Telefono("916281914");

            Assert.AreEqual("916281914", telefono.FijoUnico());
        }

        [TestMethod]
        public void Telefono_SiTieneDosTelefonosFijos_DevuelveElPrimero()
        {
            Telefono telefono = new Telefono("916281914/915311923");

            Assert.AreEqual("916281914", telefono.FijoUnico());
        }

        [TestMethod]
        public void Telefono_SiTieneParentesisYGuiones_LosElimina()
        {
            Telefono telefono = new Telefono("(91)628-19-14/915311923");

            Assert.AreEqual("916281914", telefono.FijoUnico());
        }

        [TestMethod]
        public void Telefono_SiTieneMasDeNueveDigitos_DevuelveLosPrimerosNueve()
        {
            Telefono telefono = new Telefono("916281914915311923");

            Assert.AreEqual("916281914", telefono.FijoUnico());
        }

        [TestMethod]
        public void Telefono_MovilUnico_DevuelveElPrimerMovil()
        {
            Telefono telefono = new Telefono("916281914/616546878");

            Assert.AreEqual("616546878", telefono.MovilUnico());
        }

        [TestMethod]
        public void Telefono_SiPasamosUnMovil_DevuelveCadenaVacia()
        {
            Telefono telefono = new Telefono(null);

            Assert.AreEqual("", telefono.MovilUnico());
        }
    }
}
