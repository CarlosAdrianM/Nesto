Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest

Partial Public Class Notificacion
    Implements IInteractionRequestAware
    Private Sub OKButton_Click(sender As Object, e As RoutedEventArgs) Handles OKButton.Click
        e.Handled = True
        If Not IsNothing(FinishInteraction) Then
            FinishInteraction.Invoke
        End If
    End Sub

    Public Property FinishInteraction() As Action Implements IInteractionRequestAware.FinishInteraction
    Public Property Notification() As INotification Implements IInteractionRequestAware.Notification
End Class
