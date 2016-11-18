Public Class PedidoVenta
    Public Class LineaPedidoVentaDTO
        Public Property almacen() As String
        Public Property aplicarDescuento() As Boolean
        Public Property cantidad() As Short
        Public Property delegacion() As String
        Public Property descuento() As Decimal
        Public Property descuentoProducto() As Decimal
        Public Property estado() As Short
        Public Property fechaEntrega() As System.DateTime
        Public Property formaVenta() As String
        Public Property iva() As String
        Public Property oferta() As Integer?
        Public Property precio() As Decimal
        Public Property producto() As String
        Public Property texto() As String
        Public Property tipoLinea() As Nullable(Of Byte)
        Public Property usuario() As String
        Public Property vistoBueno() As Boolean
        Public Function ShallowCopy() As LineaPedidoVentaDTO
            Return DirectCast(Me.MemberwiseClone(), LineaPedidoVentaDTO)
        End Function
    End Class
    Public Class PedidoVentaDTO
        Public Sub New()
            Me.LineasPedido = New HashSet(Of LineaPedidoVentaDTO)()
        End Sub

        Public Property empresa() As String
        Public Property numero() As Integer
        Public Property cliente() As String
        Public Property contacto() As String
        Public Property fecha() As Nullable(Of System.DateTime)
        Public Property formaPago() As String
        Public Property plazosPago() As String
        Public Property primerVencimiento() As Nullable(Of System.DateTime)
        Public Property iva() As String
        Public Property vendedor() As String
        Public Property comentarios() As String
        Public Property comentarioPicking() As String
        Public Property periodoFacturacion() As String
        Public Property ruta() As String
        Public Property serie() As String
        Public Property ccc() As String
        Public Property origen() As String
        Public Property contactoCobro() As String
        Public Property noComisiona() As Decimal
        Public Property vistoBuenoPlazosPago() As Boolean
        Public Property mantenerJunto() As Boolean
        Public Property servirJunto() As Boolean
        Public Property usuario() As String

        Public Overridable Property LineasPedido() As ICollection(Of LineaPedidoVentaDTO)

    End Class

End Class
