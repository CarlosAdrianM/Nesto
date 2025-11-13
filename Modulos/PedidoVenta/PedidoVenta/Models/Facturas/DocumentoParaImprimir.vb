''' <summary>
''' Tipos estándar de bandejas de impresión, compatible con System.Drawing.Printing.PaperSourceKind.
''' Estos valores son independientes del fabricante de la impresora.
''' </summary>
Public Enum TipoBandejaImpresion
    ''' <summary>
    ''' Selección automática de bandeja (valor 7 en PaperSourceKind)
    ''' </summary>
    AutomaticFeed = 7

    ''' <summary>
    ''' Bandeja superior (valor 1 en PaperSourceKind)
    ''' </summary>
    Upper = 1

    ''' <summary>
    ''' Bandeja media (valor 3 en PaperSourceKind)
    ''' </summary>
    Middle = 3

    ''' <summary>
    ''' Bandeja inferior (valor 4 en PaperSourceKind)
    ''' </summary>
    Lower = 4

    ''' <summary>
    ''' Alimentación manual (valor 4 en PaperSourceKind)
    ''' </summary>
    Manual = 4
End Enum

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
    ''' Tipo de bandeja de impresión a utilizar.
    ''' Usa valores estándar compatibles con todas las impresoras.
    ''' </summary>
    Public Property TipoBandeja As TipoBandejaImpresion
End Class
