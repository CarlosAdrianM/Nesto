using ControlesUsuario.Notificaciones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ControlesUsuario.Tests
{
    /// <summary>Sugerencia 1367: el panel de la campana se abre grande (como Novedades) y se redimensiona con la esquina, sin bajar del mínimo.</summary>
    [TestClass]
    public class CampanaTamanoTests
    {
        [TestMethod]
        public void SeAbreComoLaVentanaDeNovedades()
        {
            Assert.AreEqual(760, CampanaNotificaciones.ANCHO_INICIAL);
            Assert.IsTrue(CampanaNotificaciones.ALTO_LISTA_INICIAL >= 460, "Más alta que antes");
        }

        [TestMethod]
        public void Redimensionar_CreceYNoBajaDelMinimo()
        {
            Assert.AreEqual((860d, 660d), CampanaNotificaciones.Redimensionar(760, 560, 100, 100));
            Assert.AreEqual((CampanaNotificaciones.ANCHO_MINIMO, CampanaNotificaciones.ALTO_LISTA_MINIMO),
                CampanaNotificaciones.Redimensionar(760, 560, -1000, -1000));
        }
    }
}
