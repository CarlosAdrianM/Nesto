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

    Private Async Sub CarteraPagos_Loaded(sender As Object, e As RoutedEventArgs) Handles CarteraPagos.Loaded
        Dim viewModel As CarteraPagosViewModel = CType(Me.DataContext, CarteraPagosViewModel)

        ' Ponemos el foco inicial en la empresa
        txtRemesa.Focus()
    End Sub


    Private Sub txtRemesa_KeyUp(sender As Object, e As KeyEventArgs) Handles txtRemesa.KeyUp
        If e.Key = Key.Enter Then
            If txtRemesa.Text.Trim <> "0" Then
                btnCrearFichero.Focus()
            Else
                txtNumOrden.Focus()
            End If
        End If
    End Sub

    Private Sub txtRemesa_PreviewMouseMove(sender As Object, e As MouseEventArgs) Handles txtRemesa.PreviewMouseMove
        txtRemesa.SelectAll()
    End Sub

    Private Sub txtNumOrden_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtNumOrden.PreviewMouseUp
        txtNumOrden.SelectAll()
    End Sub

    Private Sub txtNumOrden_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtNumOrden.GotFocus
        txtNumOrden.SelectAll()
    End Sub

    Private Sub txtNumOrden_KeyUp(sender As Object, e As KeyEventArgs) Handles txtNumOrden.KeyUp
        If e.Key = Key.Enter Then
            btnCrearFichero.Focus()
        End If
    End Sub

End Class
