Imports System.Globalization

''' <summary>
''' NestoAPI#603: color de la prioridad de una sugerencia de contacto. Los pinceles se asignan
''' desde los recursos de la vista (PrioridadMaximaBrush, PrioridadAltaBrush…), así los colores
''' están definidos en un solo sitio.
''' </summary>
Public Class PrioridadToBrushConverter
    Implements IValueConverter

    Public Property Maxima As Brush
    Public Property Alta As Brush
    Public Property Media As Brush
    Public Property Baja As Brush
    Public Property SinPrioridad As Brush = Brushes.Transparent

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        Select Case Normalizar(TryCast(value, String))
            Case "maxima"
                Return Maxima
            Case "alta"
                Return Alta
            Case "media"
                Return Media
            Case "baja"
                Return Baja
            Case Else
                Return SinPrioridad
        End Select
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

    ''' <summary>Minúsculas y sin tildes: "Máxima" y "Maxima" son lo mismo.</summary>
    Public Shared Function Normalizar(prioridad As String) As String
        If String.IsNullOrWhiteSpace(prioridad) Then
            Return String.Empty
        End If
        Return prioridad.Trim().ToLowerInvariant().Replace("á", "a")
    End Function
End Class
