Public Class LineaPedidoBase
    Public Overridable Property BaseImponible As Decimal
    Public Property PorcentajeIva As Decimal
    Public Property PorcentajeRecargoEquivalencia As Decimal
    Public Property Producto As String
    Public Property Usuario As String

    Public Overridable ReadOnly Property ImporteIva As Decimal
        Get
            Return Math.Round(BaseImponible * PorcentajeIva, 2, MidpointRounding.AwayFromZero)
        End Get
    End Property
    Public Overridable ReadOnly Property ImporteRecargoEquivalencia As Decimal
        Get
            Return Math.Round(BaseImponible * PorcentajeRecargoEquivalencia, 2, MidpointRounding.AwayFromZero)
        End Get
    End Property
    Public Overridable ReadOnly Property Total As Decimal
        Get
            Return Math.Round(BaseImponible + ImporteIva + ImporteRecargoEquivalencia, 2, MidpointRounding.AwayFromZero)
        End Get
    End Property
End Class
