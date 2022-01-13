Class ListaRapportsView
    Public Sub New()

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().

    End Sub

    Private Async Sub ListaRapportsView_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        DataContext.cmdCargarListaRapports.Execute(Nothing)
        txtSelectorCliente.Focus()
    End Sub

    Private Sub txtFiltro_KeyEnterUpdate(sender As Object, e As KeyEventArgs) Handles txtFiltro.KeyUp
        If e.Key = Key.Enter Then
            Dim tBox As TextBox = CType(sender, TextBox)
            Dim prop As DependencyProperty = TextBox.TextProperty
            Dim binding As BindingExpression = BindingOperations.GetBindingExpression(tBox, prop)
            If binding IsNot Nothing Then
                binding.UpdateSource()
            End If
        End If
    End Sub

    Private Sub txtFiltro_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtFiltro.GotFocus
        txtFiltro.SelectAll()
    End Sub

    Private Sub txtFiltro_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtFiltro.PreviewMouseUp
        txtFiltro.SelectAll()
    End Sub
End Class
