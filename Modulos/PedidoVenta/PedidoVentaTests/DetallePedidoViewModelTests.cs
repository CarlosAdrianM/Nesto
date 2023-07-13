using Microsoft.VisualStudio.TestTools.UnitTesting;
using FakeItEasy;
using Nesto.Modulos.PedidoVenta;
using Prism.Regions;
using System.ComponentModel;
using Prism.Events;
using Prism.Services.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;

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
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService);
            PedidoVentaDTO pedido = A.Fake<PedidoVentaDTO>();
            LineaPedidoVentaDTO lineaFake = A.Fake<LineaPedidoVentaDTO>();
            lineaFake.DescuentoProducto = (decimal).4;
            lineaFake.Cantidad = 1;
            lineaFake.PrecioUnitario = 100;
            pedido.Lineas.Add(lineaFake);
            detallePedidoViewModel.pedido = pedido;

            // Act
            lineaFake.aplicarDescuento = false;

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
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService);
            PedidoVentaDTO pedido = A.Fake<PedidoVentaDTO>();
            LineaPedidoVentaDTO lineaFake = A.Fake<LineaPedidoVentaDTO>();
            lineaFake.DescuentoProducto = (decimal).4;
            lineaFake.aplicarDescuento = false;
            lineaFake.Cantidad = 1;
            lineaFake.PrecioUnitario = 100;
            pedido.Lineas.Add(lineaFake);
            detallePedidoViewModel.pedido = pedido;

            // Act
            lineaFake.aplicarDescuento = true;

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
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService);
            PedidoVentaDTO pedido = A.Fake<PedidoVentaDTO>();
            LineaPedidoVentaDTO lineaFake = A.Fake<LineaPedidoVentaDTO>();
            lineaFake.DescuentoProducto = (decimal).4;
            lineaFake.aplicarDescuento = true;
            lineaFake.Cantidad = 1;
            lineaFake.PrecioUnitario = 100;
            pedido.Lineas.Add(lineaFake);
            detallePedidoViewModel.pedido = pedido;
            var handler = A.Fake<PropertyChangedEventHandler>();
            lineaFake.PropertyChanged += handler;

            // Act
            lineaFake.DescuentoProducto = (decimal).3;

            // Assert
            A.CallTo(() => handler.Invoke(A<object>._, A<PropertyChangedEventArgs>.That.Matches(s => s.PropertyName == "descuentoProducto"))).MustHaveHappenedOnceExactly();
        }
    }
}
