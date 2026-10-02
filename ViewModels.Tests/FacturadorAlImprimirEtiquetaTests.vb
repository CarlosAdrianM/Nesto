Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Nesto.Modulos.PedidoVenta.Services
Imports Nesto.ViewModels
Imports System.Threading.Tasks

''' <summary>
''' NestoAPI#592: «Facturar al imprimir etiqueta» va por el mismo camino que la facturación de rutas
''' (FacturarPedido) y enseña lo mismo: avisos de la factura, errores, nota de entrega e impresión.
''' </summary>
<TestClass()>
Public Class FacturadorAlImprimirEtiquetaTests
    Private facturacion As IServicioFacturacionRutas
    Private impresion As IServicioImpresionDocumentos
    Private dialogos As IServicioDialogos
    Private facturador As FacturadorAlImprimirEtiqueta

    <TestInitialize()>
    Public Sub Inicializar()
        facturacion = A.Fake(Of IServicioFacturacionRutas)()
        impresion = A.Fake(Of IServicioImpresionDocumentos)()
        dialogos = A.Fake(Of IServicioDialogos)()
        facturador = New FacturadorAlImprimirEtiqueta(facturacion, impresion, dialogos)
    End Sub

    Private Sub Responder(respuesta As FacturarRutasResponseDTO)
        A.CallTo(Function() facturacion.FacturarPedido("1", 927519)).Returns(Task.FromResult(respuesta))
    End Sub

    <TestMethod()>
    Public Async Function Facturar_LlamaAFacturarPedidoDelMismoCaminoQueLasRutas() As Task
        Responder(New FacturarRutasResponseDTO())

        Await facturador.FacturarAsync("1", 927519, False)

        A.CallTo(Function() facturacion.FacturarPedido("1", 927519)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() facturacion.FacturarRutas(A(Of FacturarRutasRequestDTO).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Facturar_NotaDeEntrega_NoEnseñaErrorYDiceQueSeHaProcesado() As Task
        Dim respuesta As New FacturarRutasResponseDTO()
        respuesta.NotasEntrega.Add(New NotaEntregaCreadaDTO With {.NumeroLineas = 1})
        Responder(respuesta)

        Await facturador.FacturarAsync("1", 927519, False)

        A.CallTo(Sub() dialogos.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogos.ShowNotification("Facturación", "Nota de entrega del pedido 927519 procesada")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Facturar_AvisosDeLaFacturaYErrores_SeEnseñan() As Task
        Dim respuesta As New FacturarRutasResponseDTO()
        Dim factura As New FacturaCreadaDTO With {.NumeroFactura = "NV2616200"}
        factura.Avisos.Add("NIF no registrado en la AEAT")
        respuesta.Facturas.Add(factura)
        respuesta.PedidosConErrores.Add(New PedidoConErrorDTO With {.TipoError = "Generación PDF Factura", .MensajeError = "falló el PDF"})
        Responder(respuesta)

        Await facturador.FacturarAsync("1", 927519, False)

        A.CallTo(Sub() dialogos.ShowError("NIF no registrado en la AEAT")).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogos.ShowError("Generación PDF Factura: falló el PDF")).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogos.ShowNotification("Facturación", A(Of String).That.Contains("NV2616200"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Facturar_ConImprimirYDocumentos_ImprimeComoLaFacturacionDeRutas() As Task
        Dim respuesta As New FacturarRutasResponseDTO()
        respuesta.Facturas.Add(New FacturaCreadaDTO With {.NumeroFactura = "NV2616200", .DatosImpresion = New DocumentoParaImprimir()})
        Responder(respuesta)

        Await facturador.FacturarAsync("1", 927519, True)

        A.CallTo(Function() impresion.ImprimirDocumentos(respuesta)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Facturar_SinImprimir_NoImprimeNada() As Task
        Dim respuesta As New FacturarRutasResponseDTO()
        respuesta.Facturas.Add(New FacturaCreadaDTO With {.NumeroFactura = "NV2616200", .DatosImpresion = New DocumentoParaImprimir()})
        Responder(respuesta)

        Await facturador.FacturarAsync("1", 927519, False)

        A.CallTo(Function() impresion.ImprimirDocumentos(A(Of FacturarRutasResponseDTO).Ignored)).MustNotHaveHappened()
    End Function
End Class
