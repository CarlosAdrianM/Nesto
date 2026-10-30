Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Nesto.Modulos.PedidoVenta.Services
Imports System.Threading.Tasks

''' <summary>
''' Nesto#580 (NestoAPI#758): Facturar rutas cuenta las notas de entrega y de recogida (con su PDF NE/NR) entre los
''' documentos a imprimir y enseña las etiquetas de recogida que la API ha impreso por la Zebra.
''' </summary>
<TestClass()>
Public Class FacturarRutasPopupNotasEntregaTests

    Private dialogService As IServicioDialogos
    Private facturacion As IServicioFacturacionRutas
    Private impresion As IServicioImpresionDocumentos

    <TestInitialize()>
    Public Sub Initialize()
        dialogService = A.Fake(Of IServicioDialogos)
        facturacion = A.Fake(Of IServicioFacturacionRutas)
        impresion = A.Fake(Of IServicioImpresionDocumentos)
        A.CallTo(Function() impresion.ImprimirDocumentos(A(Of FacturarRutasResponseDTO).Ignored)).Returns(Task.FromResult(New ResultadoImpresion()))
    End Sub

    Private Function CrearVm(response As FacturarRutasResponseDTO) As FacturarRutasPopupViewModel
        A.CallTo(Function() facturacion.FacturarRutas(A(Of FacturarRutasRequestDTO).Ignored)).Returns(Task.FromResult(response))
        Dim vm As New FacturarRutasPopupViewModel(A.Fake(Of IConfiguracion), dialogService, facturacion, impresion) With {
            .PausaAntesDeCerrarMs = 0,
            .PausaTrasImprimirMs = 0
        }
        vm.TipoRutaSeleccionado = New TipoRutaInfoDTO With {.Id = "PROPIA"}
        Return vm
    End Function

    Private Shared Function ConPdf() As DocumentoParaImprimir
        Return New DocumentoParaImprimir With {.BytesPDF = {1}, .NumeroCopias = 2, .TipoBandeja = TipoBandejaImpresion.Middle}
    End Function

    Private Shared Function RespuestaConNotas() As FacturarRutasResponseDTO
        Dim response As New FacturarRutasResponseDTO With {.PedidosProcesados = 3}
        response.NotasEntrega.Add(New NotaEntregaCreadaDTO With {.NumeroPedido = 1, .Tipo = "Entrega", .CodigoDocumento = "NE1", .DatosImpresion = ConPdf()})
        response.NotasEntrega.Add(New NotaEntregaCreadaDTO With {.NumeroPedido = 2, .Tipo = "Recogida", .CodigoDocumento = "NR2", .Bultos = 3, .EtiquetasRecogidaImpresas = 3, .DatosImpresion = ConPdf()})
        response.NotasEntrega.Add(New NotaEntregaCreadaDTO With {.NumeroPedido = 3, .Tipo = "Recogida", .Bultos = 1, .EtiquetasRecogidaImpresas = 1}) ' sin PDF (ruta no propia)
        Return response
    End Function

#Region "Cuentas de la respuesta"
    <TestMethod()>
    Public Sub Cuentas_IncluyenLasNotasConPdfEnLosDocumentosAImprimir()
        Dim response = RespuestaConNotas()
        response.Facturas.Add(New FacturaCreadaDTO With {.NumeroFactura = "NV26/1", .DatosImpresion = ConPdf()})
        response.Albaranes.Add(New AlbaranCreadoDTO With {.NumeroAlbaran = 5})

        Assert.AreEqual(2, response.NotasEntregaParaImprimir)
        Assert.AreEqual(1, response.NotasDeEntregaParaImprimir)
        Assert.AreEqual(1, response.NotasDeRecogidaParaImprimir)
        Assert.AreEqual(2, response.NotasRecogidaCreadas)
        Assert.AreEqual(4, response.EtiquetasRecogidaImpresas)
        Assert.AreEqual(3, response.TotalDocumentosParaImprimir, "1 factura + 0 albaranes + 2 notas")
    End Sub

    <TestMethod()>
    Public Sub Cuentas_NotaSinTipo_CuentaComoEntrega()
        Dim response As New FacturarRutasResponseDTO()
        response.NotasEntrega.Add(New NotaEntregaCreadaDTO With {.NumeroPedido = 1, .DatosImpresion = ConPdf()})

        Assert.AreEqual(1, response.NotasDeEntregaParaImprimir)
        Assert.AreEqual(0, response.NotasDeRecogidaParaImprimir)
    End Sub

    <TestMethod()>
    Public Sub Mensaje_DiceLasNotasDeEntregaYDeRecogida()
        Dim mensaje = FacturarRutasPopupViewModel.MensajeDocumentosParaImprimir(RespuestaConNotas())

        StringAssert.Contains(mensaje, "Se han generado 2 documentos para imprimir")
        StringAssert.Contains(mensaje, "- 1 nota(s) de entrega")
        StringAssert.Contains(mensaje, "- 1 nota(s) de recogida")
        Assert.IsFalse(mensaje.Contains("factura(s)"))
        StringAssert.Contains(mensaje, "¿Desea imprimirlos ahora?")
    End Sub
#End Region

#Region "Facturar"
    <TestMethod()>
    Public Async Function Facturar_SoloConNotas_PreguntaEImprime() As Task
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Imprimir documentos", A(Of String).Ignored)).Returns(True)
        Dim response = RespuestaConNotas()
        Dim vm = CrearVm(response)

        Await vm.FacturarRutasAsync()

        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Imprimir documentos", A(Of String).That.Contains("nota(s) de recogida"))).MustHaveHappenedOnceExactly()
        A.CallTo(Function() impresion.ImprimirDocumentos(response)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Facturar_SiNoQuiereImprimir_NoImprime() As Task
        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(False)
        Dim vm = CrearVm(RespuestaConNotas())

        Await vm.FacturarRutasAsync()

        A.CallTo(Function() impresion.ImprimirDocumentos(A(Of FacturarRutasResponseDTO).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Facturar_SinNadaQueImprimir_NoPregunta() As Task
        Dim response As New FacturarRutasResponseDTO With {.PedidosProcesados = 1}
        response.NotasEntrega.Add(New NotaEntregaCreadaDTO With {.NumeroPedido = 1, .Tipo = "Entrega"})
        Dim vm = CrearVm(response)

        Await vm.FacturarRutasAsync()

        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Facturar_ElResumenEnsenaRecogidasYEtiquetas() As Task
        Dim vm = CrearVm(RespuestaConNotas())

        Await vm.FacturarRutasAsync()

        StringAssert.Contains(vm.MensajeEstado, "Notas de entrega: 1")
        StringAssert.Contains(vm.MensajeEstado, "Notas de recogida: 2")
        StringAssert.Contains(vm.MensajeEstado, "Etiquetas de recogida impresas por la Zebra: 4")
        StringAssert.Contains(vm.MensajeEstado, "Notas para imprimir: 2")
    End Function
#End Region

End Class
