Class ListaRapportsView
    Public Sub New(viewModel As ListaRapportsViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel

    End Sub

    Private Sub ListaRapportsView_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        DataContext.cmdCargarListaRapports.Execute(Nothing)
        txtSelectorCliente.Focus()
    End Sub

End Class
