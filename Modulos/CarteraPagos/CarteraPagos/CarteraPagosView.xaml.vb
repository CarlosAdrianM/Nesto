Imports Nesto.Contratos
Class CarteraPagosView
    Public Sub New(viewModel As CarteraPagosViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel


    End Sub

    Private Sub txtRemesa_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtRemesa.GotFocus
        txtRemesa.SelectAll()
    End Sub

    Private Sub txtEmpresa_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtEmpresa.GotFocus
        txtEmpresa.SelectAll()
    End Sub

    Private Async Sub CarteraPagos_Loaded(sender As Object, e As RoutedEventArgs) Handles CarteraPagos.Loaded
        Dim viewModel As CarteraPagosViewModel = CType(Me.DataContext, CarteraPagosViewModel)
        ' Ponemos e IF para que no entre cada vez que coja el foco
        If IsNothing(viewModel.empresa) Then
            Await viewModel.CargarDatos
        End If
        ' Ponemos el foco inicial en la empresa
        txtEmpresa.Focus()
    End Sub

    Private Sub txtEmpresa_KeyUp(sender As Object, e As KeyEventArgs) Handles txtEmpresa.KeyUp
        If e.Key = Key.Enter Then
            txtRemesa.Focus()
        End If
    End Sub

    Private Sub txtRemesa_KeyUp(sender As Object, e As KeyEventArgs) Handles txtRemesa.KeyUp
        If e.Key = Key.Enter Then
            btnCrearFichero.Focus()
        End If
    End Sub
End Class
