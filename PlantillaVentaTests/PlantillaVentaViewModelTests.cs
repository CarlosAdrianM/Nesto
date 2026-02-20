using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using Prism.Events;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Unity;

namespace PlantillaVentaTests
{
    [TestClass]
    public class PlantillaVentaViewModelTests
    {
        //[TestMethod]
        //public void PlantillaVenta_CargarClientes_SiEsUnClienteDeEstado5NoPasaALosProductos()
        //{
        //    IUnityContainer container = A.Fake<IUnityContainer>();
        //    IRegionManager regionManager = A.Fake<IRegionManager>();
        //    IConfiguracion configuracion = A.Fake<IConfiguracion>();
        //    IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
        //    PlantillaVentaViewModel viewModel = new PlantillaVentaViewModel(container, regionManager, configuracion, servicio);
        //    ClienteJson cliente = new ClienteJson
        //    {
        //        estado = 5
        //    };
        //    A.CallTo(() => servicio.CargarClientesVendedor("busca", null, true)).Returns(new ObservableCollection<ClienteJson>
        //    {
        //        cliente
        //    });

        //    viewModel.filtroCliente = "busca";
        //    viewModel.cmdCargarClientesVendedor.Execute();
        //    viewModel.clienteSeleccionado = cliente;

        //    Assert.AreEqual("Selección del cliente", viewModel.CurrentWizardPage?.Title);
        //}

        [TestMethod]
        public void PlantillaVenta_BaseImponible_RedondeaLosDecimales()
        {
            IUnityContainer container = A.Fake<IUnityContainer>();
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IPedidoVentaService pedidoVentaService = A.Fake<IPedidoVentaService>();
            IBorradorPlantillaVentaService servicioBorradores = A.Fake<IBorradorPlantillaVentaService>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            PlantillaVentaViewModel vm = new PlantillaVentaViewModel(container, regionManager, configuracion, servicio, eventAggregator, dialogService, pedidoVentaService, servicioBorradores);
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 1,
                precio = 4.5M,
                descuento = .35M
            });

            decimal baseImponible = vm.baseImponiblePedido;

