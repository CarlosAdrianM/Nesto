using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System.Linq;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>Nesto#647 (cola): GET api/Productos/PorCodigoBarras, escrita una sola vez para todos los servicios.</summary>
    [TestClass]
    public class ConsultaPorCodigoBarrasTests
    {
        [TestMethod]
        public void Ruta_EscapaYQuitaEspacios()
        {
            Assert.AreEqual("Productos/PorCodigoBarras?codigo=8436538394380&empresa=1", ConsultaPorCodigoBarras.Ruta(" 8436538394380 ", "1 "));
            Assert.AreEqual("Productos/PorCodigoBarras?codigo=A%26B&empresa=", ConsultaPorCodigoBarras.Ruta("A&B", null));
        }

        [TestMethod]
        public void LeerFilas_UnaPorProducto_SinRellenoNiVacias()
        {
            var filas = ConsultaPorCodigoBarras.LeerFilas(
                "[{\"Producto\":\"44967 \",\"Nombre\":\"PACK \",\"Cantidad\":1,\"Principal\":true},{\"Producto\":\"44967\",\"Cantidad\":6},null,{\"Producto\":\"\"},{\"Producto\":\"45931\",\"Nombre\":\"OTRO\"}]");

            CollectionAssert.AreEqual(new[] { "44967", "45931" }, filas.Select(f => f.Producto).ToArray());
            Assert.AreEqual("PACK", filas[0].Nombre);
        }

        [TestMethod]
        public void LeerFilas_VacioONull_ListaVacia()
        {
            Assert.AreEqual(0, ConsultaPorCodigoBarras.LeerFilas(null).Count);
            Assert.AreEqual(0, ConsultaPorCodigoBarras.LeerFilas("null").Count);
        }

        [TestMethod]
        [ExpectedException(typeof(JsonSerializationException))]
        public void LeerFilas_NoEsUnaLista_Lanza()
        {
            ConsultaPorCodigoBarras.LeerFilas("{\"Message\":\"error\"}");
        }
    }
}
