''' <summary>
''' Representa una factura creada durante la facturación de rutas.
''' Hereda de DocumentoImprimibleDTO las propiedades comunes (Empresa, NumeroPedido, Cliente, etc.) y la información de impresión.
''' </summary>
Public Class FacturaCreadaDTO
    Inherits DocumentoImprimibleDTO

    ''' <summary>
    ''' Número de factura creada
    ''' </summary>
    Public Property NumeroFactura As String

    ''' <summary>
    ''' Serie de la factura
    ''' </summary>
    Public Property Serie As String
End Class
