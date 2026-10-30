Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports CommunityToolkit.Mvvm.Messaging
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks
Imports ControlesUsuario.Dialogs

''' <summary>
''' Nesto#580 (NestoAPI#758): «Imprimir nota» / «Ver nota» en cualquier nota grabada (también de Nesto viejo) y
''' «Etiquetas de recogida» solo en las recogidas, con la pregunta de bultos si la nota no los tiene.
''' Las etiquetas las imprime la API por la Zebra: Nesto solo llama al endpoint.
''' </summary>
<TestClass()>
Public Class DetallePedidoNotaEntregaImprimirTests

    Private servicio As IPedidoVentaService
    Private dialogService As IServicioDialogos
    Private impresion As IServicioImpresionDocumentos

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IPedidoVentaService)
        dialogService = A.Fake(Of IServicioDialogos)
        impresion = A.Fake(Of IServicioImpresionDocumentos)
        A.CallTo(Function() impresion.ImprimirNotasEntrega(A(Of IEnumerable(Of NotaEntregaCreadaDTO)).Ignored)).
            Returns(Task.FromResult(New ResultadoImpresion With {.DocumentosImpresos = 1}))
    End Sub

    Private Function CrearVm() As DetallePedidoViewModel
        Dim vm = New DetallePedidoViewModel(A.Fake(Of IServicioNavegacion), A.Fake(Of IConfiguracion), servicio, New WeakReferenceMessenger(),
                                            dialogService, A.Fake(Of IAbridorPedidos), A.Fake(Of IServicioAutenticacion))
        vm.ServicioImpresion = impresion
        Return vm
    End Function

    Private Shared Function Nota(Optional tipo As String = Nothing, Optional bultos As Integer? = Nothing, Optional numero As Integer = 927519) As PedidoVentaWrapper
        Return New PedidoVentaWrapper(New PedidoVentaDTO With {
            .empresa = "1", .numero = numero, .cliente = "15191", .contacto = "0", .notaEntrega = True,
            .tipoNotaEntrega = tipo, .bultosNota = bultos,
            .Lineas = New ObservableCollection(Of LineaPedidoVentaDTO)})
    End Function

    Private Shared Function PedidoNormal() As PedidoVentaWrapper
        Return New PedidoVentaWrapper(New PedidoVentaDTO With {
            .empresa = "1", .numero = 927520, .cliente = "15191", .contacto = "0", .notaEntrega = False,
            .Lineas = New ObservableCollection(Of LineaPedidoVentaDTO)})
    End Function

    Private Sub ApiImprimeEtiquetas(impresas As Integer)
        A.CallTo(Function() servicio.ImprimirEtiquetasRecogida(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).
            Returns(Task.FromResult(New EtiquetasRecogidaRespuestaDTO With {.Impresas = impresas}))
    End Sub

    Private Sub ApiGuardaCabecera()
        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).
            ReturnsLazily(Function(empresa As String, pedido As Integer, tipo As String, bultos As Integer, factura As String) _
                              Task.FromResult(New NotaEntregaCabeceraDTO With {.Empresa = empresa, .Pedido = pedido, .Tipo = tipo, .Bultos = bultos, .FacturaOriginal = factura}))
    End Sub

#Region "Botones visibles"
    <TestMethod()>
    Public Sub Botones_PedidoNormal_NoSeVen()
        Dim vm = CrearVm()
        vm.pedido = PedidoNormal()

        Assert.IsFalse(vm.PuedeImprimirNota)
        Assert.IsFalse(vm.PuedeImprimirEtiquetasRecogida)
        Assert.IsFalse(vm.ImprimirNotaCommand.CanExecute(Nothing))
        Assert.IsFalse(vm.EtiquetasRecogidaCommand.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub Botones_NotaDeEntrega_ImprimirSiEtiquetasNo()
        Dim vm = CrearVm()
        vm.pedido = Nota("Entrega")

        Assert.IsTrue(vm.PuedeImprimirNota)
        Assert.IsTrue(vm.ImprimirNotaCommand.CanExecute(Nothing))
        Assert.IsTrue(vm.VerNotaCommand.CanExecute(Nothing))
        Assert.IsFalse(vm.PuedeImprimirEtiquetasRecogida, "Las entregas llevan la etiqueta normal del packing")
        Assert.IsFalse(vm.EtiquetasRecogidaCommand.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub Botones_NotaDeNestoViejoSinTipo_SePuedeImprimir()
        Dim vm = CrearVm()
        vm.pedido = Nota()

        Assert.IsTrue(vm.PuedeImprimirNota)
        Assert.IsFalse(vm.PuedeImprimirEtiquetasRecogida)
    End Sub

    <TestMethod()>
    Public Sub Botones_NotaDeRecogida_LosDos()
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", 2)

        Assert.IsTrue(vm.PuedeImprimirNota)
        Assert.IsTrue(vm.PuedeImprimirEtiquetasRecogida)
        Assert.IsTrue(vm.EtiquetasRecogidaCommand.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub Botones_NotaSinGrabar_NoSeVen()
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", 1, numero:=0)

        Assert.IsFalse(vm.PuedeImprimirNota)
        Assert.IsFalse(vm.PuedeImprimirEtiquetasRecogida)
    End Sub

    <TestMethod()>
    Public Async Function Botones_AlPasarARecogida_SaleEtiquetas() As Task
        ApiGuardaCabecera()
        Dim vm = CrearVm()
        vm.pedido = Nota("Entrega")
        Dim avisos As New List(Of String)
        AddHandler vm.PropertyChanged, Sub(s, e) avisos.Add(e.PropertyName)

        vm.TipoNota = TiposNotaEntrega.RECOGIDA
        Await vm.CambioCabeceraNotaEnCurso

        Assert.IsTrue(vm.PuedeImprimirEtiquetasRecogida)
        CollectionAssert.Contains(avisos, NameOf(DetallePedidoViewModel.PuedeImprimirEtiquetasRecogida))
    End Function
#End Region

#Region "Imprimir nota"
    <TestMethod()>
    Public Async Function ImprimirNota_PideElPdfALaApiEImprimeDosCopiasEnLaBandejaMiddle() As Task
        Dim pdf As Byte() = {1, 2, 3}
        A.CallTo(Function() servicio.CargarDocumentoNotaEntrega("1", 927519)).Returns(Task.FromResult(pdf))
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(True)
        Dim vm = CrearVm()
        vm.pedido = Nota()

        Await vm.ImprimirNotaAsync()

        A.CallTo(Function() impresion.ImprimirNotasEntrega(A(Of IEnumerable(Of NotaEntregaCreadaDTO)).That.Matches(
            Function(notas) notas.Count() = 1 AndAlso notas.First().NumeroPedido = 927519 AndAlso
                notas.First().DatosImpresion.BytesPDF Is pdf AndAlso notas.First().DatosImpresion.NumeroCopias = 2 AndAlso
                notas.First().DatosImpresion.TipoBandeja = TipoBandejaImpresion.Middle))).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowNotification("Nota 927519 enviada a la impresora")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function ImprimirNota_SiNoConfirma_NoPideNada() As Task
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(False)
        Dim vm = CrearVm()
        vm.pedido = Nota()

        Await vm.ImprimirNotaAsync()

        A.CallTo(Function() servicio.CargarDocumentoNotaEntrega(A(Of String).Ignored, A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function ImprimirNota_SiLaApiFalla_EnsenaElError() As Task
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(True)
        A.CallTo(Function() servicio.CargarDocumentoNotaEntrega("1", 927519)).ThrowsAsync(New Exception("El pedido 927519 no es una nota de entrega."))
        Dim vm = CrearVm()
        vm.pedido = Nota()

        Await vm.ImprimirNotaAsync()

        A.CallTo(Sub() dialogService.ShowError("No se ha podido imprimir la nota: El pedido 927519 no es una nota de entrega.")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() impresion.ImprimirNotasEntrega(A(Of IEnumerable(Of NotaEntregaCreadaDTO)).Ignored)).MustNotHaveHappened()
    End Function
#End Region

#Region "Etiquetas de recogida"
    <TestMethod()>
    Public Async Function Etiquetas_ConBultos_NoPreguntaYLlamaALaApi() As Task
        ApiImprimeEtiquetas(3)
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", 3)

        Await vm.ImprimirEtiquetasRecogidaAsync()

        A.CallTo(Function() dialogService.GetText(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.ImprimirEtiquetasRecogida("1", 927519, Nothing)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowNotification("Impresas 3 etiquetas por la Zebra")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Etiquetas_SinBultos_PreguntaLosGuardaPorElPutYLuegoImprime() As Task
        ApiGuardaCabecera()
        ApiImprimeEtiquetas(4)
        A.CallTo(Function() dialogService.GetText(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).Returns("4")
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", Nothing)

        Assert.IsTrue(vm.FaltanBultosNotaRecogida)
        Await vm.ImprimirEtiquetasRecogidaAsync()

        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega("1", 927519, "Recogida", 4, Nothing)).MustHaveHappenedOnceExactly().
            Then(A.CallTo(Function() servicio.ImprimirEtiquetasRecogida("1", 927519, Nothing)).MustHaveHappened())
        Assert.AreEqual(4, vm.BultosNota)
        Assert.AreEqual(4, vm.pedido.Model.bultosNota)
        A.CallTo(Sub() dialogService.ShowNotification("Impresas 4 etiquetas por la Zebra")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Etiquetas_ConCeroBultos_Pregunta() As Task
        ApiGuardaCabecera()
        ApiImprimeEtiquetas(1)
        A.CallTo(Function() dialogService.GetText(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).Returns("1")
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", 0)

        Await vm.ImprimirEtiquetasRecogidaAsync()

        A.CallTo(Function() dialogService.GetText("Etiquetas de recogida", A(Of String).Ignored, A(Of String).Ignored)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogService.ShowNotification("Impresas 1 etiqueta por la Zebra")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Etiquetas_SiCancelaLaPregunta_NoHaceNada() As Task
        A.CallTo(Function() dialogService.GetText(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).Returns(Nothing)
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", Nothing)

        Await vm.ImprimirEtiquetasRecogidaAsync()

        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() servicio.ImprimirEtiquetasRecogida(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Etiquetas_BultosNoValidos_EnsenaErrorDeUsted() As Task
        A.CallTo(Function() dialogService.GetText(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).Returns("cero")
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", Nothing)

        Await vm.ImprimirEtiquetasRecogidaAsync()

        A.CallTo(Sub() dialogService.ShowError("Escriba un número de bultos mayor que cero.")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.ImprimirEtiquetasRecogida(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Etiquetas_SiNoSeGuardanLosBultos_NoImprime() As Task
        A.CallTo(Function() dialogService.GetText(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).Returns("2")
        A.CallTo(Function() servicio.CambiarCabeceraNotaEntrega(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).
            ThrowsAsync(New Exception("La parada de esta nota ya está entregada."))
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", Nothing)

        Await vm.ImprimirEtiquetasRecogidaAsync()

        A.CallTo(Sub() dialogService.ShowError("No se han podido guardar los bultos de la nota: La parada de esta nota ya está entregada.")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() servicio.ImprimirEtiquetasRecogida(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Etiquetas_SiLaZebraFalla_EnsenaElErrorDeLaApi() As Task
        A.CallTo(Function() servicio.ImprimirEtiquetasRecogida(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).
            ThrowsAsync(New Exception("No se ha podido conectar con la impresora de etiquetas."))
        Dim vm = CrearVm()
        vm.pedido = Nota("Recogida", 2)

        Await vm.ImprimirEtiquetasRecogidaAsync()

        A.CallTo(Sub() dialogService.ShowError("No se han podido imprimir las etiquetas de recogida: No se ha podido conectar con la impresora de etiquetas.")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Etiquetas_EnNotaDeEntrega_NoLlamaALaApi() As Task
        Dim vm = CrearVm()
        vm.pedido = Nota("Entrega")

        Await vm.ImprimirEtiquetasRecogidaAsync()

        A.CallTo(Function() servicio.ImprimirEtiquetasRecogida(A(Of String).Ignored, A(Of Integer).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function
#End Region

End Class
