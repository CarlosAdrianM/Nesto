using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
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
using System.Linq;
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
            PlantillaVentaViewModel vm = new PlantillaVentaViewModel(container, regionManager, configuracion, servicio, eventAggregator, dialogService, pedidoVentaService, servicioBorradores, A.Fake<IServicioAutenticacion>());
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
            PlantillaVentaViewModel vm = new PlantillaVentaViewModel(container, regionManager, configuracion, servicio, eventAggregator, dialogService, pedidoVentaService, servicioBorradores, A.Fake<IServicioAutenticacion>());
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

            var vm = new PlantillaVentaViewModel(container, regionManager, configuracion, servicioMock, eventAggregator, dialogServiceMock, pedidoVentaService, servicioBorradores, A.Fake<IServicioAutenticacion>());
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
        public async Task ActualizarProductosPedido_ProductoBonificable_PrimeraVez_NoMuestraDialogo_Issue314()
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

            // Assert - Issue #314: Ya no se muestra diálogo de confirmación para productos bonificables
            VerifyConfirmationDialogCalled(dialogServiceMock, 0);
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
        public async Task ActualizarProductosPedido_ProductoBonificable_ConCantidadOferta_NoMuestraDialogo_Issue314()
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

            // Assert - Issue #314: Ya no se muestra diálogo de confirmación para productos bonificables
            VerifyConfirmationDialogCalled(dialogServiceMock, 0);
        }

        [TestMethod]
        public async Task ActualizarProductosPedido_ProductoBonificable_NuncaRevierteCantidades_Issue314()
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
                cantidad = 5,
                cantidadOferta = 2,
                fechaInsercion = DateTime.MaxValue
            };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);

            // Act
            vm.cmdActualizarProductosPedido.Execute(linea);

            // Assert - Issue #314: Sin diálogo, las cantidades se mantienen siempre
            Assert.AreEqual(5, linea.cantidad, "La cantidad debería mantenerse sin diálogo");
            Assert.AreEqual(2, linea.cantidadOferta, "La cantidadOferta debería mantenerse sin diálogo");
            VerifyConfirmationDialogCalled(dialogServiceMock, 0);
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

        #region ComentarioPicking al cambiar contacto - Issue #297

        [TestMethod]
        public void DireccionEntrega_CambiarContacto_SinModificacionUsuario_ActualizaSilenciosamente()
        {
            // Arrange
            var (vm, dialogServiceMock) = CrearViewModelConMocks();
            vm.Estado.ComentarioPickingCliente = "Dejar en recepción";
            vm.Estado.ComentarioPicking = "Dejar en recepción"; // No modificado

            var nuevaDireccion = new ControlesUsuario.Models.DireccionesEntregaCliente
            {
                contacto = "1",
                comentarioPicking = "Portería lateral",
                codigoPostal = "28001",
                plazosPago = "CAMBIO"
            };

            // Act
            vm.direccionEntregaSeleccionada = nuevaDireccion;

            // Assert - Actualización silenciosa sin diálogo
            Assert.AreEqual("Portería lateral", vm.Estado.ComentarioPicking);
            Assert.AreEqual("Portería lateral", vm.Estado.ComentarioPickingCliente);
            VerifyConfirmationDialogCalled(dialogServiceMock, 0);
        }

        [TestMethod]
        public void DireccionEntrega_CambiarContacto_ConModificacionUsuario_MuestraDialogo()
        {
            // Arrange
            var (vm, dialogServiceMock) = CrearViewModelConMocks();
            vm.Estado.ComentarioPickingCliente = "Dejar en recepción";
            vm.Estado.ComentarioPicking = "Dejar en recepción - PREGUNTAR POR MARÍA"; // Modificado

            // Configurar diálogo para que usuario acepte mantener su comentario
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

            var nuevaDireccion = new ControlesUsuario.Models.DireccionesEntregaCliente
            {
                contacto = "1",
                comentarioPicking = "Portería lateral",
                codigoPostal = "28001",
                plazosPago = "CAMBIO"
            };

            // Act
            vm.direccionEntregaSeleccionada = nuevaDireccion;

            // Assert - Se mostró diálogo y el usuario mantuvo su comentario
            VerifyConfirmationDialogCalled(dialogServiceMock, 1);
            Assert.AreEqual("Dejar en recepción - PREGUNTAR POR MARÍA", vm.Estado.ComentarioPicking);
            Assert.AreEqual("Portería lateral", vm.Estado.ComentarioPickingCliente);
        }

        [TestMethod]
        public void DireccionEntrega_CambiarContacto_UsuarioCancela_UsaComentarioNuevoContacto()
        {
            // Arrange
            var (vm, dialogServiceMock) = CrearViewModelConMocks();
            vm.Estado.ComentarioPickingCliente = "Dejar en recepción";
            vm.Estado.ComentarioPicking = "Texto personalizado del usuario";

            // Configurar diálogo para que usuario cancele (usar el del nuevo contacto)
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

            var nuevaDireccion = new ControlesUsuario.Models.DireccionesEntregaCliente
            {
                contacto = "1",
                comentarioPicking = "Portería lateral",
                codigoPostal = "28001",
                plazosPago = "CAMBIO"
            };

            // Act
            vm.direccionEntregaSeleccionada = nuevaDireccion;

            // Assert - Se reemplazó por el del nuevo contacto
            Assert.AreEqual("Portería lateral", vm.Estado.ComentarioPicking);
            Assert.AreEqual("Portería lateral", vm.Estado.ComentarioPickingCliente);
        }

        [TestMethod]
        public void DireccionEntrega_CambiarContacto_AmbosNulos_NoMuestraDialogo()
        {
            // Arrange
            var (vm, dialogServiceMock) = CrearViewModelConMocks();
            vm.Estado.ComentarioPickingCliente = null;
            vm.Estado.ComentarioPicking = null;

            var nuevaDireccion = new ControlesUsuario.Models.DireccionesEntregaCliente
            {
                contacto = "1",
                comentarioPicking = "Nuevo comentario",
                codigoPostal = "28001",
                plazosPago = "CAMBIO"
            };

            // Act
            vm.direccionEntregaSeleccionada = nuevaDireccion;

            // Assert
            Assert.AreEqual("Nuevo comentario", vm.Estado.ComentarioPicking);
            Assert.AreEqual("Nuevo comentario", vm.Estado.ComentarioPickingCliente);
            VerifyConfirmationDialogCalled(dialogServiceMock, 0);
        }

        #endregion

        #region Etiquetas Portes - Bug orden de actualización

        private PlantillaVentaViewModel CrearViewModelConPortes(
            decimal importePortes = 7M,
            decimal importeMinimoPedidoSinPortes = 100M,
            decimal comisionReembolso = 0M,
            bool esContraReembolso = false)
        {
            var (vm, _) = CrearViewModelConMocks();
            vm._clienteSeleccionado = new ClienteJson
            {
                empresa = "1",
                cliente = "00000",
                contacto = "0",
                iva = "G21",
                cifNif = "12345678A"
            };
            vm._resultadoPortes = new ResultadoPortesDTO
            {
                ImportePortes = importePortes,
                ImporteMinimoPedidoSinPortes = importeMinimoPedidoSinPortes,
                ComisionReembolso = comisionReembolso,
                EsContraReembolso = esContraReembolso
            };
            return vm;
        }

        [TestMethod]
        public void Portes_SinProductos_TotalPedidoConPortesEsCero()
        {
            // Arrange: cliente fuera de Madrid, sin productos
            var vm = CrearViewModelConPortes();

            // Act
            var total = vm.totalPedidoConPortes;

            // Assert: sin productos, el total debe ser 0
            Assert.AreEqual(0M, total,
                "Sin productos, totalPedidoConPortes debe ser 0 (no 8,47€)");
        }

        [TestMethod]
        public void Portes_SinProductos_ImportePortesMostrarEsCero()
        {
            // Arrange: sin productos
            var vm = CrearViewModelConPortes();

            // Act
            var portes = vm.ImportePortesMostrar;

            // Assert
            Assert.AreEqual(0M, portes,
                "Sin productos, ImportePortesMostrar debe ser 0");
        }

        [TestMethod]
        public void Portes_SinProductos_BaseImponiblePedidoConPortesEsCero()
        {
            // Arrange: sin productos
            var vm = CrearViewModelConPortes();

            // Act
            var baseConPortes = vm.baseImponiblePedidoConPortes;

            // Assert
            Assert.AreEqual(0M, baseConPortes,
                "Sin productos, baseImponiblePedidoConPortes debe ser 0");
        }

        [TestMethod]
        public void Portes_SinProductos_BaseImponibleParaPortesEsCero()
        {
            // Arrange: sin productos
            var vm = CrearViewModelConPortes();

            // Act
            var baseParaPortes = vm.baseImponibleParaPortes;

            // Assert
            Assert.AreEqual(0M, baseParaPortes,
                "Sin productos, baseImponibleParaPortes debe ser 0");
        }

        [TestMethod]
        public void Portes_SinProductos_TextoPortesFaltan100()
        {
            // Arrange: sin productos, umbral 100€
            var vm = CrearViewModelConPortes();

            // Act
            var texto = vm.TextoPortes;

            // Assert
            Assert.IsTrue(texto.Contains("100"),
                $"Sin productos, debería faltar 100€ para portes. TextoPortes: '{texto}'");
        }

        [TestMethod]
        public void Portes_ConProductos_BaseImponiblePedidoAplicaDescuento()
        {
            // Arrange: 24 uds x 4,95€ con 50% descuento
            var vm = CrearViewModelConPortes();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 24,
                precio = 4.95M,
                descuento = 0.5M
            });

            // Act
            var baseImponible = vm.baseImponiblePedido;

            // Assert: 24 * 4.95 = 118.80 bruto, - 59.40 dto = 59.40
            Assert.AreEqual(59.40M, baseImponible,
                "baseImponiblePedido debe aplicar el descuento (59,40€, no 118,80€)");
        }

        [TestMethod]
        public void Portes_ConProductos_BaseImponibleParaPortesCorrecta()
        {
            // Arrange: 24 uds x 4,95€ con 50% descuento
            var vm = CrearViewModelConPortes();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 24,
                precio = 4.95M,
                descuento = 0.5M
            });

            // Act
            var baseParaPortes = vm.baseImponibleParaPortes;

            // Assert
            Assert.AreEqual(59.40M, baseParaPortes,
                "baseImponibleParaPortes debe ser 59,40€");
        }

        [TestMethod]
        public void Portes_ConProductos_ImportePortesMostrarDevuelveSieteEuros()
        {
            // Arrange: base 59.40 < umbral 100, hay que cobrar portes
            var vm = CrearViewModelConPortes();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 24,
                precio = 4.95M,
                descuento = 0.5M
            });

            // Act
            var portes = vm.ImportePortesMostrar;

            // Assert
            Assert.AreEqual(7M, portes,
                "Con base 59,40€ < umbral 100€, ImportePortesMostrar debe ser 7€");
        }

        [TestMethod]
        public void Portes_ConProductos_BaseImponiblePedidoConPortesIncluyePortes()
        {
            // Arrange
            var vm = CrearViewModelConPortes();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 24,
                precio = 4.95M,
                descuento = 0.5M
            });

            // Act
            var baseConPortes = vm.baseImponiblePedidoConPortes;

            // Assert: 59.40 + 7 = 66.40
            Assert.AreEqual(66.40M, baseConPortes,
                "baseImponiblePedidoConPortes debe ser 66,40€ (59,40 + 7 portes), no 118,80€");
        }

        [TestMethod]
        public void Portes_ConProductos_TotalPedidoConPortesIncluyeIVA()
        {
            // Arrange: 24 x 4.95 con 50% dto, cliente con IVA 21%
            var vm = CrearViewModelConPortes();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 24,
                precio = 4.95M,
                descuento = 0.5M
            });

            // Act
            var total = vm.totalPedidoConPortes;

            // Assert: (59.40 + 7) * 1.21 = 66.40 * 1.21 = 80.344
            // totalPedido = 59.40 * 1.21 = 71.874, portesConIva = 7 * 1.21 = 8.47
            // total = 71.874 + 8.47 = 80.344
            Assert.AreEqual(80.344M, total,
                "totalPedidoConPortes debe ser 80,344€ (base 59,40 + portes 7, todo con IVA 21%)");
        }

        [TestMethod]
        public void Portes_ConProductos_TextoPortesFaltanParaPagados()
        {
            // Arrange: base 59.40 < umbral 100
            var vm = CrearViewModelConPortes();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 24,
                precio = 4.95M,
                descuento = 0.5M
            });

            // Act
            var texto = vm.TextoPortes;

            // Assert: falta 100 - 59.40 = 40.60
            Assert.IsFalse(texto.Contains("pagados") && !texto.Contains("Faltan"),
                $"Con base 59,40€ < umbral 100€, no debería decir 'Pedido con portes pagados'. TextoPortes: '{texto}'");
            Assert.IsTrue(texto.Contains("40,60") || texto.Contains("40.60"),
                $"Debería indicar que faltan 40,60€. TextoPortes: '{texto}'");
        }

        [TestMethod]
        public void Portes_ConProductos_PortesGratisEsFalse()
        {
            // Arrange: base 59.40 < umbral 100
            var vm = CrearViewModelConPortes();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 24,
                precio = 4.95M,
                descuento = 0.5M
            });

            // Act & Assert
            Assert.IsFalse(vm.PortesGratis,
                "Con base 59,40€ < umbral 100€, PortesGratis debe ser false");
        }

        [TestMethod]
        public void Portes_SinProductos_ListaConPortesEsNull()
        {
            // Arrange: sin productos
            var vm = CrearViewModelConPortes();

            // Act
            var lista = vm.listaProductosPedidoConPortes;

            // Assert: sin productos, la lista está vacía (no hay línea de portes)
            Assert.IsTrue(lista == null || lista.Count == 0,
                "Sin productos no debe haber línea de portes");
        }

        [TestMethod]
        public void Portes_ConProductos_ListaConPortesIncluyeLineaPortes()
        {
            // Arrange: base 59.40 < umbral 100, hay que cobrar portes
            var vm = CrearViewModelConPortes();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 24,
                precio = 4.95M,
                descuento = 0.5M
            });

            // Act
            var lista = vm.listaProductosPedidoConPortes;

            // Assert: debe incluir el producto + la línea de portes simulada
            Assert.AreEqual(2, lista.Count, "Debe haber 1 producto + 1 línea de portes");
            var lineaPortes = lista.Last();
            Assert.IsTrue(lineaPortes.esLineaPortes, "La última línea debe ser la de portes");
            Assert.AreEqual("Portes", lineaPortes.texto);
            Assert.AreEqual(7M, lineaPortes.precio);
            Assert.AreEqual(1, lineaPortes.cantidad);
        }

        [TestMethod]
        public void Portes_ConProductos_PortesPagados_ListaSinLineaPortes()
        {
            // Arrange: base > umbral, portes gratis
            var vm = CrearViewModelConPortes(importePortes: 7M, importeMinimoPedidoSinPortes: 50M);
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 24,
                precio = 4.95M,
                descuento = 0.5M
            });

            // Act
            var lista = vm.listaProductosPedidoConPortes;

            // Assert: base 59.40 >= umbral 50, portes gratis, no debe haber línea de portes
            Assert.AreEqual(1, lista.Count, "Solo debe haber 1 producto, sin línea de portes");
            Assert.IsFalse(lista[0].esLineaPortes);
        }

        [TestMethod]
        public void Portes_PropiedadesIdempotentes_MismoResultadoEnMultiplesLecturas()
        {
            // Arrange
            var vm = CrearViewModelConPortes();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 24,
                precio = 4.95M,
                descuento = 0.5M
            });

            // Act: leer en distintos órdenes
            var total1 = vm.totalPedidoConPortes;
            var base1 = vm.baseImponiblePedido;
            var basePortes1 = vm.baseImponiblePedidoConPortes;
            var portes1 = vm.ImportePortesMostrar;

            // Segunda lectura en orden inverso
            var portes2 = vm.ImportePortesMostrar;
            var basePortes2 = vm.baseImponiblePedidoConPortes;
            var base2 = vm.baseImponiblePedido;
            var total2 = vm.totalPedidoConPortes;

            // Assert: las lecturas deben ser idempotentes
            Assert.AreEqual(total1, total2, "totalPedidoConPortes no es idempotente");
            Assert.AreEqual(base1, base2, "baseImponiblePedido no es idempotente");
            Assert.AreEqual(basePortes1, basePortes2, "baseImponiblePedidoConPortes no es idempotente");
            Assert.AreEqual(portes1, portes2, "ImportePortesMostrar no es idempotente");
        }

        [TestMethod]
        public void Portes_ProductoEstado4_ConServirJunto_StockGlobalSuficiente_CuentaParaPortes()
        {
            // Bug: producto estado 4, 2 uds pedidas, stock almacén 1, stock global 4
            // Con servirJunto=true, la línea debe contar para baseImponibleParaPortes
            var vm = CrearViewModelConPortes();
            vm._direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = true };
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                estado = 0, cantidad = 1, precio = 80.96M, descuento = 0,
                stockActualizado = true, cantidadDisponible = 10, StockDisponibleTodosLosAlmacenes = 10
            });
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                estado = 4, cantidad = 2, precio = 38M, descuento = 0,
                stockActualizado = true, cantidadDisponible = 1, StockDisponibleTodosLosAlmacenes = 4
            });

            var baseParaPortes = vm.baseImponibleParaPortes;

            // 80.96 + (2 * 38) = 156.96 — ambas líneas deben contar
            Assert.AreEqual(156.96M, baseParaPortes,
                "Con servirJunto y stock global suficiente, la línea de estado 4 debe contar para portes");
        }

        [TestMethod]
        public void Portes_ProductoEstado4_SinServirJunto_StockAlmacenInsuficiente_NoCuentaParaPortes()
        {
            // Sin servirJunto, stock almacén insuficiente → la línea NO cuenta para portes
            var vm = CrearViewModelConPortes();
            vm._direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                estado = 0, cantidad = 1, precio = 80.96M, descuento = 0,
                stockActualizado = true, cantidadDisponible = 10, StockDisponibleTodosLosAlmacenes = 10
            });
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                estado = 4, cantidad = 2, precio = 38M, descuento = 0,
                stockActualizado = true, cantidadDisponible = 1, StockDisponibleTodosLosAlmacenes = 4
            });

            var baseParaPortes = vm.baseImponibleParaPortes;

            // Solo 80.96 — la línea de estado 4 no cuenta porque sin servirJunto
            // solo se mira stock del almacén (1 < 2)
            Assert.AreEqual(80.96M, baseParaPortes,
                "Sin servirJunto y stock almacén insuficiente, la línea de estado 4 NO debe contar para portes");
        }

        [TestMethod]
        public void Portes_DireccionNull_ServirJuntoPorDefectoTrue_UsaStockGlobal()
        {
            // Sin dirección seleccionada, servirJunto por defecto true
            var vm = CrearViewModelConPortes();
            // No asignar dirección — simula carga inicial
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                estado = 4, cantidad = 2, precio = 38M, descuento = 0,
                stockActualizado = true, cantidadDisponible = 1, StockDisponibleTodosLosAlmacenes = 4
            });

            var baseParaPortes = vm.baseImponibleParaPortes;

            Assert.AreEqual(76M, baseParaPortes,
                "Sin dirección (servirJunto por defecto true), debe usar stock global");
        }

        #endregion

        #region Issue #159 - Línea virtual de comisión contra reembolso

        [TestMethod]
        public void Reembolso_ConProductosYContraReembolso_ListaIncluyeLineaReembolso()
        {
            // Arrange: contra reembolso con comisión 3€
            var vm = CrearViewModelConPortes(
                importePortes: 7M,
                importeMinimoPedidoSinPortes: 50M,
                comisionReembolso: 3M,
                esContraReembolso: true);
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 24,
                precio = 4.95M,
                descuento = 0.5M
            });

            // Act
            var lista = vm.listaProductosPedidoConPortes;

            // Assert: producto + portes + reembolso. Base=59.40 < umbral 50? no, es >= 50
            // Con umbral 50 base >= umbral → portes gratis, pero reembolso sí.
            var lineaReembolso = lista.FirstOrDefault(l => l.esLineaReembolso);
            Assert.IsNotNull(lineaReembolso, "Debe existir la línea de comisión reembolso");
            Assert.AreEqual("Comisión contra reembolso", lineaReembolso.texto);
            Assert.AreEqual(3M, lineaReembolso.precio);
            Assert.AreEqual(1, lineaReembolso.cantidad);
        }

        [TestMethod]
        public void Reembolso_NoEsContraReembolso_ListaSinLineaReembolso()
        {
            var vm = CrearViewModelConPortes(
                comisionReembolso: 3M,
                esContraReembolso: false);
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 1, precio = 100M
            });

            var lista = vm.listaProductosPedidoConPortes;

            Assert.IsFalse(lista.Any(l => l.esLineaReembolso),
                "Si no es contra reembolso no debe haber línea de comisión");
        }

        [TestMethod]
        public void Reembolso_SinProductos_ListaSinLineaReembolso()
        {
            // Sin productos la línea de reembolso no debe añadirse.
            var vm = CrearViewModelConPortes(
                comisionReembolso: 3M,
                esContraReembolso: true);

            var lista = vm.listaProductosPedidoConPortes;

            Assert.IsTrue(lista == null || !lista.Any(l => l.esLineaReembolso),
                "Sin productos no debe añadirse línea de reembolso");
        }

        [TestMethod]
        public void Reembolso_ComisionCero_ListaSinLineaReembolso()
        {
            // Aunque sea contra reembolso, si la comisión es 0 no añadimos línea.
            var vm = CrearViewModelConPortes(
                comisionReembolso: 0M,
                esContraReembolso: true);
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 1, precio = 100M
            });

            var lista = vm.listaProductosPedidoConPortes;

            Assert.IsFalse(lista.Any(l => l.esLineaReembolso));
        }

        [TestMethod]
        public void Reembolso_BaseImponibleConPortes_IncluyeElImporteDeReembolso()
        {
            var vm = CrearViewModelConPortes(
                importePortes: 7M,
                importeMinimoPedidoSinPortes: 1M, // portes siempre gratis con este umbral
                comisionReembolso: 3M,
                esContraReembolso: true);
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 1, precio = 100M
            });

            // Base productos 100 + reembolso 3 = 103 (portes gratis con umbral 1)
            Assert.AreEqual(103M, vm.baseImponiblePedidoConPortes);
        }

        #endregion
    }
}
