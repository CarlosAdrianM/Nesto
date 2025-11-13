Imports Nesto.Modulos.PedidoVenta.Models.Facturas

''' <summary>
''' Servicio para imprimir documentos (facturas, albaranes y notas de entrega) usando PdfiumViewer.
''' Recibe bytes de PDFs desde la API y los envía a la impresora física.
''' </summary>
Public Interface IServicioImpresionDocumentos
    ''' <summary>
    ''' Imprime una lista de facturas que contienen bytes del PDF.
    ''' </summary>
    ''' <param name="facturas">Lista de facturas con DatosImpresion rellenos</param>
    ''' <returns>Número de documentos impresos exitosamente</returns>
    Function ImprimirFacturas(facturas As IEnumerable(Of FacturaCreadaDTO)) As Task(Of ResultadoImpresion)

    ''' <summary>
    ''' Imprime una lista de albaranes que contienen bytes del PDF.
    ''' </summary>
    ''' <param name="albaranes">Lista de albaranes con DatosImpresion rellenos</param>
    ''' <returns>Número de documentos impresos exitosamente</returns>
    Function ImprimirAlbaranes(albaranes As IEnumerable(Of AlbaranCreadoDTO)) As Task(Of ResultadoImpresion)

    ''' <summary>
    ''' Imprime una lista de notas de entrega que contienen bytes del PDF.
    ''' </summary>
    ''' <param name="notasEntrega">Lista de notas de entrega con DatosImpresion rellenos</param>
    ''' <returns>Número de documentos impresos exitosamente</returns>
    Function ImprimirNotasEntrega(notasEntrega As IEnumerable(Of NotaEntregaCreadaDTO)) As Task(Of ResultadoImpresion)

    ''' <summary>
    ''' Imprime todos los documentos de una respuesta de facturación de rutas.
    ''' </summary>
    ''' <param name="response">Response con listas de facturas, albaranes y notas de entrega</param>
    ''' <returns>Resultado consolidado de impresión</returns>
    Function ImprimirDocumentos(response As FacturarRutasResponseDTO) As Task(Of ResultadoImpresion)
End Interface

''' <summary>
''' Resultado de una operación de impresión
''' </summary>
Public Class ResultadoImpresion
    ''' <summary>
    ''' Número de documentos impresos exitosamente
    ''' </summary>
    Public Property DocumentosImpresos As Integer

    ''' <summary>
    ''' Número de documentos que fallaron al imprimir
    ''' </summary>
    Public Property DocumentosConError As Integer

    ''' <summary>
    ''' Lista de errores ocurridos durante la impresión
    ''' </summary>
    Public Property Errores As List(Of ErrorImpresion)

    Public Sub New()
        Errores = New List(Of ErrorImpresion)()
    End Sub

    ''' <summary>
    ''' Indica si hubo al menos un error
    ''' </summary>
    Public ReadOnly Property TieneErrores As Boolean
        Get
            Return Errores.Count > 0
        End Get
    End Property
End Class

''' <summary>
''' Detalle de un error de impresión
''' </summary>
Public Class ErrorImpresion
    ''' <summary>
    ''' Tipo de documento (Factura o Albarán)
    ''' </summary>
    Public Property TipoDocumento As String

    ''' <summary>
    ''' Número del documento
    ''' </summary>
    Public Property NumeroDocumento As String

    ''' <summary>
    ''' Mensaje de error
    ''' </summary>
    Public Property MensajeError As String
End Class
