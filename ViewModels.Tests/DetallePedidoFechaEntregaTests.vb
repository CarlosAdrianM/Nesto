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
''' Nesto#481 (NestoAPI#508, pedido 926673 del 22/09/26): la fecha de entrega de cabecera solo se
''' propaga a las líneas sin picking y aún no servidas. Antes iba a todas, el servidor las ignoraba en
''' silencio y el correo salía con la fecha nueva.
''' </summary>
<TestClass()>
Public Class DetallePedidoFechaEntregaTests

    <TestMethod()>
    Public Sub AdmiteCambioDeFechaEntrega_SinPickingYPendienteOEnCursoOPresupuesto_True()
        Assert.IsTrue(DetallePedidoViewModel.AdmiteCambioDeFechaEntrega(0, -3))
        Assert.IsTrue(DetallePedidoViewModel.AdmiteCambioDeFechaEntrega(0, -1))
        Assert.IsTrue(DetallePedidoViewModel.AdmiteCambioDeFechaEntrega(0, 1))
    End Sub

    <TestMethod()>
    Public Sub AdmiteCambioDeFechaEntrega_ConPickingOServida_False()
        Assert.IsFalse(DetallePedidoViewModel.AdmiteCambioDeFechaEntrega(99600, 1), "Con picking no se toca (caso 926673)")
        Assert.IsFalse(DetallePedidoViewModel.AdmiteCambioDeFechaEntrega(0, 2), "Albarán")
        Assert.IsFalse(DetallePedidoViewModel.AdmiteCambioDeFechaEntrega(0, 4), "Factura")
    End Sub

    <TestMethod()>
    Public Sub CambiarFechaDeCabecera_SoloCambiaLasLineasSinPicking_YAvisa()
        Dim dialogo = A.Fake(Of IDialogService)
        Dim vm = CrearViewModel(dialogo)
        Dim conPicking = New LineaPedidoVentaWrapper() With {.tipoLinea = 1, .Producto = "38669", .Cantidad = 1, .estado = 1, .picking = 99600, .Almacen = "ALG", .fechaEntrega = New Date(2026, 9, 22)}
        Dim sinPicking = New LineaPedidoVentaWrapper() With {.tipoLinea = 1, .Producto = "38667", .Cantidad = 1, .estado = -1, .picking = 0, .Almacen = "ALG", .fechaEntrega = New Date(2026, 9, 22)}
        vm.pedido.Lineas.Add(conPicking)
        vm.pedido.Lineas.Add(sinPicking)
        vm.UsarFechasIndividuales = False

        vm.fechaEntrega = New Date(2026, 9, 24) ' el setter ya propaga, como en la pantalla

        Assert.AreEqual(New Date(2026, 9, 22), conPicking.fechaEntrega, "La línea con picking conserva su fecha")
        Assert.AreEqual(New Date(2026, 9, 24), sinPicking.fechaEntrega, "La línea sin picking cambia")
        A.CallTo(Sub() dialogo.ShowDialog("NotificationDialog", A(Of IDialogParameters).That.Matches(Function(p) MensajeDe(p).Contains("solo en las líneas sin picking")), A(Of Action(Of IDialogResult)).Ignored)).MustHaveHappenedOnceExactly()
    End Sub

    <TestMethod()>
    Public Sub CambiarFechaDeCabecera_TodasConPicking_NoCambiaNadaYAvisa()
        Dim dialogo = A.Fake(Of IDialogService)
        Dim vm = CrearViewModel(dialogo)
        Dim linea = New LineaPedidoVentaWrapper() With {.tipoLinea = 1, .Producto = "38669", .Cantidad = 1, .estado = 1, .picking = 99600, .Almacen = "ALG", .fechaEntrega = New Date(2026, 9, 22)}
        vm.pedido.Lineas.Add(linea)
        vm.UsarFechasIndividuales = False

        vm.fechaEntrega = New Date(2026, 9, 24) ' el setter ya propaga, como en la pantalla

        Assert.AreEqual(New Date(2026, 9, 22), linea.fechaEntrega)
        A.CallTo(Sub() dialogo.ShowDialog("NotificationDialog", A(Of IDialogParameters).That.Matches(Function(p) MensajeDe(p).Contains("Ninguna línea")), A(Of Action(Of IDialogResult)).Ignored)).MustHaveHappenedOnceExactly()
    End Sub

    <TestMethod()>
    Public Sub CambiarFechaDeCabecera_SinLineasProtegidas_NoAvisa()
        Dim dialogo = A.Fake(Of IDialogService)
        Dim vm = CrearViewModel(dialogo)
        vm.pedido.Lineas.Add(New LineaPedidoVentaWrapper() With {.tipoLinea = 1, .Producto = "38667", .Cantidad = 1, .estado = -1, .picking = 0, .Almacen = "ALG"})
        vm.UsarFechasIndividuales = False

        vm.fechaEntrega = New Date(2026, 9, 24) ' el setter ya propaga, como en la pantalla

        A.CallTo(Sub() dialogo.ShowDialog(A(Of String).Ignored, A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)).MustNotHaveHappened()
    End Sub

    Private Shared Function MensajeDe(p As IDialogParameters) As String
        Return If(p.GetValue(Of String)("message"), String.Empty)
    End Function

    Private Shared Function CrearViewModel(dialogo As IDialogService) As DetallePedidoViewModel
        Dim vm = New DetallePedidoViewModel(A.Fake(Of IRegionManager), A.Fake(Of IConfiguracion), A.Fake(Of IPedidoVentaService),
                                            A.Fake(Of IEventAggregator), dialogo, A.Fake(Of IUnityContainer), A.Fake(Of IServicioAutenticacion))
        vm.ServicioServirJunto = A.Fake(Of IServirJuntoService)
        vm.pedido = New PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1", .numero = 926673, .servirJunto = False, .modoServicio = 3})
        Return vm
    End Function
End Class
