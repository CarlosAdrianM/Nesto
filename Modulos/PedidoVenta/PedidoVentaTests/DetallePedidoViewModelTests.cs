using Microsoft.VisualStudio.TestTools.UnitTesting;
using FakeItEasy;
using Nesto.Modulos.PedidoVenta;
using Prism.Regions;
using System.ComponentModel;
using Prism.Events;
using Prism.Services.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using ControlesUsuario.Models;
using Unity;

namespace PedidoVentaTests
{
    [TestClass]
    public class DetallePedidoViewModelTests
    {
        [TestMethod]
        public void DetallePedidoViewModel_siAplicaDescuentoEsFalse_noTieneEnCuentaElDescuentoProducto()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);
            PedidoVentaDTO pedido = new PedidoVentaDTO();
            LineaPedidoVentaDTO lineaFake = new LineaPedidoVentaDTO() { id = 1 };
            lineaFake.DescuentoProducto = (decimal).4;
            lineaFake.Cantidad = 1;
            lineaFake.PrecioUnitario = 100;
            pedido.Lineas.Add(lineaFake);
            detallePedidoViewModel.pedido = new PedidoVentaWrapper(pedido);

            // Act
            lineaFake.AplicarDescuento = false;

            // Assert
            Assert.AreEqual(100, detallePedidoViewModel.pedido.BaseImponible);
        }

        [TestMethod]
        public void DetallePedidoViewModel_siAplicaDescuentoEsTrue_calculaCorrectamenteLosNuevosDescuentos()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);
            PedidoVentaDTO pedido = new PedidoVentaDTO();
            LineaPedidoVentaDTO lineaFake = new LineaPedidoVentaDTO { id = 1 };
            lineaFake.DescuentoProducto = (decimal).4;
            lineaFake.AplicarDescuento = false;
            lineaFake.Cantidad = 1;
            lineaFake.PrecioUnitario = 100;
            pedido.Lineas.Add(lineaFake);
            detallePedidoViewModel.pedido = new PedidoVentaWrapper(pedido);

            // Act
            lineaFake.AplicarDescuento = true;

            // Assert
            Assert.AreEqual(60, detallePedidoViewModel.pedido.BaseImponible);
        }

        [TestMethod]
        public void DetallePedidoViewModel_siSeModificaElDescuentoProducto_debeLanzarsePropertyChanged()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);
            PedidoVentaWrapper pedido = new PedidoVentaWrapper(new PedidoVentaDTO());
            LineaPedidoVentaWrapper lineaFake = new LineaPedidoVentaWrapper { id = 1 };
            lineaFake.DescuentoProducto = (decimal).4;
            lineaFake.AplicarDescuento = true;
            lineaFake.Cantidad = 1;
            lineaFake.PrecioUnitario = 100;
            pedido.Lineas.Add(lineaFake);
            detallePedidoViewModel.pedido = pedido;
            var handler = A.Fake<PropertyChangedEventHandler>();
            lineaFake.PropertyChanged += handler;

            // Act
            lineaFake.DescuentoProducto = (decimal).3;

            // Assert
            A.CallTo(() => handler.Invoke(A<object>._, A<PropertyChangedEventArgs>.That.Matches(s => s.PropertyName == "DescuentoProducto"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void DetallePedidoViewModel_alAsignarClienteCompleto_debeAsignarOrigenDesdeEmpresaCliente()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);

            // Simular que ya existe un pedido
            PedidoVentaDTO pedido = new PedidoVentaDTO { empresa = "1", numero = 0 };
            detallePedidoViewModel.pedido = new PedidoVentaWrapper(pedido);

            // Simular selección de cliente
            ClienteDTO clienteSeleccionado = new ClienteDTO
            {
                empresa = "1",
                cliente = "12345",
                contacto = "1"
            };

            // Act
            detallePedidoViewModel.ClienteCompleto = clienteSeleccionado;

            // Assert
            Assert.AreEqual("1", detallePedidoViewModel.pedido.Model.origen);
        }

        [TestMethod]
        public void DetallePedidoViewModel_alAsignarClienteCompleto_debeAsignarContactoCobroDesdeContactoCliente()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);

            // Simular que ya existe un pedido
            PedidoVentaDTO pedido = new PedidoVentaDTO { empresa = "1", numero = 0 };
            detallePedidoViewModel.pedido = new PedidoVentaWrapper(pedido);

            // Simular selección de cliente
            ClienteDTO clienteSeleccionado = new ClienteDTO
            {
                empresa = "1",
                cliente = "12345",
                contacto = "2"
            };

            // Act
            detallePedidoViewModel.ClienteCompleto = clienteSeleccionado;

            // Assert
            Assert.AreEqual("2", detallePedidoViewModel.pedido.Model.contactoCobro);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task DetallePedidoViewModel_alCrearPedidoNuevo_debeInicializarOrigenYContactoCobroVacios()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();

            // Mockear la configuración para devolver un vendedor por defecto
            A.CallTo(() => configuracion.leerParametro("1", A<string>._)).Returns(System.Threading.Tasks.Task.FromResult("VEN01"));
            A.CallTo(() => configuracion.usuario).Returns("TEST_USER");

            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);

            // Crear un ResumenPedido para un pedido nuevo (numero = 0)
            var resumenPedido = new PedidoVentaModel.ResumenPedido { empresa = "1", numero = 0 };

            // Act - Cargar pedido nuevo mediante el comando (simulando OnNavigatedTo)
            detallePedidoViewModel.cmdCargarPedido.Execute(resumenPedido);

            // Esperar a que se complete la operación asíncrona (con timeout de 5 segundos)
            var maxWait = 50; // 50 * 100ms = 5 segundos
            while (detallePedidoViewModel.pedido == null && maxWait-- > 0)
            {
                await System.Threading.Tasks.Task.Delay(100);
            }

            // Assert - Verificar que se inicializan como cadenas vacías
            Assert.IsNotNull(detallePedidoViewModel.pedido, "El pedido no debe ser null");
            Assert.IsNotNull(detallePedidoViewModel.pedido.Model.origen, "El origen no debe ser null");
            Assert.IsNotNull(detallePedidoViewModel.pedido.Model.contactoCobro, "El contactoCobro no debe ser null");
            Assert.AreEqual(string.Empty, detallePedidoViewModel.pedido.Model.origen);
            Assert.AreEqual(string.Empty, detallePedidoViewModel.pedido.Model.contactoCobro);
        }

        #region Tests CCC (Cuenta Corriente Cliente)

        [TestMethod]
        [TestCategory("CCC")]
        public void DetallePedidoViewModel_alCambiarDireccionEntregaEnPedidoNuevo_NOcopiaCCC_loManejaElSelectorCCC()
        {
            // Carlos 20/11/24: El CCC ya NO se copia automáticamente en DireccionEntregaSeleccionada.
            // Ahora lo maneja el control SelectorCCC de forma independiente.
            // Este test verifica que el setter de DireccionEntregaSeleccionada NO modifica el CCC.

            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);

            // Simular pedido NUEVO (numero = 0)
            PedidoVentaDTO pedido = new PedidoVentaDTO
            {
                empresa = "1",
                numero = 0,  // Pedido nuevo
                ccc = ""     // Sin CCC inicial
            };
            detallePedidoViewModel.pedido = new PedidoVentaWrapper(pedido);

            // Simular selección de dirección con CCC
            DireccionesEntregaCliente direccionConCCC = new DireccionesEntregaCliente
            {
                contacto = "1",
                nombre = "Dirección Principal",
                ccc = "ES1234567890123456789012",
                formaPago = "EFC",
                plazosPago = "CONTADO",
                iva = "G21",
                vendedor = "VEN01",
                ruta = "1",
                periodoFacturacion = "NRM"
            };

            // Act
            detallePedidoViewModel.DireccionEntregaSeleccionada = direccionConCCC;

            // Assert: El CCC NO se copia - lo maneja SelectorCCC
            Assert.IsTrue(string.IsNullOrEmpty(detallePedidoViewModel.pedido.ccc),
                "El CCC NO debe copiarse desde DireccionEntregaSeleccionada - lo maneja SelectorCCC automáticamente");
        }

        [TestMethod]
        [TestCategory("CCC")]
        public void DetallePedidoViewModel_alCambiarDireccionEntregaEnPedidoExistente_NOdebeCambiarCCC()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);

            // Simular pedido EXISTENTE (numero > 0) con CCC ya establecido
            PedidoVentaDTO pedido = new PedidoVentaDTO
            {
                empresa = "1",
                numero = 900630,  // Pedido existente
                ccc = "ES3333333333333333333333"  // CCC original del pedido
            };
            detallePedidoViewModel.pedido = new PedidoVentaWrapper(pedido);

            // Simular selección de dirección con CCC DIFERENTE
            DireccionesEntregaCliente direccionConOtroCCC = new DireccionesEntregaCliente
            {
                contacto = "1",
                nombre = "Dirección Secundaria",
                ccc = "ES1111111111111111111111",  // CCC diferente
                formaPago = "EFC",
                plazosPago = "CONTADO",
                iva = "G21",
                vendedor = "VEN01",
                ruta = "1",
                periodoFacturacion = "NRM"
            };

            // Act
            detallePedidoViewModel.DireccionEntregaSeleccionada = direccionConOtroCCC;

            // Assert
            Assert.AreEqual("ES3333333333333333333333", detallePedidoViewModel.pedido.ccc,
                "En un pedido EXISTENTE, el CCC NO debe cambiar al seleccionar otra dirección de entrega");
        }

        [TestMethod]
        [TestCategory("CCC")]
        public void DetallePedidoViewModel_EstaCreandoPedido_esTrueSoloParaPedidosNuevos()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);

            // Test 1: Pedido nuevo (numero = 0)
            PedidoVentaDTO pedidoNuevo = new PedidoVentaDTO { empresa = "1", numero = 0 };
            detallePedidoViewModel.pedido = new PedidoVentaWrapper(pedidoNuevo);

            Assert.IsTrue(detallePedidoViewModel.EstaCreandoPedido,
                "EstaCreandoPedido debe ser TRUE cuando numero = 0");

            // Test 2: Pedido existente (numero > 0)
            PedidoVentaDTO pedidoExistente = new PedidoVentaDTO { empresa = "1", numero = 900630 };
            detallePedidoViewModel.pedido = new PedidoVentaWrapper(pedidoExistente);

            Assert.IsFalse(detallePedidoViewModel.EstaCreandoPedido,
                "EstaCreandoPedido debe ser FALSE cuando numero > 0");

            // Test 3: Sin pedido
            detallePedidoViewModel.pedido = null;

            Assert.IsFalse(detallePedidoViewModel.EstaCreandoPedido,
                "EstaCreandoPedido debe ser FALSE cuando pedido es null");
        }

        [TestMethod]
        [TestCategory("CCC")]
        public void DetallePedidoViewModel_alCambiarDireccion_copiaDatosFacturacionExceptoCCCEnPedidoNuevo()
        {
            // Carlos 20/11/24: El CCC ya NO se copia aquí - lo maneja SelectorCCC.
            // Los demás datos de facturación SÍ se copian.

            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);

            // Simular pedido NUEVO
            PedidoVentaDTO pedido = new PedidoVentaDTO { empresa = "1", numero = 0 };
            detallePedidoViewModel.pedido = new PedidoVentaWrapper(pedido);

            // Simular dirección con todos los datos de facturación
            DireccionesEntregaCliente direccion = new DireccionesEntregaCliente
            {
                contacto = "1",
                formaPago = "TRF",
                plazosPago = "30D",
                iva = "G04",
                vendedor = "VEN02",
                ruta = "5",
                periodoFacturacion = "QUI",
                ccc = "ES9999999999999999999999"
            };

            // Act
            detallePedidoViewModel.DireccionEntregaSeleccionada = direccion;

            // Assert - Verificar que todos los campos EXCEPTO CCC se copian
            Assert.AreEqual("TRF", detallePedidoViewModel.pedido.formaPago, "formaPago debe copiarse");
            Assert.AreEqual("30D", detallePedidoViewModel.pedido.plazosPago, "plazosPago debe copiarse");
            Assert.AreEqual("G04", detallePedidoViewModel.pedido.iva, "iva debe copiarse");
            Assert.AreEqual("VEN02", detallePedidoViewModel.pedido.vendedor, "vendedor debe copiarse");
            Assert.AreEqual("5", detallePedidoViewModel.pedido.ruta, "ruta debe copiarse");
            Assert.AreEqual("QUI", detallePedidoViewModel.pedido.periodoFacturacion, "periodoFacturacion debe copiarse");
            // CCC NO se copia - lo maneja SelectorCCC
            Assert.IsTrue(string.IsNullOrEmpty(detallePedidoViewModel.pedido.ccc),
                "CCC NO debe copiarse desde DireccionEntregaSeleccionada - lo maneja SelectorCCC");
        }

        [TestMethod]
        [TestCategory("CCC")]
        public void DetallePedidoViewModel_alCambiarDireccion_NOcopiaDatosEnPedidoExistente()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);

            // Simular pedido EXISTENTE con datos originales
            PedidoVentaDTO pedido = new PedidoVentaDTO
            {
                empresa = "1",
                numero = 900630,
                formaPago = "EFC",
                plazosPago = "CONTADO",
                iva = "G21",
                vendedor = "VEN01",
                ruta = "1",
                periodoFacturacion = "NRM",
                ccc = "ES_ORIGINAL_CCC"
            };
            detallePedidoViewModel.pedido = new PedidoVentaWrapper(pedido);

            // Simular dirección con datos DIFERENTES
            DireccionesEntregaCliente direccion = new DireccionesEntregaCliente
            {
                contacto = "2",
                formaPago = "TRF",
                plazosPago = "60D",
                iva = "G04",
                vendedor = "VEN99",
                ruta = "9",
                periodoFacturacion = "QUI",
                ccc = "ES_OTRO_CCC_COMPLETAMENTE_DIFERENTE"
            };

            // Act
            detallePedidoViewModel.DireccionEntregaSeleccionada = direccion;

            // Assert - Verificar que NINGÚN campo cambia
            Assert.AreEqual("EFC", detallePedidoViewModel.pedido.formaPago, "formaPago NO debe cambiar");
            Assert.AreEqual("CONTADO", detallePedidoViewModel.pedido.plazosPago, "plazosPago NO debe cambiar");
            Assert.AreEqual("G21", detallePedidoViewModel.pedido.iva, "iva NO debe cambiar");
            Assert.AreEqual("VEN01", detallePedidoViewModel.pedido.vendedor, "vendedor NO debe cambiar");
            Assert.AreEqual("1", detallePedidoViewModel.pedido.ruta, "ruta NO debe cambiar");
            Assert.AreEqual("NRM", detallePedidoViewModel.pedido.periodoFacturacion, "periodoFacturacion NO debe cambiar");
            Assert.AreEqual("ES_ORIGINAL_CCC", detallePedidoViewModel.pedido.ccc, "CCC NO debe cambiar");
        }

        #endregion

        #region Tests PasarAPresupuestoCommand (Issue #257)

        [TestMethod]
        [TestCategory("PasarAPresupuesto")]
        public void PasarAPresupuestoCommand_CanExecute_esTrueCuandoHayLineasPendientesSinPicking()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel vm = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);

            // Pedido NO es presupuesto, con línea en estado -1 (pendiente) y sin picking
            PedidoVentaDTO pedido = new PedidoVentaDTO { empresa = "1", numero = 12345 };
            LineaPedidoVentaDTO linea = new LineaPedidoVentaDTO
            {
                id = 1,
                estado = -1,  // ESTADO_LINEA_PENDIENTE
                picking = 0   // Sin picking
            };
            pedido.Lineas.Add(linea);
            vm.pedido = new PedidoVentaWrapper(pedido);

            // Act & Assert
            Assert.IsTrue(vm.PasarAPresupuestoCommand.CanExecute(),
                "CanExecute debe ser TRUE cuando hay líneas pendientes sin picking");
        }

        [TestMethod]
        [TestCategory("PasarAPresupuesto")]
        public void PasarAPresupuestoCommand_CanExecute_esTrueCuandoHayLineasEnCursoSinPicking()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel vm = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);

            // Pedido NO es presupuesto, con línea en estado 1 (en curso) y sin picking
            PedidoVentaDTO pedido = new PedidoVentaDTO { empresa = "1", numero = 12345 };
            LineaPedidoVentaDTO linea = new LineaPedidoVentaDTO
            {
                id = 1,
                estado = 1,   // ESTADO_LINEA_EN_CURSO
                picking = 0   // Sin picking
            };
            pedido.Lineas.Add(linea);
            vm.pedido = new PedidoVentaWrapper(pedido);

            // Act & Assert
            Assert.IsTrue(vm.PasarAPresupuestoCommand.CanExecute(),
                "CanExecute debe ser TRUE cuando hay líneas en curso sin picking");
        }

        [TestMethod]
        [TestCategory("PasarAPresupuesto")]
        public void PasarAPresupuestoCommand_CanExecute_esFalseCuandoPedidoEsPresupuesto()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel vm = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);

            // Pedido YA es presupuesto (todas las líneas en estado -3)
            PedidoVentaDTO pedido = new PedidoVentaDTO { empresa = "1", numero = 12345 };
            LineaPedidoVentaDTO linea = new LineaPedidoVentaDTO
            {
                id = 1,
                estado = -3,  // ESTADO_LINEA_PRESUPUESTO
                picking = 0
            };
            pedido.Lineas.Add(linea);
            vm.pedido = new PedidoVentaWrapper(pedido);

            // Act & Assert
            Assert.IsFalse(vm.PasarAPresupuestoCommand.CanExecute(),
                "CanExecute debe ser FALSE cuando el pedido ya es presupuesto");
        }

        [TestMethod]
        [TestCategory("PasarAPresupuesto")]
        public void PasarAPresupuestoCommand_CanExecute_esFalseCuandoTodasLasLineasTienenPicking()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel vm = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);

            // Pedido con líneas pendientes pero CON picking asignado
            PedidoVentaDTO pedido = new PedidoVentaDTO { empresa = "1", numero = 12345 };
            LineaPedidoVentaDTO linea = new LineaPedidoVentaDTO
            {
                id = 1,
                estado = -1,  // ESTADO_LINEA_PENDIENTE
                picking = 100 // CON picking
            };
            pedido.Lineas.Add(linea);
            vm.pedido = new PedidoVentaWrapper(pedido);

            // Act & Assert
            Assert.IsFalse(vm.PasarAPresupuestoCommand.CanExecute(),
                "CanExecute debe ser FALSE cuando todas las líneas tienen picking asignado");
        }

        [TestMethod]
        [TestCategory("PasarAPresupuesto")]
        public void PasarAPresupuestoCommand_CanExecute_esFalseCuandoLineasEstanAlbaranadas()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel vm = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);

            // Pedido con líneas en estado albarán (2) - no se pueden pasar a presupuesto
            PedidoVentaDTO pedido = new PedidoVentaDTO { empresa = "1", numero = 12345 };
            LineaPedidoVentaDTO linea = new LineaPedidoVentaDTO
            {
                id = 1,
                estado = 2,   // ESTADO_ALBARAN
                picking = 0
            };
            pedido.Lineas.Add(linea);
            vm.pedido = new PedidoVentaWrapper(pedido);

            // Act & Assert
            Assert.IsFalse(vm.PasarAPresupuestoCommand.CanExecute(),
                "CanExecute debe ser FALSE cuando las líneas están albaraneadas");
        }

        [TestMethod]
        [TestCategory("PasarAPresupuesto")]
        public void PasarAPresupuestoCommand_CanExecute_esTrueCuandoAlgunasLineasSonValidasYOtrasNo()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IUnityContainer container = A.Fake<IUnityContainer>();
            DetallePedidoViewModel vm = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, container);

            // Pedido con mezcla de líneas: una válida y una albaraneada
            PedidoVentaDTO pedido = new PedidoVentaDTO { empresa = "1", numero = 12345 };

            // Línea albaraneada (NO válida para pasar a presupuesto)
            LineaPedidoVentaDTO lineaAlbaranada = new LineaPedidoVentaDTO
            {
                id = 1,
                estado = 2,   // ESTADO_ALBARAN
                picking = 0
            };
            pedido.Lineas.Add(lineaAlbaranada);

            // Línea pendiente sin picking (SÍ válida para pasar a presupuesto)
            LineaPedidoVentaDTO lineaPendiente = new LineaPedidoVentaDTO
            {
                id = 2,
                estado = -1,  // ESTADO_LINEA_PENDIENTE
                picking = 0   // Sin picking
            };
            pedido.Lineas.Add(lineaPendiente);

            vm.pedido = new PedidoVentaWrapper(pedido);

            // Act & Assert
            Assert.IsTrue(vm.PasarAPresupuestoCommand.CanExecute(),
                "CanExecute debe ser TRUE cuando al menos una línea es válida para pasar a presupuesto");
        }

        #endregion
    }
}
