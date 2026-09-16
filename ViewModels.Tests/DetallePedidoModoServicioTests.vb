Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Services.ServirJunto
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Prism.Events
Imports Prism.Regions
Imports Prism.Services.Dialogs
Imports Unity

''' <summary>
''' Nesto#476: el selector de modo de servicio de DetallePedido sustituye a la casilla «Servir junto».
''' El wrapper mantiene coherentes modoServicio y servirJunto, y la validación del servidor solo se
''' dispara al SALIR de «Todo junto», no al cargar el pedido ni al cambiar entre modos parciales.
''' </summary>
<TestClass()>
Public Class DetallePedidoModoServicioTests

    <TestMethod()>
    Public Sub Wrapper_PedidoAnteriorAlModo_MuestraElQueDerivaDeServirJunto()
        Dim marcado = New PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1", .servirJunto = True})
        Dim desmarcado = New PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1", .servirJunto = False})

        Assert.AreEqual(ModosServicio.TODO_JUNTO, marcado.ModoServicio)
        Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, desmarcado.ModoServicio)
    End Sub

    <TestMethod()>
    Public Sub Wrapper_ElegirUnModoParcial_DesmarcaServirJuntoYLoGuardaEnElModelo()
        Dim wrapper = New PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1", .servirJunto = True})

        wrapper.ModoServicio = ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ

        Assert.AreEqual(CByte(4), wrapper.Model.modoServicio)
        Assert.IsFalse(wrapper.servirJunto)
    End Sub

    <TestMethod()>
    Public Sub Wrapper_VolverATodoJunto_MarcaServirJunto()
        Dim wrapper = New PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1", .servirJunto = False, .modoServicio = 4})

        wrapper.ModoServicio = ModosServicio.TODO_JUNTO

        Assert.IsTrue(wrapper.servirJunto)
        Assert.AreEqual(CByte(1), wrapper.Model.modoServicio)
    End Sub

    <TestMethod()>
    Public Sub Wrapper_MarcarServirJuntoPorCodigo_PoneElModo1_YDesmarcarloConModoParcialLoRespeta()
        Dim wrapper = New PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1", .servirJunto = False, .modoServicio = 4})

        wrapper.servirJunto = True
        Assert.AreEqual(ModosServicio.TODO_JUNTO, wrapper.ModoServicio)

        wrapper.ModoServicio = 4
        wrapper.servirJunto = False
        Assert.AreEqual(CByte(4), wrapper.ModoServicio, "Desmarcar con un modo parcial puesto no lo pisa")

        wrapper.servirJunto = True
        wrapper.servirJunto = False
        Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, wrapper.ModoServicio, "Desmarcar desde el 1 cae al 2")
    End Sub

    Private Shared Function CrearViewModel(servicio As IServirJuntoService, pedido As PedidoVentaDTO) As DetallePedidoViewModel
        Dim vm = New DetallePedidoViewModel(A.Fake(Of IRegionManager), A.Fake(Of IConfiguracion), A.Fake(Of IPedidoVentaService),
                                            A.Fake(Of IEventAggregator), A.Fake(Of IDialogService), A.Fake(Of IUnityContainer), A.Fake(Of IServicioAutenticacion))
        vm.ServicioServirJunto = servicio
        vm.pedido = New PedidoVentaWrapper(pedido)
        vm.pedido.Lineas.Add(New LineaPedidoVentaWrapper() With {.tipoLinea = 1, .Producto = "12345", .texto = "CHAMPU", .Cantidad = 1, .estado = -1, .Almacen = "ALG"})
        Return vm
    End Function

    Private Shared Function LlamadaAValidar(servicio As IServirJuntoService) As FakeItEasy.Configuration.IReturnValueArgumentValidationConfiguration(Of Task(Of ValidarServirJuntoResponse))
        Return A.CallTo(Function() servicio.Validar(
            A(Of String).Ignored,
            A(Of List(Of ProductoBonificadoConCantidadRequest)).Ignored,
            A(Of List(Of ProductoBonificadoConCantidadRequest)).Ignored,
            A(Of String).Ignored,
            A(Of String).Ignored,
            A(Of String).Ignored,
            A(Of String).Ignored,
            A(Of Nullable(Of Boolean)).Ignored,
            A(Of List(Of LineaPortesServirJuntoDTO)).Ignored,
            A(Of Nullable(Of Integer)).Ignored,
            A(Of Nullable(Of Byte)).Ignored))
    End Function

    <TestMethod()>
    Public Sub SalirDeTodoJunto_ValidaConElServidor()
        Dim servicio = A.Fake(Of IServirJuntoService)
        LlamadaAValidar(servicio).Returns(Task.FromResult(New ValidarServirJuntoResponse With {.PuedeDesmarcar = True, .ProductosProblematicos = New List(Of ProductoSinStockDTO)}))
        Dim vm = CrearViewModel(servicio, New PedidoVentaDTO With {.empresa = "1", .numero = 1, .servirJunto = True})

        vm.pedido.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO

        LlamadaAValidar(servicio).MustHaveHappenedOnceExactly()
        Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, vm.pedido.ModoServicio)
    End Sub

    <TestMethod()>
    Public Sub SalirDeTodoJunto_SiElServidorDeniega_VuelveElSelectorATodoJunto()
        Dim servicio = A.Fake(Of IServirJuntoService)
        LlamadaAValidar(servicio).Returns(Task.FromResult(New ValidarServirJuntoResponse With {
            .PuedeDesmarcar = False, .Mensaje = "No", .ProductosProblematicos = New List(Of ProductoSinStockDTO)}))
        Dim vm = CrearViewModel(servicio, New PedidoVentaDTO With {.empresa = "1", .numero = 1, .servirJunto = True})

        vm.pedido.ModoServicio = ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ

        Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.pedido.ModoServicio)
        Assert.IsTrue(vm.pedido.servirJunto)
        ' La reversión no vuelve a validar (salió del 1 una sola vez)
        LlamadaAValidar(servicio).MustHaveHappenedOnceExactly()
    End Sub

    <TestMethod()>
    Public Sub CambiarEntreModosParciales_NoVuelveAValidar()
        Dim servicio = A.Fake(Of IServirJuntoService)
        Dim vm = CrearViewModel(servicio, New PedidoVentaDTO With {.empresa = "1", .numero = 1, .servirJunto = False, .modoServicio = 2})

        vm.pedido.ModoServicio = ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ

        LlamadaAValidar(servicio).MustNotHaveHappened()
        Assert.AreEqual(CByte(4), vm.pedido.ModoServicio)
    End Sub

    <TestMethod()>
    Public Sub CargarUnPedidoQueNoEsTodoJunto_NoValida()
        Dim servicio = A.Fake(Of IServirJuntoService)

        Dim vm = CrearViewModel(servicio, New PedidoVentaDTO With {.empresa = "1", .numero = 1, .servirJunto = False, .modoServicio = 4})

        LlamadaAValidar(servicio).MustNotHaveHappened()
        Assert.AreEqual(CByte(4), vm.pedido.ModoServicio)
    End Sub
End Class
