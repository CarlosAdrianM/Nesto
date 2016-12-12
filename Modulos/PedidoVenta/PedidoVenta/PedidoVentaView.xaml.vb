Public Class PedidoVentaView
    Public Sub New(viewModel As PedidoVentaViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        viewModel.cmdCargarListaPedidos.Execute(Nothing)
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel

    End Sub

    Private Sub grdLineas_CellEditEnding(sender As Object, e As DataGridCellEditEndingEventArgs) Handles grdLineas.CellEditEnding
        DataContext.cmdCeldaModificada.Execute(e)
    End Sub
End Class
