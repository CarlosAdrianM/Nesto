Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO

Public Class RapportView

    Public Sub New()

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
    End Sub

    Private Sub TextBlock_MouseRightButtonUp(sender As Object, e As MouseButtonEventArgs)
        Dim a = e.OriginalSource.GetType()
        If e.OriginalSource.GetType() = GetType(Run) Then
            Dim texto As String = e.OriginalSource.Text.Trim()
            Clipboard.SetText(texto)
            MsgBox("Copiado al portapapeles: " & texto, Title:="Información")
        End If
    End Sub
End Class
