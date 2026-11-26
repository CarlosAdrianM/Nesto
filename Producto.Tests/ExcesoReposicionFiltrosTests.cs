using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Modules.Producto.ViewModels;
using Newtonsoft.Json;

namespace Producto.Tests
{
    /// <summary>
    /// Sugerencia 1429 (Manuel): filtros flexibles en «Devolver exceso a Algete»: el nombre contiene / no contiene, solo con más de N
    /// unidades, sin ventas en N meses y cuántas dejar en la tienda; la API devuelve los filtros aplicados y la última venta de cada línea.
    /// </summary>
    [TestClass]
    public class ExcesoReposicionFiltrosTests
    {
        private IServicioEnvioReposiciones _servicio = null!;
        private ExcesoReposicionDialogViewModel _vm = null!;
        private readonly List<PeticionExcesoReposicion> _peticiones = new();

        [TestInitialize]
        public void Preparar()
        {
            _servicio = A.Fake<IServicioEnvioReposiciones>();
            A.CallTo(() => _servicio.CalcularExceso(A<PeticionExcesoReposicion>._, A<bool>._))
                .ReturnsLazily((PeticionExcesoReposicion p, bool _) =>
                {
                    _peticiones.Add(p);
                    return Task.FromResult(new ResultadoExcesoReposicion
                    {
                        Lineas = new List<LineaExcesoReposicion>
                        {
                            new() { Producto = "40001", Nombre = "ESMALTE ROJO", Stock = 12, Exceso = 8, UltimaVenta = new DateTime(2026, 3, 2) }
                        },
                        NumeroLineas = 1,
                        TotalUnidades = 8,
                        Filtros = "Filtros: el nombre lleva «ESMALTE»; sin ventas en 6 meses."
                    });
                });
            _vm = new ExcesoReposicionDialogViewModel(_servicio, A.Fake<IServicioDialogos>());
            _vm.OnDialogOpened(new ParametrosDialogo { { ExcesoReposicionDialogViewModel.PARAMETRO_ORIGEN, "ALC" } });
        }

        [TestMethod]
        public void ElNombreContiene_ValeComoFiltroObligatorio_ElNoContieneNo()
        {
            _vm.TextoNombreExcluir = "PERMANENTE";
            Assert.IsFalse(_vm.CalcularCommand.CanExecute(null), "excluir solo no vale: sería casi todo el almacén");

            _vm.TextoNombre = "esmalte";
            Assert.IsTrue(_vm.CalcularCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Calcular_MandaTodosLosFiltros_YEnseniaFiltrosYUltimaVenta()
        {
            _vm.TextoNombre = " esmalte ";
            _vm.TextoNombreExcluir = "permanente";
            _vm.StockMinimo = "2";
            _vm.SinVentasEnMeses = "6";
            _vm.DejarEnTienda = "1";

            await _vm.CalcularAsync();

            PeticionExcesoReposicion p = _peticiones.Single();
            Assert.AreEqual("esmalte", p.TextoNombre);
            Assert.AreEqual("permanente", p.TextoNombreExcluir);
            Assert.AreEqual(2, p.StockMinimo);
            Assert.AreEqual(6, p.SinVentasEnMeses);
            Assert.AreEqual(1, p.DejarEnTienda);
            Assert.AreEqual("Filtros: el nombre lleva «ESMALTE»; sin ventas en 6 meses.", _vm.Filtros);
            Assert.AreEqual(new DateTime(2026, 3, 2), _vm.Lineas.Single().UltimaVenta);
        }

        [TestMethod]
        public async Task NumerosVacios_NoViajan()
        {
            _vm.Familia = "FABY";

            await _vm.CalcularAsync();

            PeticionExcesoReposicion p = _peticiones.Single();
            Assert.IsNull(p.StockMinimo);
            Assert.IsNull(p.SinVentasEnMeses);
            Assert.IsNull(p.DejarEnTienda, "vacío = el máximo de la ficha");
            Assert.IsNull(p.TextoNombre);
        }

        [DataTestMethod]
        [DataRow("dos", null, null, ExcesoReposicionDialogViewModel.MOTIVO_STOCK_MINIMO)]
        [DataRow("-1", null, null, ExcesoReposicionDialogViewModel.MOTIVO_STOCK_MINIMO)]
        [DataRow(null, "0", null, ExcesoReposicionDialogViewModel.MOTIVO_SIN_VENTAS)]
        [DataRow(null, "61", null, ExcesoReposicionDialogViewModel.MOTIVO_SIN_VENTAS)]
        [DataRow(null, null, "x", ExcesoReposicionDialogViewModel.MOTIVO_DEJAR)]
        public async Task NumeroQueNoVale_NoLlamaALaApiYLoDice(string stock, string meses, string dejar, string motivo)
        {
            _vm.Familia = "FABY";
            _vm.StockMinimo = stock;
            _vm.SinVentasEnMeses = meses;
            _vm.DejarEnTienda = dejar;

            await _vm.CalcularAsync();

            Assert.AreEqual(0, _peticiones.Count);
            Assert.AreEqual(motivo, _vm.Error);
        }

        [TestMethod]
        public async Task CambiarUnFiltroNuevo_ObligaAVolverACalcular()
        {
            _vm.Familia = "FABY";
            await _vm.CalcularAsync();
            Assert.IsTrue(_vm.HayCalculo);

            _vm.DejarEnTienda = "2";

            Assert.IsFalse(_vm.HayCalculo);
            Assert.IsNull(_vm.Filtros);
            Assert.IsFalse(_vm.CrearCommand.CanExecute(null));
        }

        [TestMethod]
        public void LaLineaLeeUltimaVentaDeLaApi()
        {
            var linea = JsonConvert.DeserializeObject<LineaExcesoReposicion>("{\"Producto\":\"1\",\"UltimaVenta\":\"2026-03-02T00:00:00\"}");
            Assert.AreEqual(new DateTime(2026, 3, 2), linea!.UltimaVenta);
            Assert.IsNull(JsonConvert.DeserializeObject<LineaExcesoReposicion>("{\"Producto\":\"1\",\"UltimaVenta\":null}")!.UltimaVenta);
        }
    }
}
