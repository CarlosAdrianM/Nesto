Imports Nesto.Contratos
Class CarteraPagosView
    Public Sub New(viewModel As CarteraPagosViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel

        ' Ponemos el foco inicial en la empresa
        txtEmpresa.Focus()

    End Sub

    Private Sub txtRemesa_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtRemesa.GotFocus
        txtRemesa.SelectAll()
    End Sub

    Private Sub txtEmpresa_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtEmpresa.GotFocus
        txtEmpresa.SelectAll()
    End Sub
End Class
