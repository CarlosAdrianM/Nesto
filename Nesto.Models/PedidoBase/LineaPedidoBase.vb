Public Class LineaPedidoBase
    Public Overridable Property BaseImponible As Decimal
    Public Property PorcentajeIva As Decimal
    Public Property Producto As String

    Public Overridable ReadOnly Property ImporteIva As Decimal
        Get
            Return BaseImponible * PorcentajeIva
        End Get
    End Property
    Public Overridable ReadOnly Property Total As Decimal
        Get
            Return BaseImponible + ImporteIva
        End Get
    End Property
End Class
