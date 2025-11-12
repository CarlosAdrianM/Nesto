''' <summary>
''' Representa un albarán creado durante la facturación de rutas.
''' Hereda de DocumentoImprimibleDTO las propiedades comunes (Empresa, NumeroPedido, Cliente, etc.) y la información de impresión.
''' </summary>
Public Class AlbaranCreadoDTO
    Inherits DocumentoImprimibleDTO

    ''' <summary>
    ''' Número de albarán creado
    ''' </summary>
    Public Property NumeroAlbaran As Integer
End Class
