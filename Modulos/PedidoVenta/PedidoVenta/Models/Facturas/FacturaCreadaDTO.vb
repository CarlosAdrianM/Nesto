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

    ''' <summary>
    ''' NestoAPI#327/#592: avisos de la facturación (p. ej. NIF no registrado en la AEAT) que tienen que
    ''' llegarle al que factura, venga de Agencias o de la facturación de rutas.
    ''' </summary>
    Public Property Avisos As List(Of String) = New List(Of String)
End Class
