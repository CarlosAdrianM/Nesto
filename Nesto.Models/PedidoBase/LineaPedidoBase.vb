Public Class LineaPedidoBase
    Public Property AplicarDescuento As Boolean = True
    Public Property Cantidad As Integer = 1
    Public Property DescuentoEntidad As Decimal
    Public Property DescuentoLinea As Decimal
    Public Property DescuentoPP As Decimal
    Public Property DescuentoProducto As Decimal
    Public Property PorcentajeIva As Decimal
    Public Property PorcentajeRecargoEquivalencia As Decimal
    Public Property Producto As String
    Public Property PrecioUnitario As Decimal
    Public Property Usuario As String

    Public ReadOnly Property BaseImponible As Decimal
        Get
            Return Math.Round(Bruto - ImporteDescuento, 2, MidpointRounding.AwayFromZero)
        End Get
    End Property
    Public Overridable ReadOnly Property Bruto As Decimal
        Get
            Return PrecioUnitario * Cantidad
        End Get
    End Property

    Public ReadOnly Property ImporteDescuento As Decimal
        Get
            Return Bruto * SumaDescuentos
        End Get
    End Property

    Public Overridable ReadOnly Property ImporteIva As Decimal
        Get
            Return BaseImponible * PorcentajeIva
        End Get
    End Property
    Public Overridable ReadOnly Property ImporteRecargoEquivalencia As Decimal
        Get
            Return BaseImponible * PorcentajeRecargoEquivalencia
        End Get
    End Property
    Public Overridable ReadOnly Property SumaDescuentos As Decimal
        Get
            If AplicarDescuento Then
                Return 1 - (1 - DescuentoEntidad) * (1 - DescuentoProducto) * (1 - DescuentoLinea)
            Else
                Return DescuentoLinea
            End If
        End Get
    End Property
    Public Overridable ReadOnly Property Total As Decimal
        Get
            Return BaseImponible + ImporteIva + ImporteRecargoEquivalencia
        End Get
    End Property
End Class
