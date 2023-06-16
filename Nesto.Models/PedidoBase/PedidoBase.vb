Public Class PedidoBase(Of T As LineaPedidoBase)
    Public Sub New()
        Lineas = New List(Of T)
    End Sub
    Public Property Usuario As String
    Public ReadOnly Property BaseImponible As Decimal
        Get
            Return Math.Round(Lineas.Sum(Function(l) l.BaseImponible), 2, MidpointRounding.AwayFromZero)
        End Get
    End Property

    Public ReadOnly Property Total As Decimal
        Get
            Return Math.Round(Lineas.Sum(Function(l) l.Total), 2, MidpointRounding.AwayFromZero)
        End Get
    End Property

    Public Overridable Property Lineas As ICollection(Of T)
    Public Overridable Property ParametrosIva As IEnumerable(Of ParametrosIvaBase)
End Class
