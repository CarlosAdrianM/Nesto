using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PlantillaVenta;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#531 (gemela de NestoAPI#649): en un pedido que sale por ruta desde Algete, una muestra solo
    /// puede llevar las unidades que hay en Algete (la API rechaza el resto al grabar). La plantilla lo
    /// ofrece antes de grabar, con confirmación. Estos helpers deciden qué líneas y con qué texto.
    /// </summary>
    [TestClass]
    public class MuestrasAlgetePlantillaTests
    {
        private const string DESCRIPCION_MMP = "Muestras y material promocional";

        private static LineaPlantillaVenta Muestra(int cantidad, int disponible, string producto = "42543",
            string subGrupo = DESCRIPCION_MMP, int idLineaPedido = 0, bool stockActualizado = true)
            => new LineaPlantillaVenta
            {
                producto = producto,
                texto = "MUESTRA CONTORNO DE OJOS SPLENDORE",
                subGrupo = subGrupo,
                cantidad = cantidad,
                cantidadDisponible = disponible,
                idLineaPedido = idLineaPedido,
                stockActualizado = stockActualizado
            };

        [TestMethod]
        public void MuestraSinStockEnAlgete_EnRutaFW_Aparece()
        {
            // El caso de María José (15/10/26): ruta FW, ALG, 5 del 42543 y ninguna en Algete
            var resultado = PlantillaVentaViewModel.MuestrasPorEncimaDeAlgete(
                new List<LineaPlantillaVenta> { Muestra(5, 0) }, "FW ", "ALG", esPresupuesto: false);

            Assert.AreEqual(1, resultado.Count);
        }

        [TestMethod]
        public void MuestraConStockSuficienteEnAlgete_NoAparece()
        {
            Assert.AreEqual(0, PlantillaVentaViewModel.MuestrasPorEncimaDeAlgete(
                new List<LineaPlantillaVenta> { Muestra(5, 5) }, "16", "ALG", false).Count);
        }

        [TestMethod]
        public void SeReconoceElSubgrupoPorCodigoOPorDescripcion()
        {
            Assert.IsTrue(PlantillaVentaViewModel.EsMuestra(Muestra(1, 0, subGrupo: "MMP")));
            Assert.IsTrue(PlantillaVentaViewModel.EsMuestra(Muestra(1, 0, subGrupo: "muestras y material promocional  ")));
            Assert.IsFalse(PlantillaVentaViewModel.EsMuestra(Muestra(1, 0, subGrupo: "Promoción")));
        }

        [TestMethod]
        public void ProductoQueNoEsMuestra_NoAparece()
        {
            Assert.AreEqual(0, PlantillaVentaViewModel.MuestrasPorEncimaDeAlgete(
                new List<LineaPlantillaVenta> { Muestra(5, 0, subGrupo: "Cremas") }, "FW", "ALG", false).Count);
        }

        [TestMethod]
        public void PedidoQueNoSalePorRutaDesdeAlgete_NoSeMira()
        {
            var lineas = new List<LineaPlantillaVenta> { Muestra(5, 0) };

            Assert.AreEqual(0, PlantillaVentaViewModel.MuestrasPorEncimaDeAlgete(lineas, "GLV", "ALG", false).Count, "Glovo");
            Assert.AreEqual(0, PlantillaVentaViewModel.MuestrasPorEncimaDeAlgete(lineas, null, "ALG", false).Count, "sin ruta");
            Assert.AreEqual(0, PlantillaVentaViewModel.MuestrasPorEncimaDeAlgete(lineas, "FW", "REI", false).Count, "pedido de tienda");
            Assert.AreEqual(0, PlantillaVentaViewModel.MuestrasPorEncimaDeAlgete(lineas, "FW", "ALG", true).Count, "presupuesto");
        }

        [TestMethod]
        public void LineaYaGuardadaOSinStockCargado_NoSeMira()
        {
            // Editando un pedido, el disponible incluye la reserva de la propia línea: lo decide la API
            Assert.AreEqual(0, PlantillaVentaViewModel.MuestrasPorEncimaDeAlgete(
                new List<LineaPlantillaVenta> { Muestra(5, 0, idLineaPedido: 328400100) }, "FW", "ALG", false).Count);
            Assert.AreEqual(0, PlantillaVentaViewModel.MuestrasPorEncimaDeAlgete(
                new List<LineaPlantillaVenta> { Muestra(5, 0, stockActualizado: false) }, "FW", "ALG", false).Count);
        }

        [TestMethod]
        public void Oferta_DiceCuantasHayEnAlgeteYEsDeUsted()
        {
            string texto = PlantillaVentaViewModel.ConstruirOfertaAjustarMuestrasAlgete(new List<LineaPlantillaVenta>
            {
                Muestra(5, 0),
                Muestra(5, 2, producto: "43893")
            });

            StringAssert.Contains(texto, "42543 MUESTRA CONTORNO DE OJOS SPLENDORE: pide 5, no hay ninguna en Algete");
            StringAssert.Contains(texto, "43893 MUESTRA CONTORNO DE OJOS SPLENDORE: pide 5, solo hay 2 en Algete");
            StringAssert.Contains(texto, "¿Quiere dejarlas en lo que hay en Algete");
            StringAssert.Contains(texto, "puede cambiarlas usted");
        }

        [TestMethod]
        public void Ajustar_DejaLoQueHayEnAlgeteYSinNingunaLaQuita()
        {
            var sinNinguna = Muestra(5, 0);
            var conDos = Muestra(5, 2, producto: "43893");
            var conNegativo = Muestra(3, -1, producto: "40000");

            PlantillaVentaViewModel.AjustarMuestrasAAlgete(new List<LineaPlantillaVenta> { sinNinguna, conDos, conNegativo });

            Assert.AreEqual(0, sinNinguna.cantidad);
            Assert.AreEqual(2, conDos.cantidad);
            Assert.AreEqual(0, conNegativo.cantidad);
        }
    }
}
