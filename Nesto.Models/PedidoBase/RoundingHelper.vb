Public Class RoundingHelper
    ' Se utiliza para mantener compatibilidad con Nesto viejo
    ' Una vez no se use hay que cambiarlo por AwayFromZero 
    ' También hay que cambiarlo en la API (la clase se llama igual RoundingHelper)
    ' Lo ideal es no redondear la BaseImponible en base de datos tampoco aunque
    ' la suma de líneas no cuadre. El redondeo sobre totales en el pedido, no en líneas
    Public Shared Function Vb6Round(value As Decimal, decimals As Integer) As Decimal
        Dim d As Double = value
        Dim factor As Double = Math.Pow(10, decimals)
        Return Math.Round(d * factor, 0, MidpointRounding.ToEven) / factor
    End Function
End Class
