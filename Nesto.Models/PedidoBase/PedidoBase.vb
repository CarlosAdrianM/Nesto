Public Class PedidoBase(Of T As LineaPedidoBase)
    Public Sub New()
        Lineas = New List(Of T)
    End Sub
    Public Property Usuario As String
    Private _descuentoEntidad As Decimal
    Public Property DescuentoEntidad As Decimal
        Get
            Return _descuentoEntidad
        End Get
        Set(value As Decimal)
            _descuentoEntidad = value
            For Each linea In Lineas
                linea.DescuentoEntidad = value
            Next
        End Set
    End Property
    Private _descuentoPP As Decimal
    Public Property DescuentoPP As Decimal
        Get
            Return _descuentoPP
        End Get
        Set(value As Decimal)
            _descuentoPP = value
            For Each linea In Lineas
                linea.DescuentoPP = value
            Next
        End Set
    End Property
    ' Usamos AwayFromZeroRound para ser coherentes con SQL Server ROUND()
    Public ReadOnly Property BaseImponible As Decimal
        Get
            Return RoundingHelper.AwayFromZeroRound(Lineas.Sum(Function(l) l.BaseImponible), 2)
        End Get
    End Property

    ' Usamos AwayFromZeroRound para ser coherentes con SQL Server ROUND()
    Public ReadOnly Property Total As Decimal
        Get
            Return RoundingHelper.AwayFromZeroRound(Lineas.Sum(Function(l) l.Total), 2)
        End Get
    End Property

    Public Overridable Property Lineas As ICollection(Of T)
    Public Overridable Property ParametrosIva As IEnumerable(Of ParametrosIvaBase)
End Class
