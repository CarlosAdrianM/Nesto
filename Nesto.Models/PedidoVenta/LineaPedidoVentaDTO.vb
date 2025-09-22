Imports Newtonsoft.Json

Public Class LineaPedidoVentaDTO
    Inherits LineaPedidoBase

    <JsonIgnore>
    Public Property Pedido As PedidoVentaDTO
    Public Property Albaran As Integer?
    Public Property almacen() As String
    Public Property delegacion() As String
    Public Property EsPresupuesto As Boolean
    Public Property estado() As Short = 1
    Public Property Factura As String
    Public Property fechaEntrega() As Date
    Public Property formaVenta() As String
    Public Property id() As Integer
    Public Property iva() As String
    Public Property oferta() As Integer?
    Public Property picking() As Integer
    Public Property texto As String
    Public Property tipoLinea() As Nullable(Of Byte) = 1
    Public Property vistoBueno() As Boolean

    Public ReadOnly Property estaAlbaraneada() As Boolean
        Get
            Return estado >= 2
        End Get
    End Property

    Public ReadOnly Property estaFacturada As Boolean
        Get
            Return estado >= 4
        End Get
    End Property

    Public ReadOnly Property tienePicking As Boolean
        Get
            Return picking <> 0
        End Get
    End Property
    Public Property DescuentoCliente As Decimal
        Get
            Return DescuentoEntidad
        End Get
        Set(value As Decimal)
            DescuentoEntidad = value
        End Set
    End Property

    Public Overrides ReadOnly Property SumaDescuentos As Decimal
        Get
            Dim descuentoPP = If(IsNothing(Pedido), 0, Pedido.DescuentoPP)

            Return If(AplicarDescuento,
                1 - ((1 - DescuentoEntidad) * (1 - DescuentoProducto) * (1 - DescuentoLinea) * (1 - descuentoPP)),
                1 - ((1 - DescuentoLinea) * (1 - descuentoPP)))
        End Get
    End Property

    Public Function ShallowCopy() As LineaPedidoVentaDTO
        Return DirectCast(MemberwiseClone(), LineaPedidoVentaDTO)
    End Function

End Class



