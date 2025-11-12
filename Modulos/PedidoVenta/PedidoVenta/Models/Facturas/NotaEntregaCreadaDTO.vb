''' <summary>
''' Representa una nota de entrega creada durante el proceso de facturación de rutas.
''' Las notas de entrega documentan entregas de productos que pueden estar ya facturados o pendientes de facturación.
''' Hereda de DocumentoCreadoDTO las propiedades comunes (Empresa, NumeroPedido, Cliente, etc.).
''' NO hereda de DocumentoImprimibleDTO porque las notas de entrega NO se imprimen directamente.
''' </summary>
Public Class NotaEntregaCreadaDTO
    Inherits DocumentoCreadoDTO

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
