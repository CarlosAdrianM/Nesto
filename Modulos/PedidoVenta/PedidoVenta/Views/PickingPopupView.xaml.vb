Public Class PickingPopupView
    Private Sub txtNumeroPedido_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtNumeroPedido.GotFocus
        txtNumeroPedido.SelectAll()
    End Sub

    Private Sub txtCliente_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtCliente.GotFocus
        txtCliente.SelectAll()
    End Sub

    Private Sub txtNumeroPedido_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtNumeroPedido.PreviewMouseUp
        txtNumeroPedido.SelectAll()
    End Sub

    Private Sub txtCliente_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtCliente.PreviewMouseUp
        txtCliente.SelectAll()
    End Sub

    Private Sub PickingPopupView_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Keyboard.Focus(txtNumeroPedido)
    End Sub
End Class
