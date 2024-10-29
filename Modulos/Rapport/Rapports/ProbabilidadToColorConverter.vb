Imports System.Globalization
Public Class ProbabilidadToColorConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        Dim probabilidad As Double = CDbl(value)

        If probabilidad >= 0.8 Then
            Return Brushes.Green
        ElseIf probabilidad < 0.45 Then
            Return Brushes.Red
        Else
            Return Brushes.LightBlue
        End If
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class
