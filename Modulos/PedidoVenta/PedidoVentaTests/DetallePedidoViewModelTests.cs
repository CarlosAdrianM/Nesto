﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FakeItEasy;
using Nesto.Modulos.PedidoVenta;
using static Nesto.Models.PedidoVenta;
using Microsoft.Practices.Prism.Regions;
using Nesto.Contratos;
using System.ComponentModel;
using System.Windows.Controls;

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
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio);
            PedidoVentaDTO pedido = A.Fake<PedidoVentaDTO>();
            LineaPedidoVentaDTO lineaFake = A.Fake<LineaPedidoVentaDTO>();
            lineaFake.descuentoProducto = (decimal).4;
            lineaFake.cantidad = 1;
            lineaFake.precio = 100;
            pedido.LineasPedido.Add(lineaFake);
            detallePedidoViewModel.pedido = pedido;

            // Act
            lineaFake.aplicarDescuento = false;

            // Assert
            Assert.AreEqual(100, detallePedidoViewModel.pedido.baseImponible);
        }

        [TestMethod]
        public void DetallePedidoViewModel_siAplicaDescuentoEsTrue_calculaCorrectamenteLosNuevosDescuentos()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio);
            PedidoVentaDTO pedido = A.Fake<PedidoVentaDTO>();
            LineaPedidoVentaDTO lineaFake = A.Fake<LineaPedidoVentaDTO>();
            lineaFake.descuentoProducto = (decimal).4;
            lineaFake.aplicarDescuento = false;
            lineaFake.cantidad = 1;
            lineaFake.precio = 100;
            pedido.LineasPedido.Add(lineaFake);
            detallePedidoViewModel.pedido = pedido;

            // Act
            lineaFake.aplicarDescuento = true;

            // Assert
            Assert.AreEqual(60, detallePedidoViewModel.pedido.baseImponible);
        }

        [TestMethod]
        public void DetallePedidoViewModel_siSeModificaElDescuentoProducto_debeLanzarsePropertyChanged()
        {
            // Arrange
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            DetallePedidoViewModel detallePedidoViewModel = new DetallePedidoViewModel(regionManager, configuracion, servicio);
            PedidoVentaDTO pedido = A.Fake<PedidoVentaDTO>();
            LineaPedidoVentaDTO lineaFake = A.Fake<LineaPedidoVentaDTO>();
            lineaFake.descuentoProducto = (decimal).4;
            lineaFake.aplicarDescuento = true;
            lineaFake.cantidad = 1;
            lineaFake.precio = 100;
            pedido.LineasPedido.Add(lineaFake);
            detallePedidoViewModel.pedido = pedido;
            var handler = A.Fake<PropertyChangedEventHandler>();
            lineaFake.PropertyChanged += handler;

            // Act
            lineaFake.descuentoProducto = (decimal).3;

            // Assert
            A.CallTo(() => handler.Invoke(A<object>._, A<PropertyChangedEventArgs>.That.Matches(s => s.PropertyName == "descuentoProducto"))).MustHaveHappened(Repeated.Exactly.Once);
        }
    }
}