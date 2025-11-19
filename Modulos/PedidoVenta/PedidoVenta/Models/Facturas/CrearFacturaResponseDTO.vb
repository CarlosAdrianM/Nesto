''' <summary>
''' DTO de respuesta para la creación de facturas
''' Incluye el número de factura y la empresa donde se facturó
''' (que puede ser diferente a la empresa original si hubo traspaso)
''' </summary>
Public Class CrearFacturaResponseDTO
    ''' <summary>
    ''' Número de la factura creada
    ''' </summary>
    Public Property NumeroFactura As String

    ''' <summary>
    ''' Empresa donde se facturó el pedido
    ''' Puede ser diferente a la empresa original si hubo traspaso a empresa espejo
    ''' </summary>
    Public Property Empresa As String

    ''' <summary>
    ''' Número del pedido facturado
    ''' </summary>
    Public Property NumeroPedido As Integer
End Class
