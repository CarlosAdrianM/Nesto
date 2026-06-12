using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System.Collections.Generic;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#379: set_iva crasheaba con ArgumentNullException (Parameter 'source') cuando
    /// ParametrosIva aún no estaba cargado (pedido nuevo, carga async en curso o fallida),
    /// dejando la línea a medio actualizar tras CargarDatosProducto.
    /// </summary>
    [TestClass]
    public class LineaPedidoVentaWrapperTests
    {
        private PedidoVentaWrapper CrearPedidoConUnaLinea(IEnumerable<ParametrosIvaBase> parametrosIva)
        {
            var pedido = new PedidoVentaDTO
            {
                ParametrosIva = parametrosIva
            };
            pedido.Lineas.Add(new LineaPedidoVentaDTO { id = 1, Cantidad = 1 });
            return new PedidoVentaWrapper(pedido);
        }

        [TestMethod]
        public void SetIva_ParametrosIvaSinCargar_NoLanzaYDejaPorcentajesACero()
        {
            var wrapper = CrearPedidoConUnaLinea(null);
            var linea = wrapper.Lineas[0];

            linea.iva = "G21";

            Assert.AreEqual("G21", linea.iva);
            Assert.AreEqual(0, linea.Model.PorcentajeIva);
            Assert.AreEqual(0, linea.Model.PorcentajeRecargoEquivalencia);
        }

        [TestMethod]
        public void SetIva_ValorNulo_NoLanzaYDejaPorcentajesACero()
        {
            var wrapper = CrearPedidoConUnaLinea(new List<ParametrosIvaBase>
            {
                new ParametrosIvaBase { CodigoIvaProducto = "G21", PorcentajeIvaProducto = 0.21m }
            });
            var linea = wrapper.Lineas[0];

            linea.iva = null;

            Assert.IsNull(linea.iva);
            Assert.AreEqual(0, linea.Model.PorcentajeIva);
        }

        [TestMethod]
        public void SetIva_CodigoExisteEnParametros_AsignaPorcentajes()
        {
            var wrapper = CrearPedidoConUnaLinea(new List<ParametrosIvaBase>
            {
                new ParametrosIvaBase { CodigoIvaProducto = "G21", PorcentajeIvaProducto = 0.21m, PorcentajeIvaRecargoEquivalencia = 0.052m }
            });
            var linea = wrapper.Lineas[0];

            linea.iva = "g21";

            Assert.AreEqual("G21", linea.iva);
            Assert.AreEqual(0.21m, linea.Model.PorcentajeIva);
            Assert.AreEqual(0.052m, linea.Model.PorcentajeRecargoEquivalencia);
        }

        [TestMethod]
        public void SetIva_CodigoNoExisteEnParametros_AsignaCodigoConPorcentajesACero()
        {
            var wrapper = CrearPedidoConUnaLinea(new List<ParametrosIvaBase>
            {
                new ParametrosIvaBase { CodigoIvaProducto = "G21", PorcentajeIvaProducto = 0.21m }
            });
            var linea = wrapper.Lineas[0];

            linea.iva = "EX";

            Assert.AreEqual("EX", linea.iva);
            Assert.AreEqual(0, linea.Model.PorcentajeIva);
        }
    }
}
