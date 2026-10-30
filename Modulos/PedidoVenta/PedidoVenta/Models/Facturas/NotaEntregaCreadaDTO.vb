''' <summary>
''' Representa una nota de entrega creada durante el proceso de facturación de rutas.
''' Las notas de entrega documentan entregas de productos que pueden estar ya facturados o pendientes de facturación.
''' Hereda de DocumentoImprimibleDTO para soportar impresión de PDFs.
''' </summary>
Public Class NotaEntregaCreadaDTO
    Inherits DocumentoImprimibleDTO

    ''' <summary>
    ''' Número de líneas procesadas en la nota de entrega
    ''' </summary>
    Public Property NumeroLineas As Integer

    ''' <summary>
    ''' Indica si alguna línea era YaFacturado=true (requirió dar de baja stock)
    ''' </summary>
    Public Property TeniaLineasYaFacturadas As Boolean

    ''' <summary>
    ''' Base imponible total de la nota de entrega
    ''' </summary>
    Public Property BaseImponible As Decimal
End Class
