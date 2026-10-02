Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Nesto.Modulos.PedidoVenta.Services
Imports CommunityToolkit.Mvvm.Messaging
Imports System.Threading.Tasks
Imports Unity

''' <summary>
''' ELMAH 01-02/10/26 (Andre): «Crear albarán» sobre una nota de entrega daba «El pedido es nota de entrega»
''' (prdCrearAlbaránVta no deja albaranear notas). Una nota de entrega no lleva albarán: los botones de albarán
''' del detalle la procesan por el camino de Facturar rutas (FacturarPedido, NestoAPI#592), como Agencias.
''' </summary>
<TestClass()>
Public Class DetallePedidoNotaEntregaTests

    Private servicio As IPedidoVentaService
    Private dialogService As IServicioDialogos
    Private facturacion As IServicioFacturacionRutas
    Private vm As DetallePedidoViewModel

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IPedidoVentaService)
        dialogService = A.Fake(Of IServicioDialogos)
        facturacion = A.Fake(Of IServicioFacturacionRutas)
        vm = New DetallePedidoViewModel(A.Fake(Of IServicioNavegacion), A.Fake(Of IConfiguracion), servicio, New WeakReferenceMessenger(),
                                        dialogService, A.Fake(Of IUnityContainer), A.Fake(Of IServicioAutenticacion))
        vm.Facturador = New FacturadorPedido(facturacion, A.Fake(Of IServicioImpresionDocumentos), dialogService)
        vm.pedido = New PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1", .numero = 927519, .notaEntrega = True})
        A.CallTo(Function() facturacion.FacturarPedido(A(Of String).Ignored, A(Of Integer).Ignored)).
            Returns(Task.FromResult(New FacturarRutasResponseDTO With {.NotasEntrega = New List(Of NotaEntregaCreadaDTO) From {New NotaEntregaCreadaDTO()}}))
    End Sub

    <TestMethod()>
    Public Async Function ProcesarNotaEntrega_Confirmada_VaPorFacturarPedidoYNoCreaAlbaran() As Task
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(True)

        Await vm.ProcesarNotaEntregaAsync()

        A.CallTo(Function() facturacion.FacturarPedido("1", 927519)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.CrearAlbaranVenta(A(Of String).Ignored, A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function ProcesarNotaEntrega_NoConfirmada_NoHaceNada() As Task
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(False)

        Await vm.ProcesarNotaEntregaAsync()

        A.CallTo(Function() facturacion.FacturarPedido(A(Of String).Ignored, A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function ProcesarNotaEntrega_FallaLaApi_LoDiceYNoRevienta() As Task
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(True)
        A.CallTo(Function() facturacion.FacturarPedido(A(Of String).Ignored, A(Of Integer).Ignored)).
            ThrowsAsync(New Exception("sin conexión"))

        Await vm.ProcesarNotaEntregaAsync()

        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("sin conexión"))).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Sub EsNotaEntrega_SoloSiElPedidoLoEs()
        Assert.IsTrue(vm.EsNotaEntrega)
        vm.pedido = New PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1", .numero = 1, .notaEntrega = False})
        Assert.IsFalse(vm.EsNotaEntrega)
    End Sub

End Class
