' Nesto#552: pantalla «Pendientes de entregar» (solo lectura). La lógica está en PendientesEntregarViewModel.
Public Class PendientesEntregarView

    Public Sub New(viewModel As PendientesEntregarViewModel)
        InitializeComponent()
        DataContext = viewModel
    End Sub

    Private Sub dgLineas_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs)
        ' Solo si el doble clic es sobre una fila (no sobre la cabecera ni la barra de desplazamiento)
        Dim origen = TryCast(e.OriginalSource, DependencyObject)
        While origen IsNot Nothing AndAlso TypeOf origen IsNot DataGridRow
            origen = If(TypeOf origen Is Visual, VisualTreeHelper.GetParent(origen), LogicalTreeHelper.GetParent(origen))
        End While
        Dim fila = TryCast(origen, DataGridRow)
        If fila Is Nothing Then
            Return
        End If
        Dim viewModel = TryCast(DataContext, PendientesEntregarViewModel)
        viewModel?.AbrirPedidoCommand.Execute(fila.Item)
    End Sub
End Class
