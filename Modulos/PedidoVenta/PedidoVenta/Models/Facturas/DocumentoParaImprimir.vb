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

    ''' <summary>
    ''' Nesto#620 (NestoAPI#806): la impresora del servidor en la que la API ya lo ha impreso al facturar (mismo nombre y nivel
    ''' que en la API: uno por documento, dentro de DatosImpresion). Nothing = no se ha impreso en el servidor y lo imprime Nesto.
    ''' Si Nesto no lo mirara, el documento saldría dos veces (por eso la API aún no imprime para los usuarios con dominio).
    ''' </summary>
    Public Property ImpresoEnServidor As String

    ''' <summary>
    ''' Nesto#620: LA regla de «ya está impreso, no lo imprima Nesto». La usan los contadores de Facturar rutas, FacturadorPedido
    ''' (Agencias y detalle), ServicioImpresionDocumentos y la factura en papel del detalle del pedido.
    ''' </summary>
    Public ReadOnly Property EstaImpresoEnServidor As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(ImpresoEnServidor)
        End Get
    End Property
End Class
