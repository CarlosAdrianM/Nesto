using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#430 (slice 1): al cambiar el almacén con el pedido empezado, se refrescan los
    /// stocks conservando las cantidades y se avisa de las líneas del pedido que se quedan
    /// sin stock suficiente en el nuevo almacén. Este helper decide cuáles son.
    /// </summary>
    [TestClass]
    public class CambioAlmacenTests
    {
        private static LineaPlantillaVenta Linea(short cantidad, int disponible, short cantidadOferta = 0,
            bool esPortes = false, string producto = "12345")
            => new LineaPlantillaVenta
            {
                producto = producto,
                cantidad = cantidad,
                cantidadOferta = cantidadOferta,
                cantidadDisponible = disponible,
                esLineaPortes = esPortes
            };

        [TestMethod]
        public void LineasConFaltaDeStock_SinLineas_DevuelveVacia()
        {
            Assert.AreEqual(0, PlantillaVentaViewModel.LineasConFaltaDeStock(null).Count);
            Assert.AreEqual(0, PlantillaVentaViewModel.LineasConFaltaDeStock(new List<LineaPlantillaVenta>()).Count);
        }

        [TestMethod]
        public void LineasConFaltaDeStock_LineaSinCantidad_NoAparece()
        {
            // Productos del catálogo que no están en el pedido: no son problema del cambio.
            var lineas = new List<LineaPlantillaVenta> { Linea(cantidad: 0, disponible: 0) };

            Assert.AreEqual(0, PlantillaVentaViewModel.LineasConFaltaDeStock(lineas).Count);
        }

        [TestMethod]
        public void LineasConFaltaDeStock_CantidadMayorQueDisponible_Aparece()
        {
            var lineas = new List<LineaPlantillaVenta> { Linea(cantidad: 5, disponible: 3) };

            var falta = PlantillaVentaViewModel.LineasConFaltaDeStock(lineas);

            Assert.AreEqual(1, falta.Count);
        }

        [TestMethod]
        public void LineasConFaltaDeStock_StockJusto_NoAparece()
        {
            var lineas = new List<LineaPlantillaVenta> { Linea(cantidad: 2, disponible: 2) };

            Assert.AreEqual(0, PlantillaVentaViewModel.LineasConFaltaDeStock(lineas).Count);
        }

        [TestMethod]
        public void LineasConFaltaDeStock_LaCantidadDeOfertaTambienCuenta()
        {
            // 1 vendida + 2 de oferta = 3 unidades a servir; con 2 disponibles falta stock.
            var lineas = new List<LineaPlantillaVenta> { Linea(cantidad: 1, disponible: 2, cantidadOferta: 2) };

            Assert.AreEqual(1, PlantillaVentaViewModel.LineasConFaltaDeStock(lineas).Count);
        }

        [TestMethod]
        public void LineasConFaltaDeStock_LaLineaDePortesSeIgnora()
        {
            var lineas = new List<LineaPlantillaVenta> { Linea(cantidad: 1, disponible: 0, esPortes: true) };

            Assert.AreEqual(0, PlantillaVentaViewModel.LineasConFaltaDeStock(lineas).Count);
        }

        #region Novedad 1215 (Sancho): el disponible es el del almacén elegido, no el del primero de la lista

        private static List<StockAlmacenDTO> Stocks(params (string almacen, int disponible)[] porAlmacen) =>
            porAlmacen.Select(s => new StockAlmacenDTO { almacen = s.almacen, stock = s.disponible, cantidadDisponible = s.disponible }).ToList();

        [TestMethod]
        public void StockDelAlmacen_SinDistinguirMayusculasNiEspacios()
        {
            var stocks = Stocks(("ALG", 40), ("REI ", 1), ("ALC", 0));

            Assert.AreEqual(1, PlantillaVentaViewModel.StockDelAlmacen(stocks, " rei").cantidadDisponible);
            Assert.IsNull(PlantillaVentaViewModel.StockDelAlmacen(stocks, "XXX"));
            Assert.IsNull(PlantillaVentaViewModel.StockDelAlmacen(null, "REI"));
        }

        [TestMethod]
        public void PonerDisponibleDelAlmacen_ElPrimeroDeLaListaTieneStock_PoneElDelElegido()
        {
            // Como devuelve la API: el campo legado es el del primer almacén (ALG)
            var linea = new LineaPlantillaVenta { producto = "100", cantidad = 3, cantidadDisponible = 40, StockDisponibleTodosLosAlmacenes = 41,
                stocks = Stocks(("ALG", 40), ("REI", 1), ("ALC", 0)) };

            bool conDato = PlantillaVentaViewModel.PonerDisponibleDelAlmacen(linea, "REI");
            linea.stockActualizado = conDato;

            Assert.IsTrue(conDato);
            Assert.AreEqual(1, linea.cantidadDisponible);
            Assert.AreEqual(1, linea.stock);
            Assert.AreNotEqual(Brushes.Green, linea.colorStock, "Con 1 disponible en Reina no llegan las 3: no puede quedarse verde");
        }

        [TestMethod]
        public void PonerDisponibleDelAlmacen_ElElegidoNoVieneEnLaLista_SinDatoYNuncaElDeOtroAlmacen()
        {
            var linea = new LineaPlantillaVenta { producto = "100", cantidad = 3, cantidadDisponible = 40, stocks = Stocks(("ALG", 40)) };

            linea.stockActualizado = PlantillaVentaViewModel.PonerDisponibleDelAlmacen(linea, "REI");

            Assert.IsFalse(linea.stockActualizado);
            Assert.AreEqual(0, linea.cantidadDisponible, "No se queda el disponible de Algete como si fuera el de Reina");
            Assert.AreEqual(Brushes.Gray, linea.colorStock);
            Assert.IsTrue(PlantillaVentaViewModel.SinDatoDelAlmacen(linea));
            Assert.AreEqual(0, PlantillaVentaViewModel.LineasConFaltaDeStock(new[] { linea }).Count,
                "Sin dato del almacén no se puede decir que falte stock");
        }

        [TestMethod]
        public void PonerDisponibleDelAlmacen_SinListaPorAlmacen_SeQuedaElDatoDeSiempre()
        {
            var linea = new LineaPlantillaVenta { producto = "100", cantidadDisponible = 7 };

            Assert.IsTrue(PlantillaVentaViewModel.PonerDisponibleDelAlmacen(linea, "REI"));
            Assert.AreEqual(7, linea.cantidadDisponible);
        }

        [TestMethod]
        public void CopiarStocksEnSitio_PoneElDisponibleDelAlmacenElegido()
        {
            var original = new LineaPlantillaVenta { producto = "100", cantidad = 3, cantidadDisponible = 40, stockActualizado = true };
            var respuesta = new List<LineaPlantillaVenta>
            {
                new LineaPlantillaVenta { producto = "100", cantidadDisponible = 40, StockDisponibleTodosLosAlmacenes = 41,
                    stocks = Stocks(("ALG", 40), ("REI", 1), ("ALC", 0)) }
            };

            var resultado = PlantillaVentaViewModel.CopiarStocksEnSitio(new List<LineaPlantillaVenta> { original }, respuesta, "REI");

            Assert.AreSame(original, resultado.Single());
            Assert.AreEqual(1, original.cantidadDisponible);
            Assert.IsTrue(original.stockActualizado);
            Assert.AreEqual(41, original.StockDisponibleTodosLosAlmacenes);
            Assert.AreEqual(1, PlantillaVentaViewModel.LineasConFaltaDeStock(resultado).Count);
        }

        // --- El cambio de almacén en la plantilla (setter de almacenSeleccionado) ---

        private IPlantillaVentaService _servicio;
        private IServicioDialogos _dialogos;

        private PlantillaVentaViewModel CrearViewModel(string almacenesUsuario)
        {
            _servicio = A.Fake<IPlantillaVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenesPlantillaVenta)).Returns(almacenesUsuario);
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, _servicio,
                new WeakReferenceMessenger(), _dialogos, A.Fake<IPedidoVentaService>(), new BorradorPlantillaVentaService(configuracion),
                A.Fake<IServicioAutenticacion>());
            vm.ServicioChequesRegalo = A.Fake<IServicioChequesRegalo>();
            vm.ServicioCupones = A.Fake<IServicioCupones>();
            return vm;
        }

        /// <summary>Como la API: copias de las líneas con la lista por almacén y el legado del PRIMER almacén.</summary>
        private void PonerStocksComoLaApi(params (string almacen, int disponible)[] porAlmacen)
        {
            A.CallTo(() => _servicio.PonerStocks(A<ObservableCollection<LineaPlantillaVenta>>._, A<string>._, A<List<string>>._))
                .ReturnsLazily((ObservableCollection<LineaPlantillaVenta> lineas, string almacen, List<string> almacenes) =>
                    Task.FromResult(new ObservableCollection<LineaPlantillaVenta>(lineas.Select(l => new LineaPlantillaVenta
                    {
                        producto = l.producto,
                        cantidad = l.cantidad,
                        cantidadOferta = l.cantidadOferta,
                        stock = l.stock,
                        cantidadDisponible = porAlmacen[0].disponible,
                        StockDisponibleTodosLosAlmacenes = porAlmacen.Sum(s => s.disponible),
                        stocks = Stocks(porAlmacen)
                    }))));
        }

        private static void CargarLineas(PlantillaVentaViewModel vm, params LineaPlantillaVenta[] lineas) =>
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>(lineas);

        private static LineaPlantillaVenta LineaDelPedido(string producto, short cantidad) => new LineaPlantillaVenta
        {
            producto = producto, texto = "PRODUCTO " + producto, cantidad = cantidad, estado = 1,
            cantidadDisponible = 40, StockDisponibleTodosLosAlmacenes = 41, stockActualizado = true
        };

        [TestMethod]
        public void CambiarAReina_ElPrimeroDeLaListaTieneStock_LaLineaTomaElDeReinaYDejaDeEstarVerde() => EnStaConDispatcher(async () =>
        {
            var vm = CrearViewModel(almacenesUsuario: "");
            PonerStocksComoLaApi(("ALG", 40), ("REI", 1), ("ALC", 0));
            LineaPlantillaVenta linea = LineaDelPedido("100", 3);
            CargarLineas(vm, linea);
            Assert.AreEqual(Brushes.Green, linea.colorStock, "En Algete hay de sobra");

            vm.almacenSeleccionado = vm.listaAlmacenes.Single(a => a.Codigo == "REI");
            await Task.Delay(500);

            Assert.AreEqual(1, linea.cantidadDisponible, "El disponible de Reina, no el de Algete");
            Assert.IsTrue(linea.stockActualizado);
            Assert.AreNotEqual(Brushes.Green, linea.colorStock);
            Assert.IsTrue(linea.esSobrePedido);
            A.CallTo(() => _dialogos.ShowNotification("Cambio de almacén",
                    A<string>.That.Matches(m => m.Contains("1 línea(s)") && m.Contains("disponibles 1"))))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CargarStockProducto(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        });

        [TestMethod]
        public void CambiarAReina_HayStockEnReina_SinAviso() => EnStaConDispatcher(async () =>
        {
            var vm = CrearViewModel(almacenesUsuario: "");
            PonerStocksComoLaApi(("ALG", 0), ("REI", 5), ("ALC", 0));
            LineaPlantillaVenta linea = LineaDelPedido("100", 3);
            CargarLineas(vm, linea);

            vm.almacenSeleccionado = vm.listaAlmacenes.Single(a => a.Codigo == "REI");
            await Task.Delay(500);

            Assert.AreEqual(5, linea.cantidadDisponible);
            Assert.AreEqual(Brushes.Green, linea.colorStock);
            A.CallTo(() => _dialogos.ShowNotification(A<string>._, A<string>._)).MustNotHaveHappened();
        });

        [TestMethod]
        public void CambiarAReina_ReinaNoEstaEnLosAlmacenesDelUsuario_LasDelPedidoSePidenAReina() => EnStaConDispatcher(async () =>
        {
            // «Solo el mío» (Algete): PonerStock solo trae ALG
            var vm = CrearViewModel(almacenesUsuario: "ALG");
            PonerStocksComoLaApi(("ALG", 40));
            A.CallTo(() => _servicio.CargarStockProducto(A<string>._, "REI", "100"))
                .Returns(Task.FromResult(new StockProductoDTO { stock = 2, cantidadDisponible = 2, StockDisponibleTodosLosAlmacenes = 42 }));
            LineaPlantillaVenta enElPedido = LineaDelPedido("100", 3);
            LineaPlantillaVenta fueraDelPedido = LineaDelPedido("200", 0);
            CargarLineas(vm, enElPedido, fueraDelPedido);

            vm.almacenSeleccionado = vm.listaAlmacenes.Single(a => a.Codigo == "REI");
            await Task.Delay(500);

            Assert.AreEqual(2, enElPedido.cantidadDisponible, "El de Reina, pedido aparte");
            Assert.IsTrue(enElPedido.stockActualizado);
            Assert.AreNotEqual(Brushes.Green, enElPedido.colorStock);
            Assert.IsFalse(fueraDelPedido.stockActualizado, "Sin dato de Reina: gris hasta que se le ponga cantidad");
            Assert.AreEqual(0, fueraDelPedido.cantidadDisponible, "Nunca el de Algete como si fuera el de Reina");
            A.CallTo(() => _servicio.CargarStockProducto(A<string>._, A<string>._, "200")).MustNotHaveHappened();
            A.CallTo(() => _dialogos.ShowNotification("Cambio de almacén",
                    A<string>.That.Matches(m => m.Contains("1 línea(s)") && m.Contains("disponibles 2"))))
                .MustHaveHappenedOnceExactly();
        });

        [TestMethod]
        public void CambiarAReina_FueraDeLaListaYFallaLaConsulta_LaLineaQuedaSinDatoYNoSeAvisa() => EnStaConDispatcher(async () =>
        {
            var vm = CrearViewModel(almacenesUsuario: "ALG");
            PonerStocksComoLaApi(("ALG", 40));
            A.CallTo(() => _servicio.CargarStockProducto(A<string>._, A<string>._, A<string>._)).Returns(Task.FromResult<StockProductoDTO>(null));
            LineaPlantillaVenta linea = LineaDelPedido("100", 3);
            CargarLineas(vm, linea);

            vm.almacenSeleccionado = vm.listaAlmacenes.Single(a => a.Codigo == "REI");
            await Task.Delay(500);

            Assert.IsFalse(linea.stockActualizado);
            Assert.AreEqual(Brushes.Gray, linea.colorStock);
            A.CallTo(() => _dialogos.ShowNotification(A<string>._, A<string>._)).MustNotHaveHappened();
        });

        private static void EnStaConDispatcher(Func<Task> prueba)
        {
            Exception error = null;
            var hilo = new System.Threading.Thread(() =>
            {
                System.Windows.Threading.Dispatcher dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
                _ = dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await prueba();
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    finally
                    {
                        dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);
                    }
                });
                System.Windows.Threading.Dispatcher.Run();
            });
            hilo.SetApartmentState(System.Threading.ApartmentState.STA);
            hilo.Start();
            if (!hilo.Join(TimeSpan.FromSeconds(60)))
            {
                Assert.Fail("La prueba no ha terminado");
            }
            if (error != null)
            {
                throw new AssertFailedException(error.ToString(), error);
            }
        }

        #endregion
    }
}
