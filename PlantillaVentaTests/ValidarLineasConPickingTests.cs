using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Models;
using Nesto.Modulos.PlantillaVenta;
using System.Collections.Generic;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#397 (requisito de Carlos, 13/07/26): las líneas con PICKING ya están preparadas en
    /// el almacén y NO se pueden modificar ni quitar desde "Modificar con plantilla" — si se
    /// cambiaran, lo enviado no coincidiría con lo facturado. La validación compara el estado
    /// FRESCO del pedido con el DTO del PUT.
    /// </summary>
    [TestClass]
    public class ValidarLineasConPickingTests
    {
        private static PedidoParaPlantillaModel EstadoFresco()
        {
            return new PedidoParaPlantillaModel
            {
                Lineas = new List<LineaParaPlantillaModel>
                {
                    new LineaParaPlantillaModel
                    {
                        Producto = "38697", Cantidad = 2, Precio = 15m, Descuento = 0m,
                        IdLineaPago = 101, PagoTienePicking = true
                    },
                    new LineaParaPlantillaModel
                    {
                        Producto = "12345", Cantidad = 1, Precio = 10m,
                        IdLineaPago = 201, PagoTienePicking = false
                    }
                },
                Regalos = new List<RegaloParaPlantillaModel>
                {
                    new RegaloParaPlantillaModel { Producto = "45473", Cantidad = 1, IdLinea = 301, TienePicking = true }
                }
            };
        }

        private static PedidoVentaDTO PedidoConLineas(params LineaPedidoVentaDTO[] lineas)
        {
            var pedido = new PedidoVentaDTO();
            foreach (var linea in lineas)
            {
                pedido.Lineas.Add(linea);
            }
            return pedido;
        }

        private static LineaPedidoVentaDTO Linea(int id, string producto, int cantidad, decimal precio, decimal descuento = 0)
        {
            return new LineaPedidoVentaDTO { id = id, Producto = producto, Cantidad = cantidad, PrecioUnitario = precio, DescuentoLinea = descuento };
        }

        [TestMethod]
        public void LineasConPickingIntactas_EsValido()
        {
            var pedido = PedidoConLineas(
                Linea(101, "38697", 2, 15m),
                Linea(201, "12345", 5, 10m),   // sin picking: se puede cambiar la cantidad
                Linea(301, "45473", 1, 0m, descuento: 1m));

            Assert.IsNull(PlantillaVentaViewModel.ValidarLineasConPicking(EstadoFresco(), pedido));
        }

        [TestMethod]
        public void QuitarLineaConPicking_SeRechaza()
        {
            // El PUT borraría la línea 101 (no viene en el DTO) pero ya está preparada.
            var pedido = PedidoConLineas(Linea(201, "12345", 1, 10m), Linea(301, "45473", 1, 0m, 1m));

            var error = PlantillaVentaViewModel.ValidarLineasConPicking(EstadoFresco(), pedido);

            Assert.IsNotNull(error);
            StringAssert.Contains(error, "38697");
            StringAssert.Contains(error, "quitar");
        }

        [TestMethod]
        public void CambiarCantidadDeLineaConPicking_SeRechaza()
        {
            var pedido = PedidoConLineas(
                Linea(101, "38697", 5, 15m),   // era 2
                Linea(201, "12345", 1, 10m),
                Linea(301, "45473", 1, 0m, 1m));

            var error = PlantillaVentaViewModel.ValidarLineasConPicking(EstadoFresco(), pedido);

            Assert.IsNotNull(error);
            StringAssert.Contains(error, "38697");
            StringAssert.Contains(error, "modificar");
        }

        [TestMethod]
        public void CambiarPrecioDeLineaConPicking_SeRechaza()
        {
            var pedido = PedidoConLineas(
                Linea(101, "38697", 2, 12m),   // era 15
                Linea(201, "12345", 1, 10m),
                Linea(301, "45473", 1, 0m, 1m));

            Assert.IsNotNull(PlantillaVentaViewModel.ValidarLineasConPicking(EstadoFresco(), pedido));
        }

        [TestMethod]
        public void QuitarRegaloConPicking_SeRechaza()
        {
            var pedido = PedidoConLineas(Linea(101, "38697", 2, 15m), Linea(201, "12345", 1, 10m));

            var error = PlantillaVentaViewModel.ValidarLineasConPicking(EstadoFresco(), pedido);

            Assert.IsNotNull(error);
            StringAssert.Contains(error, "45473");
        }

        [TestMethod]
        public void LineaSinPicking_SePuedeQuitarYModificar()
        {
            // Solo la 201 es libre; quitarla no debe dar error si las de picking están intactas.
            var pedido = PedidoConLineas(Linea(101, "38697", 2, 15m), Linea(301, "45473", 1, 0m, 1m));

            Assert.IsNull(PlantillaVentaViewModel.ValidarLineasConPicking(EstadoFresco(), pedido));
        }

        [TestMethod]
        public void SinEstadoFresco_NoBloquea()
        {
            // Si el pedido ya no existe (404) u otro fallo de datos, esta validación no es quien
            // debe cortar: el PUT del servidor tiene sus propias protecciones.
            Assert.IsNull(PlantillaVentaViewModel.ValidarLineasConPicking(null, PedidoConLineas()));
        }
    }
}
