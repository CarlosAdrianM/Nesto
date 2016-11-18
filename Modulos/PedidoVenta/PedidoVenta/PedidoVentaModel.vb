Public Class PedidoVentaModel
    Public Class ResumenPedido
        Public Property empresa As String
        Public Property numero As Integer
        Public Property cliente As String
        Public Property contacto As String
        Public Property nombre As String
        Public Property direccion As String
        Public Property codPostal As String
        Public ReadOnly Property noTieneProductos As Boolean
            Get
                Return Not tieneProductos
            End Get
        End Property
        Public Property poblacion As String
        Public Property provincia As String
        Public Property fecha As DateTime
        Public Property tieneProductos As Boolean
        Public Property tienePendientes As Boolean
        Public Property tienePicking As Boolean
        Public Property tieneFechasFuturas As Boolean
        Public Property baseImponible As Decimal
        Public Property total As Decimal
        Public Property vendedor As String
    End Class
End Class
