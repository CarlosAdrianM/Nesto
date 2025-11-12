''' <summary>
''' Clase base abstracta para todos los documentos creados durante la facturación de rutas.
''' Contiene las propiedades comunes a todos los tipos de documentos (facturas, albaranes, notas de entrega).
''' </summary>
Public MustInherit Class DocumentoCreadoDTO
    ''' <summary>
    ''' Código de empresa
    ''' </summary>
    Public Property Empresa As String

    ''' <summary>
    ''' Número de pedido del que se creó el documento
    ''' </summary>
    Public Property NumeroPedido As Integer

    ''' <summary>
    ''' Cliente
    ''' </summary>
    Public Property Cliente As String

    ''' <summary>
    ''' Contacto del cliente
    ''' </summary>
    Public Property Contacto As String

    ''' <summary>
    ''' Nombre del cliente (para mostrar en UI)
    ''' </summary>
    Public Property NombreCliente As String
End Class
