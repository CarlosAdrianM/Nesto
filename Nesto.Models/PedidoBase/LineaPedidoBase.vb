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

    ' CLAVE PARA EL ASIENTO CONTABLE (02/12/25):
    ' El SP prdCrearFacturaVta construye el asiento usando:
    '   - HABER Ventas (700): SUM(ROUND(Bruto, 2))
    '   - DEBE Descuentos (665): SUM(ROUND(Bruto * Dto, 2))
    '   - La diferencia (Ventas - Descuentos) debe coincidir con SUM(BaseImponible)
    '
    ' Por tanto, BaseImponible DEBE calcularse como:
    '   BaseImponible = ROUND(Bruto, 2) - ROUND(Bruto * SumaDescuentos, 2)
    ' Y NO como antes:
    '   BaseImponible = ROUND(Bruto - (Bruto * SumaDescuentos), 2)  <-- INCORRECTO
    '
    ' Usamos AwayFromZeroRound para ser coherentes con SQL Server ROUND()
    Public ReadOnly Property BaseImponible As Decimal
        Get
            Return RoundingHelper.AwayFromZeroRound(Bruto, 2) - ImporteDescuento
        End Get
    End Property
    Public Overridable ReadOnly Property Bruto As Decimal
        Get
            Return PrecioUnitario * Cantidad
        End Get
    End Property

    ' ImporteDescuento se redondea a 2 decimales (coherente con el SP)
    ' Usamos AwayFromZeroRound para ser coherentes con SQL Server ROUND()
    Public ReadOnly Property ImporteDescuento As Decimal
        Get
            Return RoundingHelper.AwayFromZeroRound(Bruto * SumaDescuentos, 2)
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
            Return If(AplicarDescuento, 1 - ((1 - DescuentoEntidad) * (1 - DescuentoProducto) * (1 - DescuentoLinea)), DescuentoLinea)
        End Get
    End Property
    Public Overridable ReadOnly Property Total As Decimal
        Get
            Return BaseImponible + ImporteIva + ImporteRecargoEquivalencia
        End Get
    End Property
End Class
