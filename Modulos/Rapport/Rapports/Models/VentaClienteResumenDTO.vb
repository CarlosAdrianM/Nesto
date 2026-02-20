Public Class VentaClienteResumenDTO
    Public Property Nombre As String
    Public Property VentaAnnoActual As Decimal
    Public Property VentaAnnoAnterior As Decimal
    Public Property UnidadesAnnoActual As Integer
    Public Property UnidadesAnnoAnterior As Integer
    Public ReadOnly Property Diferencia As Decimal
        Get
            Return VentaAnnoActual - VentaAnnoAnterior
        End Get
    End Property
    Public ReadOnly Property Ratio As Decimal
        Get
            Return If(VentaAnnoAnterior = 0, 0, (VentaAnnoActual / VentaAnnoAnterior) - 1)
        End Get
    End Property
End Class
