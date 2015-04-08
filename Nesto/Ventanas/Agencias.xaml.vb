Public Class Agencias

    Private Sub txtNumeroPedido_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtNumeroPedido.GotFocus
        txtNumeroPedido.SelectAll()
    End Sub

    Private Sub txtNumeroBultos_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtNumeroBultos.GotFocus
        txtNumeroBultos.SelectAll()
    End Sub

    Private Sub txtNumeroPedido_KeyUp(sender As Object, e As KeyEventArgs) Handles txtNumeroPedido.KeyUp
        If e.Key = Key.Return Then
            e.OriginalSource.MoveFocus(New TraversalRequest(FocusNavigationDirection.Next))
        End If
    End Sub

    Private Sub txtNumeroBultos_KeyUp(sender As Object, e As KeyEventArgs) Handles txtNumeroBultos.KeyUp
        If e.Key = Key.Return Then
            e.OriginalSource.MoveFocus(New TraversalRequest(FocusNavigationDirection.Next))
        End If
    End Sub


    Private Sub txtNombreFiltro_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtNombreFiltro.GotFocus
        txtNombreFiltro.SelectAll()
    End Sub
End Class
