Imports System.Globalization
Imports System.Windows.Data
Imports Nesto.Infrastructure.Shared

''' <summary>
''' Nesto#558: código del resultado del rapport (SeguimientoClienteDTO.Resultado) → su nombre corto para la lista
''' («PediraMasTarde» → «Pedirá más tarde»). Un código que no esté en el catálogo se enseña tal cual; Nothing, vacío.
''' </summary>
Public Class ResultadoRapportConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        Return ResultadoRapport.NombreDe(TryCast(value, String))
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotSupportedException()
    End Function
End Class
