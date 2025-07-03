Imports System.Globalization

Partial Public Class ListaRapportsView
    Public Sub New()

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().

    End Sub

    Private Async Sub ListaRapportsView_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Dim unused1 = DataContext.cmdCargarListaRapports.Execute(Nothing)
        Dim unused = txtSelectorCliente.Focus()
    End Sub

    Private Sub txtFiltro_KeyEnterUpdate(sender As Object, e As KeyEventArgs) Handles txtFiltro.KeyUp
        If e.Key = Key.Enter Then
            Dim tBox As TextBox = CType(sender, TextBox)
            Dim prop As DependencyProperty = TextBox.TextProperty
            Dim binding As BindingExpression = BindingOperations.GetBindingExpression(tBox, prop)
            binding?.UpdateSource()
        End If
    End Sub

    Private Sub txtFiltro_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtFiltro.GotFocus
        txtFiltro.SelectAll()
    End Sub

    Private Sub txtFiltro_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtFiltro.PreviewMouseUp
        txtFiltro.SelectAll()
    End Sub
End Class
Public Class StringEqualityConverter
    Implements IMultiValueConverter

    Public Function Convert(values() As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IMultiValueConverter.Convert
        Return values.Length >= 2 AndAlso values(0) IsNot Nothing AndAlso values(1) IsNot Nothing AndAlso values(0).ToString() = values(1).ToString()
    End Function

    Public Function ConvertBack(value As Object, targetTypes() As Type, parameter As Object, culture As CultureInfo) As Object() Implements IMultiValueConverter.ConvertBack
        Return targetTypes.Select(Function(t) Binding.DoNothing).ToArray()
    End Function
End Class


