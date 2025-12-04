Public Class RoundingHelper
    ' AwayFromZero: Redondeo comercial estándar (0.5 redondea hacia arriba)
    ' Coherente con SQL Server ROUND() y con la legislación española
    ' Usado en NestoAPI (RoundingHelper.DosDecimalesRound)
    Public Shared Function AwayFromZeroRound(value As Decimal, decimals As Integer) As Decimal
        Return Math.Round(value, decimals, MidpointRounding.AwayFromZero)
    End Function

    ' Vb6Round: Banker's rounding (ToEven) - OBSOLETO
    ' Se mantiene solo por compatibilidad con código antiguo
    ' NO usar en código nuevo - usar AwayFromZeroRound en su lugar
    Public Shared Function Vb6Round(value As Decimal, decimals As Integer) As Decimal
        Dim d As Double = value
        Dim factor As Double = Math.Pow(10, decimals)
        Return Math.Round(d * factor, 0, MidpointRounding.ToEven) / factor
    End Function
End Class
