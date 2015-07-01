Public Class PlanesVentajas
    Private Sub txtFiltro_KeyUp(sender As Object, e As KeyEventArgs) Handles txtFiltro.KeyUp
        If e.Key = Key.Return Then
            e.OriginalSource.MoveFocus(New TraversalRequest(FocusNavigationDirection.Next))
        End If
    End Sub

    Private Sub txtFiltro_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtFiltro.GotFocus
        txtFiltro.SelectAll()
    End Sub
End Class
