''' <summary>
''' Contiene la información necesaria para imprimir un documento (factura o albarán).
''' El servidor genera los bytes del PDF y especifica cómo imprimirlo.
''' El cliente WPF usa PdfiumViewer para enviarlo a la impresora.
''' </summary>
Public Class DocumentoParaImprimir
    ''' <summary>
    ''' Bytes del documento PDF listo para imprimir
    ''' </summary>
    Public Property BytesPDF As Byte()

    ''' <summary>
    ''' Número de copias a imprimir
    ''' </summary>
    Public Property NumeroCopias As Integer

    ''' <summary>
    ''' Bandeja de impresión a utilizar
    ''' </summary>
    Public Property Bandeja As String
End Class