            Assert.AreEqual(2.92M, baseImponible);
        }

        [TestMethod]
        public void PlantillaVenta_BaseImponiblePortes_RedondeaLosDecimales()
        {
            IUnityContainer container = A.Fake<IUnityContainer>();
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IPedidoVentaService pedidoVentaService = A.Fake<IPedidoVentaService>();
            IBorradorPlantillaVentaService servicioBorradores = A.Fake<IBorradorPlantillaVentaService>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            PlantillaVentaViewModel vm = new PlantillaVentaViewModel(container, regionManager, configuracion, servicio, eventAggregator, dialogService, pedidoVentaService, servicioBorradores);
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 1,
                precio = 4.5M,
                descuento = .35M
            });

            decimal baseImponiblePortes = vm.baseImponibleParaPortes;

            Assert.AreEqual(2.92M, baseImponiblePortes);
        }

        #region Ganavisiones - FASE 6 Tests

        private (PlantillaVentaViewModel ViewModel, IDialogService DialogService) CrearViewModelConMocks(
            HashSet<string> productosBonificablesIds = null)
        {
            IUnityContainer container = A.Fake<IUnityContainer>();
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPlantillaVentaService servicioMock = A.Fake<IPlantillaVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogServiceMock = A.Fake<IDialogService>();
            IPedidoVentaService pedidoVentaService = A.Fake<IPedidoVentaService>();
            IBorradorPlantillaVentaService servicioBorradores = A.Fake<IBorradorPlantillaVentaService>();

            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");

            // Configurar evento para evitar null reference
            var clienteCreadoEvent = A.Fake<ClienteCreadoEvent>();
            A.CallTo(() => eventAggregator.GetEvent<ClienteCreadoEvent>()).Returns(clienteCreadoEvent);

            // Configurar productos bonificables
            if (productosBonificablesIds == null)
            {
                productosBonificablesIds = new HashSet<string>();
            }
            A.CallTo(() => servicioMock.CargarProductosBonificablesIds()).Returns(Task.FromResult(productosBonificablesIds));

            var vm = new PlantillaVentaViewModel(container, regionManager, configuracion, servicioMock, eventAggregator, dialogServiceMock, pedidoVentaService, servicioBorradores);
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();

            return (vm, dialogServiceMock);
        }

        // Helper para verificar que se llamó a ShowDialog con ConfirmationDialog
        // ShowConfirmation es un método de extensión que internamente llama a ShowDialog
        private static void VerifyConfirmationDialogCalled(IDialogService dialogServiceMock, int times = 1)
        {
            if (times == 0)
            {
                A.CallTo(() => dialogServiceMock.ShowDialog(
                    "ConfirmationDialog",
                    A<IDialogParameters>.Ignored,
                    A<Action<IDialogResult>>.Ignored
                )).MustNotHaveHappened();
            }
            else
            {
                A.CallTo(() => dialogServiceMock.ShowDialog(
                    "ConfirmationDialog",
                    A<IDialogParameters>.Ignored,
                    A<Action<IDialogResult>>.Ignored
                )).MustHaveHappened(times, Times.Exactly);
            }
        }

        [TestMethod]
        public async Task ActualizarProductosPedido_ProductoBonificable_PrimeraVez_MuestraDialogo()
        {
            // Arrange
            var productosBonificables = new HashSet<string> { "PROD1" };
            var (vm, dialogServiceMock) = CrearViewModelConMocks(productosBonificables);

            // Simular OnNavigatedTo para cargar el cache
            vm.OnNavigatedTo(null);
            await Task.Delay(100); // Dar tiempo a que cargue el cache

            var linea = new LineaPlantillaVenta
            {
                producto = "PROD1",
                texto = "Producto de Prueba",
                cantidad = 1,
                fechaInsercion = DateTime.MaxValue // Primera vez en el pedido
            };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);

            // Act
            vm.cmdActualizarProductosPedido.Execute(linea);

            // Assert - Verificar que se llamó al diálogo de confirmación (ShowDialog con "ConfirmationDialog")
            VerifyConfirmationDialogCalled(dialogServiceMock, 1);
        }

        [TestMethod]
        public async Task ActualizarProductosPedido_ProductoBonificable_NoEsPrimeraVez_NoMuestraDialogo()
        {
            // Arrange
            var productosBonificables = new HashSet<string> { "PROD1" };
            var (vm, dialogServiceMock) = CrearViewModelConMocks(productosBonificables);

            vm.OnNavigatedTo(null);
            await Task.Delay(100);

            var linea = new LineaPlantillaVenta
            {
                producto = "PROD1",
                texto = "Producto de Prueba",
                cantidad = 2,
                fechaInsercion = DateTime.Now // Ya está en el pedido (no es primera vez)
            };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);

            // Act
            vm.cmdActualizarProductosPedido.Execute(linea);

            // Assert - NO debe llamar al diálogo
            VerifyConfirmationDialogCalled(dialogServiceMock, 0);
        }

        [TestMethod]
        public async Task ActualizarProductosPedido_ProductoNoBonificable_PrimeraVez_NoMuestraDialogo()
        {
            // Arrange
            var productosBonificables = new HashSet<string> { "OTRO_PROD" };
            var (vm, dialogServiceMock) = CrearViewModelConMocks(productosBonificables);

            vm.OnNavigatedTo(null);
            await Task.Delay(100);

            var linea = new LineaPlantillaVenta
            {
                producto = "PROD1", // No está en la lista de bonificables
                texto = "Producto Normal",
                cantidad = 1,
                fechaInsercion = DateTime.MaxValue
            };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);

            // Act
            vm.cmdActualizarProductosPedido.Execute(linea);

            // Assert - NO debe llamar al diálogo
            VerifyConfirmationDialogCalled(dialogServiceMock, 0);
        }

        [TestMethod]
        public async Task ActualizarProductosPedido_ProductoBonificable_CantidadCero_NoMuestraDialogo()
        {
            // Arrange
            var productosBonificables = new HashSet<string> { "PROD1" };
            var (vm, dialogServiceMock) = CrearViewModelConMocks(productosBonificables);

            vm.OnNavigatedTo(null);
            await Task.Delay(100);

            var linea = new LineaPlantillaVenta
            {
                producto = "PROD1",
                texto = "Producto de Prueba",
                cantidad = 0,
                cantidadOferta = 0, // Ambas cantidades en cero
                fechaInsercion = DateTime.MaxValue
            };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);

            // Act
            vm.cmdActualizarProductosPedido.Execute(linea);

            // Assert - NO debe llamar al diálogo si no hay cantidad
            VerifyConfirmationDialogCalled(dialogServiceMock, 0);
        }

        [TestMethod]
        public async Task ActualizarProductosPedido_ProductoBonificable_ConCantidadOferta_MuestraDialogo()
        {
            // Arrange
            var productosBonificables = new HashSet<string> { "PROD1" };
            var (vm, dialogServiceMock) = CrearViewModelConMocks(productosBonificables);

            vm.OnNavigatedTo(null);
            await Task.Delay(100);

            var linea = new LineaPlantillaVenta
            {
                producto = "PROD1",
                texto = "Producto de Prueba",
                cantidad = 0,
                cantidadOferta = 1, // Solo cantidadOferta
                fechaInsercion = DateTime.MaxValue
            };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);

            // Act
            vm.cmdActualizarProductosPedido.Execute(linea);

            // Assert - Debe mostrar diálogo si hay cantidadOferta > 0
            VerifyConfirmationDialogCalled(dialogServiceMock, 1);
        }

        [TestMethod]
        public async Task ActualizarProductosPedido_UsuarioCancela_RevierteCantidades()
        {
            // Arrange
            var productosBonificables = new HashSet<string> { "PROD1" };
            var (vm, dialogServiceMock) = CrearViewModelConMocks(productosBonificables);

            // Configurar ShowDialog para que simule que el usuario cancela
            // (ShowConfirmation internamente llama a ShowDialog con "ConfirmationDialog")
            A.CallTo(() => dialogServiceMock.ShowDialog(
                "ConfirmationDialog",
                A<IDialogParameters>.Ignored,
                A<Action<IDialogResult>>.Ignored
            )).Invokes((string name, IDialogParameters parameters, Action<IDialogResult> callback) =>
            {
                var result = A.Fake<IDialogResult>();
                A.CallTo(() => result.Result).Returns(ButtonResult.Cancel);
                callback(result);
            });

            vm.OnNavigatedTo(null);
            await Task.Delay(100);

            var linea = new LineaPlantillaVenta
            {
                producto = "PROD1",
                texto = "Producto de Prueba",
                cantidad = 5,
                cantidadOferta = 2,
                fechaInsercion = DateTime.MaxValue
            };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);

            // Act
            vm.cmdActualizarProductosPedido.Execute(linea);

            // Assert - Las cantidades deben haberse revertido a 0
            Assert.AreEqual(0, linea.cantidad, "La cantidad debería ser 0 tras cancelar");
            Assert.AreEqual(0, linea.cantidadOferta, "La cantidadOferta debería ser 0 tras cancelar");
        }

        [TestMethod]
        public async Task ActualizarProductosPedido_UsuarioAcepta_MantieneCantidades()
        {
            // Arrange
            var productosBonificables = new HashSet<string> { "PROD1" };
            var (vm, dialogServiceMock) = CrearViewModelConMocks(productosBonificables);

            // Configurar ShowDialog para que simule que el usuario acepta
            A.CallTo(() => dialogServiceMock.ShowDialog(
                "ConfirmationDialog",
                A<IDialogParameters>.Ignored,
                A<Action<IDialogResult>>.Ignored
            )).Invokes((string name, IDialogParameters parameters, Action<IDialogResult> callback) =>
            {
                var result = A.Fake<IDialogResult>();
                A.CallTo(() => result.Result).Returns(ButtonResult.OK);
                callback(result);
            });

            vm.OnNavigatedTo(null);
            await Task.Delay(100);

            var linea = new LineaPlantillaVenta
            {
                producto = "PROD1",
                texto = "Producto de Prueba",
                cantidad = 5,
                cantidadOferta = 2,
                fechaInsercion = DateTime.MaxValue
            };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);

            // Act
            vm.cmdActualizarProductosPedido.Execute(linea);

            // Assert - Las cantidades deben mantenerse
            Assert.AreEqual(5, linea.cantidad, "La cantidad debería mantenerse tras aceptar");
            Assert.AreEqual(2, linea.cantidadOferta, "La cantidadOferta debería mantenerse tras aceptar");
        }

        [TestMethod]
        public void ActualizarProductosPedido_CacheNoInicializado_NoMuestraDialogo()
        {
            // Arrange - Sin llamar a OnNavigatedTo, el cache será null
            var productosBonificables = new HashSet<string> { "PROD1" };
            var (vm, dialogServiceMock) = CrearViewModelConMocks(productosBonificables);
            // NO llamamos a OnNavigatedTo, así _productosBonificablesIds será null

            var linea = new LineaPlantillaVenta
            {
                producto = "PROD1",
                texto = "Producto de Prueba",
                cantidad = 1,
                fechaInsercion = DateTime.MaxValue
            };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);

            // Act
            vm.cmdActualizarProductosPedido.Execute(linea);

            // Assert - NO debe llamar al diálogo si el cache es null
            VerifyConfirmationDialogCalled(dialogServiceMock, 0);
        }

        #endregion

        #region Estado Synchronization Tests - Issue #287

        [TestMethod]
        public void FormaVentaSeleccionada_AlCambiar_SincronizaConEstado()
        {
            // Arrange
            var (vm, _) = CrearViewModelConMocks();

            // Act
            vm.formaVentaSeleccionada = 2; // Teléfono

            // Assert
            Assert.AreEqual(2, vm.Estado.FormaVenta);
        }

        [TestMethod]
        public void EsPresupuesto_AlCambiar_SincronizaConEstado()
        {
            // Arrange
            var (vm, _) = CrearViewModelConMocks();

            // Act
            vm.EsPresupuesto = true;

            // Assert
            Assert.IsTrue(vm.Estado.EsPresupuesto);
        }

        [TestMethod]
        public void ComentarioRuta_AlCambiar_SincronizaConEstado()
        {
            // Arrange
            var (vm, _) = CrearViewModelConMocks();

            // Act
            vm.ComentarioRuta = "Test comentario";

            // Assert
            Assert.AreEqual("Test comentario", vm.Estado.ComentarioRuta);
        }

        [TestMethod]
        public void FechaEntrega_AlCambiar_SincronizaConEstado()
        {
            // Arrange
            var (vm, _) = CrearViewModelConMocks();
            var fechaEsperada = new DateTime(2025, 6, 15);

            // Act
            vm.fechaEntrega = fechaEsperada;

            // Assert
            Assert.AreEqual(fechaEsperada, vm.Estado.FechaEntrega);
        }

        [TestMethod]
        public void CobroTarjetaCorreo_AlCambiar_SincronizaConEstado()
        {
            // Arrange
            var (vm, _) = CrearViewModelConMocks();

            // Act
            vm.CobroTarjetaCorreo = "test@example.com";

            // Assert
            Assert.AreEqual("test@example.com", vm.Estado.CobroTarjetaCorreo);
        }

        [TestMethod]
        public void CobroTarjetaMovil_AlCambiar_SincronizaConEstado()
        {
            // Arrange
            var (vm, _) = CrearViewModelConMocks();

            // Act
            vm.CobroTarjetaMovil = "612345678";

            // Assert
            Assert.AreEqual("612345678", vm.Estado.CobroTarjetaMovil);
        }

        #endregion

        #region Borrador FechaEntrega Tests

        [TestMethod]
        public void FechaEntrega_RestaurarBorradorConFechaAnteriorAMinima_AjustaAlMinimo()
        {
            // Arrange
            var (vm, _) = CrearViewModelConMocks();
            var fechaMinima = DateTime.Today;
            var fechaBorrador = DateTime.Today.AddDays(-2); // Borrador guardado hace 2 días

            // Simulate: fechaMinimaEntrega se calcula durante la carga del cliente
            vm.fechaMinimaEntrega = fechaMinima;

            // Act: restaurar fecha del borrador que está en el pasado
            vm.fechaEntrega = fechaBorrador;

            // Assert: fechaEntrega no debe ser anterior a fechaMinimaEntrega
            // (el DateTimePicker de WPF lanza ArgumentOutOfRangeException si Value < Minimum)
            Assert.IsTrue(vm.fechaEntrega >= vm.fechaMinimaEntrega,
                $"fechaEntrega ({vm.fechaEntrega:d}) no debería ser anterior a fechaMinimaEntrega ({vm.fechaMinimaEntrega:d})");
        }

        #endregion
    }
}
