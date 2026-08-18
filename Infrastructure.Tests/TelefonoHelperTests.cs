using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Shared;
using System.Linq;

namespace Infrastructure.Tests
{
    /// <summary>
    /// Nesto#444 (TelefonoHelper): troceo de la cadena de teléfonos de la ficha del cliente para mostrarlos
    /// y copiarlos de uno en uno. Formatos calibrados contra datos reales de la tabla
    /// Clientes (18/08/26): separador habitual "/", a veces con espacios sueltos; el espacio
    /// se usa TAMBIÉN dentro de un mismo número ("91 698 57 05"), así que solo separa
    /// teléfonos cuando todos los trozos tienen entidad de teléfono completo (9+ dígitos).
    /// Lo que no se reconoce se muestra tal cual (no inventar).
    /// </summary>
    [TestClass]
    public class TelefonoHelperTests
    {
        [TestMethod]
        public void TrocearTelefonos_SeparadorBarra_DevuelveCadaTelefono()
        {
            var telefonos = TelefonoHelper.TrocearTelefonos("911234567/680396700");

            CollectionAssert.AreEqual(new[] { "911234567", "680396700" }, telefonos.ToArray());
        }

        [TestMethod]
        public void TrocearTelefonos_TresTelefonosConBarra_DevuelveLosTres()
        {
            var telefonos = TelefonoHelper.TrocearTelefonos("918922717/655771198/918917854");

            CollectionAssert.AreEqual(new[] { "918922717", "655771198", "918917854" }, telefonos.ToArray());
        }

        [TestMethod]
        public void TrocearTelefonos_BarraConEspaciosSueltos_LimpiaLosEspacios()
        {
            // Caso real: "654500287 /963391393"
            var telefonos = TelefonoHelper.TrocearTelefonos("654500287 /963391393");

            CollectionAssert.AreEqual(new[] { "654500287", "963391393" }, telefonos.ToArray());
        }

        [TestMethod]
        public void TrocearTelefonos_EspacioEntreTelefonosCompletos_LosSepara()
        {
            // Caso real: "961372311 675358196 690748242" (todos con 9+ dígitos)
            var telefonos = TelefonoHelper.TrocearTelefonos("961372311 675358196 690748242");

            CollectionAssert.AreEqual(new[] { "961372311", "675358196", "690748242" }, telefonos.ToArray());
        }

        [TestMethod]
        public void TrocearTelefonos_EspaciosDentroDeUnTelefono_NoLoTrocea()
        {
            // Casos reales: "91 698 57 05" y "353 86 381 7079" son UN teléfono cada uno
            CollectionAssert.AreEqual(new[] { "91 698 57 05" },
                TelefonoHelper.TrocearTelefonos("91 698 57 05").ToArray());
            CollectionAssert.AreEqual(new[] { "353 86 381 7079" },
                TelefonoHelper.TrocearTelefonos("353 86 381 7079").ToArray());
        }

        [TestMethod]
        public void TrocearTelefonos_PrefijoInternacionalSeparadoPorEspacio_NoLoTrocea()
        {
            // Caso real: "0041 0435348419" (el prefijo no llega a 9 dígitos)
            CollectionAssert.AreEqual(new[] { "0041 0435348419" },
                TelefonoHelper.TrocearTelefonos("0041 0435348419").ToArray());
        }

        [TestMethod]
        public void TrocearTelefonos_OtrosSeparadoresDuros_TambienSeparan()
        {
            CollectionAssert.AreEqual(new[] { "915235434", "607654464" },
                TelefonoHelper.TrocearTelefonos("915235434;607654464").ToArray());
            CollectionAssert.AreEqual(new[] { "915235434", "607654464" },
                TelefonoHelper.TrocearTelefonos("915235434,607654464").ToArray());
        }

        [TestMethod]
        public void TrocearTelefonos_FormatoDesconocido_SeMuestraTalCual()
        {
            // Caso real: "608679964-Paula Cantalejo" — el guion es ambiguo, no se trocea
            CollectionAssert.AreEqual(new[] { "608679964-Paula Cantalejo" },
                TelefonoHelper.TrocearTelefonos("608679964-Paula Cantalejo").ToArray());
            CollectionAssert.AreEqual(new[] { "+390385090415" },
                TelefonoHelper.TrocearTelefonos("+390385090415").ToArray());
        }

        [TestMethod]
        public void TrocearTelefonos_VacioONulo_DevuelveListaVacia()
        {
            Assert.AreEqual(0, TelefonoHelper.TrocearTelefonos(null).Count);
            Assert.AreEqual(0, TelefonoHelper.TrocearTelefonos("").Count);
            Assert.AreEqual(0, TelefonoHelper.TrocearTelefonos("   ").Count);
            Assert.AreEqual(0, TelefonoHelper.TrocearTelefonos(" / ").Count);
        }
    }
}
