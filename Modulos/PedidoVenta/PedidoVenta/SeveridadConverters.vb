Imports System
Imports System.Globalization
Imports System.Windows.Data
Imports System.Windows.Media

''' <summary>
''' Convierte NivelSeveridad a un color de fondo para las filas del DataGrid
''' </summary>
Public Class SeveridadToColorConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        If value Is Nothing Then
            Return Brushes.White
        End If

        Dim severidad As NivelSeveridad
        If [Enum].TryParse(value.ToString(), severidad) Then
            Select Case severidad
                Case NivelSeveridad.Error
                    ' Rojo claro para errores
                    Return New SolidColorBrush(Color.FromRgb(255, 235, 235)) ' #FFEBEB
                Case NivelSeveridad.Warning
                    ' Amarillo claro para warnings
                    Return New SolidColorBrush(Color.FromRgb(255, 251, 230)) ' #FFFBE6
                Case NivelSeveridad.Info
                    ' Azul claro para info
                    Return New SolidColorBrush(Color.FromRgb(230, 244, 255)) ' #E6F4FF
                Case Else
                    Return Brushes.White
            End Select
        End If

        Return Brushes.White
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class

''' <summary>
''' Convierte NivelSeveridad a un icono emoji para la columna de severidad
''' </summary>
Public Class SeveridadToIconConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        If value Is Nothing Then
            Return "?"
        End If

        Dim severidad As NivelSeveridad
        If [Enum].TryParse(value.ToString(), severidad) Then
            Select Case severidad
                Case NivelSeveridad.Error
                    Return "X"  ' X roja para errores
                Case NivelSeveridad.Warning
                    Return "!"  ' Signo de exclamacion para warnings
                Case NivelSeveridad.Info
                    Return "i"  ' i para informacion
                Case Else
                    Return "?"
            End Select
        End If

        Return "?"
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class
