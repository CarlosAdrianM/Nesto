using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PlantillaVenta;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#550 (sugerencia 759, Paloma): el desplegable «Ampliar en el pedido» enseñaba solo números. Cada opción
    /// debe decir de qué pedido es: «928141 · del 08/10/2026 · 5 líneas · 138,10 € · entrega 09/10/2027», con
    /// «retenido» y «con picking» cuando toca, y el 0 como «Pedido nuevo».
    /// </summary>
    [TestClass]
    public class PedidoPendientePlantillaTests
    {
        private static PedidoPendientePlantilla Pedido928141() => new PedidoPendientePlantilla
        {
            Numero = 928141,
            Fecha = new DateTime(2026, 10, 8),
            NumeroLineas = 5,
            BaseImponible = 138.10M,
            FechaEntrega = new DateTime(2027, 10, 9)
        };

        [TestMethod]
        public void Describir_PedidoNormal_NumeroFechaLineasBaseYEntrega()
        {
            Assert.AreEqual("928141 · del 08/10/2026 · 5 líneas · 138,10 € · entrega 09/10/2027", PedidoPendientePlantilla.Describir(Pedido928141()));
        }

        [TestMethod]
        public void Describir_Retenido_LoDice()
        {
            var pedido = Pedido928141();
            pedido.Retenido = true;

            Assert.AreEqual("928141 · del 08/10/2026 · 5 líneas · 138,10 € · entrega 09/10/2027 · retenido", PedidoPendientePlantilla.Describir(pedido));
        }

        [TestMethod]
        public void Describir_ConPicking_LoDice()
        {
            var pedido = Pedido928141();
            pedido.TienePicking = true;

            Assert.AreEqual("928141 · del 08/10/2026 · 5 líneas · 138,10 € · entrega 09/10/2027 · con picking", PedidoPendientePlantilla.Describir(pedido));
        }

        [TestMethod]
        public void Describir_RetenidoYConPicking_PrimeroRetenido()
        {
            var pedido = Pedido928141();
            pedido.Retenido = true;
            pedido.TienePicking = true;

            StringAssert.EndsWith(PedidoPendientePlantilla.Describir(pedido), " · retenido · con picking");
        }

        [TestMethod]
        public void Describir_UnaLinea_EnSingular()
        {
            var pedido = new PedidoPendientePlantilla { Numero = 916961, Fecha = new DateTime(2026, 9, 1), NumeroLineas = 1, BaseImponible = 1234.5M, FechaEntrega = new DateTime(2026, 9, 2) };

            Assert.AreEqual("916961 · del 01/09/2026 · 1 línea · 1.234,50 € · entrega 02/09/2026", PedidoPendientePlantilla.Describir(pedido));
        }

        [TestMethod]
        public void Describir_NotaDeEntrega_LoDice()
        {
            var pedido = Pedido928141();
            pedido.NotaEntrega = true;

            StringAssert.EndsWith(PedidoPendientePlantilla.Describir(pedido), " · nota de entrega");
        }

        [TestMethod]
        public void Describir_SinFechasNiLineas_SoloElNumero()
        {
            Assert.AreEqual("929105", PedidoPendientePlantilla.Describir(new PedidoPendientePlantilla { Numero = 929105 }));
        }

        [TestMethod]
        public void Describir_Cero_EsPedidoNuevo()
        {
            Assert.AreEqual("Pedido nuevo", PedidoPendientePlantilla.Describir(new PedidoPendientePlantilla { Numero = 0 }));
            Assert.AreEqual("Pedido nuevo", PedidoPendientePlantilla.PedidoNuevo().Texto);
            Assert.AreEqual(0, PedidoPendientePlantilla.PedidoNuevo().Numero);
        }

        [TestMethod]
        public void Describir_Nothing_EsPedidoNuevo()
        {
            Assert.AreEqual("Pedido nuevo", PedidoPendientePlantilla.Describir(null));
        }

        [TestMethod]
        public void Texto_EsLoQueEnsenaElDesplegable()
        {
            var pedido = Pedido928141();

            Assert.AreEqual(PedidoPendientePlantilla.Describir(pedido), pedido.Texto);
        }
    }
}
