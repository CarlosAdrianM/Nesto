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
    }
}
